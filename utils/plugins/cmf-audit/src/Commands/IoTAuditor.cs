using audit.Objects;
using audit.Services;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "iot" command group
    /// </summary>
    [CmfCommand("iot", Parent = "review", Description = "Will audit the IoT package of an already checked-out project")]
    public partial class IoTAuditor : BaseCommand
    {
        #region Public Constructors

        public IoTAuditor() : this(new FileSystem())
        {
        }

        public IoTAuditor(IFileSystem fileSystem) : base(fileSystem)
        {
        }

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Configure command
        /// </summary>
        /// <param name="cmd"></param>
        public override void Configure(Command cmd)
        {
            // if options are not defined, try to use predefined environment variables from pipeline
            cmd.AddArgument(new Argument<IDirectoryInfo>(
                name: "workingDir",
                parse: (argResult) => Parse<IDirectoryInfo>(argResult, "."),
                isDefault: true
            )
            {
                Description = "Working Directory"
            });

            AuditCommandOptions.AddCommonOptions(cmd, generateReportByDefault: false);

            // Add the handler
            cmd.Handler = CommandHandler.Create<IDirectoryInfo,
                string,
                bool, string,
                bool, string, string, string, string, bool>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            IDirectoryInfo workingDir,
            string teamProject,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            IFileInfo cmfPackageFile = this.fileSystem.FileInfo.New(Path.Combine(workingDir.FullName, "cmfpackage.json"));
            if (!cmfPackageFile.Exists)
            {
                throw new CliException($"No cmfpackage.json found in '{workingDir.FullName}'. Run 'review iot' from inside an already checked-out IoT package, or use 'review-remote-ado iot' to fetch one from Azure DevOps.");
            }

            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);
            this.fileSystem.Directory.SetCurrentDirectory(workingDir.FullName);

            await RunAuditAsync(workingDir, cmfPackage, teamProject, cmfPackage.PackageId, generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }

        /// <summary>
        /// Runs the IoT package audit against an already-resolved local working directory. Shared by
        /// the local "review iot" command and the "review-remote-ado iot" command (<see cref="IoTAuditorRemote"/>),
        /// which differ only in how <paramref name="workingDir"/>/<paramref name="cmfPackage"/> are obtained.
        /// </summary>
        protected async Task RunAuditAsync(
            IDirectoryInfo workingDir, CmfPackage cmfPackage,
            string teamProject, string packageId,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            List<Tag> tags = [new("PackageType", "IoT")];
            var reporters = new Reporters(
                tableReports:
                [
                    AuditUtilities.DisplayTable(cmfPackage.PackageId, PackageMetadataSummary(cmfPackage), ["Identifier", "Value"], "Package", Spectre.Console.Color.Green)
                ],
                postEvents:
                [
                    new PostTelemetry(teamProject, packageId, cmfPackage.Version, "PackageMetadata", "PackageMetadata", PackageMetadataSummary(cmfPackage), tags)
                ]
            );
            reporters.RowAdder = (type, name, metadata) =>
            {
                if (metadata.Count > 0)
                {
                    reporters.PostEvents.Add(new PostTelemetry(teamProject, packageId, cmfPackage.Version, type, name, metadata, tags));
                }
            };

            var invalidContentToPack = new Dictionary<string, string>();

            PackageJsonAuditor(cmfPackage, reporters, workingDir.GetFiles("package.json", SearchOption.TopDirectoryOnly));

            foreach (var content in cmfPackage.ContentToPack)
            {
                var contentPath = content.Source;
                bool hasVersionMapping = false;

                if (content.Source.Contains("$(version)"))
                {
                    contentPath = content.Source.Replace("$(version)", cmfPackage.Version);
                    hasVersionMapping = true;
                }

                try
                {
                    IDirectoryInfo? currentWorkingDir = ResolveCurrentWorkingDirForContent(workingDir, contentPath);

                    if (currentWorkingDir == null)
                    {
                        invalidContentToPack.TryAdd(content.Source, contentPath);
                        Log.Warning($"Skipping {contentPath}...");
                        continue;
                    }

                    var files = currentWorkingDir.GetFiles("*", SearchOption.AllDirectories);

                    RunAuditorsPerContentType(cmfPackage, reporters, files);

                    if (hasVersionMapping)
                    {
                        var unReferencedContent = currentWorkingDir
                                                    .GetFiles(content.Source.Replace("$(version)/", ""), SearchOption.AllDirectories)
                                                    .Where(file => !file.FullName.Contains(cmfPackage.Version))
                                                    .ToArray();

                        RunAuditorsPerContentType(cmfPackage, reporters, unReferencedContent);
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // Error referencing file that does not exist
                    invalidContentToPack.TryAdd(content.Source, contentPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }

            if (invalidContentToPack.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, invalidContentToPack, ["Source", "Resolved Source"], "Invalid Data Package Content to Pack", Spectre.Console.Color.Yellow));
            }

            #region Post Event

            if (postEvent && reporters.PostEvents.Count != 0)
            {
                if (string.IsNullOrEmpty(tenantName))
                {
                    tenantName = CLI.Core.Objects.ExecutionContext.Instance.ProjectConfig?.Tenant
                        ?? throw new CliException("No MES tenant found: pass --tenantName or run from a project with a .project-config.json.");
                }
                var restAPIService = new RestApiService(new ClientConfiguration(
                    hostAddress, tenantName, clientId, securityAccessToken, useSSL));
                await restAPIService.PostEventsAsync(reporters.PostEvents);
            }
            #endregion

            #region Generate Report

            if (generateReport)
            {
                Log.Information($"Data {cmfPackage.PackageName} Generating Reports...");
                var reportOutputDirectory = this.fileSystem.DirectoryInfo.New(reportOutputFolder);
                reportOutputDirectory.Create();

                var auditFolder = reportOutputDirectory.CreateSubdirectory("Review");
                auditFolder = auditFolder.CreateSubdirectory($"{teamProject}");
                var rootAuditFolder = auditFolder.CreateSubdirectory($"{cmfPackage.PackageType}_{cmfPackage.PackageId}_{cmfPackage.Version}");

                foreach (var report in reporters.PanelReports)
                {
                    report(rootAuditFolder, null);
                }

                foreach (var report in reporters.TableReports)
                {
                    report(rootAuditFolder);
                }

                AuditUtilities.MergeDirectoryCSVsIntoExcel(rootAuditFolder);

                Log.Information($"IoT Package {cmfPackage.PackageName} Generated Reports");
            }

            #endregion

            Log.Information($"Successfully audited the iot package - {cmfPackage.PackageId}");
        }

        internal static IDirectoryInfo? ResolveCurrentWorkingDirForContent(IDirectoryInfo workingDir, string contentPath)
        {
            var currentWorkingDir = workingDir;
            var contentDirs = contentPath.Replace("\\", "/").Split("/");
            foreach (var dir in contentDirs)
            {
                if (dir != "+" && dir != "*")
                {
                    if (dir == "..")
                    {
                        currentWorkingDir = currentWorkingDir?.Parent;
                    }
                    else
                    {
                        currentWorkingDir = currentWorkingDir?.GetDirectories()?.FirstOrDefault(wD => wD.Name == dir);
                    }
                }
            }

            return currentWorkingDir;
        }

        #endregion Public Methods

        internal static void RunAuditorsPerContentType(CmfPackage cmfPackage, Reporters reporters, IFileInfo[] files)
        {
            PackageJsonAuditor(cmfPackage, reporters, files.Where(file => file.Name == "package.json"));
            TypescriptAuditor(cmfPackage, reporters, files.Where(file => file.Extension == ".ts"));
        }

        internal static void PackageJsonAuditor(CmfPackage cmfPackage, Reporters reporters, IEnumerable<IFileInfo> packageJsonFiles)
        {
            if (packageJsonFiles?.Any() ?? false)
            {
                foreach (var file in packageJsonFiles)
                {
                    var result = new Dictionary<string, string>();
                    var root = JsonNode.Parse(file.ReadToString());

                    // Extract name and version
                    if (root?["name"] != null)
                    {
                        result["name"] = root["name"]!.ToString().Replace("/", "_");
                    }

                    if (root?["version"] != null)
                    {
                        result["version"] = root["version"]!.ToString();
                    }

                    // Helper to flatten dependency sections
                    void AddDependencies(JsonNode? section, string prefix)
                    {
                        if (section is JsonObject deps)
                        {
                            foreach (var kvp in deps)
                            {
                                result[$"{prefix}:{kvp.Key}"] = kvp.Value?.ToString() ?? "";
                            }
                        }
                    }

                    // Extract dependency sections
                    var packageKey = $"{root?["name"]?.ToString() ?? file.Directory.Name}@{root?["version"]?.ToString() ?? "unknown"}";
                    reporters.RowAdder(packageKey, "peerDependencies", root?["peerDependencies"]?.Deserialize<Dictionary<string, string>>() ?? []);
                    reporters.RowAdder(packageKey, "dependencies", root?["dependencies"]?.Deserialize<Dictionary<string, string>>() ?? []);
                    reporters.RowAdder(packageKey, "devDependencies", root?["devDependencies"]?.Deserialize<Dictionary<string, string>>() ?? []);

                    AddDependencies(root?["peerDependencies"], "peerDependencies");
                    AddDependencies(root?["dependencies"], "dependencies");
                    AddDependencies(root?["devDependencies"], "devDependencies");

                    reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, result, $"Package_JSON", Spectre.Console.Color.Green, ["Identifier", "Value"]));
                }
            }
        }

        internal static void TypescriptAuditor(CmfPackage cmfPackage, Reporters reporters, IEnumerable<IFileInfo> typescriptFiles)
        {
            var bundledTasks = 0;
            var bundledConverters = 0;
            var atlTasks = 0;
            var atlConverters = 0;

            var taskDict = new Dictionary<string, string[]>();
            var converterDict = new Dictionary<string, string[]>();

            foreach (var file in typescriptFiles)
            {
                var fileLines = file.ReadToStringList();
                if (fileLines.Contains("@Task.Task()") || fileLines.Contains("@Task.Task({"))
                {
                    #region Task Auditor

                    TaskAuditor(cmfPackage, reporters, ref bundledTasks, ref atlTasks, taskDict, file, fileLines);

                    #endregion Task Auditor
                }
                else if (fileLines.Contains("@Converter.Converter()") || fileLines.Contains("@Converter.Converter({"))
                {
                    #region Converter Auditor

                    ConverterAuditor(cmfPackage, reporters, ref bundledConverters, ref atlConverters, converterDict, file, fileLines);

                    #endregion Converter Auditor
                }
                else if (fileLines.Any(line => line.Contains("extends DeviceDriverBase")))
                {
                    #region Driver Auditor

                    DriverAuditor(cmfPackage, reporters, file, fileLines);

                    #endregion Driver Auditor
                }
            }

            #region Summary

            if (taskDict.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, taskDict, ["Task Name", "Nr. of Injects", "Nr. of Inputs", "Nr. of Outputs", "Lines of Code"], "Summary_Tasks_Table", Spectre.Console.Color.Green));
            }

            if (converterDict.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, converterDict, ["Converter Name", "Nr. of Injects", "Nr. of Parameters", "Lines of Code"], "Summary_Converter_Table", Spectre.Console.Color.Green));
            }

            if (taskDict.Count > 0 || converterDict.Count > 0)
            {
                reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, new Dictionary<string, string>() {
                    { "Total Customization Objects", (bundledTasks + atlTasks + bundledConverters + atlConverters).ToString() },
                    { "Total Tasks", (bundledTasks + atlTasks).ToString() },
                    { "Total Converters", (bundledConverters + atlConverters).ToString() },
                    { "Automation Task Libraries Tasks", atlTasks.ToString() },
                    { "Automation Task Libraries Converters", atlConverters.ToString() },
                    { "Bundled Tasks Tasks", bundledTasks.ToString() },
                    { "Bundled Tasks Converters", bundledConverters.ToString() }
                }, "Task_Transition", Spectre.Console.Color.Green, ["Identifier", "Value"]));
            }

            #endregion Summary
        }

        internal static void DriverAuditor(CmfPackage cmfPackage, Reporters reporters, IFileInfo file, List<string> fileLines)
        {
            var parentDir = file.Directory;
            var inversify = parentDir.GetFiles().FirstOrDefault(file => file.FullName.Contains("inversify.config.ts"));
            var inversifyContent = inversify?.ReadToString() ?? string.Empty;
            var indexTS = parentDir.GetFiles().FirstOrDefault(file => file.FullName.Contains("index.ts"));
            var indexTSContent = indexTS?.ReadToString() ?? string.Empty;
            var driverName = DriverName().Match(fileLines.FirstOrDefault(fl => fl.Contains("extends DeviceDriverBase"))).Groups[1];

            var content = new Dictionary<string, string>
                    {
                        { "Injects", Regex.Unescape(JsonSerializer.Serialize(Binds().Match(inversifyContent).Groups[1].Value)).Trim('"') },
                        { "AddOns", Regex.Unescape(JsonSerializer.Serialize(AddOns().Match(indexTSContent).Groups[1].ToString())).Trim('"')},
                        { "Lines of Code", fileLines.Count.ToString() }

                    };
            reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, content, $"Driver_{driverName}", Spectre.Console.Color.Green, ["Identifier", "Value"]));
            reporters.RowAdder("Driver", driverName.ToString(), content);
        }

        internal static void ConverterAuditor(CmfPackage cmfPackage, Reporters reporters, ref int bundledConverters, ref int atlConverters, Dictionary<string, string[]> converterDict, IFileInfo file, List<string> fileLines)
        {
            var parentDir = file.Directory;
            var designer = parentDir.GetFiles().FirstOrDefault(file => file.FullName.Contains("-designer.ts"));

            var converter = ParseConverterClass([.. fileLines], designer);

            if (converter != null && !converterDict.ContainsKey(converter.ClassName))
            {
                var converterData = new Dictionary<string, string>() {
                            { "Injects", JsonSerializer.Serialize(converter.Injects.Select(i =>Regex.Unescape(i).Trim('"'))) },
                            { "Parameters", JsonSerializer.Serialize(converter.Parameters.Select(i =>Regex.Unescape(i).Trim('"'))) },
                            { "Lines of Code", fileLines.Count.ToString() },
                        };

                if (designer != null)
                {
                    converterData.Add("Bundled", "true");
                    bundledConverters++;
                }
                else
                {
                    converterData.Add("ATL", "true");
                    atlConverters++;
                }
                reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, converterData, $"Converter_{converter.ClassName}", Spectre.Console.Color.Green, ["Identifier", "Value"]));
                reporters.RowAdder("Converter", converter.ClassName, converterData);

                converterDict.Add(converter.ClassName, [converter.Injects.Count.ToString(), converter.Parameters.Count.ToString(), fileLines.Count.ToString()]);
            }
        }

        internal static void TaskAuditor(CmfPackage cmfPackage, Reporters reporters, ref int bundledTasks, ref int atlTasks, Dictionary<string, string[]> taskDict, IFileInfo file, List<string> fileLines)
        {
            var parentDir = file.Directory;
            var task = ParseTaskClass([.. fileLines]);

            if (task != null && !taskDict.ContainsKey(task.ClassName))
            {
                var taskData = new Dictionary<string, string>() {
                            { "Injects", JsonSerializer.Serialize(task.Injects.Select(i =>Regex.Unescape(i).Trim('"'))) },
                            { "Inputs", JsonSerializer.Serialize(task.Inputs.Select(i =>Regex.Unescape(i).Trim('"'))) },
                            { "Outputs", JsonSerializer.Serialize(task.Outputs.Select(i =>Regex.Unescape(i).Trim('"'))) },
                            { "Lines of Code", fileLines.Count.ToString() },
                        };

                if (parentDir.GetFiles().Any(file => file.FullName.Contains(".html")))
                {
                    taskData.Add("Bundled", "true");
                    bundledTasks++;
                }
                else
                {
                    taskData.Add("ATL", "true");
                    atlTasks++;
                }
                reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, taskData, $"Task_{task.ClassName}", Spectre.Console.Color.Green, ["Identifier", "Value"]));
                reporters.RowAdder("Task", task.ClassName, taskData);

                taskDict.Add(task.ClassName, [task.Injects.Count.ToString(), task.Inputs.Count.ToString(), task.Outputs.Count.ToString(), fileLines.Count.ToString()]);
            }
        }

        internal static Dictionary<string, string> PackageMetadataSummary(CmfPackage cmfPackage)
        {
            var tableValuesCmfPackage = new Dictionary<string, string>
            {
                { "PackageId", cmfPackage.PackageId },
                { "PackageVersion", cmfPackage.Version },
                { "Number of First Level Dependencies", cmfPackage?.Dependencies?.Count.ToString() ?? "0" },
                { "ContentToPack", JsonSerializer.Serialize(cmfPackage?.ContentToPack ?? [])  }
            };

            return tableValuesCmfPackage;
        }

        internal static TaskClass ParseTaskClass(string[] lines)
        {
            var task = new TaskClass();
            string? lastDecorator = null;

            // First pass: capture properties of interfaces that extend System.TaskDefaultSettings
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                var ifaceMatch = Regex.Match(trimmed, @"export\s+interface\s+(\w+)\s+extends\s+System\.TaskDefaultSettings");
                if (ifaceMatch.Success)
                {
                    // Capture interface properties
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        var innerLine = lines[j].Trim();
                        if (innerLine.StartsWith("}")) break;

                        var propMatch = Regex.Match(innerLine, @"(\w+)\s*:");
                        if (propMatch.Success)
                            task.Settings.Add(propMatch.Groups[1].Value);
                    }
                }
            }

            // Second pass: parse class and collect members
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (trimmed.StartsWith("@Task.Task") && !trimmed.StartsWith("export class"))
                {
                    lastDecorator = "class";
                    continue;
                }

                if (lastDecorator == "class")
                {
                    var match = Regex.Match(trimmed, @"class\s+(\w+).*implements\s+(\w+)");
                    var matchWOInterface = Regex.Match(trimmed, @"class\s+(\w+).*");
                    if (match.Success)
                    {
                        task.ClassName = match.Groups[1].Value;
                    }
                    else if (matchWOInterface.Success)
                    {
                        task.ClassName = matchWOInterface.Groups[1].Value;
                    }
                    else
                    {
                        Log.Error($"Unable to parse task '{trimmed}'");
                        return null;
                    }
                    lastDecorator = null;
                    continue;
                }

                if (trimmed.StartsWith("@Task.InputProperty")) { lastDecorator = "input"; continue; }
                if (trimmed.StartsWith("@Task.OutputProperty")) { lastDecorator = "output"; continue; }
                if (trimmed.StartsWith("@DI.Inject"))
                {
                    lastDecorator = "injects";

                    var match = Regex.Match(trimmed, @"\((.*)\)");
                    if (match.Success)
                    {
                        task.Injects.Add(match.Groups[1].Value);
                    }

                    continue;
                }

                if (lastDecorator == "input" || lastDecorator == "output")
                {
                    var match = Regex.Match(trimmed, @"public\s+(\w+):");
                    if (match.Success)
                    {
                        if (lastDecorator == "input") task.Inputs.Add(match.Groups[1].Value);
                        if (lastDecorator == "output") task.Outputs.Add(match.Groups[1].Value);
                    }
                    lastDecorator = null;
                    continue;
                }
            }

            return task;
        }

        internal static ConverterClass ParseConverterClass(string[] lines, IFileInfo designer)
        {
            var converter = new ConverterClass();
            string? lastDecorator = null;

            // Parse the converter class and collect its members
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (trimmed.StartsWith("@Converter.Converter"))
                {
                    lastDecorator = "class";
                    continue;
                }

                if (lastDecorator == "class" && trimmed.StartsWith("export class"))
                {
                    var match = Regex.Match(trimmed, @"class\s+(\w+).*implements\s+(\w+)");
                    var matchWOInterface = Regex.Match(trimmed, @"class\s+(\w+).*");
                    if (match.Success)
                    {
                        converter.ClassName = match.Groups[1].Value;
                    }
                    else if (matchWOInterface.Success)
                    {
                        converter.ClassName = matchWOInterface.Groups[1].Value;
                    }
                    else
                    {
                        Log.Error($"Unable to parse converter '{trimmed}'");
                        return null;
                    }

                    var designerContent = lines.ToList();
                    if (designer != null)
                    {
                        designerContent = designer.ReadToStringList();
                    }
                    var designerParameters = false;

                    for (int j = 0; j < designerContent.Count; j++)
                    {
                        if (designerContent[j].Contains("parameters:"))
                        {
                            designerParameters = true;
                            continue;
                        }

                        if (designerParameters)
                        {
                            if (designerContent[j].Contains('}'))
                            {
                                break;
                            }
                            var matchParam = Regex.Match(designerContent[j], @"\w.*(?=:)");

                            if (matchParam.Success)
                            {
                                converter.Parameters.Add(matchParam.Groups[0].Value);
                            }
                        }
                    }

                    lastDecorator = null;
                    continue;
                }

                if (trimmed.StartsWith("@DI.Inject"))
                {
                    lastDecorator = "injects";

                    var match = Regex.Match(trimmed, @"\((.*)\)");
                    if (match.Success)
                    {
                        converter.Injects.Add(match.Groups[1].Value);
                    }

                    continue;
                }
            }

            return converter;
        }

        [GeneratedRegex(@"container\.bind(?:<[^>]+>)?\(\s*([^)]+)\)", RegexOptions.Compiled)]
        private static partial Regex Binds();
        [GeneratedRegex(@"{\s*name:\s*""([^""]+)""", RegexOptions.Compiled)]
        private static partial Regex AddOns();
        [GeneratedRegex(@"export\s+class\s+([^\s]+)\s+extends", RegexOptions.Compiled)]
        private static partial Regex DriverName();
    }

    public class TaskClass
    {
        public string ClassName { get; set; }
        public List<string> Inputs { get; set; } = new();
        public List<string> Outputs { get; set; } = new();
        public List<string> Injects { get; set; } = new();
        public List<string> Settings { get; set; } = new();
    }

    public class ConverterClass
    {
        public string ClassName { get; set; }
        public List<string> Parameters { get; set; } = new();
        public List<string> Injects { get; set; } = new();
    }
}
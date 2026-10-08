using audit.CodeAnalysis;
using audit.Objects;
using audit.Services;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Services.Common;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "data" command group
    /// </summary>
    [CmfCommand("data", Parent = "review", Description = "Will audit the Data package of an already checked-out project")]
    public class DataAuditor : BaseCommand
    {
        #region Public Constructors

        public DataAuditor() : this(new FileSystem())
        {
        }

        public DataAuditor(IFileSystem fileSystem) : base(fileSystem)
        {
        }

        #endregion Public Constructors

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
                throw new CliException($"No cmfpackage.json found in '{workingDir.FullName}'. Run 'review data' from inside an already checked-out Data package, or use 'review-remote-ado data' to fetch one from Azure DevOps.");
            }

            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);
            this.fileSystem.Directory.SetCurrentDirectory(workingDir.FullName);
            IDirectoryInfo rootDir = FileSystemUtilities.GetProjectRoot(this.fileSystem);

            await RunAuditAsync(workingDir, rootDir, cmfPackage, teamProject, cmfPackage.PackageId, generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }

        /// <summary>
        /// Runs the Data package audit against an already-resolved local working directory. Shared by
        /// the local "review data" command and the "review-remote-ado data" command (<see cref="DataAuditorRemote"/>),
        /// which differ only in how <paramref name="workingDir"/>/<paramref name="cmfPackage"/> are obtained.
        /// </summary>
        protected async Task RunAuditAsync(
            IDirectoryInfo workingDir, IDirectoryInfo rootDir, CmfPackage cmfPackage,
            string teamProject, string packageId,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            List<Tag> tags = [new("PackageType", "Data")];
            var reporters = new Reporters(
                tableReports:
                [
                    AuditUtilities.DisplayTable(cmfPackage.PackageId, PackageMetadataSummary(cmfPackage), ["Identifier", "Value"], "Package", Spectre.Console.Color.Green)
                ],
                postEvents:
                [
                    new PostTelemetry(teamProject, packageId, cmfPackage.Version, "PackageMetadata", "PackageMetadata", PackageMetadataSummary(cmfPackage), tags)
                ],
                chart: new BarChart()
                                .Label($"[green bold underline]{cmfPackage.PackageId} Data Package[/]")
                                .CenterLabel()
            );
            reporters.RowAdder = (type, name, metadata) =>
            {
                if (metadata.Count > 0)
                {
                    reporters.PostEvents.Add(new PostTelemetry(teamProject, packageId, cmfPackage.Version, type, name, metadata, tags));
                }
            };
            reporters.RowAdderWithTags = (type, name, innerTags, metadata) =>
            {
                (innerTags ??= []).AddRange(tags);

                if (metadata.Count > 0)
                {
                    reporters.PostEvents.Add(new PostTelemetry(teamProject, packageId, cmfPackage.Version, type, name, metadata, innerTags));
                }
            };

            var invalidContentToPack = new Dictionary<string, string>();
            var processRules = new Dictionary<string, int>();

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
                    if (hasVersionMapping)
                    {
                        var unReferencedContent = workingDir
                                                    .GetFiles(content.Source.Replace("$(version)/", ""), SearchOption.AllDirectories)
                                                    .Where(file => !file.FullName.Contains(cmfPackage.Version))
                                                    .ToArray();

                        RunAuditorsPerContentType(cmfPackage, rootDir, reporters, processRules, content, unReferencedContent, "UnReferenced ");
                    }

                    var files = workingDir.GetFiles(contentPath, SearchOption.AllDirectories);

                    RunAuditorsPerContentType(cmfPackage, rootDir, reporters, processRules, content, files);
                }
                catch (DirectoryNotFoundException ex)
                {
                    // Error referencing file that does not exist
                    Log.Error(ex.Message);
                    invalidContentToPack.TryAdd(content.Source, contentPath);
                }
            }

            if (reporters.chart.Data.Count > 0)
            {
                AnsiConsole.Write(reporters.chart);

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[blue]────────────────────────────────────────[/]").Centered());
                AnsiConsole.WriteLine();
            }

            if (invalidContentToPack.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, invalidContentToPack, ["Source", "Resolved Source"], "Invalid Data Package Content to Pack", Spectre.Console.Color.Yellow));
            }
            if (processRules.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, processRules.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()), ["Execution", "Number of Process Rules"], "Process Rules", Spectre.Console.Color.Green));
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

            #endregion Post Event

            #region Generate Report

            if (generateReport)
            {
                Log.Information($"Data {cmfPackage.PackageName} Generating Reports...");
                var reportOutputDirectory = this.fileSystem.DirectoryInfo.New(reportOutputFolder);
                reportOutputDirectory.Create();

                var auditFolder = reportOutputDirectory.CreateSubdirectory("Review");
                var rootAuditFolder = auditFolder.CreateSubdirectory($"Data_{cmfPackage.PackageName}");

                foreach (var report in reporters.PanelReports)
                {
                    report(rootAuditFolder, null);
                }

                foreach (var report in reporters.TableReports)
                {
                    report(rootAuditFolder);
                }

                AuditUtilities.MergeDirectoryCSVsIntoExcel(rootAuditFolder);

                Log.Information($"Data {cmfPackage.PackageName} Generated Reports");
            }

            #endregion Generate Report

            Log.Information($"Successfully audited the data package - {cmfPackage.PackageId}");
        }

        private void RunAuditorsPerContentType(
            CmfPackage cmfPackage,
            IDirectoryInfo rootDir,
            Reporters reporters,
            Dictionary<string, int> processRules,
            ContentToPack? content,
            IFileInfo[] files,
            string appendName = "")
        {
            switch (content.ContentType)
            {
                case ContentType.ExportedObjects:
                    {
                        var nrOfExpObj = files.Length;
                        var auditorExportedFiles = AuditorExportedFiles(files, cmfPackage, reporters);
                        reporters.chart.AddItem($"{appendName}ExportedObjects", nrOfExpObj, ColorLimitsPerContentType(nrOfExpObj, ContentType.ExportedObjects));

                        if (auditorExportedFiles.Any())
                        {
                            reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId, auditorExportedFiles.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()), ["Type of Exported Object", "Nr of Exported Objects"], "Exported Objects", Spectre.Console.Color.Green));
                        }
                        break;
                    }
                case ContentType.ProcessRulesPre:
                    {
                        var numberOfProcessRules = files.Where(file => file.Extension == ".cs").Count();
                        reporters.chart.AddItem($"{appendName}ProcessRulesPre", numberOfProcessRules, ColorLimitsPerContentType(numberOfProcessRules, ContentType.ProcessRulesPost));
                        reporters.RowAdder("ProcessRules", "Pre", new Dictionary<string, string>() { { "Number of Rules", numberOfProcessRules.ToString() } });
                        ProcessRuleAdder("Pre", processRules, appendName, numberOfProcessRules);
                        break;
                    }
                case ContentType.ProcessRulesPost:
                    {
                        var numberOfProcessRules = files.Where(file => file.Extension == ".cs").Count();
                        reporters.chart.AddItem($"{appendName}ProcessRulesPost", numberOfProcessRules, ColorLimitsPerContentType(numberOfProcessRules, ContentType.ProcessRulesPost));
                        reporters.RowAdder("ProcessRules", "Post", new Dictionary<string, string>() { { "Number of Rules", numberOfProcessRules.ToString() } });
                        ProcessRuleAdder("Post", processRules, appendName, numberOfProcessRules);
                        break;
                    }
                case ContentType.EntityTypes:
                    {
                        var filesEntityTypes = files.Where(file => file.Extension == ".cs");
                        var nrFilesET = filesEntityTypes.Count();
                        var listOfEntityTypes = new Dictionary<string, string[]>
                        {
                            { $"{appendName}Number of Entity Types", [nrFilesET.ToString()] }
                        };

                        filesEntityTypes.ForEach(file =>
                        {
                            var relativePath = file.FullName.Replace(rootDir.FullName, "").Replace("\\", "/");
                            // Entity types with the same file name in different folders are keyed by their path
                            if (!listOfEntityTypes.TryAdd(file.Name, [relativePath]))
                            {
                                listOfEntityTypes.TryAdd(relativePath, [relativePath]);
                            }
                        });

                        if (nrFilesET > 0)
                        {
                            reporters.chart.AddItem($"{appendName}EntityTypes", nrFilesET, ColorLimitsPerContentType(nrFilesET, ContentType.EntityTypes));
                            reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, listOfEntityTypes, "Entity Types", Spectre.Console.Color.Green, ["Identifier", "Value"]));

                            reporters.RowAdder("EntityTypes", "EntityTypes", listOfEntityTypes.ToDictionary(item => item.Key, item => JsonSerializer.Serialize(item.Value)));
                        }

                        break;
                    }
                case ContentType.DEE:
                    {
                        var filesDEEs = files.Where(file => file.Extension == ".cs" && !file.Name.Contains("DeeDevBase.cs"));
                        var listOfDEEs = new Dictionary<string, string[]>();

                        filesDEEs.ForEach(file =>
                        {
                            var deeContent = this.fileSystem.File.ReadAllLines(file.FullName);

                            for (int i = 0; i < deeContent.Length; i++)
                            {
                                if (deeContent[i].Contains("Start DEE Code"))
                                {
                                    reporters.RowAdder("DEE", "codeExecution", new() {
                                        { "FileName", file.Name },
                                        { "LoC", deeContent.Length.ToString() },
                                        { "Complexity", CSharpComplexityCalculator.CalculateClassComplexity(deeContent).ToString() },
                                    });

                                    listOfDEEs.TryAdd(file.FullName.Replace(rootDir.FullName, "").Replace("\\", "/"), [file.Name]);
                                }
                            }
                        });

                        var nrFilesDEES = listOfDEEs.Count;
                        listOfDEEs.Add($"{appendName}Number of DEEs", [nrFilesDEES.ToString()]);

                        if (nrFilesDEES > 0)
                        {
                            reporters.chart.AddItem($"{appendName}DEEs", nrFilesDEES, ColorLimitsPerContentType(nrFilesDEES, ContentType.DEE));
                            reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, listOfDEEs, "DEEs", Spectre.Console.Color.Green, ["Identifier", "Value"]));
                        }

                        break;
                    }
                case ContentType.MasterData:
                    {
                        var filesMDXML = files.Where(file => file.Extension == ".xml");
                        var filesMDJSON = files.Where(file => file.Extension == ".json").ToArray();
                        var nrOfFilesMDXML = filesMDXML.Count();
                        var nrOfFilesMDJSON = filesMDJSON.Length;

                        AuditorMasterDataJSON(filesMDJSON, cmfPackage, ref reporters, appendName);

                        if (nrOfFilesMDXML > 0)
                        {
                            reporters.chart.AddItem($"{appendName}Master Data XML", nrOfFilesMDXML, ColorLimitsPerContentType(nrOfFilesMDXML, ContentType.MasterData));
                        }
                        if (nrOfFilesMDJSON > 0)
                        {
                            reporters.chart.AddItem($"{appendName}Master Data JSON", nrOfFilesMDJSON, ColorLimitsPerContentType(nrOfFilesMDJSON, ContentType.MasterData));
                        }
                        break;
                    }
                case ContentType.AutomationWorkFlows:
                    {
                        var workflows = files.Where(file => file.Extension == ".json").ToArray();
                        var nrOfFilesAWfJSON = workflows.Length;

                        AuditorAutomationWorkflows(workflows, cmfPackage, ref reporters, appendName);

                        if (nrOfFilesAWfJSON > 0)
                        {
                            reporters.chart.AddItem($"{appendName}Automation Workflows", nrOfFilesAWfJSON, ColorLimitsPerContentType(nrOfFilesAWfJSON, ContentType.AutomationWorkFlows));
                        }
                        break;
                    }
            }

            static void ProcessRuleAdder(string name, Dictionary<string, int> processRules, string appendName = "", int numberOfProcessRules = 0)
            {
                var key = $"{appendName}{name}";
                if (processRules.ContainsKey(key))
                {
                    processRules[key] += numberOfProcessRules;
                }
                else
                {
                    processRules.Add($"{appendName}{name}", numberOfProcessRules);
                }
            }
        }

        // Severity color scales, ordered by ascending upper bound: the first entry whose bound is
        // >= the element count wins; anything above the last bound falls through to IndianRed.
        private static readonly (int UpperBound, Color Color)[] ExportedObjectsScale =
        [
            (0, Color.DarkGreen), (1, Color.Green4), (2, Color.Green3), (3, Color.SeaGreen1),
            (4, Color.Yellow3), (5, Color.Yellow), (10, Color.Gold3), (15, Color.Orange3),
            (20, Color.Red1), (25, Color.Red3)
        ];

        // Shared by ProcessRulesPre, ProcessRulesPost and EntityTypes.
        private static readonly (int UpperBound, Color Color)[] LowCountScale =
        [
            (0, Color.DarkGreen), (1, Color.Green4), (2, Color.Orange3), (4, Color.Red1), (6, Color.Red3)
        ];

        // Shared by DEE and MasterData.
        private static readonly (int UpperBound, Color Color)[] HighVolumeScale =
        [
            (0, Color.DarkGreen), (5, Color.Green4), (8, Color.Green3), (12, Color.SeaGreen1),
            (18, Color.Yellow3), (25, Color.Yellow), (30, Color.Gold3), (50, Color.Orange3),
            (60, Color.Red1), (70, Color.Red3)
        ];

        private static readonly (int UpperBound, Color Color)[] WorkflowScale =
        [
            (0, Color.DarkGreen), (2, Color.Green4), (3, Color.Green3), (5, Color.SeaGreen1),
            (8, Color.Yellow3), (13, Color.Yellow), (15, Color.Gold3), (17, Color.Orange3),
            (20, Color.Red1), (25, Color.Red3)
        ];

        internal static Color ColorLimitsPerContentType(int numberOfElements, ContentType type)
        {
            var scale = type switch
            {
                ContentType.ExportedObjects => ExportedObjectsScale,
                ContentType.ProcessRulesPre or ContentType.ProcessRulesPost or ContentType.EntityTypes => LowCountScale,
                ContentType.DEE or ContentType.MasterData => HighVolumeScale,
                ContentType.AutomationWorkFlows => WorkflowScale,
                _ => null
            };

            if (scale == null)
            {
                return Color.IndianRed;
            }

            foreach (var (upperBound, color) in scale)
            {
                if (numberOfElements <= upperBound)
                {
                    return color;
                }
            }

            return Color.IndianRed;
        }

        internal static void AuditorAutomationWorkflows(
            IFileInfo[] fileInfoAWfFiles,
            CmfPackage cmfPackage,
            ref Reporters reporters,
            string appendName = "")
        {
            List<List<string>> tasksListInfo = [];
            List<List<string>> convertersListInfo = [];
            List<List<string>> codeTasksListInfo = [];

            foreach (var workflow in fileInfoAWfFiles)
            {
                ComponentRoot parsed = null;
                try
                {
                    parsed = JsonSerializer.Deserialize<ComponentRoot>(workflow.ReadToString());

                    foreach (var task in parsed.Tasks)
                    {
                        List<string> taskListInfo = [];

                        taskListInfo.Add(workflow.Name.Replace(".json", ""));
                        taskListInfo.Add(task.Reference?.Name);
                        taskListInfo.Add(task.Reference?.Package?.Name);
                        taskListInfo.Add(task.Reference?.Package?.Version);

                        if (task?.Settings != null)
                        {
                            taskListInfo.Add(task.Settings?.Inputs?.Count.ToString() ?? 0.ToString());
                            taskListInfo.Add(task.Settings?.Outputs?.Count.ToString() ?? 0.ToString());
                            taskListInfo.Add(task.Id.ToString());

                            // Code Task Review
                            if (task.Settings?.TsCode != null)
                            {
                                var codeTaskLoC = task.Settings.TsCode.Count;
                                var numberOfServiceCalls = 0;
                                List<string> mesEntities = [];
                                foreach (var loc in task.Settings.TsCode)
                                {
                                    if (loc.Contains("this.framework.system.call("))
                                    {
                                        numberOfServiceCalls += 1;
                                    }
                                    if (loc.Contains("$type"))
                                    {
                                        var match = Regex.Match(loc, @"\[\s*""\$type""\s*\]\s*=\s*""([^""]+)""");
                                        if (match.Success)
                                        {
                                            mesEntities.Add(match.Groups[1].Value);
                                        }
                                        else
                                        {
                                            mesEntities.Add(loc);
                                        }
                                    }
                                }

                                var complexity = TSComplexityCalculator.CalculateClassComplexity(task.Settings.TsCode.ToArray()).ToString();

                                reporters.RowAdder("CodeTask", "codeExecution", new() {
                                    { "Page", taskListInfo[0] },
                                    { "TaskId", task.Id },
                                    { "LoC", codeTaskLoC.ToString() },
                                    { "Nr of Service Calls", numberOfServiceCalls.ToString() },
                                    { "MES Entities Mentioned",string.Join(';', mesEntities) },
                                    { "Complexity", complexity },
                                });

                                codeTasksListInfo.Add(
                                [
                                    workflow.Name.Replace(".json", ""),
                                    task.Id,
                                    codeTaskLoC.ToString(),
                                    numberOfServiceCalls.ToString(),
                                    string.Join(';', mesEntities),
                                    complexity
                                ]);
                            }

                            reporters.RowAdder("Task", taskListInfo[1], new() {
                                { "Page", taskListInfo[0] },
                                { "Package", taskListInfo[2] },
                                { "Version", taskListInfo[3] },
                                { "TaskId", taskListInfo[6] },
                                { "Nr. of Inputs", taskListInfo[4] },
                                { "Nr. of Outputs", taskListInfo[5] },
                            });
                            tasksListInfo.Add(taskListInfo);
                        }
                    }

                    foreach (var converter in parsed?.Converters ?? [])
                    {
                        List<string> converterListInfo = [];

                        converterListInfo.Add(workflow.Name.Replace(".json", ""));
                        converterListInfo.Add(converter.Reference?.Name);
                        converterListInfo.Add(converter.Reference?.Package?.Name);
                        converterListInfo.Add(converter.Reference?.Package?.Version);

                        reporters.RowAdder("Converter", converterListInfo[1], new() {
                            { "Page", converterListInfo[0] },
                            { "Package", converterListInfo[2] },
                            { "Version", converterListInfo[3] }
                        });
                        convertersListInfo.Add(converterListInfo);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error parsing {workflow} - {ex.Message}");
                }
            }

            if (tasksListInfo.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId,
                                        tasksListInfo,
                                        ["Page", "Name", "Package", "Version", "Nr. of Inputs", "Nr. of Outputs", "TaskId"],
                                        $"{appendName}Tasks Summary", Spectre.Console.Color.Green));
            }

            if (codeTasksListInfo.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId,
                                        codeTasksListInfo,
                                        ["Page", "Id", "Lines of Code", "Number of Service Calls", "Nr. of Explicit MES Entities", "Cyclomatic Complexity"],
                                        $"{appendName}Code Tasks Summary", Spectre.Console.Color.Green));
            }

            if (convertersListInfo.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(cmfPackage.PackageId,
                                        convertersListInfo,
                                        ["Page", "Name", "Package", "Version"],
                                        $"{appendName}Converters Summary", Spectre.Console.Color.Green));
            }
        }

        internal static Reporters AuditorMasterDataJSON(
            IFileInfo[] fileInfoMasterDataFiles,
            CmfPackage cmfPackage,
            ref Reporters reporters,
            string appendName = "")
        {
            var entityNr = new Dictionary<string, int>();
            var entity = new Dictionary<string, List<object>>();
            foreach (IFileInfo file in fileInfoMasterDataFiles)
            {
                var masterdata = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(file.ReadToString());

                foreach (var item in masterdata?.Keys)
                {
                    if (item != "WorksheetNameMapping" && item != "Index")
                    {
                        if (entityNr.ContainsKey(item))
                        {
                            entity[item].AddRange(masterdata[item].Select(x => x.Value).ToList());
                            entityNr[item] += masterdata[item].Count;
                        }
                        else
                        {
                            entity.Add(item, masterdata[item].Select(x => x.Value).ToList());
                            entityNr.Add(item, masterdata[item].Count);
                        }
                    }
                }
            }

            foreach (var item in entity)
            {
                foreach (var value in item.Value)
                {
                    var identifier = ExtractValuesFromJSONObject((JsonElement)value, ["Name", "Action"]);
                    var result = new Dictionary<string, string>() { { "Content", JsonSerializer.Serialize(value) } };

                    // Try to always store data by a know name, if we are not able to do that, provide a unique id
                    reporters.RowAdderWithTags(item.Key, identifier.FirstOrDefault().Value ?? Guid.NewGuid().ToString(), !string.IsNullOrEmpty(appendName) ? [new Tag("Context", appendName.Trim())] : null, result);
                }
            }

            if (entityNr.Count > 0)
            {
                reporters.PanelReports.Add(AuditUtilities.DisplayPanel(
                                                cmfPackage.PackageId,
                                                entityNr.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
                                                $"{appendName}Nr of Entities",
                                                Spectre.Console.Color.Green,
                                                ["Entity", "Nr Of Records"]));
            }

            #region Generate Barchart for Entities

            var barchart = new BarChart()
                                .Label($"[green bold underline]{appendName}{cmfPackage.PackageId} Entities[/]")
                                .CenterLabel();

            if (entityNr.Count > 0)
            {
                foreach (var item in entityNr)
                {
                    barchart.AddItem($"{appendName}{item.Key}", item.Value, ColorLimitsPerContentType(item.Value, ContentType.MasterData));
                }

                AnsiConsole.Write(barchart);

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[blue]────────────────────────────────────────[/]").Centered());
                AnsiConsole.WriteLine();
            }

            #endregion Generate Barchart for Entities

            #region Generate Entity Table

            var entities = new List<List<string>>();
            foreach (var table in entity)
            {
                foreach (var row in table.Value)
                {
                    Dictionary<string, string> valuesFromJsonObject =
                        table.Key == "<SM>DEEAction"
                        ? ExtractValuesFromJSONObject((JsonElement)row, ["Action", "Description"])
                        : ExtractValuesFromJSONObject((JsonElement)row, ["Name", "Description"]);

                    entities.Add([table.Key, .. valuesFromJsonObject.Values]);
                }
            }

            if (entities.Count > 0)
            {
                reporters.TableReports.Add(AuditUtilities.DisplayTable(
                    cmfPackage.PackageId,
                    entities,
                    ["Type of Entity", "Name", "Description", "Extra"],
                    $"{appendName}Entity Row Name and Description",
                    Spectre.Console.Color.Green));
            }

            #endregion Generate Entity Table

            return reporters;
        }

        internal static Dictionary<string, int> AuditorExportedFiles(IFileInfo[] fileInfoExportedFiles, CmfPackage cmfPackage, Reporters reporters)
        {
            var numberOfExportedObjectsByType = new Dictionary<string, int>();
            foreach (var item in fileInfoExportedFiles.Where(file => file.Extension == ".xml"))
            {
                (var type, var name) = ExtractTypeAndName(item.ReadToString());
                type ??= "Unknown";

                if (!numberOfExportedObjectsByType.TryAdd(type, 1))
                {
                    numberOfExportedObjectsByType[type] += 1;
                }
                reporters.RowAdder("Exported Objects", type, new() { { "Name", name } });
            }

            return numberOfExportedObjectsByType;
        }

        internal static (string, string) ExtractTypeAndName(string xml)
        {
            var doc = XDocument.Parse(xml);
            var objElement = doc.Descendants("Object").FirstOrDefault();

            if (objElement == null)
            {
                return (null, null);
            }

            var typeAttribute = objElement.Attribute("type")?.Value;
            var objectType = typeAttribute?.Split(',')[0].Trim();
            var name = objElement?.Element("Name")?.Attribute("value")?.Value ?? objElement.Descendants("Name").FirstOrDefault()?.Attribute("value")?.Value;

            return (objectType, name);
        }

        internal static Dictionary<string, string> PackageMetadataSummary(CmfPackage cmfPackage)
        {
            var tableValuesCmfPackage = new Dictionary<string, string>
            {
                { "PackageId", cmfPackage.PackageId },
                { "PackageVersion", cmfPackage.Version },
                { "Number of First Level Dependencies", cmfPackage?.Dependencies?.Count.ToString() ?? "0" }
            };

            foreach (var item in cmfPackage.ContentToPack)
            {
                tableValuesCmfPackage.Add(item.Source, item.ContentType.ToString());
            }

            return tableValuesCmfPackage;
        }

        internal static Dictionary<string, string> ExtractValuesFromJSONObject(JsonElement jsonObject, string[] values)
        {
            Dictionary<string, string> extractedValues = [];
            foreach (var value in values)
            {
                if (jsonObject.TryGetProperty(value, out JsonElement extractedValue))
                {
                    var parsedExtractedValue = extractedValue.GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(parsedExtractedValue))
                    {
                        extractedValues.Add(value, parsedExtractedValue);
                    }
                }
            }
            return extractedValues;
        }
    }
}

public class ComponentRoot
{
    [JsonPropertyName("tasks")]
    public List<TaskItem> Tasks { get; set; }

    [JsonPropertyName("converters")]
    public List<ConverterItem> Converters { get; set; }
}

public class ConverterItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("reference")]
    public Reference Reference { get; set; }
}

public class TaskItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("reference")]
    public Reference Reference { get; set; }

    [JsonPropertyName("settings")]
    public Settings Settings { get; set; }

    [JsonPropertyName("driver")]
    public string Driver { get; set; }
}

public class Reference
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("package")]
    public Package Package { get; set; }
}

public class Package
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class InputOutput
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("valueType")]
    public JsonElement ValueType { get; set; }

    [JsonPropertyName("defaultValue")]
    public JsonElement DefaultValue { get; set; }
}

public class InputOutputValueType
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Settings
{
    [JsonPropertyName("tsCode")]
    public List<string> TsCode { get; set; }

    [JsonPropertyName("inputs")]
    public List<InputOutput> Inputs { get; set; }

    [JsonPropertyName("outputs")]
    public List<InputOutput> Outputs { get; set; }
}
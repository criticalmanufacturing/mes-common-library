using audit.Objects;
using audit.Services;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using System.Text.Json;
using System.Xml.Linq;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "root" command group
    /// </summary>
    [CmfCommand("root", Parent = "review", Description = "Will audit the Root package of an already checked-out project")]
    public class RootAuditor : BaseCommand
    {
        #region Public Constructors

        public RootAuditor() : this(new FileSystem())
        {
        }

        public RootAuditor(IFileSystem fileSystem) : base(fileSystem)
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

            if (workingDir.GetFiles("cmfpackage.json").Count() == 0)
            {
                throw new CliException($"No cmfpackage.json found in '{workingDir.FullName}'. Run 'review root' from inside an already checked-out project root, or use 'review-remote-ado root' to fetch one from Azure DevOps.");
            }

            IFileInfo cmfPackageFile = this.fileSystem.FileInfo.New($"{workingDir.FullName}/cmfpackage.json");
            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);

            Func<string, Task<string>> getFileContent = path => AuditUtilities.GetLocalFileContentAsync(workingDir, this.fileSystem, path);
            var allFilePaths = EnumerateScriptFilePaths(workingDir);

            await RunAuditAsync(workingDir, cmfPackage, teamProject, getFileContent, allFilePaths, generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }

        #endregion Public Methods

        internal static IEnumerable<string> EnumerateScriptFilePaths(IDirectoryInfo workingDir)
        {
            static bool IsScriptExtension(string extension)
                => extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".sh", StringComparison.OrdinalIgnoreCase);

            static bool IsExcludedDirectory(IDirectoryInfo directory)
                => directory.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
                   || directory.Name.Equals(".nuget", StringComparison.OrdinalIgnoreCase);

            var rootPathPrefix = workingDir.FullName.Replace('\\', '/') + "/";
            var pending = new Stack<IDirectoryInfo>();
            pending.Push(workingDir);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                foreach (var directory in current.GetDirectories())
                {
                    if (!IsExcludedDirectory(directory))
                    {
                        pending.Push(directory);
                    }
                }

                foreach (var file in current.GetFiles("*", SearchOption.TopDirectoryOnly))
                {
                    if (IsScriptExtension(file.Extension))
                    {
                        yield return file.FullName.Replace('\\', '/').Replace(rootPathPrefix, "");
                    }
                }
            }
        }

        /// <summary>
        /// Runs the Root package audit against an already-resolved local working directory. Shared by
        /// the local "review root" command and the "review-remote-ado root" command (<see cref="RootAuditorRemote"/>),
        /// which differ only in how <paramref name="getFileContent"/>/<paramref name="allFilePaths"/> are sourced.
        /// </summary>
        protected async Task RunAuditAsync(
            IDirectoryInfo workingDir, CmfPackage cmfPackage, string teamProject,
            Func<string, Task<string>> getFileContent, IEnumerable<string> allFilePaths,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            Log.Debug($"{DateTime.Now}: Project Metadata Summary and Package Metadata Summary");
            var results = await Task.WhenAll(ProjectMetadataSummary(getFileContent, cmfPackage), PackageMetadataSummary(getFileContent, cmfPackage));

            List<Tag> tags = [new("PackageType", "Root")];
            var reporters = new Reporters(
                tableReports:
                [
                    AuditUtilities.DisplayTable(cmfPackage.PackageId, results[0], ["Identifier", "Value"], "Project Configuration", Color.Green),
                    AuditUtilities.DisplayTable(cmfPackage.PackageId, results[1], ["Identifier", "Value"], "Package", Color.Green)
                ],
                postEvents:
                [
                    new PostTelemetry(teamProject, cmfPackage.PackageId, cmfPackage.Version, "ProjectMetadata", "ProjectMetadata", results[0], tags),
                    new PostTelemetry(teamProject, cmfPackage.PackageId, cmfPackage.Version, "PackageMetadata", "PackageMetadata", results[1], tags)
                ]
            );
            reporters.RowAdder = (type, name, metadata) =>
            {
                if (metadata.Count > 0)
                {
                    reporters.PostEvents.Add(new PostTelemetry(teamProject, cmfPackage.PackageId, cmfPackage.Version, type, name, metadata, tags));
                }
            };


            Log.Debug($"{DateTime.Now}: ValidateUnusedPackages");
            var unusedPackages = ValidateUnusedPackages(cmfPackage, workingDir);
            reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, unusedPackages, "Unused Packages", Color.Red, ["Package Name", "Version"]));
            Log.Debug($"{DateTime.Now}: Finished ValidateUnusedPackages");

            Log.Debug($"{DateTime.Now}: ValidateCommittedScripts");
            var commitedScripts = ValidateCommittedScripts(allFilePaths);
            reporters.PanelReports.Add(AuditUtilities.DisplayPanel(cmfPackage.PackageId, commitedScripts, "Comitted Scripts", Color.Red, ["File Name", "Path"]));
            Log.Debug($"{DateTime.Now}: Finished ValidateCommittedScripts");

            reporters.RowAdder("Root Validators", "Unused Packages", unusedPackages);
            reporters.RowAdder("Root Validators", "Comitted Scripts", commitedScripts);

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
                Log.Information("Root Generating Reports...");
                var reportOutputDirectory = this.fileSystem.DirectoryInfo.New(reportOutputFolder);
                reportOutputDirectory.Create();

                var auditFolder = reportOutputDirectory.CreateSubdirectory("Review");
                var rootAuditFolder = auditFolder.CreateSubdirectory("Root");

                foreach (var report in reporters.TableReports)
                {
                    report(rootAuditFolder);
                }

                foreach (var report in reporters.PanelReports)
                {
                    report(rootAuditFolder, null);
                }

                AuditUtilities.MergeDirectoryCSVsIntoExcel(rootAuditFolder);

                Log.Information("Root Generated Reports");
            }

            #endregion

            Log.Information($"Successfully audited the root package");
        }

        /// <summary>
        /// Create a Package Metadata Summary
        /// </summary>
        /// <param name="getFileContent">Reads the raw text content of a repo-root-relative path, or null if it doesn't exist.</param>
        /// <param name="cmfPackage"></param>
        /// <returns></returns>
        internal static async Task<Dictionary<string, string>> PackageMetadataSummary(Func<string, Task<string>> getFileContent, CmfPackage cmfPackage)
        {
            try
            {
                var tableValuesCmfPackage = new Dictionary<string, string>();

                var globalJsonRaw = await getFileContent("global.json");
                var globalJson = globalJsonRaw != null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(globalJsonRaw) : null;
                var repositoriesRaw = await getFileContent("repositories.json");
                var repositories = repositoriesRaw != null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(repositoriesRaw) : null;
                (string sdkVersion, string strategy) = ExtractVersionAndStrategy(globalJson);
                var nugetXML = await getFileContent("NuGet.Config");
                var nugetSources = ParseNugetFeeds(nugetXML);

                tableValuesCmfPackage.Add("PackageId", cmfPackage.PackageId);
                tableValuesCmfPackage.Add("PackageVersion", cmfPackage.Version);
                tableValuesCmfPackage.Add("Number of First Level Dependencies", cmfPackage?.Dependencies?.Count.ToString() ?? "0");
                tableValuesCmfPackage.Add("Number of Test Packages", cmfPackage?.TestPackages?.Count.ToString() ?? "0");

                tableValuesCmfPackage.Add("GlobalJson Version", sdkVersion);
                tableValuesCmfPackage.Add("GlobalJson Strategy", strategy);

                if (repositories != null)
                {
                    (string ciRepo, List<string> repositoriesLocations) = ExtractRepositories(repositories);
                    tableValuesCmfPackage.Add("CIRepository", ciRepo);

                    for (int i = 0; i < repositoriesLocations.Count; i++)
                    {
                        tableValuesCmfPackage.Add($"Repository {i + 1}", repositoriesLocations[i]);
                    }
                    tableValuesCmfPackage.Add($"Number of Repositories", repositoriesLocations.Count.ToString());
                }

                foreach (var nugetSource in nugetSources)
                {
                    tableValuesCmfPackage.Add($"Nuget Source {nugetSource.Key}", nugetSource.Value);
                }

                return tableValuesCmfPackage;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to Execute Package Metadata Summary for {cmfPackage.PackageId}", ex);
            }
        }

        /// <summary>
        /// Create a Project Metadata Summary
        /// </summary>
        /// <param name="getFileContent">Reads the raw text content of a repo-root-relative path, or null if it doesn't exist.</param>
        /// <param name="cmfPackage"></param>
        /// <returns></returns>
        internal static async Task<Dictionary<string, string>> ProjectMetadataSummary(
            Func<string, Task<string>> getFileContent,
            CmfPackage cmfPackage)
        {
            try
            {
                var projectContent = await getFileContent(".project-config.json")
                    ?? throw new CliException("Could not find .project-config.json in the project root.");
                ProjectConfig projectConfig = JsonConvert.DeserializeObject<ProjectConfig>(projectContent);

                // Build the result directly – no reflection, no property maps.
                var tableProjectConfiguration = new Dictionary<string, string>
                {
                    [nameof(ProjectConfig.ProjectName)] = projectConfig.ProjectName ?? string.Empty,
                    [nameof(ProjectConfig.NPMRegistry)] = projectConfig.NPMRegistry?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.NuGetRegistry)] = projectConfig.NuGetRegistry?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.MESVersion)] = projectConfig.MESVersion?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.YoGeneratorVersion)] = projectConfig.YoGeneratorVersion?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.NGXSchematicsVersion)] = projectConfig.NGXSchematicsVersion?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.NugetVersion)] = projectConfig.NugetVersion?.ToString() ?? string.Empty,
                    [nameof(ProjectConfig.TestScenariosNugetVersion)] = projectConfig.TestScenariosNugetVersion?.ToString() ?? string.Empty
                };

                return tableProjectConfiguration;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Failed to Execute Project Metadata Summary for {cmfPackage.PackageId}",
                    ex);
            }
        }

        /// <summary>
        /// Search for Packages that exist but are not referenced by root tree
        /// </summary>
        /// <param name="cmfPackage"></param>
        /// <param name="workingDir"></param>
        /// <returns></returns>
        internal static Dictionary<string, string> ValidateUnusedPackages(CmfPackage cmfPackage, IDirectoryInfo workingDir)
        {
            cmfPackage.LoadDependencies([], null, true);
            var packagesThatDependOnRoot = FlattenDependencies(cmfPackage);
            List<CmfPackage> cmfPackagesInRepo = [.. workingDir.GetFiles("cmfpackage.json", SearchOption.AllDirectories).Select(pkg => CmfPackage.Load(pkg, setDefaultValues: true, workingDir.FileSystem))];

            var unusedPackages = new Dictionary<string, string>();
            foreach (var pkgInRepo in cmfPackagesInRepo)
            {
                if (!packagesThatDependOnRoot.Any(pkgDependsOnRoot => pkgDependsOnRoot.Name == pkgInRepo.Name && pkgDependsOnRoot.Version == pkgInRepo.Version))
                {
                    unusedPackages.Add(pkgInRepo.PackageName, pkgInRepo.Version);
                }
            }

            return unusedPackages;
        }

        /// <summary>
        /// Search for committed scripts
        /// </summary>
        /// <param name="filePaths">Every file path (relative to the project root) present in the project.</param>
        /// <returns></returns>
        internal static Dictionary<string, string> ValidateCommittedScripts(IEnumerable<string> filePaths)
        {
            static bool ContainsPathSegment(string path, string segment)
            {
                var startIndex = 0;
                while ((startIndex = path.IndexOf(segment, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var startBoundary = startIndex == 0 || path[startIndex - 1] == '/' || path[startIndex - 1] == '\\';
                    var endIndex = startIndex + segment.Length;
                    var endBoundary = endIndex == path.Length || path[endIndex] == '/' || path[endIndex] == '\\';

                    if (startBoundary && endBoundary)
                    {
                        return true;
                    }

                    startIndex = endIndex;
                }

                return false;
            }

            var scriptsInProject = filePaths
                                    .Where(path =>
                                        !ContainsPathSegment(path, "node_modules") &&
                                        !ContainsPathSegment(path, ".nuget") &&
                                        (
                                            path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                                            path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                                            path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                                            path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
                                        ))
                                    .ToList();

            var scripts = new Dictionary<string, string>();

            foreach (var script in scriptsInProject)
            {
                // Scripts with the same name in different folders are keyed by their full path
                if (!scripts.TryAdd(Path.GetFileName(script), script))
                {
                    scripts.TryAdd(script, script);
                }
            }
            return scripts;
        }

        /// <summary>
        /// Parser of global json .net
        /// </summary>
        /// <param name="globalJson"></param>
        /// <returns></returns>
        internal static (string, string) ExtractVersionAndStrategy(Dictionary<string, object> globalJson)
        {
            // Get the SDK version
            if (globalJson != null && globalJson.TryGetValue("sdk", out object sdkObj) && sdkObj is JsonElement sdkElement)
            {
                string sdkVersion = "";
                string strategy = "";
                if (sdkElement.TryGetProperty("version", out JsonElement versionElement))
                {
                    sdkVersion = versionElement.GetString(); ;
                }
                if (sdkElement.TryGetProperty("rollForward", out JsonElement strategyElement))
                {
                    strategy = strategyElement.GetString();
                }
                return (sdkVersion, strategy);
            }
            return (null, null);
        }

        /// <summary>
        /// Parser for repositories file
        /// </summary>
        /// <param name="repositoryContent"></param>
        /// <returns></returns>
        internal static (string, List<string>) ExtractRepositories(Dictionary<string, object> repositoryContent)
        {
            var ciRepo = "";
            List<string> repositories = [];
            if (repositoryContent.TryGetValue("CIRepository", out object repository) && repository is JsonElement repositoryElement)
            {
                ciRepo = repositoryElement.GetString();
            }

            // Parse Repositories array
            if (repositoryContent.TryGetValue("Repositories", out object reposObj)
                && reposObj is JsonElement reposElement
                && reposElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var repoElement in reposElement.EnumerateArray())
                {
                    if (repoElement.ValueKind == JsonValueKind.String)
                    {
                        repositories.Add(repoElement.GetString() ?? string.Empty);
                    }
                }
            }
            return (ciRepo, repositories);
        }

        /// <summary>
        /// Parser of the nuget feeds declared
        /// </summary>
        /// <param name="xmlContent"></param>
        /// <returns></returns>
        internal static Dictionary<string, string> ParseNugetFeeds(string xmlContent)
        {
            var packageSources = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return packageSources;
            }

            var doc = XDocument.Parse(xmlContent);

            // Find all package source entries
            var sourceElements = doc.Root?.Element("packageSources")?.Elements("add");
            if (sourceElements != null)
            {
                foreach (var element in sourceElements)
                {
                    var key = element.Attribute("key")?.Value;
                    var value = element.Attribute("value")?.Value;

                    if (key != null && value != null)
                    {
                        packageSources[key] = value;
                    }
                }
            }

            return packageSources;
        }

        /// <summary>
        /// Flatten tree into a list
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="flattendDeps"></param>
        /// <returns></returns>
        internal static List<CmfPackage> FlattenDependencies(CmfPackage pkg, List<CmfPackage> flattendDeps = null)
        {
            flattendDeps ??= new List<CmfPackage>();
            flattendDeps.Add(pkg);

            if (pkg.Dependencies.HasAny())
            {
                for (int i = 0; i < pkg.Dependencies.Count; i++)
                {
                    Dependency dependency = pkg.Dependencies[i];
                    if (!dependency.IsMissing)
                    {
                        FlattenDependencies(dependency.CmfPackage, flattendDeps);
                    }
                }
            }

            return flattendDeps;
        }
    }
}
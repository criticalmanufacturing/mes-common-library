using audit.Objects;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using System.Reflection;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "init" command: deploys the Cmf.Audit package and, for Azure DevOps projects, the audit pipelines
    /// (optionally merging the Audit Review stage into the project's own CD-Containers pipeline).
    /// </summary>
    [CmfCommand("init", Description = "Deploys the audit pipelines and the Cmf.Audit package into the project")]
    public class Init : TemplateCommand
    {
        public const string AuditPackageName = "Cmf.Audit";

        /// <summary>
        /// Pipeline files owned by the audit: written on a clean init, only overwritten with --force.
        /// </summary>
        internal static readonly string[] AuditBuildFiles =
        [
            "CD-AuditReview.yml",
            ".tasks/assembling-audit-package.yml",
            ".tasks/audit-review.yml",
            ".tasks/install-cmf-audit.yml",
            ".tasks/run-audit-review.yml",
        ];

        /// <summary>
        /// Generic tasks the audit depends on, which projects initialized by cmf-pipeline already have:
        /// only written when missing, never overwritten.
        /// </summary>
        internal static readonly string[] SharedBuildFiles =
        [
            ".tasks/use-dotnet-version.yml",
            ".tasks/use-node-version.yml",
        ];

        /// <summary>
        /// Current version of cmf-audit, pinned in the generated pipelines.
        /// </summary>
        internal static readonly string? CmfAuditVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];

        #region Public Constructors

        public Init() : base("audit-init")
        {
        }

        public Init(IFileSystem fileSystem) : base("audit-init", fileSystem)
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
            cmd.AddOption(new Option<bool?>(
                aliases: ["--azureDevOps"],
                description: "Whether the project uses Azure DevOps pipelines. Prompted when not provided"));

            cmd.AddOption(new Option<bool?>(
                aliases: ["--includeInCDContainers"],
                description: "Whether to merge the Audit Review stage (installs Cmf.Audit and runs the audit) into Builds/CD-Containers.yml. Prompted when not provided (default: true)"));

            cmd.AddOption(new Option<string>(
                aliases: ["--agentPool"],
                description: "Azure DevOps agent pool. Defaults to the one used by CD-Containers, global.yml or .pipeline-config.json"));

            cmd.AddOption(new Option<string>(
                aliases: ["--defaultServiceConnection"],
                description: "Service connection used to pull the build-runner image. Defaults to the one used by CD-Containers or global.yml"));

            cmd.AddOption(new Option<string>(
                aliases: ["--cmfAuditVersion"],
                description: "Version of cmf-audit installed by the pipelines",
                getDefaultValue: () => CmfAuditVersion ?? "latest"));

            cmd.AddOption(new Option<Uri>(
                aliases: ["--cmfAuditRegistry"],
                description: "NPM registry the pipelines install cmf-audit from",
                getDefaultValue: () => new Uri(Environment.GetEnvironmentVariable("cmf_audit_registry") ?? "https://registry.npmjs.com/")));

            cmd.AddOption(new Option<bool>(
                aliases: ["--force"],
                description: "Overwrite the audit files (Cmf.Audit package and audit pipelines) if they already exist"));

            cmd.Handler = CommandHandler.Create<bool?, bool?, string, string, string, Uri, bool>(
                (azureDevOps, includeInCDContainers, agentPool, defaultServiceConnection, cmfAuditVersion, cmfAuditRegistry, force) =>
                    Execute(new InitArguments
                    {
                        AzureDevOps = azureDevOps,
                        IncludeInCDContainers = includeInCDContainers,
                        AgentPool = agentPool,
                        DefaultServiceConnection = defaultServiceConnection,
                        CmfAuditVersion = cmfAuditVersion,
                        CmfAuditRegistry = cmfAuditRegistry,
                        Force = force,
                    }));
        }

        /// <summary>
        /// Executes the init
        /// </summary>
        public void Execute(InitArguments args)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var projectRoot = FileSystemUtilities.GetProjectRoot(this.fileSystem, throwException: true);

            if (IsGitHubProject(projectRoot))
            {
                throw new CliException("'audit init' is currently not supported for projects hosted on GitHub.");
            }

            var auditPackage = CopyAuditPackage(projectRoot, args.Force);

            var azureDevOps = Ask(args.AzureDevOps, "Is this project using [green]Azure DevOps[/] pipelines?", defaultValue: true);
            if (!azureDevOps)
            {
                Log.Warning("Not an Azure DevOps project: skipping the audit pipelines, nothing will be copied into the Builds folder.");
                return;
            }

            var builds = this.fileSystem.DirectoryInfo.New(this.fileSystem.Path.Combine(projectRoot.FullName, "Builds"));
            var cdContainersFile = this.fileSystem.FileInfo.New(this.fileSystem.Path.Combine(builds.FullName, "CD-Containers.yml"));
            var cdContainers = cdContainersFile.Exists ? this.fileSystem.File.ReadAllText(cdContainersFile.FullName) : null;

            GenerateAuditPipelines(builds, cdContainers, args);
            var declaredInGlobalYml = DeclareAuditPackageInGlobalYml(builds, auditPackage);

            if (cdContainers == null)
            {
                Log.Information("No Builds/CD-Containers.yml found, skipping the Audit Review stage integration.");
            }
            else if (Ask(args.IncludeInCDContainers, "Include the [green]Audit Review[/] stage in CD-Containers?", defaultValue: true))
            {
                MergeIntoCdContainers(builds, cdContainersFile, cdContainers, declaredInGlobalYml ? null : auditPackage);
            }

            Log.Information("Audit initialization finished.");
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Whether the project is hosted on GitHub: running inside GitHub Actions, or the enclosing
        /// git repository has a remote pointing to github.com.
        /// </summary>
        private bool IsGitHubProject(IDirectoryInfo projectRoot)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (var dir = projectRoot; dir != null; dir = dir.Parent)
            {
                var gitConfig = this.fileSystem.Path.Combine(dir.FullName, ".git", "config");
                if (this.fileSystem.File.Exists(gitConfig))
                {
                    return this.fileSystem.File.ReadAllLines(gitConfig)
                        .Any(line => line.TrimStart().StartsWith("url", StringComparison.OrdinalIgnoreCase)
                                     && line.Contains("github.com", StringComparison.OrdinalIgnoreCase));
                }
            }

            return false;
        }

        /// <summary>
        /// Copies the Cmf.Audit package matching the project's MES version to the project root.
        /// It is deliberately not a dependency of the project's root package: the audit pipelines install it
        /// into the environment on their own, so it has its own lifecycle. Returns it as "Id@version".
        /// </summary>
        private string CopyAuditPackage(IDirectoryInfo projectRoot, bool force)
        {
            var mesVersion = CLI.Core.Objects.ExecutionContext.Instance.ProjectConfig?.MESVersion
                ?? throw new CliException("Could not resolve the project's MES version from .project-config.json.");

            var target = this.fileSystem.Path.Combine(projectRoot.FullName, AuditPackageName);
            if (this.fileSystem.Directory.Exists(target) && !force)
            {
                Log.Warning($"'{AuditPackageName}' already exists in the project root, skipping it. Use --force to overwrite it.");
            }
            else
            {
                var source = ResolveAuditPackageTemplate(mesVersion);
                FileSystemUtilities.CopyDirectory(source, target, this.fileSystem, null, true, false);
                AlignMesDependencies(this.fileSystem, this.fileSystem.DirectoryInfo.New(target), mesVersion);
                Log.Information($"Copied the '{AuditPackageName}' package for MES {mesVersion.ToString(3)} to '{target}'.");
            }

            var auditPackage = JObject.Parse(this.fileSystem.File.ReadAllText(this.fileSystem.Path.Combine(target, "cmfpackage.json")));
            var version = auditPackage["version"]?.Value<string>() ?? throw new CliException($"'{AuditPackageName}' has no version in its cmfpackage.json.");
            return $"{AuditPackageName}@{version}";
        }

        /// <summary>
        /// The Cmf.Audit package is kept per MES minor version (resources/audit_package/&lt;Major&gt;.&lt;Minor&gt;/Cmf.Audit),
        /// as its exported objects are bound to it; the patch is aligned with the project afterwards.
        /// </summary>
        private string ResolveAuditPackageTemplate(Version mesVersion)
        {
            var templates = this.fileSystem.DirectoryInfo.New(this.fileSystem.Path.Combine(AppContext.BaseDirectory, "resources", "audit_package"));
            var source = this.fileSystem.Path.Combine(templates.FullName, $"{mesVersion.Major}.{mesVersion.Minor}", AuditPackageName);
            if (!this.fileSystem.Directory.Exists(source))
            {
                var supported = templates.Exists
                    ? templates.GetDirectories()
                        .Where(dir => Version.TryParse(dir.Name, out _) && this.fileSystem.Directory.Exists(this.fileSystem.Path.Combine(dir.FullName, AuditPackageName)))
                        .Select(dir => dir.Name)
                        .OrderBy(Version.Parse)
                        .ToList()
                    : [];
                throw new CliException($"The '{AuditPackageName}' package is not available for MES {mesVersion.Major}.{mesVersion.Minor}. Supported MES versions: {(supported.Count > 0 ? string.Join(", ", supported) : "none")}.");
            }
            return source;
        }

        /// <summary>
        /// Aligns the dependencies on MES packages (those with the same major.minor as the MES version, e.g. Cmf.Environment)
        /// of every cmfpackage.json in <paramref name="package"/> with the project's MES patch version.
        /// </summary>
        internal static void AlignMesDependencies(IFileSystem fileSystem, IDirectoryInfo package, Version mesVersion)
        {
            var projectVersion = $"{mesVersion.Major}.{mesVersion.Minor}.{Math.Max(mesVersion.Build, 0)}";

            foreach (var cmfPackageFile in package.GetFiles("cmfpackage.json", SearchOption.AllDirectories))
            {
                var cmfPackage = JObject.Parse(fileSystem.File.ReadAllText(cmfPackageFile.FullName));
                var changed = false;

                foreach (var dependency in (cmfPackage["dependencies"] as JArray)?.OfType<JObject>() ?? [])
                {
                    if (Version.TryParse(dependency["version"]?.Value<string>(), out var dependencyVersion)
                        && dependencyVersion.Major == mesVersion.Major
                        && dependencyVersion.Minor == mesVersion.Minor
                        && dependencyVersion.ToString() != projectVersion)
                    {
                        Log.Debug($"Aligning '{dependency["id"]}' dependency of '{cmfPackage["packageId"]}' from {dependencyVersion} to {projectVersion}");
                        dependency["version"] = projectVersion;
                        changed = true;
                    }
                }

                if (changed)
                {
                    fileSystem.File.WriteAllText(cmfPackageFile.FullName, cmfPackage.ToString(Newtonsoft.Json.Formatting.Indented));
                }
            }
        }

        /// <summary>
        /// Declares the package the audit pipelines install (CmfAuditPackageToInstall) in Builds/.vars/global.yml.
        /// Creates the file when the project has none. Returns whether the variable ended up declared there.
        /// </summary>
        private bool DeclareAuditPackageInGlobalYml(IDirectoryInfo builds, string auditPackage)
        {
            var globalYmlPath = this.fileSystem.Path.Combine(builds.FullName, ".vars", "global.yml");
            var globalYml = ReadIfExists(globalYmlPath);
            if (globalYml == null)
            {
                // CD-AuditReview always references .vars/global.yml, so give it one when the project has none yet
                this.fileSystem.Directory.CreateDirectory(this.fileSystem.Path.GetDirectoryName(globalYmlPath)!);
                globalYml = "variables:\n";
                this.fileSystem.File.WriteAllText(globalYmlPath, globalYml);
                Log.Information("Created 'Builds/.vars/global.yml'.");
            }

            var updated = PipelineYamlMerger.AddVariable(globalYml, PipelineYamlMerger.AuditPackageVariable, $"'{auditPackage}'",
                "Cmf.Audit customization package installed by the audit pipelines (Id@version)");
            if (updated != globalYml)
            {
                this.fileSystem.File.WriteAllText(globalYmlPath, updated);
                Log.Information($"Added the '{PipelineYamlMerger.AuditPackageVariable}' variable to 'Builds/.vars/global.yml'.");
            }
            return true;
        }

        /// <summary>
        /// Renders the audit pipelines into a staging folder and copies them into Builds,
        /// resolving the values that were hardcoded per project from the project's own pipelines.
        /// </summary>
        private void GenerateAuditPipelines(IDirectoryInfo builds, string? cdContainers, InitArguments args)
        {
            var globalYml = ReadIfExists(this.fileSystem.Path.Combine(builds.FullName, ".vars", "global.yml"));

            var agentPool = FirstNonEmpty(
                args.AgentPool,
                cdContainers == null ? null : PipelineYamlMerger.ReadPoolName(cdContainers),
                globalYml == null ? null : PipelineYamlMerger.ReadVariable(globalYml, "AgentPool"),
                ReadLegacyPipelineConfigAgentPool(builds),
                "Linux")!;
            var defaultServiceConnection = FirstNonEmpty(
                args.DefaultServiceConnection,
                cdContainers == null ? null : PipelineYamlMerger.ReadBuildRunnerProperty(cdContainers, "endpoint"),
                globalYml == null ? null : PipelineYamlMerger.ReadVariable(globalYml, "DefaultServiceConnection")) ?? string.Empty;
            var containerOptions = FirstNonEmpty(
                cdContainers == null ? null : PipelineYamlMerger.ReadBuildRunnerProperty(cdContainers, "options"),
                "--volume /mnt:/mnt")!;

            var (environments, defaultEnvironment) = ResolveEnvironments(builds, cdContainers);
            if (environments.Count == 0)
            {
                Log.Warning("No environments found (Builds/.vars/<Environment>.yml), CD-AuditReview will not have an Environment parameter.");
            }

            var cmfAuditRegistry = args.CmfAuditRegistry?.ToString() ?? "https://registry.npmjs.com/";
            var cmfAuditVersion = string.IsNullOrWhiteSpace(args.CmfAuditVersion) ? CmfAuditVersion ?? "latest" : args.CmfAuditVersion;

            var staging = this.fileSystem.DirectoryInfo.New(this.fileSystem.Path.Combine(this.fileSystem.Path.GetTempPath(), $"cmf-audit-init-{Guid.NewGuid():N}"));
            try
            {
                Log.Information("Generating the audit pipelines...");
                RunCommand(new List<string>
                {
                    "--output", staging.FullName,
                    "--agentPool", agentPool,
                    "--defaultServiceConnection", defaultServiceConnection,
                    // single-quoted YAML scalar, as the options usually start with "--", which the template engine would read as a flag
                    "--containerOptions", $"'{containerOptions.Replace("'", "''")}'",
                    "--hasEnvironments", (environments.Count > 0).ToString().ToLowerInvariant(),
                    "--defaultEnvironment", defaultEnvironment ?? string.Empty,
                    "--environmentValues", string.Join("\n", environments.Select(env => $"    - {env}")),
                    "--cmfAuditVersion", cmfAuditVersion,
                    "--cmfAuditRegistry", cmfAuditRegistry,
                });

                foreach (var file in AuditBuildFiles)
                {
                    CopyBuildFile(staging, builds, file, overwrite: args.Force, ownedByAudit: true);
                }
                foreach (var file in SharedBuildFiles)
                {
                    CopyBuildFile(staging, builds, file, overwrite: false, ownedByAudit: false);
                }
            }
            finally
            {
                if (staging.Exists)
                {
                    staging.Delete(recursive: true);
                }
            }
        }

        private void CopyBuildFile(IDirectoryInfo staging, IDirectoryInfo builds, string relativePath, bool overwrite, bool ownedByAudit)
        {
            var source = this.fileSystem.Path.Combine(staging.FullName, "Builds", relativePath);
            var target = this.fileSystem.Path.Combine(builds.FullName, relativePath);

            if (this.fileSystem.File.Exists(target) && !overwrite)
            {
                if (ownedByAudit)
                {
                    Log.Warning($"'Builds/{relativePath}' already exists, skipping it. Use --force to overwrite it.");
                }
                else
                {
                    Log.Debug($"'Builds/{relativePath}' already exists, keeping the project's version.");
                }
                return;
            }

            this.fileSystem.Directory.CreateDirectory(this.fileSystem.Path.GetDirectoryName(target)!);
            this.fileSystem.File.Copy(source, target, overwrite: true);
            Log.Information($"Generated 'Builds/{relativePath}'.");
        }

        /// <summary>
        /// Merges the Audit Review stage (Cmf.Audit installation + audit) into the project's CD-Containers.
        /// </summary>
        private void MergeIntoCdContainers(IDirectoryInfo builds, IFileInfo cdContainersFile, string cdContainers, string? auditPackageToDeclare)
        {
            if (PipelineYamlMerger.IsAuditMerged(cdContainers))
            {
                Log.Information("CD-Containers already includes the Audit Review stage, nothing to merge.");
                return;
            }

            var environmentExpression = PipelineYamlMerger.ResolveEnvironmentExpression(cdContainers);
            var missing = new List<string>();
            if (environmentExpression == null)
            {
                missing.Add("a target environment (an 'Environment' parameter or the CustomerEnvironmentCodename/CustomerEnvironmentName variable)");
            }
            if (!PipelineYamlMerger.HasStage(cdContainers, "Installation"))
            {
                missing.Add("an 'Installation' stage");
            }
            if (!cdContainers.Contains("EnvironmentCustomerPortalPAT"))
            {
                missing.Add("an 'EnvironmentCustomerPortalPAT' variable");
            }
            if (missing.Count > 0)
            {
                Log.Warning($"Could not merge the Audit Review stage into CD-Containers, it does not have {string.Join(", ", missing)}. Please add it manually, following 'Builds/CD-AuditReview.yml'.");
                return;
            }

            foreach (var task in new[] { "read-release-manifest.yml", "cmf-login.yml", "portal-deploy.yml", "generate-pat.yml" })
            {
                if (!this.fileSystem.File.Exists(this.fileSystem.Path.Combine(builds.FullName, ".tasks", task)))
                {
                    Log.Warning($"'Builds/.tasks/{task}' was not found, the Audit Review stage requires it.");
                }
            }

            var merged = PipelineYamlMerger.MergeIntoCdContainers(cdContainers, environmentExpression!, auditPackageToDeclare);
            this.fileSystem.File.WriteAllText(cdContainersFile.FullName, merged);

            Log.Information("Merged the Audit Review stage into 'Builds/CD-Containers.yml', right after the Installation stage. It installs the Cmf.Audit package and runs the audit on every non-production run.");
        }

        /// <summary>
        /// Environments come from the CD-Containers "Environment" parameter when it has one, otherwise from Builds/.vars/&lt;Environment&gt;.yml.
        /// </summary>
        private (List<string> Environments, string? Default) ResolveEnvironments(IDirectoryInfo builds, string? cdContainers)
        {
            var (environments, defaultEnvironment) = cdContainers == null
                ? (new List<string>(), null)
                : PipelineYamlMerger.ReadEnvironmentParameter(cdContainers);

            if (environments.Count == 0)
            {
                var vars = this.fileSystem.DirectoryInfo.New(this.fileSystem.Path.Combine(builds.FullName, ".vars"));
                if (vars.Exists)
                {
                    environments = vars.GetFiles("*.yml")
                        .Select(file => this.fileSystem.Path.GetFileNameWithoutExtension(file.Name))
                        .Where(name => !name.Equals("global", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            if (defaultEnvironment == null || !environments.Contains(defaultEnvironment))
            {
                defaultEnvironment = environments.FirstOrDefault();
            }

            return (environments, defaultEnvironment);
        }

        /// <summary>
        /// Reads the AgentPool from the legacy Builds/.pipeline-config.json, when present.
        /// </summary>
        private string? ReadLegacyPipelineConfigAgentPool(IDirectoryInfo builds)
        {
            var pipelineConfig = ReadIfExists(this.fileSystem.Path.Combine(builds.FullName, ".pipeline-config.json"));
            if (pipelineConfig == null)
            {
                return null;
            }

            try
            {
                return JObject.Parse(pipelineConfig)["AgentPool"]?.Value<string>();
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to read 'Builds/.pipeline-config.json': {ex.Message}");
                return null;
            }
        }

        private string? ReadIfExists(string path) =>
            this.fileSystem.File.Exists(path) ? this.fileSystem.File.ReadAllText(path) : null;

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        /// <summary>
        /// Returns the provided answer, or prompts the user for it. Falls back to <paramref name="defaultValue"/> when the console is not interactive.
        /// </summary>
        private static bool Ask(bool? provided, string question, bool defaultValue)
        {
            if (provided.HasValue)
            {
                return provided.Value;
            }

            if (!AnsiConsole.Profile.Capabilities.Interactive || Console.IsInputRedirected)
            {
                Log.Debug($"Non-interactive console, using the default answer '{defaultValue}' for: {question}");
                return defaultValue;
            }

            // The prompt is written line by line (instead of AnsiConsole.Confirm, which leaves the cursor on the prompt line)
            // because the host cmf-cli relays the plugin output per line, so a prompt without a line break is never displayed.
            var hint = defaultValue ? "[[Y/n]]" : "[[y/N]]";
            while (true)
            {
                AnsiConsole.MarkupLine($"{question} {hint} (press Enter for {(defaultValue ? "yes" : "no")}):");
                var answer = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(answer))
                {
                    return defaultValue;
                }

                if (answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (answer.Equals("n", StringComparison.OrdinalIgnoreCase) || answer.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                AnsiConsole.MarkupLine("[red]Please answer 'y' or 'n'.[/]");
            }
        }

        #endregion Private Methods
    }

    /// <summary>
    /// Arguments of the "init" command
    /// </summary>
    public class InitArguments
    {
        public bool? AzureDevOps { get; set; }
        public bool? IncludeInCDContainers { get; set; }
        public string? AgentPool { get; set; }
        public string? DefaultServiceConnection { get; set; }
        public string? CmfAuditVersion { get; set; }
        public Uri? CmfAuditRegistry { get; set; }
        public bool Force { get; set; }
    }
}

using audit.Objects;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using SharpCompress.Archives;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "pipeline" command group
    /// </summary>
    [CmfCommand("pipeline", Parent = "review", Description = "Will audit the Pipeline of an already checked-out project")]
    public partial class PipelineAuditor : BaseCommand
    {
        #region Public Constructors

        public PipelineAuditor() : this(new FileSystem())
        {
        }

        public PipelineAuditor(IFileSystem fileSystem) : base(fileSystem)
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
            var defaultOptions = AzureDevOpsOptions.GetDefaultValues();

            // if options are not defined, try to use predefined environment variables from pipeline
            cmd.AddArgument(new Argument<IDirectoryInfo>(
                name: "workingDir",
                parse: (argResult) => Parse<IDirectoryInfo>(argResult, "."),
                isDefault: true
            )
            {
                Description = "Working Directory"
            });

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--azureDevOpsUrl" },
                description: "The URI of the TFS collection or Azure DevOps organization, baked into the generated pipeline's repository URL. predefined variable: System.TeamFoundationCollectionUri",
                getDefaultValue: () => defaultOptions.azureDevOpsUrl
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--repository" },
                description: "The name of the triggering repository, baked into the generated pipeline's repository URL. predefined variable: Build.Repository.Name",
                getDefaultValue: () => defaultOptions.repository
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--infrastructureFileName" },
                description: "Infrastructure file name",
                getDefaultValue: () => ".pipeline-config.json"
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--pipelineVersion" },
                description: "cmf pipeline version to use",
                getDefaultValue: () => "latest"
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--npmRegistry" },
                description: "npm registry version to use",
                getDefaultValue: () => "https://criticalmanufacturing.io/repository/npm/"
                ));

            cmd.AddOption(new Option<bool>(
                aliases: new string[] { "--generateReport" },
                description: "Will generate a report and persist the flagged artifacts",
                getDefaultValue: () => true
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--reportOutputFolder" },
                description: "Folder where a report will be generated, requires the generateReport flag to be true",
                getDefaultValue: () => Path.GetTempPath()
                ));
            // Add the handler
            cmd.Handler = CommandHandler.Create<IDirectoryInfo, string, string, string, string, string, bool, string>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(IDirectoryInfo workingDir, string azureDevOpsUrl, string repository, string infrastructureFileName, string pipelineVersion, string npmRegistry, bool generateReport, string reportOutputFolder)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var repoHashes = HashLocalPipelines(workingDir);
            (IDirectoryInfo workingDirectory, IFileInfo infraFile, string projectNpmRegistry) = await PrepareLocalPipelineWorkspace(workingDir, infrastructureFileName);

            Stream GetFileContentStream(string path) => this.fileSystem.File.OpenRead(Path.Combine(workingDir.FullName, path));

            await RunAuditAsync(repoHashes, infraFile, workingDirectory, azureDevOpsUrl, repository, pipelineVersion, npmRegistry, projectNpmRegistry, generateReport, reportOutputFolder, GetFileContentStream);
        }

        /// <summary>
        /// Runs the pipeline generation-and-compare audit against an already-resolved committed baseline
        /// (<paramref name="repoHashes"/>) and generation workspace. Shared by the local "review pipeline"
        /// command and the "review-remote-ado pipeline" command (<see cref="PipelineAuditorRemote"/>), which
        /// differ only in how the committed <c>/Builds</c> folder and pipeline/project config are sourced.
        /// </summary>
        protected async Task RunAuditAsync(
            Dictionary<string, string> repoHashes, IFileInfo infraFile, IDirectoryInfo generationWorkspace,
            string azureDevOpsUrl, string repository, string pipelineVersion, string npmRegistry, string projectNpmRegistry,
            bool generateReport, string reportOutputFolder, Func<string, Stream> getFileContentStream)
        {
            if (npmRegistry != projectNpmRegistry)
            {
                Log.Warning($"There is a mismatch between the npm registry provided '{npmRegistry}' and the project npm registry '{projectNpmRegistry}'");
            }

            // Step 6: run cmf pipeline azureDevOps init
            Log.Status("Generating Pipelines", ctx => RunCmfPipelineAzureDevOpsInit(azureDevOpsUrl, repository, pipelineVersion, generationWorkspace, infraFile, npmRegistry));

            // Step 7: Iterate over the files generated by the cmf pipeline command and compare the hashes agains the project hash
            var pipelineRunHash = HashRunPipelineFiles(repoHashes, generationWorkspace);
            var reportOutputDirectory = this.fileSystem.DirectoryInfo.New(reportOutputFolder);

            ValidatePipelineFiles(repoHashes, pipelineRunHash, generateReport, getFileContentStream, generationWorkspace, reportOutputDirectory);

            Log.Information($"Successfully audited the pipeline process");
        }

        /// <summary>
        /// Hash the committed <c>Builds</c> folder of an already checked-out project.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <returns></returns>
        /// <exception cref="CliException"></exception>
        internal static Dictionary<string, string> HashLocalPipelines(IDirectoryInfo projectRoot)
        {
            var repoHashes = new Dictionary<string, string>();
            var buildsDir = projectRoot.GetDirectories("Builds").FirstOrDefault();

            if (buildsDir == null)
            {
                throw new CliException($"No 'Builds' folder found in '{projectRoot.FullName}'.");
            }

            foreach (var file in buildsDir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var key = file.FullName.Replace('\\', '/').Replace(projectRoot.FullName.Replace('\\', '/') + "/", "");

                if (!key.Contains(".vars") && !key.Contains(".json") && !key.Contains(".gitignore"))
                {
                    Log.Debug($"Processing: {key}");
                    bool contentWasChanged;
                    using (var configurationStream = file.OpenRead())
                    {
                        contentWasChanged = HandleYamlFiles(repoHashes, key, configurationStream, false);
                    }

                    if (!contentWasChanged)
                    {
                        using var hashStream = file.OpenRead();
                        AddHashFile(repoHashes, key, hashStream);
                    }
                }
            }

            return repoHashes;
        }

        /// <summary>
        /// Create workspace to be able to run cmf pipeline, sourcing the infrastructure/project config from
        /// an already checked-out project instead of downloading them from Azure DevOps.
        /// </summary>
        /// <param name="projectRoot"></param>
        /// <param name="infrastructureFileName"></param>
        /// <returns></returns>
        /// <exception cref="CliException"></exception>
        private async Task<(IDirectoryInfo, IFileInfo, string)> PrepareLocalPipelineWorkspace(IDirectoryInfo projectRoot, string infrastructureFileName)
        {
            // Create a temp folder
            var workingDirectory = this.fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            workingDirectory.Create();

            // Retrieve all arguments of cmf pipeline azureDevOps init and copy to temp folder
            var infraFileName = string.IsNullOrEmpty(infrastructureFileName) ? "/.pipeline-config.json" : infrastructureFileName;
            var infraContent = await AuditUtilities.GetLocalFileContentAsync(projectRoot, this.fileSystem, infraFileName);
            if (infraContent == null)
            {
                throw new CliException($"Could not find '{infraFileName}' in '{projectRoot.FullName}'.");
            }
            var infraFile = this.fileSystem.FileInfo.New(AuditUtilities.SafeCombine(workingDirectory.FullName, infraFileName));
            var infraStream = infraFile.CreateText();
            infraStream.Write(infraContent);
            infraStream.Close();

            // cmf pipeline requires to be in a root package folder, so let´s use the one from the project
            var projectFileName = "/.project-config.json";
            this.fileSystem.Directory.SetCurrentDirectory(projectRoot.FullName);
            var projectRootDir = FileSystemUtilities.GetProjectRoot(this.fileSystem);
            var projectContent = await AuditUtilities.GetLocalFileContentAsync(projectRootDir, this.fileSystem, projectFileName);
            if (projectContent == null)
            {
                throw new CliException($"Could not find '{projectFileName}' in '{projectRootDir.FullName}'.");
            }
            var projectContentObject = JsonConvert.DeserializeObject<ProjectConfig>(projectContent);

            var projectFile = this.fileSystem.FileInfo.New(AuditUtilities.SafeCombine(workingDirectory.FullName, projectFileName));

            var projectStream = projectFile.CreateText();
            projectStream.Write(projectContent);
            projectStream.Close();

            Log.Debug($"Copied infrastructure to {infraFile.FullName}");

            return (workingDirectory, infraFile, projectContentObject.NPMRegistry.ToString());
        }

        #endregion Public Methods

        /// <summary>
        /// Create a SHA256 Hash
        /// </summary>
        /// <param name="contentStream"></param>
        /// <returns></returns>
        internal static string ComputeSHA256Hash(Stream contentStream)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(contentStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        /// <summary>
        /// Hash and add a File
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="entry"></param>
        /// <param name="yamlStream"></param>
        internal static void AddHashFile(Dictionary<string, string> dictionary, string entry, Stream yamlStream)
        {
            string hash = ComputeSHA256Hash(yamlStream);
            Log.Debug($"File: {entry} | Hash: {hash}");
            dictionary.Add(entry, hash);
        }

        /// <summary>
        /// Traverse comitted pipeline files and create Hash for comparison
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="azureDevOpsClient"></param>
        /// <returns></returns>
        /// <exception cref="CliException"></exception>
        internal static async Task<Dictionary<string, string>> HashProjectPipelines(string repository, AzureDevOpsClientWrapper azureDevOpsClient)
        {
            var repoHashes = new Dictionary<string, string>();

            // Step 1: Get the zip stream for the "Builds" folder
            using (var zipStream = azureDevOpsClient.GetRepositoryItemsAsZip(
                repository,
                path: "/Builds"
            ))
            {
                if (zipStream == null)
                {
                    throw new CliException("Failed to get zip stream.");
                }

                // Step 2: Prepare zip for iteration
                using var memoryStream = new MemoryStream();
                await zipStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;  // Reset to the start for reading

                using var archive = ArchiveFactory.Open(memoryStream);

                // Step 3: Iterate over zip, exclude jsons and folder .vars and load file name and a hash of the content to a dictionary
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory && !entry.Key.Contains(".vars") && !entry.Key.Contains(".json") && !entry.Key.Contains(".gitignore"))
                    {
                        Log.Debug($"Processing: {entry.Key}");
                        // Buffer the entry: HandleYamlFiles consumes (and disposes) the stream it reads
                        using var entryStream = entry.OpenEntryStream();
                        var content = new MemoryStream();
                        entryStream.CopyTo(content);

                        bool contentWasChanged = HandleYamlFiles(repoHashes, entry.Key, new MemoryStream(content.ToArray()), false);

                        if (!contentWasChanged)
                        {
                            content.Position = 0;
                            AddHashFile(repoHashes, entry.Key, content);
                        }
                    }
                }
            }

            return repoHashes;
        }

        /// <summary>
        /// Traverse generated pipelines and create Hash for comparison
        /// </summary>
        /// <param name="repoHashes"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        internal static Dictionary<string, string> HashRunPipelineFiles(Dictionary<string, string> repoHashes, IDirectoryInfo workingDirectory)
        {
            var pipelineRunHash = new Dictionary<string, string>();
            foreach (var file in workingDirectory.EnumerateFiles("*.yml", SearchOption.AllDirectories).ToList())
            {
                if (!file.FullName.Contains(".vars"))
                {
                    Log.Debug($"Processing Generated Pipeline file: {file.FullName}");

                    string key = file.FullName.Replace('\\', '/').Replace(workingDirectory.FullName.Replace('\\', '/') + "/", "").Replace('\\', '/');

                    bool contentWasChanged = false;

                    using (var configurationStream = file.OpenRead())
                    {
                        contentWasChanged = HandleYamlFiles(pipelineRunHash, key, configurationStream, contentWasChanged);
                    }

                    if (!contentWasChanged)
                    {
                        using var hashStream = file.OpenRead();
                        AddHashFile(pipelineRunHash, key, hashStream);
                    }
                }
            }
            return pipelineRunHash;
        }

        /// <summary>
        /// Create workspace to be able to run cmf pipeline
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="infrastructureFileName"></param>
        /// <param name="azureDevOpsClient"></param>
        /// <returns></returns>
        protected async Task<(IDirectoryInfo, IFileInfo, string)> PrepareCmfPipelineWorkspace(string repository, string infrastructureFileName, AzureDevOpsClientWrapper azureDevOpsClient)
        {
            // Create a temp folder
            var workingDirectory = this.fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            workingDirectory.Create();

            // Retrieve all arguments of cmf pipeline azureDevOps init and copy to temp folder
            var infraFileName = string.IsNullOrEmpty(infrastructureFileName) ? "/.pipeline-config.json" : infrastructureFileName;
            var infra = azureDevOpsClient.GetRepositoryItemContent(repository, infraFileName);
            var infraFile = this.fileSystem.FileInfo.New(AuditUtilities.SafeCombine(workingDirectory.FullName, infraFileName));
            var infraStream = infraFile.CreateText();
            infraStream.Write(await AuditUtilities.StreamToStringAsync(infra));
            infraStream.Close();

            // cmf pipeline requires to be in a root package folder, so let´s use the one from the project
            (string projectContent, ProjectConfig projectContentObject) = await AuditUtilities.GetProjectConfigContentAsync(azureDevOpsClient, repository);

            var projectFileName = "/.project-config.json";
            var projectFile = this.fileSystem.FileInfo.New(AuditUtilities.SafeCombine(workingDirectory.FullName, projectFileName));

            var projectStream = projectFile.CreateText();
            projectStream.Write(projectContent);
            projectStream.Close();

            Log.Debug($"Copied infrastructure to {infraFile.FullName}");

            return (workingDirectory, infraFile, projectContentObject.NPMRegistry.ToString());
        }

        /// <summary>
        /// Compare generated pipelines with repository pipelines
        /// </summary>
        /// <param name="repoHashes"></param>
        /// <param name="pipelineRunHash"></param>
        /// <param name="generateReport"></param>
        /// <param name="getItemContentFromAzure"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="reportOutputFolder"></param>
        internal static void ValidatePipelineFiles(Dictionary<string, string> repoHashes, Dictionary<string, string> pipelineRunHash, bool generateReport, Func<string, Stream> getItemContentFromAzure, IDirectoryInfo workingDirectory, IDirectoryInfo reportOutputFolder)
        {
            var reports = new List<Action<IDirectoryInfo, string[]>>();

            // Extract all the cases where a key exists in repoHashes dictionary but not in pipelineRunHash and vice versa
            var onlyInRepoHashes = repoHashes.Where(kv => !pipelineRunHash.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            var onlyInPipelinesHashes = pipelineRunHash.Where(kv => !repoHashes.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

            // Extract all the cases where a key exists in both but the value is different
            var mismatchedValues = repoHashes.Keys
                                    .Intersect(pipelineRunHash.Keys)
                                    .Where(key => repoHashes[key] != pipelineRunHash[key])
                                    .ToDictionary(key => key, key => new[] { repoHashes[key], pipelineRunHash[key] });

            if (onlyInPipelinesHashes.Count != 0)
            {
                reports.Add(AuditUtilities.DisplayPanel("Pipeline", onlyInPipelinesHashes, "Hash only exists in Repository", Color.Yellow));
            }

            if (onlyInRepoHashes.Count != 0)
            {
                reports.Add(AuditUtilities.DisplayPanel("Pipeline", onlyInRepoHashes, "Hash only exists in the Pipelines Run", Color.Red));
            }

            if (mismatchedValues.Count != 0)
            {
                reports.Add(AuditUtilities.DisplayPanel("Pipeline", mismatchedValues, "Hash Mismatch", Color.Red));
            }

            if (generateReport)
            {
                reportOutputFolder.Create();

                var auditFolder = reportOutputFolder.CreateSubdirectory("Review");
                var pipelineAuditFolder = auditFolder.CreateSubdirectory("Pipeline");

                foreach (var report in reports)
                {
                    report(pipelineAuditFolder, ["File", "Repo Hash", "Run Hash"]);
                }

                GeneratePipelineAuditArtifacts(onlyInRepoHashes, onlyInPipelinesHashes, mismatchedValues, getItemContentFromAzure, workingDirectory, pipelineAuditFolder);

                AuditUtilities.MergeDirectoryCSVsIntoExcel(pipelineAuditFolder);
            }
        }

        /// <summary>
        /// Generate Pipelines
        /// </summary>
        /// <param name="onlyInRepoHashes"></param>
        /// <param name="onlyInPipelinesHashes"></param>
        /// <param name="mismatchedValues"></param>
        /// <param name="getItemContentFromAzure"></param>
        /// <param name="pipelineRunDirectory"></param>
        /// <param name="reportOutputFolder"></param>
        /// <exception cref="CliException"></exception>
        internal static void GeneratePipelineAuditArtifacts(Dictionary<string, string> onlyInRepoHashes, Dictionary<string, string> onlyInPipelinesHashes, Dictionary<string, string[]> mismatchedValues, Func<string, Stream> getItemContentFromAzure, IDirectoryInfo pipelineRunDirectory, IDirectoryInfo reportOutputFolder)
        {
            Log.Status("Building Pipeline Review Report", ctx =>
            {
                if (onlyInRepoHashes.Count > 0)
                {
                    var onlyInRepoHashesFolder = reportOutputFolder.CreateSubdirectory("OnlyInRepositoryFiles");
                    onlyInRepoHashes.ForEach(fileHash =>
                    {
                        HandleRepositoryFiles(getItemContentFromAzure, reportOutputFolder, fileHash.Key, onlyInRepoHashesFolder);
                    });
                }
                if (onlyInPipelinesHashes.Count > 0)
                {
                    var onlyInPipelinesFolder = reportOutputFolder.CreateSubdirectory("OnlyInPipelineFiles");
                    onlyInPipelinesHashes.ForEach(fileHash =>
                    {
                        HandlePipelineFiles(pipelineRunDirectory, fileHash.Key, onlyInPipelinesFolder);
                    });
                }
                if (mismatchedValues.Count > 0)
                {
                    var mismatchedHashes = reportOutputFolder.CreateSubdirectory("MismatchHashes");
                    mismatchedValues.ForEach(fileHash =>
                    {
                        HandleRepositoryFiles(getItemContentFromAzure, reportOutputFolder, fileHash.Key, mismatchedHashes);
                        HandlePipelineFiles(pipelineRunDirectory, fileHash.Key, mismatchedHashes);
                    });
                }
            });

            static void HandleRepositoryFiles(Func<string, Stream> getItemContentFromAzure, IDirectoryInfo reportOutputFolder, string fileName, IDirectoryInfo onlyInRepoHashesFolder)
            {
                using var fileContent = getItemContentFromAzure(fileName);
                var fileInfo = onlyInRepoHashesFolder.FileSystem.FileInfo.New(AuditUtilities.SafeCombine(Path.Combine(onlyInRepoHashesFolder.FullName, "Repository"), fileName));
                var dir = reportOutputFolder.FileSystem.DirectoryInfo.New(fileInfo.DirectoryName);
                dir.Create();
                using var fileStream = fileInfo.Open(FileMode.Create, FileAccess.Write);
                fileContent.CopyTo(fileStream);
            }

            static void HandlePipelineFiles(IDirectoryInfo pipelineRunDirectory, string fileName, IDirectoryInfo onlyInPipelinesFolder)
            {
                var file = pipelineRunDirectory.GetFiles(fileName).FirstOrDefault();
                var fileInfo = onlyInPipelinesFolder.FileSystem.FileInfo.New(AuditUtilities.SafeCombine(Path.Combine(onlyInPipelinesFolder.FullName, "PipelineRun"), fileName));
                fileInfo.Directory.Create();
                if (file == null)
                {
                    throw new CliException($"Couldn't find file in Pipeline run {fileName}");
                }
                file.CopyTo(fileInfo.FullName, true);
            }
        }

        /// <summary>
        /// Hash parts that must be common between comitted and generated pipelines
        /// </summary>
        /// <param name="ymlConfigurationStream"></param>
        /// <returns></returns>
        internal static string GenerateHashFromPipelineYaml(Stream ymlConfigurationStream)
        {
            using var reader = new StreamReader(ymlConfigurationStream);
            var deserializer = new DeserializerBuilder().Build();

            Dictionary<object, object> contentYaml = deserializer.Deserialize<Dictionary<object, object>>(reader);

            bool contentWasChanged = false;
            // Check if 'parameters' exists and is a dictionary
            if (contentYaml.TryGetValue("parameters", out var parameters) && parameters is Dictionary<object, object> paramDict)
            {
                if (paramDict.ContainsKey("Environment"))
                {
                    paramDict["Environment"] = null;
                    contentWasChanged = true;
                }
                if (paramDict.ContainsKey("EnvironmentConfigName"))
                {
                    paramDict["EnvironmentConfigName"] = null;
                    contentWasChanged = true;
                }
            }

            if (contentWasChanged)
            {
                var serializer = new SerializerBuilder().Build();
                using var yamlStream = new MemoryStream();
                using (var writer = new StreamWriter(yamlStream, leaveOpen: true))
                {
                    serializer.Serialize(writer, contentYaml);
                    writer.Flush();
                    yamlStream.Position = 0;  // Reset stream to the beginning for hashing
                }

                // Compute the hash from the serialized YAML
                return ComputeSHA256Hash(yamlStream);
            }

            return ComputeSHA256Hash(ymlConfigurationStream);
        }

        internal static bool HandleYamlFiles(Dictionary<string, string> repoHashes, string entry, Stream configurationStream, bool contentWasChanged)
        {
            if (entry.Contains(".yml") || entry.Contains(".yaml"))
            {
                using var reader = new StreamReader(configurationStream);
                var deserializer = new DeserializerBuilder().Build();

                Dictionary<object, object> contentYaml = deserializer.Deserialize<Dictionary<object, object>>(reader);

                // Check if 'parameters' exists and is a dictionary
                if (contentYaml.TryGetValue("parameters", out var parameters) && parameters is List<dynamic> parameterList)
                {
                    // Filter the list and check if any changes were made
                    var updatedParameters = parameterList
                        .Where(parameter => parameter?["name"] != "Environment" && parameter?["name"] != "EnvironmentConfigName")
                        .ToList();

                    // If the filtered list is smaller, a change occurred
                    if (updatedParameters.Count != parameterList.Count)
                    {
                        contentYaml["parameters"] = updatedParameters;
                        contentWasChanged = true;
                    }
                }

                if (contentWasChanged)
                {
                    var serializer = new SerializerBuilder().Build();
                    using var yamlStream = new MemoryStream();
                    using (var writer = new StreamWriter(yamlStream, leaveOpen: true))
                    {
                        serializer.Serialize(writer, contentYaml);
                        writer.Flush();
                        yamlStream.Position = 0;  // Reset stream to the beginning for hashing
                    }

                    // Compute the hash from the serialized YAML
                    AddHashFile(repoHashes, entry, yamlStream);
                }
            }

            return contentWasChanged;
        }

        /// <summary>
        /// Run Cmf Pipeline command
        /// </summary>
        /// <param name="azureDevOpsUrl"></param>
        /// <param name="repository"></param>
        /// <param name="pipelineVersion"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="infraFile"></param>
        /// <param name="npmRegistry"></param>
        private static void RunCmfPipelineAzureDevOpsInit(string azureDevOpsUrl, string repository, string pipelineVersion, IDirectoryInfo workingDirectory, IFileInfo infraFile, string npmRegistry)
        {
            var arguments = BuildCmfPipelineArguments(azureDevOpsUrl, repository, pipelineVersion, infraFile.FullName);

            var executable = "npx" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : "");
            CommandLineRunner cmd = new(executable, workingDirectory, new Dictionary<string, string>() { { "npm_config_registry", npmRegistry } });

            cmd.Run(arguments, outputHandler: Console.WriteLine, errorHandler: Log.Error);
        }

        /// <summary>
        /// Builds the argument list for "cmf pipeline azureDevOps init", rejecting values that could be
        /// interpreted by a shell (npx is a batch file on Windows, so cmd.exe still parses its arguments).
        /// </summary>
        internal static List<string> BuildCmfPipelineArguments(string azureDevOpsUrl, string repository, string pipelineVersion, string infraFilePath)
        {
            if (string.IsNullOrWhiteSpace(pipelineVersion) || !PipelineVersionRegex().IsMatch(pipelineVersion))
            {
                throw new CliException($"Invalid pipeline version '{pipelineVersion}'. Expected a semantic version or a dist-tag (e.g. 'latest').");
            }

            var repositoryUrl = $"{azureDevOpsUrl}/_git/{repository}";
            foreach (var (name, value) in new[] { ("azureDevOpsUrl", azureDevOpsUrl), ("repository", repository), ("infrastructure", infraFilePath) })
            {
                if (value != null && value.IndexOfAny(UnsafeArgumentChars) >= 0)
                {
                    throw new CliException($"The value of '{name}' contains characters that are not allowed: '{value}'.");
                }
            }

            // Dummy values are used for the release arguments as they only affect the .vars folder, which is not hashed
            return
            [
                "--yes", $"@criticalmanufacturing/pipeline@{pipelineVersion}", "azureDevOps", "init",
                "--infrastructure", infraFilePath,
                "--repositoryUrl", repositoryUrl,
                "--releaseCustomerEnvironment", "DummyEnv",
                "--releaseSite", "DummySite",
                "--releaseLicense", "DummyLicense",
                "--releaseDeploymentTarget", "DummyDeployTarget",
                "--backupShare", "DummyBackupShare",
            ];
        }

        private static readonly char[] UnsafeArgumentChars = ['"', '&', '|', '<', '>', '^', '%', '`', '$', ';', '\n', '\r'];

        [GeneratedRegex(@"^[0-9A-Za-z][0-9A-Za-z.+\-]*$")]
        private static partial Regex PipelineVersionRegex();
    }
}
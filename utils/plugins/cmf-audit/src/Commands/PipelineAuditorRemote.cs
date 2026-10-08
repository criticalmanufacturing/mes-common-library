using audit.Objects;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.IO.Abstractions;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "pipeline" command group under "review-remote-ado": fetches the committed pipelines and project
    /// configuration from Azure DevOps before generating and comparing.
    /// </summary>
    [CmfCommand("pipeline", Parent = "review-remote-ado", Description = "Will audit the project Pipeline, fetching it from Azure DevOps")]
    public class PipelineAuditorRemote : PipelineAuditor, IAuditor
    {
        #region Public Constructors

        public PipelineAuditorRemote() : this(new FileSystem())
        {
        }

        public PipelineAuditorRemote(IFileSystem fileSystem, AzureDevOpsClientWrapper azureDevOpsClient = null) : base(fileSystem)
        {
            this.azureDevOpsClient = azureDevOpsClient;
        }

        private readonly AzureDevOpsClientWrapper azureDevOpsClient;

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
            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--azureDevOpsUrl" },
                description: "The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri",
                getDefaultValue: () => defaultOptions.azureDevOpsUrl
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--teamProject" },
                description: "The name of the project that contains this build. predefined variable: System.TeamProject",
                getDefaultValue: () => defaultOptions.teamProject
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--repository" },
                description: "The name of the triggering repository. predefined variable: Build.Repository.Name",
                getDefaultValue: () => defaultOptions.repository
                ));

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--personalAccessToken" },
                description: "Token used to access AzureDevOps REST API. predefined variable: System.AccessToken",
                getDefaultValue: () => defaultOptions.personalAccessToken
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
            cmd.Handler = CommandHandler.Create<string, string, string, string, string, string, string, bool, string>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken, string infrastructureFileName, string pipelineVersion, string npmRegistry, bool generateReport, string reportOutputFolder)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var azureDevOpsClient = this.azureDevOpsClient ?? new AzureDevOpsClientWrapper(azureDevOpsUrl, teamProject, repository, personalAccessToken);
            var repoHashes = await HashProjectPipelines(repository, azureDevOpsClient);

            (IDirectoryInfo workingDirectory, IFileInfo infraFile, string projectNpmRegistry) = await PrepareCmfPipelineWorkspace(repository, infrastructureFileName, azureDevOpsClient);

            await RunAuditAsync(repoHashes, infraFile, workingDirectory, azureDevOpsUrl, repository, pipelineVersion, npmRegistry, projectNpmRegistry, generateReport, reportOutputFolder,
                path => azureDevOpsClient.GetRepositoryItemContent(repository, path));
        }

        #endregion Public Methods
    }
}

using audit.Objects;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Objects;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "root" command group under "review-remote-ado": fetches the Root package from Azure DevOps before auditing it.
    /// </summary>
    [CmfCommand("root", Parent = "review-remote-ado", Description = "Will audit the project Root, fetching it from Azure DevOps")]
    public class RootAuditorRemote : RootAuditor
    {
        #region Public Constructors

        public RootAuditorRemote() : this(new FileSystem())
        {
        }

        public RootAuditorRemote(IFileSystem fileSystem, AzureDevOpsClientWrapper azureDevOpsClient = null) : base(fileSystem)
        {
            this.azureDevOpsClient = azureDevOpsClient;
        }

        private readonly AzureDevOpsClientWrapper azureDevOpsClient;

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
            AuditCommandOptions.AddRemoteOptions(cmd);

            // Add the handler
            cmd.Handler = CommandHandler.Create<IDirectoryInfo,
             string, string, string, string,
             bool, string,
             bool, string, string, string, string, bool>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            IDirectoryInfo workingDir,
            string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);
            var azureDevOpsClient = this.azureDevOpsClient ?? new AzureDevOpsClientWrapper(azureDevOpsUrl, teamProject, repository, personalAccessToken);

            if (workingDir.GetFiles("cmfpackage.json").Count() == 0)
            {
                workingDir = await AuditUtilities.GetAllCmfPackagesAsync(azureDevOpsClient, repository, this.fileSystem);
            }

            IFileInfo cmfPackageFile = this.fileSystem.FileInfo.New($"{workingDir.FullName}/cmfpackage.json");
            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);

            var metadataProject = azureDevOpsClient.GetRepositoryItems(repository, "");

            Func<string, Task<string>> getFileContent = path => AuditUtilities.GetFileContentAsync(azureDevOpsClient, repository, path);
            var allFilePaths = metadataProject.Select(item => item.Path);

            await RunAuditAsync(workingDir, cmfPackage, teamProject, getFileContent, allFilePaths, generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }
    }
}

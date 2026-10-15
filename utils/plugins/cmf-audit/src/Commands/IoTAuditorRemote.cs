using audit.Objects;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;
using AuditUtilities = audit.Objects.Utilities;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "iot" command group under "review-remote-ado": fetches the IoT package from Azure DevOps before auditing it.
    /// </summary>
    [CmfCommand("iot", Parent = "review-remote-ado", Description = "Will audit the IoT package, fetching it from Azure DevOps")]
    public class IoTAuditorRemote : IoTAuditor
    {
        #region Public Constructors

        public IoTAuditorRemote() : this(new FileSystem())
        {
        }

        public IoTAuditorRemote(IFileSystem fileSystem, AzureDevOpsClientWrapper azureDevOpsClient = null) : base(fileSystem)
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

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--packageId" },
                description: "Id of Package to Review, will only be used if command called directly",
                getDefaultValue: () => "Cmf.Custom.IoT.Packages"
                ));

            // Add the handler
            cmd.Handler = CommandHandler.Create<IDirectoryInfo,
                string, string, string, string,
                bool, string,
                bool, string, string, string, string, bool,
                string>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            IDirectoryInfo workingDir,
            string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL,
            string packageId = "Cmf.Custom.IoT.Packages")
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            (workingDir, CmfPackage cmfPackage, _) = await AuditUtilities.PrepareAzureRepo(workingDir, azureDevOpsUrl, teamProject, repository, personalAccessToken, packageId, this.azureDevOpsClient);

            await RunAuditAsync(workingDir, cmfPackage, teamProject, packageId, generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }
    }
}

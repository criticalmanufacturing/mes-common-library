using audit.Objects;
using Cmf.CLI.Core;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Commands;
using Cmf.CLI.Core.Enums;
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
    /// "review-remote-ado" command group: fetches the project from Azure DevOps before auditing it.
    /// </summary>
    [CmfCommand("review-remote-ado", Description = "Will audit the project, fetching it from Azure DevOps")]
    public class ReviewRemote : BaseCommand
    {
        #region Public Constructors

        public ReviewRemote() : this(new FileSystem())
        {
        }

        public ReviewRemote(IFileSystem fileSystem, AzureDevOpsClientWrapper azureDevOpsClient = null) : base(fileSystem)
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
            AuditCommandOptions.AddCommonOptions(cmd, generateReportByDefault: false);
            AuditCommandOptions.AddRemoteOptions(cmd);

            cmd.AddGlobalOption(new Option<string>(
                aliases: new string[] { "--infrastructureFileName" },
                description: "Infrastructure file name",
                getDefaultValue: () => ".pipeline-config.json"
                ));

            cmd.AddGlobalOption(new Option<string>(
                aliases: new string[] { "--pipelineVersion" },
                description: "cmf pipeline version to use",
                getDefaultValue: () => "latest"
                ));

            cmd.AddGlobalOption(new Option<string>(
                aliases: new string[] { "--npmRegistry" },
                description: "npm registry version to use",
                getDefaultValue: () => "https://criticalmanufacturing.io/repository/npm/"
                ));


            cmd.AddGlobalOption(new Option<string>(
                aliases: new string[] { "--validatorsToRun" },
                description: "Will generate a run the pipeline Auditor, accepts comma separated packages",
                getDefaultValue: () => ""
                ));

            // Add the handler
            cmd.Handler = CommandHandler.Create<
             string, string, string, string,
             bool, string,
             bool, string, string, string, string, bool,
             string, string, string,
             string>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL,
            string infrastructureFileName, string pipelineVersion, string npmRegistry,
            string validatorsToRun)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var azureDevOpsClient = this.azureDevOpsClient ?? new AzureDevOpsClientWrapper(azureDevOpsUrl, teamProject, repository, personalAccessToken);
            var workingDir = await AuditUtilities.GetAllCmfPackagesAsync(azureDevOpsClient, repository, this.fileSystem);

            #region Load root cmfPackage

            IFileInfo cmfPackageFile = this.fileSystem.FileInfo.New($"{workingDir.FullName}/cmfpackage.json");
            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);
            cmfPackage.LoadDependencies([], null, true);

            #endregion

            #region Register Custom Package handlers

            // Data and IoT-Data packages share the same auditor; only the presentation-tier IoT package uses the IoT auditor.
            Task RunDataAuditor(IDirectoryInfo validatorWorkingDir, string packageId) =>
                new DataAuditorRemote(this.fileSystem, azureDevOpsClient).ExecuteAsync(validatorWorkingDir, azureDevOpsUrl, teamProject, repository, personalAccessToken, generateReport, reportOutputFolder,
                                                                postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL, packageId);

            Task RunIoTAuditor(IDirectoryInfo validatorWorkingDir, string packageId) =>
                new IoTAuditorRemote(this.fileSystem, azureDevOpsClient).ExecuteAsync(validatorWorkingDir, azureDevOpsUrl, teamProject, repository, personalAccessToken, generateReport, reportOutputFolder,
                                                                postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL, packageId);

            Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>> validatorsRegistered = [];
            Review.AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.Data, [RunDataAuditor]);
            Review.AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.IoTData, [RunDataAuditor]);
            Review.AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.IoT, [RunIoTAuditor]);

            (var tasks, var unauditedDependencies) = Review.BuildTaskTree(cmfPackage, validatorsRegistered, []);

            #endregion

            #region Generate Report of Unaudited Dependencies

            var tableReports = new List<Action<IDirectoryInfo>>();
            if (unauditedDependencies?.Count > 0)
            {
                tableReports.Add(
                    AuditUtilities.DisplayTable("Review",
                    unauditedDependencies.GroupBy(x => new { x.Id, x.Version }).Select(g => g.First()).ToList().ToDictionary(dep => $"{dep.Id}.{dep.Version}", dep => dep.Version),
                    ["Dependency Id", "Dependency Version"],
                    "Unaudited Dependencies",
                    Spectre.Console.Color.Yellow));
            }

            #endregion

            #region Register Special Cases
            if (validatorsToRun.Length == 0 || validatorsToRun.Contains("Root"))
            {
                tasks.Add(new RootAuditorRemote(this.fileSystem, azureDevOpsClient).ExecuteAsync(workingDir, azureDevOpsUrl, teamProject, repository, personalAccessToken, generateReport, reportOutputFolder,
                                                                            postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL));
            }

            #endregion

            await Task.WhenAll(tasks);

            #region Generate Report

            if (generateReport)
            {
                Log.Information($"Review Generating Reports...");
                var reportOutputDirectory = this.fileSystem.DirectoryInfo.New(reportOutputFolder);
                reportOutputDirectory.Create();

                var auditFolder = reportOutputDirectory.CreateSubdirectory("Review");

                foreach (var report in tableReports)
                {
                    report(auditFolder);
                }

                AuditUtilities.MergeDirectoryCSVsIntoExcel(auditFolder, searchOption: SearchOption.AllDirectories, createGroupSheets: true);

                Log.Information($"Review Generated Reports");
            }

            #endregion

            Log.Information($"Successfully audited the project");
        }

        #endregion Public Methods
    }
}

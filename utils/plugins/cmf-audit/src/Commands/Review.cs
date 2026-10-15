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
    /// "review" command group
    /// </summary>
    [CmfCommand("review", Description = "Will audit an already checked-out project")]
    public class Review : BaseCommand
    {
        #region Public Constructors

        public Review() : this(new FileSystem())
        {
        }

        public Review(IFileSystem fileSystem) : base(fileSystem)
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

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--validatorsToRun" },
                description: "Will generate a run the pipeline Auditor, accepts comma separated packages",
                getDefaultValue: () => ""
                ));

            // Add the handler
            cmd.Handler = CommandHandler.Create<
             IDirectoryInfo, string,
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
            string teamProject,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL,
            string validatorsToRun)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            #region Load root cmfPackage

            IFileInfo cmfPackageFile = this.fileSystem.FileInfo.New($"{workingDir.FullName}/cmfpackage.json");
            if (!cmfPackageFile.Exists)
            {
                throw new CliException($"No cmfpackage.json found in '{workingDir.FullName}'. Run 'review' from inside an already checked-out project root, or use 'review-remote-ado' to fetch one from Azure DevOps.");
            }

            CmfPackage cmfPackage = CmfPackage.Load(cmfPackageFile, setDefaultValues: true, this.fileSystem);
            cmfPackage.LoadDependencies([], null, true);

            #endregion

            #region Register Custom Package handlers

            // Data and IoT-Data packages share the same auditor; only the presentation-tier IoT package uses the IoT auditor.
            Task RunDataAuditor(IDirectoryInfo validatorWorkingDir, string packageId) =>
                new DataAuditor(this.fileSystem).ExecuteAsync(validatorWorkingDir, teamProject, generateReport, reportOutputFolder,
                                                                postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);

            Task RunIoTAuditor(IDirectoryInfo validatorWorkingDir, string packageId) =>
                new IoTAuditor(this.fileSystem).ExecuteAsync(validatorWorkingDir, teamProject, generateReport, reportOutputFolder,
                                                                postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);

            Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>> validatorsRegistered = [];
            AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.Data, [RunDataAuditor]);
            AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.IoTData, [RunDataAuditor]);
            AddValidatorIfSelected(validatorsRegistered, validatorsToRun, PackageType.IoT, [RunIoTAuditor]);

            (var tasks, var unauditedDependencies) = BuildTaskTree(cmfPackage, validatorsRegistered, []);

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
                tasks.Add(new RootAuditor(this.fileSystem).ExecuteAsync(workingDir, teamProject, generateReport, reportOutputFolder,
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

        /// <summary>
        /// Registers <paramref name="validators"/> for <paramref name="packageType"/> when the
        /// package type was explicitly requested, or when no filter was supplied (audit everything).
        /// </summary>
        internal static void AddValidatorIfSelected(Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>> validatorsByType, string packagesToAudit, PackageType packageType, List<Func<IDirectoryInfo, string, Task>> validators)
        {
            var auditAll = packagesToAudit.Length == 0;
            if (auditAll || packagesToAudit.Split(',').Any(requested => requested == packageType.ToString()))
            {
                validatorsByType.Add(packageType, validators);
            }
        }

        /// <summary>
        /// Traverse tree and add Validators
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="validatorsRegistered"></param>
        /// <param name="validatorsToExecute"></param>
        /// <param name="unauditedDependencies"></param>
        /// <returns></returns>
        internal static (List<Task>, List<Dependency>) BuildTaskTree(CmfPackage pkg, Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>> validatorsRegistered, List<Task> validatorsToExecute = null, List<Dependency> unauditedDependencies = null)
        {
            validatorsToExecute ??= [];

            List<Task>? validatorResults = validatorsRegistered
                ?.Where(validator => validator.Key == pkg.PackageType)
                ?.SelectMany(validator => validator.Value)
                ?.Select(validator => Task.Run(() => validator(pkg.GetFileInfo().Directory, pkg.PackageId)))
                ?.ToList();

            if (validatorResults != null && validatorResults.Count > 0)
            {
                Log.Debug($"Found {validatorResults.Count} validators for package {pkg.PackageId}");
                validatorsToExecute.AddRange(validatorResults);
            }

            if (pkg.Dependencies.HasAny())
            {
                for (int i = 0; i < pkg.Dependencies.Count; i++)
                {
                    Dependency dependency = pkg.Dependencies[i];
                    if (!dependency.IsMissing)
                    {
                        BuildTaskTree(dependency.CmfPackage, validatorsRegistered, validatorsToExecute, unauditedDependencies);
                    }
                    else
                    {
                        unauditedDependencies ??= [];
                        Log.Debug($"Found unaudited dependency {dependency.Id}.{dependency.Version}");
                        unauditedDependencies.Add(dependency);
                    }
                }
            }

            return (validatorsToExecute, unauditedDependencies);
        }
    }
}
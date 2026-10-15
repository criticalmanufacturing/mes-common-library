using audit.Objects;
using Cmf.CLI.Core.Attributes;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Utilities;
using Cmf.CLI.Core.Objects;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Abstractions;

namespace Cmf.Cli.Plugin.Audit.Commands.install
{
    /// <summary>
    /// "data" command under "review-remote-github": downloads the project from GitHub before auditing it.
    /// </summary>
    [CmfCommand("data", Parent = "review-remote-github", Description = "Will audit the Data package, downloading the project from GitHub")]
    public class DataAuditorRemoteGitHub : DataAuditor
    {
        #region Public Constructors

        public DataAuditorRemoteGitHub() : this(new FileSystem())
        {
        }

        public DataAuditorRemoteGitHub(IFileSystem fileSystem, GitHubClient gitHubClient = null) : base(fileSystem)
        {
            this.gitHubClient = gitHubClient;
        }

        private readonly GitHubClient gitHubClient;

        #endregion Public Constructors

        /// <summary>
        /// Configure command
        /// </summary>
        /// <param name="cmd"></param>
        public override void Configure(Command cmd)
        {
            AuditCommandOptions.AddCommonOptions(cmd, generateReportByDefault: false);
            AuditCommandOptions.AddGitHubOptions(cmd);

            cmd.AddOption(new Option<string>(
                aliases: new string[] { "--packageId" },
                description: "Id of the package to review",
                getDefaultValue: () => "Cmf.Custom.Data"
                ));

            // Add the handler
            cmd.Handler = CommandHandler.Create<
             string, string, string, string, string,
             string,
             bool, string,
             bool, string, string, string, string, bool,
             string>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            string repository, string gitRef, string project, string path, string githubToken,
            string teamProject,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL,
            string packageId = "Cmf.Custom.Data")
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var folder = GitHubClient.ResolveProjectPath(path, project);
            var projectRoot = await (this.gitHubClient ?? new GitHubClient(this.fileSystem)).DownloadFolderAsync(repository, gitRef, folder, githubToken);
            var packageDir = GitHubClient.FindPackageDirectory(projectRoot, packageId, this.fileSystem);

            await ExecuteAsync(packageDir, teamProject ?? folder.Split('/').Last(), generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }
    }
}

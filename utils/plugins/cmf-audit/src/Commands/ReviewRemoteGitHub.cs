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
    /// "review-remote-github" command group: downloads a project from GitHub before auditing it.
    /// </summary>
    [CmfCommand("review-remote-github", Description = "Will audit the project, downloading it from GitHub")]
    public class ReviewRemoteGitHub : Review
    {
        #region Public Constructors

        public ReviewRemoteGitHub() : this(new FileSystem())
        {
        }

        public ReviewRemoteGitHub(IFileSystem fileSystem, GitHubClient gitHubClient = null) : base(fileSystem)
        {
            this.gitHubClient = gitHubClient;
        }

        private readonly GitHubClient gitHubClient;

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Configure command
        /// </summary>
        /// <param name="cmd"></param>
        public override void Configure(Command cmd)
        {
            AuditCommandOptions.AddCommonOptions(cmd, generateReportByDefault: false);
            AuditCommandOptions.AddGitHubOptions(cmd);

            cmd.AddGlobalOption(new Option<string>(
                aliases: new string[] { "--validatorsToRun" },
                description: "Will generate a run the pipeline Auditor, accepts comma separated packages",
                getDefaultValue: () => ""
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
            string validatorsToRun)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var folder = GitHubClient.ResolveProjectPath(path, project);
            var workingDir = await (this.gitHubClient ?? new GitHubClient(this.fileSystem)).DownloadFolderAsync(repository, gitRef, folder, githubToken);

            await ExecuteAsync(workingDir, teamProject ?? folder.Split('/').Last(), generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL, validatorsToRun ?? "");
        }

        #endregion Public Methods
    }
}

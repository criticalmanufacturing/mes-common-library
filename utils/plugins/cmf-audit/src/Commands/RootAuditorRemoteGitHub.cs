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
    /// "root" command under "review-remote-github": downloads the project from GitHub before auditing it.
    /// </summary>
    [CmfCommand("root", Parent = "review-remote-github", Description = "Will audit the project Root, downloading it from GitHub")]
    public class RootAuditorRemoteGitHub : RootAuditor
    {
        #region Public Constructors

        public RootAuditorRemoteGitHub() : this(new FileSystem())
        {
        }

        public RootAuditorRemoteGitHub(IFileSystem fileSystem, GitHubClient gitHubClient = null) : base(fileSystem)
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

            // Add the handler
            cmd.Handler = CommandHandler.Create<
             string, string, string, string, string,
             string,
             bool, string,
             bool, string, string, string, string, bool>(ExecuteAsync);
        }

        /// <summary>
        /// Executes the specified target
        /// </summary>
        /// <exception cref="PluginException"></exception>
        public async Task ExecuteAsync(
            string repository, string gitRef, string project, string path, string githubToken,
            string teamProject,
            bool generateReport, string reportOutputFolder,
            bool postEvent, string hostAddress, string tenantName, string clientId, string securityAccessToken, bool useSSL)
        {
            using var activity = CLI.Core.Objects.ExecutionContext.ServiceProvider?.GetService<ITelemetryService>()?.StartExtendedActivity(this.GetType().Name);

            var folder = GitHubClient.ResolveProjectPath(path, project);
            var projectRoot = await (this.gitHubClient ?? new GitHubClient(this.fileSystem)).DownloadFolderAsync(repository, gitRef, folder, githubToken);
            var packageDir = projectRoot;

            await ExecuteAsync(packageDir, teamProject ?? folder.Split('/').Last(), generateReport, reportOutputFolder, postEvent, hostAddress, tenantName, clientId, securityAccessToken, useSSL);
        }
    }
}

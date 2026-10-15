using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.IO;

namespace audit.Objects
{
    /// <summary>
    /// Minimal Azure DevOps REST API client used to read a repository's contents remotely,
    /// so a project can be audited without a local checkout.
    /// </summary>
    public class AzureDevOpsClientWrapper : IDisposable
    {
        /// <summary>
        /// The azure dev ops URL
        /// </summary>
        public readonly string AzureDevOpsUrl;

        /// <summary>
        /// The personal access token
        /// </summary>
        public readonly string PersonalAccessToken;

        /// <summary>
        /// The team project
        /// </summary>
        public readonly TeamProject TeamProject;

        private readonly VssConnection connection;

        public AzureDevOpsClientWrapper()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureDevOpsClientWrapper" /> class.
        /// </summary>
        /// <param name="azureDevOpsUrl">The azure dev ops URL.</param>
        /// <param name="teamProject">The team project.</param>
        /// <param name="repository">The repository. Unused: kept for call-site compatibility.</param>
        /// <param name="personalAccessToken">The personal access token.</param>
        public AzureDevOpsClientWrapper(string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken)
        {
            AzureDevOpsUrl = azureDevOpsUrl;
            PersonalAccessToken = personalAccessToken;

            var orgUrl = new Uri(azureDevOpsUrl);
            connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, personalAccessToken));
            TeamProject = ProjectClient.GetProject(teamProject).Result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureDevOpsClientWrapper" /> class.
        /// </summary>
        public AzureDevOpsClientWrapper(string azureDevOpsUrl, string teamProject, string personalAccessToken)
            : this(azureDevOpsUrl, teamProject, null, personalAccessToken)
        {
        }

        private GitHttpClient GitClient => connection.GetClient<GitHttpClient>();

        private ProjectHttpClient ProjectClient => connection.GetClient<ProjectHttpClient>();

        /// <summary>
        /// Gets a repository's contents at the given path as a zip stream.
        /// </summary>
        public virtual Stream GetRepositoryItemsAsZip(string id, string path)
        {
            try
            {
                return GitClient.GetItemZipAsync(project: TeamProject.Name, id, path).Result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the list of items (files/folders) under the given repository path.
        /// </summary>
        public virtual List<GitItem> GetRepositoryItems(string id, string path, VersionControlRecursionType versionControlRecursionType = VersionControlRecursionType.Full)
        {
            return GitClient.GetItemsAsync(TeamProject.Name, id, path, versionControlRecursionType, download: false, includeContentMetadata: true).Result;
        }

        /// <summary>
        /// Gets the content of a single repository item as a stream.
        /// </summary>
        public virtual Stream GetRepositoryItemContent(string id, string path, VersionControlRecursionType versionControlRecursionType = VersionControlRecursionType.Full)
        {
            return GitClient.GetItemContentAsync(project: TeamProject.Name, repositoryId: id, path: path).Result;
        }

        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}

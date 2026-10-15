using System;

namespace audit.Objects
{
    /// <summary>
    /// Default values for Azure DevOps connection options, sourced from the standard
    /// Azure Pipelines predefined environment variables.
    /// </summary>
    public class AzureDevOpsOptions
    {
        #region Environment Variables

        /// <summary>
        /// The build repository name
        /// </summary>
        public const string ENV_BUILD_REPOSITORY_NAME = "BUILD_REPOSITORY_NAME";

        /// <summary>
        /// The system accesstoken
        /// </summary>
        public const string ENV_SYSTEM_ACCESSTOKEN = "SYSTEM_ACCESSTOKEN";

        /// <summary>
        /// The system teamfoundationcollectionuri
        /// </summary>
        public const string ENV_SYSTEM_TEAMFOUNDATIONCOLLECTIONURI = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";

        /// <summary>
        /// The system teamproject
        /// </summary>
        public const string ENV_SYSTEM_TEAMPROJECT = "SYSTEM_TEAMPROJECT";

        #endregion Environment Variables

        /// <summary>
        /// Gets or sets the azure dev ops URL.
        /// </summary>
        public string azureDevOpsUrl { get; set; }

        /// <summary>
        /// Gets or sets the personal access token.
        /// </summary>
        public string personalAccessToken { get; set; }

        /// <summary>
        /// Gets or sets the repository.
        /// </summary>
        public string repository { get; set; }

        /// <summary>
        /// Gets or sets the team project.
        /// </summary>
        public string teamProject { get; set; }

        /// <summary>
        /// Gets the default values from the standard Azure Pipelines environment variables.
        /// </summary>
        public static AzureDevOpsOptions GetDefaultValues()
        {
            return new()
            {
                azureDevOpsUrl = Environment.GetEnvironmentVariable(ENV_SYSTEM_TEAMFOUNDATIONCOLLECTIONURI),
                teamProject = Environment.GetEnvironmentVariable(ENV_SYSTEM_TEAMPROJECT),
                repository = Environment.GetEnvironmentVariable(ENV_BUILD_REPOSITORY_NAME),
                personalAccessToken = Environment.GetEnvironmentVariable(ENV_SYSTEM_ACCESSTOKEN),
            };
        }
    }
}

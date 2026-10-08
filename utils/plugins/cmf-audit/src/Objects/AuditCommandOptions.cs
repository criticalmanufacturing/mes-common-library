using audit.Objects;
using System.CommandLine;

namespace audit.Objects
{
    /// <summary>
    /// Builds the command-line options shared by every audit command, keeping their
    /// aliases, descriptions and defaults identical across the whole command group.
    /// </summary>
    internal static class AuditCommandOptions
    {
        /// <summary>
        /// Environment variable used as default for --securityAccessToken
        /// </summary>
        public const string ENV_SECURITY_TOKEN = "CMF_AUDIT_SECURITY_TOKEN";

        /// <summary>
        /// Adds the reporting and MES post-event options that are common to every audit command,
        /// regardless of whether the project is sourced locally or fetched from Azure DevOps.
        /// </summary>
        /// <param name="cmd">The command being configured.</param>
        /// <param name="generateReportByDefault">
        /// Default for <c>--generateReport</c>. The top-level <c>review</c> command defaults to
        /// <c>false</c>; the per-package auditors default to <c>true</c>.
        /// </param>
        public static void AddCommonOptions(Command cmd, bool generateReportByDefault)
        {
            var defaults = AzureDevOpsOptions.GetDefaultValues();

            cmd.AddOption(new Option<string>(
                aliases: ["--teamProject"],
                description: "The name of the project that contains this build. predefined variable: System.TeamProject",
                getDefaultValue: () => defaults.teamProject ?? Cmf.CLI.Core.Objects.ExecutionContext.Instance.ProjectConfig?.ProjectName));

            cmd.AddOption(new Option<bool>(
                aliases: ["--generateReport"],
                description: "Will generate a report and persist the flagged artifacts",
                getDefaultValue: () => generateReportByDefault));

            cmd.AddOption(new Option<string>(
                aliases: ["--reportOutputFolder"],
                description: "Folder where a report will be generated, requires the generateReport flag to be true",
                getDefaultValue: () => Path.GetTempPath()));

            cmd.AddOption(new Option<bool>(
                aliases: ["--postEvent"],
                description: "Will perform a post event to an MES System",
                getDefaultValue: () => false));

            cmd.AddOption(new Option<string>(
                aliases: ["--hostAddress"],
                description: "Host of the MES system that receives the audit events (used with --postEvent)",
                getDefaultValue: () => "localhost"));

            cmd.AddOption(new Option<string>(
                aliases: ["--tenantName"],
                description: "MES tenant name (defaults to the tenant in .project-config.json)"));

            cmd.AddOption(new Option<string>(
                aliases: ["--clientId"],
                description: "MES OAuth client id",
                getDefaultValue: () => "MES"));

            cmd.AddOption(new Option<string>(
                aliases: ["--securityAccessToken"],
                description: "MES security portal access token. Prefer setting the CMF_AUDIT_SECURITY_TOKEN environment variable, so the token does not appear in process lists or shell history",
                getDefaultValue: () => Environment.GetEnvironmentVariable(ENV_SECURITY_TOKEN) ?? ""));

            cmd.AddOption(new Option<bool>(
                aliases: ["--useSSL"],
                description: "Use HTTPS to reach the MES system. Disabling it sends the token in clear text",
                getDefaultValue: () => true));
        }

        /// <summary>
        /// Adds the Azure DevOps connection options needed only by the "review-remote-ado" command
        /// tree, which fetches the project from Azure DevOps instead of reading a local checkout.
        /// </summary>
        /// <param name="cmd">The command being configured.</param>
        public static void AddRemoteOptions(Command cmd)
        {
            var defaults = AzureDevOpsOptions.GetDefaultValues();

            cmd.AddOption(new Option<string>(
                aliases: ["--azureDevOpsUrl"],
                description: "The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri",
                getDefaultValue: () => defaults.azureDevOpsUrl));

            cmd.AddOption(new Option<string>(
                aliases: ["--repository"],
                description: "The name of the triggering repository. predefined variable: Build.Repository.Name",
                getDefaultValue: () => defaults.repository));

            cmd.AddOption(new Option<string>(
                aliases: ["--personalAccessToken"],
                description: "Token used to access AzureDevOps REST API. Prefer setting the SYSTEM_ACCESSTOKEN environment variable. predefined variable: System.AccessToken",
                getDefaultValue: () => defaults.personalAccessToken));
        }
    }
}

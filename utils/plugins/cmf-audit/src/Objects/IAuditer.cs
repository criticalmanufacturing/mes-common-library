using Cmf.CLI.Core.Commands;

namespace audit.Objects
{
    public interface IAuditor : IBaseCommand
    {
        Task ExecuteAsync(string azureDevOpsUrl, string teamProject, string repository, string personalAccessToken, string infrastructureFileName, string pipelineVersion, string npmRegistry, bool generateReport, string reportOutputFolder);
    }
}

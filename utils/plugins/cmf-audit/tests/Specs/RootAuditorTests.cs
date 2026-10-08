using audit.Objects;
using Cmf.Cli.Plugin.Audit.Commands.install;
using Cmf.CLI.Utilities;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Moq;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace specs
{
    public class RootAuditorTests
    {
        #region Pure helper tests

        [Fact]
        public void ExtractVersionAndStrategy_ReadsSdkVersionAndRollForward()
        {
            var globalJson = JsonSerializer.Deserialize<Dictionary<string, object>>(
                """{"sdk": {"version": "8.0.100", "rollForward": "latestMinor"}}""");

            (string version, string strategy) = RootAuditor.ExtractVersionAndStrategy(globalJson);

            Assert.Equal("8.0.100", version);
            Assert.Equal("latestMinor", strategy);
        }

        [Fact]
        public void ExtractVersionAndStrategy_NoSdkKey_ReturnsNulls()
        {
            var globalJson = JsonSerializer.Deserialize<Dictionary<string, object>>("{}");

            (string version, string strategy) = RootAuditor.ExtractVersionAndStrategy(globalJson);

            Assert.Null(version);
            Assert.Null(strategy);
        }

        [Fact]
        public void ExtractRepositories_ReadsCIRepositoryAndList()
        {
            var repositoriesJson = JsonSerializer.Deserialize<Dictionary<string, object>>(
                """{"CIRepository": "MyCIRepo", "Repositories": ["RepoA", "RepoB"]}""");

            (string ciRepo, List<string> repositories) = RootAuditor.ExtractRepositories(repositoriesJson);

            Assert.Equal("MyCIRepo", ciRepo);
            Assert.Equal(["RepoA", "RepoB"], repositories);
        }

        [Fact]
        public void ParseNugetFeeds_ReadsPackageSources()
        {
            var xml = """
                <configuration>
                  <packageSources>
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                    <add key="internal" value="https://internal.feed/nuget" />
                  </packageSources>
                </configuration>
                """;

            var feeds = RootAuditor.ParseNugetFeeds(xml);

            Assert.Equal("https://api.nuget.org/v3/index.json", feeds["nuget.org"]);
            Assert.Equal("https://internal.feed/nuget", feeds["internal"]);
        }

        [Fact]
        public void ValidateCommittedScripts_FiltersScriptExtensionsOnly()
        {
            var items = new List<GitItem>
            {
                new() { Path = "/scripts/deploy.ps1" },
                new() { Path = "/scripts/run.sh" },
                new() { Path = "/scripts/legacy.bat" },
                new() { Path = "/scripts/legacy.cmd" },
                new() { Path = "/src/Program.cs" },
                new() { Path = "/README.md" },
            };

            var scripts = RootAuditor.ValidateCommittedScripts(items.Select(i => i.Path));

            Assert.Equal(4, scripts.Count);
            Assert.Equal("/scripts/deploy.ps1", scripts["deploy.ps1"]);
            Assert.Equal("/scripts/run.sh", scripts["run.sh"]);
            Assert.Equal("/scripts/legacy.bat", scripts["legacy.bat"]);
            Assert.Equal("/scripts/legacy.cmd", scripts["legacy.cmd"]);
        }

        [Fact]
        public void ValidateCommittedScripts_NoScripts_ReturnsEmpty()
        {
            var items = new List<GitItem> { new() { Path = "/src/Program.cs" } };

            var scripts = RootAuditor.ValidateCommittedScripts(items.Select(i => i.Path));

            Assert.Empty(scripts);
        }

        #endregion

        #region ExecuteAsync integration tests

        private static Stream Json(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        private const string CmfPackageJson = """
            {
                "packageId": "Cmf.Custom.Root",
                "version": "1.0.0",
                "description": "Test root package",
                "packageType": "Generic"
            }
            """;

        [Fact]
        public async Task ExecuteAsync_Local_ReadsEverythingFromDiskWithoutAzureDevOps()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/workdir/cmfpackage.json", new MockFileData(CmfPackageJson));
            fileSystem.AddFile("/workdir/scripts/deploy.ps1", new MockFileData("Write-Host hi"));
            fileSystem.AddFile("/workdir/src/Program.cs", new MockFileData("// noop"));
            fileSystem.AddFile("/workdir/global.json", new MockFileData("""{"sdk": {"version": "8.0.100", "rollForward": "latestMinor"}}"""));
            fileSystem.AddFile("/workdir/repositories.json", new MockFileData("""{"CIRepository": "MyCIRepo", "Repositories": ["RepoA"]}"""));
            fileSystem.AddFile("/workdir/NuGet.Config", new MockFileData("""<configuration><packageSources><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>"""));
            fileSystem.AddFile("/workdir/.project-config.json", new MockFileData("""{"ProjectName": "TestProject"}"""));
            var workingDir = fileSystem.DirectoryInfo.New("/workdir");

            var logWriter = TestSupport.GetLogStringWriter();
            var command = new RootAuditor(fileSystem);

            await command.ExecuteAsync(
                workingDir,
                teamProject: "proj",
                generateReport: false, reportOutputFolder: "/reports",
                postEvent: false, hostAddress: null, tenantName: null, clientId: null, securityAccessToken: null, useSSL: true);

            Assert.Contains("Successfully audited the root package", logWriter.ToString());
        }

        [Fact]
        public async Task ExecuteAsync_Local_NoCmfPackageJson_ThrowsWithReviewRemoteHint()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/workdir");
            var workingDir = fileSystem.DirectoryInfo.New("/workdir");
            var command = new RootAuditor(fileSystem);

            var ex = await Assert.ThrowsAsync<CliException>(() => command.ExecuteAsync(
                workingDir,
                teamProject: "proj",
                generateReport: false, reportOutputFolder: "/reports",
                postEvent: false, hostAddress: null, tenantName: null, clientId: null, securityAccessToken: null, useSSL: true));

            Assert.Contains("review-remote-ado", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_Remote_FetchesEverythingFromAzureDevOps()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/workdir/cmfpackage.json", new MockFileData(CmfPackageJson));
            var workingDir = fileSystem.DirectoryInfo.New("/workdir");

            var azureDevOpsClient = new Mock<AzureDevOpsClientWrapper>();
            azureDevOpsClient
                .Setup(c => c.GetRepositoryItems("myrepo", "", It.IsAny<VersionControlRecursionType>()))
                .Returns([new GitItem { Path = "/scripts/deploy.ps1" }, new GitItem { Path = "/src/Program.cs" }]);
            azureDevOpsClient
                .Setup(c => c.GetRepositoryItemContent("myrepo", "global.json", It.IsAny<VersionControlRecursionType>()))
                .Returns(() => Json("""{"sdk": {"version": "8.0.100", "rollForward": "latestMinor"}}"""));
            azureDevOpsClient
                .Setup(c => c.GetRepositoryItemContent("myrepo", "repositories.json", It.IsAny<VersionControlRecursionType>()))
                .Returns(() => Json("""{"CIRepository": "MyCIRepo", "Repositories": ["RepoA"]}"""));
            azureDevOpsClient
                .Setup(c => c.GetRepositoryItemContent("myrepo", "NuGet.Config", It.IsAny<VersionControlRecursionType>()))
                .Returns(() => Json("""<configuration><packageSources><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>"""));
            azureDevOpsClient
                .Setup(c => c.GetRepositoryItemContent("myrepo", ".project-config.json", It.IsAny<VersionControlRecursionType>()))
                .Returns(() => Json("""{"ProjectName": "TestProject"}"""));

            var logWriter = TestSupport.GetLogStringWriter();
            var command = new RootAuditorRemote(fileSystem, azureDevOpsClient.Object);

            await command.ExecuteAsync(
                workingDir,
                azureDevOpsUrl: "https://dev.azure.com/fake-org", teamProject: "proj", repository: "myrepo", personalAccessToken: "fake-pat",
                generateReport: false, reportOutputFolder: "/reports",
                postEvent: false, hostAddress: null, tenantName: null, clientId: null, securityAccessToken: null, useSSL: true);

            Assert.Contains("Successfully audited the root package", logWriter.ToString());
            azureDevOpsClient.Verify(c => c.GetRepositoryItems("myrepo", "", It.IsAny<VersionControlRecursionType>()), Times.Once);
            azureDevOpsClient.Verify(c => c.GetRepositoryItemContent("myrepo", "global.json", It.IsAny<VersionControlRecursionType>()), Times.Once);
            azureDevOpsClient.Verify(c => c.GetRepositoryItemContent("myrepo", ".project-config.json", It.IsAny<VersionControlRecursionType>()), Times.Once);
        }

        #endregion
    }
}

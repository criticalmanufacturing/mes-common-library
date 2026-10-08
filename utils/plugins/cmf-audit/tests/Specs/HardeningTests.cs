using audit.Objects;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Utilities;
using AuditUtilities = audit.Objects.Utilities;
using Cmf.Cli.Plugin.Audit.Commands.install;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace specs
{
    /// <summary>
    /// Regression tests for the path-traversal, command-injection and robustness fixes.
    /// </summary>
    public class HardeningTests
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "audit-root");

        #region SafeCombine

        [Fact]
        public void SafeCombine_RelativePath_StaysInsideRoot()
        {
            var result = AuditUtilities.SafeCombine(Root, "Builds/pipeline.yml");

            Assert.Equal(Path.GetFullPath(Path.Combine(Root, "Builds", "pipeline.yml")), result);
        }

        [Fact]
        public void SafeCombine_LeadingSlash_IsTreatedAsRelativeToRoot()
        {
            var result = AuditUtilities.SafeCombine(Root, "/.project-config.json");

            Assert.Equal(Path.GetFullPath(Path.Combine(Root, ".project-config.json")), result);
        }

        [Theory]
        [InlineData("../outside.txt")]
        [InlineData("Builds/../../outside.txt")]
        [InlineData("/../../etc/passwd")]
        public void SafeCombine_PathEscapingRoot_Throws(string maliciousPath)
        {
            Assert.Throws<CliException>(() => AuditUtilities.SafeCombine(Root, maliciousPath));
        }

        #endregion

        #region BuildCmfPipelineArguments

        [Fact]
        public void BuildCmfPipelineArguments_ValidInput_PassesValuesAsSeparateArguments()
        {
            var args = PipelineAuditor.BuildCmfPipelineArguments("https://dev.azure.com/org", "My Repo", "1.2.3-beta.1", "/tmp/w/.pipeline-config.json");

            Assert.Contains("@criticalmanufacturing/pipeline@1.2.3-beta.1", args);
            Assert.Equal("https://dev.azure.com/org/_git/My Repo", args[args.IndexOf("--repositoryUrl") + 1]);
            Assert.Equal("/tmp/w/.pipeline-config.json", args[args.IndexOf("--infrastructure") + 1]);
        }

        [Theory]
        [InlineData("latest; rm -rf /")]
        [InlineData("1.0.0 && calc")]
        [InlineData("")]
        [InlineData("$(whoami)")]
        public void BuildCmfPipelineArguments_InvalidVersion_Throws(string version)
        {
            Assert.Throws<CliException>(() => PipelineAuditor.BuildCmfPipelineArguments("https://dev.azure.com/org", "repo", version, "/tmp/infra.json"));
        }

        [Theory]
        [InlineData("repo\" & calc")]
        [InlineData("repo|whoami")]
        [InlineData("repo%PATH%")]
        public void BuildCmfPipelineArguments_ShellMetacharactersInRepository_Throws(string repository)
        {
            Assert.Throws<CliException>(() => PipelineAuditor.BuildCmfPipelineArguments("https://dev.azure.com/org", repository, "latest", "/tmp/infra.json"));
        }

        #endregion

        #region HashProjectPipelines

        private sealed class ZipAzureDevOpsClient(byte[] zip) : AzureDevOpsClientWrapper
        {
            public override Stream GetRepositoryItemsAsZip(string id, string path) => new MemoryStream(zip);
        }

        [Fact]
        public async Task HashProjectPipelines_UnchangedYaml_HashesOriginalContent()
        {
            const string yaml = "trigger: none\nsteps:\n- script: echo hi\n";
            using var zipBuffer = new MemoryStream();
            using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var writer = new StreamWriter(archive.CreateEntry("Builds/CI.yml").Open());
                writer.Write(yaml);
            }

            var hashes = await PipelineAuditor.HashProjectPipelines("repo", new ZipAzureDevOpsClient(zipBuffer.ToArray()));

            var expected = PipelineAuditor.ComputeSHA256Hash(new MemoryStream(Encoding.UTF8.GetBytes(yaml)));
            Assert.Equal(expected, hashes["Builds/CI.yml"]);
        }

        #endregion

        #region Missing optional files and duplicates

        [Fact]
        public void ExtractVersionAndStrategy_NullGlobalJson_ReturnsNulls()
        {
            (string version, string strategy) = RootAuditor.ExtractVersionAndStrategy(null);

            Assert.Null(version);
            Assert.Null(strategy);
        }

        [Fact]
        public void ParseNugetFeeds_NullContent_ReturnsEmpty()
        {
            Assert.Empty(RootAuditor.ParseNugetFeeds(null));
        }

        [Fact]
        public void ValidateCommittedScripts_SameFileNameInDifferentFolders_KeepsBoth()
        {
            var scripts = RootAuditor.ValidateCommittedScripts(["/a/build.ps1", "/b/build.ps1"]);

            Assert.Equal(2, scripts.Count);
            Assert.Equal("/a/build.ps1", scripts["build.ps1"]);
            Assert.Equal("/b/build.ps1", scripts["/b/build.ps1"]);
        }

        [Fact]
        public void AuditorExportedFiles_XmlWithoutObjectElement_IsCountedAsUnknown()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/pkg/Empty.xml", new MockFileData("<Root></Root>"));

            var reporters = new Reporters();
            reporters.RowAdder = (type, name, metadata) => { };

            var result = DataAuditor.AuditorExportedFiles([fileSystem.FileInfo.New("/pkg/Empty.xml")], TestSupport.CreateCmfPackage("Cmf.Custom.Data", PackageType.Data), reporters);

            Assert.Equal(1, result["Unknown"]);
        }

        #endregion
    }
}

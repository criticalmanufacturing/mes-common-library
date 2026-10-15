using Cmf.CLI.Utilities;
using Cmf.Cli.Plugin.Audit.Commands.install;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace specs
{
    public class PipelineAuditorTests
    {
        private static Stream Stream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

        #region ComputeSHA256Hash / AddHashFile

        [Fact]
        public void ComputeSHA256Hash_SameContent_ProducesSameHash()
        {
            var hash1 = PipelineAuditor.ComputeSHA256Hash(Stream("hello world"));
            var hash2 = PipelineAuditor.ComputeSHA256Hash(Stream("hello world"));

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void ComputeSHA256Hash_DifferentContent_ProducesDifferentHash()
        {
            var hash1 = PipelineAuditor.ComputeSHA256Hash(Stream("hello world"));
            var hash2 = PipelineAuditor.ComputeSHA256Hash(Stream("goodbye world"));

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void ComputeSHA256Hash_MatchesIndependentlyComputedHash()
        {
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello world"))).ToLower();

            var actual = PipelineAuditor.ComputeSHA256Hash(Stream("hello world"));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AddHashFile_AddsComputedHashUnderGivenKey()
        {
            var dictionary = new Dictionary<string, string>();

            PipelineAuditor.AddHashFile(dictionary, "file.yml", Stream("content"));

            Assert.Equal(PipelineAuditor.ComputeSHA256Hash(Stream("content")), dictionary["file.yml"]);
        }

        #endregion

        #region HandleYamlFiles

        [Fact]
        public void HandleYamlFiles_NonYamlEntry_ReturnsFalseAndLeavesDictionaryUntouched()
        {
            var dictionary = new Dictionary<string, string>();

            var changed = PipelineAuditor.HandleYamlFiles(dictionary, "file.json", Stream("{}"), false);

            Assert.False(changed);
            Assert.Empty(dictionary);
        }

        [Fact]
        public void HandleYamlFiles_EnvironmentParameterPresent_IsFilteredAndHashed()
        {
            var yaml = """
                parameters:
                  - name: Environment
                    type: string
                  - name: OtherParam
                    type: string
                """;
            var dictionary = new Dictionary<string, string>();

            var changed = PipelineAuditor.HandleYamlFiles(dictionary, "pipeline.yml", Stream(yaml), false);

            Assert.True(changed);
            Assert.True(dictionary.ContainsKey("pipeline.yml"));
        }

        [Fact]
        public void HandleYamlFiles_NoEnvironmentParameter_IsNotChangedAndDictionaryUntouched()
        {
            var yaml = """
                parameters:
                  - name: OtherParam
                    type: string
                """;
            var dictionary = new Dictionary<string, string>();

            var changed = PipelineAuditor.HandleYamlFiles(dictionary, "pipeline.yml", Stream(yaml), false);

            Assert.False(changed);
            Assert.Empty(dictionary);
        }

        #endregion

        #region HashRunPipelineFiles

        [Fact]
        public void HashRunPipelineFiles_HashesYamlFilesAndSkipsVarsFolder()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/run/pipeline.yml", new MockFileData("foo: bar"));
            fileSystem.AddFile("/run/sub/nested.yml", new MockFileData("baz: qux"));
            fileSystem.AddFile("/run/.vars/should-be-skipped.yml", new MockFileData("secret: value"));
            var workingDirectory = fileSystem.DirectoryInfo.New("/run");

            var result = PipelineAuditor.HashRunPipelineFiles([], workingDirectory);

            Assert.True(result.ContainsKey("pipeline.yml"));
            Assert.True(result.ContainsKey("sub/nested.yml"));
            Assert.DoesNotContain(result.Keys, key => key.Contains(".vars"));
        }

        #endregion

        #region HashLocalPipelines

        [Fact]
        public void HashLocalPipelines_HashesBuildsFolderAndSkipsExcludedFiles()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/project/Builds/pipeline.yml", new MockFileData("foo: bar"));
            fileSystem.AddFile("/project/Builds/sub/nested.yml", new MockFileData("baz: qux"));
            fileSystem.AddFile("/project/Builds/.vars/secret.yml", new MockFileData("secret: value"));
            fileSystem.AddFile("/project/Builds/config.json", new MockFileData("{}"));
            fileSystem.AddFile("/project/Builds/.gitignore", new MockFileData("*.tmp"));
            var projectRoot = fileSystem.DirectoryInfo.New("/project");

            var result = PipelineAuditor.HashLocalPipelines(projectRoot);

            Assert.True(result.ContainsKey("Builds/pipeline.yml"));
            Assert.True(result.ContainsKey("Builds/sub/nested.yml"));
            Assert.DoesNotContain(result.Keys, key => key.Contains(".vars"));
            Assert.DoesNotContain(result.Keys, key => key.Contains(".json"));
            Assert.DoesNotContain(result.Keys, key => key.Contains(".gitignore"));
        }

        [Fact]
        public void HashLocalPipelines_NoBuildsFolder_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/project");
            var projectRoot = fileSystem.DirectoryInfo.New("/project");

            Assert.Throws<CliException>(() => PipelineAuditor.HashLocalPipelines(projectRoot));
        }

        #endregion

        #region GeneratePipelineAuditArtifacts

        [Fact]
        public void GeneratePipelineAuditArtifacts_CopiesRepoOnlyPipelineOnlyAndMismatchedFiles()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/run/only-in-pipeline.yml", new MockFileData("pipeline-content"));
            fileSystem.AddFile("/run/mismatch.yml", new MockFileData("pipeline-mismatch-content"));
            fileSystem.AddDirectory("/reports");
            var pipelineRunDirectory = fileSystem.DirectoryInfo.New("/run");
            var reportOutputFolder = fileSystem.DirectoryInfo.New("/reports");

            var onlyInRepoHashes = new Dictionary<string, string> { ["only-in-repo.yml"] = "hashA" };
            var onlyInPipelinesHashes = new Dictionary<string, string> { ["only-in-pipeline.yml"] = "hashB" };
            var mismatchedValues = new Dictionary<string, string[]> { ["mismatch.yml"] = ["repoHash", "pipelineHash"] };

            Stream GetItemContentFromAzure(string path) => Stream($"repo-content:{path}");

            PipelineAuditor.GeneratePipelineAuditArtifacts(onlyInRepoHashes, onlyInPipelinesHashes, mismatchedValues, GetItemContentFromAzure, pipelineRunDirectory, reportOutputFolder);

            Assert.Equal("repo-content:only-in-repo.yml", fileSystem.File.ReadAllText("/reports/OnlyInRepositoryFiles/Repository/only-in-repo.yml"));
            Assert.Equal("pipeline-content", fileSystem.File.ReadAllText("/reports/OnlyInPipelineFiles/PipelineRun/only-in-pipeline.yml"));
            Assert.Equal("repo-content:mismatch.yml", fileSystem.File.ReadAllText("/reports/MismatchHashes/Repository/mismatch.yml"));
            Assert.Equal("pipeline-mismatch-content", fileSystem.File.ReadAllText("/reports/MismatchHashes/PipelineRun/mismatch.yml"));
        }

        [Fact]
        public void GeneratePipelineAuditArtifacts_PipelineFileMissingFromRun_Throws()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/run");
            fileSystem.AddDirectory("/reports");
            var pipelineRunDirectory = fileSystem.DirectoryInfo.New("/run");
            var reportOutputFolder = fileSystem.DirectoryInfo.New("/reports");

            var onlyInPipelinesHashes = new Dictionary<string, string> { ["missing.yml"] = "hashB" };

            Assert.ThrowsAny<Exception>(() => PipelineAuditor.GeneratePipelineAuditArtifacts(
                [], onlyInPipelinesHashes, [], _ => Stream(""), pipelineRunDirectory, reportOutputFolder));
        }

        #endregion
    }
}

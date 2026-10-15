using audit.Objects;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net;
using Xunit;

namespace specs
{
    public class GitHubClientTests
    {
        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public HttpRequestMessage LastRequest;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(respond(request));
            }
        }

        private static byte[] BuildArchive(params (string path, string content)[] files)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (path, content) in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry("owner-repo-abc123/" + path).Open());
                    writer.Write(content);
                }
            }
            return ms.ToArray();
        }

        [Fact]
        public async Task DownloadFolderAsync_ExtractsOnlyRequestedFolder_AndSendsToken()
        {
            var archive = BuildArchive(
                ("features/ProjA/cmfpackage.json", "{}"),
                ("features/ProjA/src/file.txt", "a"),
                ("features/ProjB/cmfpackage.json", "{}"),
                ("README.md", "x"));
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
            var client = new GitHubClient(new FileSystem(), new HttpClient(handler));

            var dir = await client.DownloadFolderAsync("owner/repo", "11.3/dev", "features/ProjA", "secret");
            try
            {
                Assert.True(File.Exists(Path.Combine(dir.FullName, "cmfpackage.json")));
                Assert.True(File.Exists(Path.Combine(dir.FullName, "src", "file.txt")));
                Assert.Equal(2, Directory.GetFiles(dir.FullName, "*", SearchOption.AllDirectories).Length);
                Assert.Equal("https://api.github.com/repos/owner/repo/zipball/11.3/dev", handler.LastRequest.RequestUri.ToString());
                Assert.Equal("secret", handler.LastRequest.Headers.Authorization.Parameter);
            }
            finally
            {
                dir.Delete(true);
            }
        }

        [Fact]
        public async Task DownloadFolderAsync_MissingFolder_Throws()
        {
            var archive = BuildArchive(("README.md", "x"));
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
            var client = new GitHubClient(new FileSystem(), new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<CliException>(() => client.DownloadFolderAsync("owner/repo", "11.3", "features/Nope", null));
            Assert.Contains("not found", ex.Message);
        }

        [Fact]
        public async Task DownloadFolderAsync_NotFound_MentionsToken()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var client = new GitHubClient(new FileSystem(), new HttpClient(handler));

            var ex = await Assert.ThrowsAsync<CliException>(() => client.DownloadFolderAsync("owner/repo", "11.3", "x", null));
            Assert.Contains("GITHUB_TOKEN", ex.Message);
        }

        [Theory]
        [InlineData("not-a-repo")]
        [InlineData("a/b/c")]
        public async Task DownloadFolderAsync_InvalidRepository_Throws(string repository)
        {
            var client = new GitHubClient(new FileSystem(), new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));
            await Assert.ThrowsAsync<CliException>(() => client.DownloadFolderAsync(repository, "11.3", "x", null));
        }

        [Fact]
        public void ResolveProjectPath_UsesProjectUnderFeatures_AndPathTakesPrecedence()
        {
            Assert.Equal("features/Foo", GitHubClient.ResolveProjectPath(null, "Foo"));
            Assert.Equal("some/where", GitHubClient.ResolveProjectPath("/some/where/", "Foo"));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("../etc", null)]
        [InlineData(null, "..")]
        public void ResolveProjectPath_InvalidInput_Throws(string path, string project)
        {
            Assert.Throws<CliException>(() => GitHubClient.ResolveProjectPath(path, project));
        }

        [Fact]
        public void MakeUniqueSheetName_DuplicateNames_GetNumericSuffixWithinExcelLimit()
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var longName = new string('a', 31);

            Assert.Equal("P_Package_1", audit.Objects.Utilities.MakeUniqueSheetName("P_Package_1", used));
            Assert.Equal("p_package_1_2", audit.Objects.Utilities.MakeUniqueSheetName("p_package_1", used));
            Assert.Equal("P_Package_1_3", audit.Objects.Utilities.MakeUniqueSheetName("P_Package_1", used));
            Assert.Equal(longName, audit.Objects.Utilities.MakeUniqueSheetName(longName, used));
            Assert.Equal(31, audit.Objects.Utilities.MakeUniqueSheetName(longName, used).Length);
        }
    }
}

using Cmf.CLI.Core;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace audit.Objects
{
    /// <summary>
    /// Default values for the GitHub connection options.
    /// </summary>
    public static class GitHubOptions
    {
        /// <summary>
        /// Environment variable used as default for --githubToken
        /// </summary>
        public const string ENV_GITHUB_TOKEN = "GITHUB_TOKEN";

        /// <summary>
        /// Repository audited when --repository is not supplied
        /// </summary>
        public const string DefaultRepository = "criticalmanufacturing/mes-common-library";

        /// <summary>
        /// Branch or tag audited when --gitRef is not supplied
        /// </summary>
        public const string DefaultRef = "11.3/dev";

        /// <summary>
        /// Folder that holds the projects when only --project is supplied
        /// </summary>
        public const string DefaultFeaturesPath = "features";
    }

    /// <summary>
    /// Minimal GitHub REST API client used to fetch a project folder of a repository, so it can be
    /// audited without a local checkout.
    /// </summary>
    public class GitHubClient
    {
        private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

        private readonly HttpClient httpClient;
        private readonly IFileSystem fileSystem;

        public GitHubClient(IFileSystem fileSystem, HttpClient httpClient = null)
        {
            this.fileSystem = fileSystem;
            this.httpClient = httpClient ?? SharedHttpClient;
        }

        /// <summary>
        /// Resolves the repository folder to audit from --path / --project.
        /// </summary>
        /// <exception cref="CliException">When neither is supplied, or the path is not a relative path inside the repository.</exception>
        public static string ResolveProjectPath(string path, string project)
        {
            var resolved = !string.IsNullOrWhiteSpace(path)
                ? path
                : !string.IsNullOrWhiteSpace(project) ? $"{GitHubOptions.DefaultFeaturesPath}/{project}" : null;

            if (resolved == null)
            {
                throw new CliException("Please specify the project to audit with --project <name> (under 'features') or --path <folder>");
            }

            resolved = resolved.Replace('\\', '/').Trim('/');
            if (resolved.Split('/').Any(segment => segment == ".." || segment == "."))
            {
                throw new CliException($"Invalid path '{resolved}': it must be a relative path inside the repository.");
            }

            return resolved;
        }

        /// <summary>
        /// Downloads the folder <paramref name="path"/> of <paramref name="repository"/> at <paramref name="gitRef"/>
        /// into a new temporary directory, which becomes the project root (its contents are placed directly in it).
        /// </summary>
        /// <param name="repository">GitHub repository in the "owner/name" form.</param>
        /// <param name="gitRef">Branch, tag or commit.</param>
        /// <param name="path">Repository folder to download.</param>
        /// <param name="token">Optional token; required for private repositories and avoids anonymous rate limits.</param>
        public async Task<IDirectoryInfo> DownloadFolderAsync(string repository, string gitRef, string path, string token)
        {
            var parts = (repository ?? "").Split('/');
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace) || repository.IndexOfAny([' ', '?', '#', '\\']) >= 0)
            {
                throw new CliException($"Invalid repository '{repository}'. Expected the 'owner/name' form.");
            }
            if (string.IsNullOrWhiteSpace(gitRef))
            {
                throw new CliException("Please specify the branch, tag or commit to audit with --gitRef");
            }

            var url = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/zipball/{string.Join('/', gitRef.Split('/').Select(Uri.EscapeDataString))}";
            Log.Information($"Downloading '{path}' from {repository}@{gitRef}...");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("cmf-audit", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var hint = response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? $" Check the repository and ref, and set a token with access to it ({GitHubOptions.ENV_GITHUB_TOKEN} or --githubToken)."
                    : "";
                throw new CliException($"Failed to download {repository}@{gitRef} from GitHub: {(int)response.StatusCode} {response.ReasonPhrase}.{hint}");
            }

            // ZipArchive needs a seekable stream; spool to disk because the repository archive can be large
            var zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
            try
            {
                await using (var zipFile = File.Create(zipPath))
                {
                    await response.Content.CopyToAsync(zipFile);
                }

                var workingDirectory = this.fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
                workingDirectory.Create();

                var extracted = ExtractFolder(zipPath, path, workingDirectory);
                if (extracted == 0)
                {
                    throw new CliException($"Folder '{path}' was not found in {repository}@{gitRef}.");
                }

                Log.Information($"Downloaded {extracted} files to '{workingDirectory.FullName}'");
                return workingDirectory;
            }
            finally
            {
                File.Delete(zipPath);
            }
        }

        /// <summary>
        /// Finds the package with the given id inside a downloaded project.
        /// </summary>
        /// <exception cref="CliException">When no package has that id.</exception>
        public static IDirectoryInfo FindPackageDirectory(IDirectoryInfo projectRoot, string packageId, IFileSystem fileSystem)
        {
            foreach (var file in projectRoot.GetFiles("cmfpackage.json", SearchOption.AllDirectories))
            {
                var package = CmfPackage.Load(file, setDefaultValues: true, fileSystem);
                if (package.PackageId == packageId)
                {
                    return file.Directory;
                }
            }

            throw new CliException($"No package found with ID '{packageId}'");
        }

        private int ExtractFolder(string zipPath, string folder, IDirectoryInfo destination)
        {
            var prefix = folder + "/";
            var extracted = 0;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                // GitHub archives wrap everything in a single "<owner>-<repo>-<sha>/" folder
                var name = entry.FullName;
                var slash = name.IndexOf('/');
                if (slash < 0 || name.EndsWith('/'))
                {
                    continue;
                }

                var repoPath = name[(slash + 1)..];
                if (!repoPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var target = this.fileSystem.FileInfo.New(Utilities.SafeCombine(destination.FullName, repoPath[prefix.Length..]));
                target.Directory.Create();

                using var entryStream = entry.Open();
                using var targetStream = target.Open(FileMode.Create, FileAccess.Write);
                entryStream.CopyTo(targetStream);
                extracted++;
            }

            return extracted;
        }
    }
}

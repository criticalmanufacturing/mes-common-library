using Cmf.CLI.Core;
using Cmf.CLI.Core.Objects;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ExecutionContext = Cmf.CLI.Core.Objects.ExecutionContext;

namespace audit.Services
{
    /// <summary>
    /// NPM client that talks to a Verdaccio registry to resolve the latest published
    /// version of this plugin's npm package.
    /// </summary>
    public class VerdaccioService : INPMClient
    {
        private readonly Uri address;
#pragma warning disable CS0649
        private HttpClient client; // this is only assigned in tests
#pragma warning restore CS0649
        private readonly bool noOp;

        public VerdaccioService(Uri registryAddress, bool noOp = false) => (this.address, this.noOp) = (registryAddress, noOp);

        public async Task<string> GetLatestVersion(bool preRelease = false)
        {
            if (noOp)
            {
                Log.Debug("NoOp Verdaccio is on: skipping version check.");
                return null;
            }

            // http://verdaccio/@criticalmanufacturing/dev
            var client = this.GetClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            try
            {
                var res = await client.GetAsync($"{address.AbsoluteUri.TrimEnd('/')}/{ExecutionContext.PackageId}");
                var body = await res.Content.ReadFromJsonAsync<JsonElement>();
                return body.GetProperty("dist-tags").GetProperty(preRelease ? "next" : "latest").GetString();
            }
            catch (Exception e)
            {
                Log.Debug(e.Message);
                Log.Warning("Could not retrieve latest version information. Try again later.");
            }

            return null;
        }

        public IPackage[] FindPlugins(Uri[] registries)
        {
            // http://verdaccio/-/verdaccio/search/cmf-cli-plugin
            throw new NotImplementedException();
        }

        protected virtual HttpClient GetClient()
        {
            if (this.client != null)
            {
                return this.client;
            }
            var client = new HttpClient();
            // remove the scope @ as it's not a valid user agent character
            client.DefaultRequestHeaders.Add("User-Agent",
                $"{ExecutionContext.PackageId.Replace("@", "")} v{ExecutionContext.CurrentVersion}");
            return client;
        }
    }
}

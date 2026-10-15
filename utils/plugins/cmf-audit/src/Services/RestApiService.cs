using Cmf.CLI.Core;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace audit.Services
{
    public interface IRestApiService
    {
        Task PostEventAsync(PostTelemetry eventData);
        Task PostEventsAsync(List<PostTelemetry> eventsData);
    }

    public class RestApiService : IRestApiService, IDisposable
    {
        private string? _accessToken;
        private HttpClient? _httpClient;
        private ClientConfiguration _clientConfiguration;

        public RestApiService(ClientConfiguration clientConfiguration)
        {
            this._clientConfiguration = clientConfiguration;
            if (!clientConfiguration.useSSL)
            {
                Log.Warning("SSL is disabled: the security token and audit data will be sent unencrypted.");
            }
            this._accessToken = AccessTokenAsync().GetAwaiter().GetResult();
        }

        public async Task<string> AccessTokenAsync()
        {
            this._httpClient = new HttpClient();
            this._httpClient.BaseAddress = new Uri($"http{(_clientConfiguration.useSSL ? "s" : "")}://{_clientConfiguration.HostAddress}");
            var url = $"/SecurityPortal/api/tenant/{Uri.EscapeDataString(_clientConfiguration.ClientTenantName ?? string.Empty)}/oauth2/token";

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _clientConfiguration.ClientId },
                { "grant_type", "refresh_token" },
                { "refresh_token", _clientConfiguration.SecurityAccessToken }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await this._httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(body);
            var accessToken = tokenResponse.GetProperty("access_token").GetString();

            _accessToken = accessToken;
            return accessToken;
        }

        public async Task PostEventsAsync(List<PostTelemetry> eventsData)
        {
            int batchSize = eventsData.Count;

            for (int i = 0; i < eventsData.Count; i += batchSize)
            {
                var batch = eventsData.Skip(i).Take(batchSize).ToList();

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/DataPlatform/PostMultipleIoTEvents");

                // Required headers
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Headers.Add("Cmf_ClientTenantName", _clientConfiguration.ClientTenantName);
                request.Headers.Add("Cmf_HostName", _clientConfiguration.HostAddress);

                var input = new List<PostEventInput>();
                foreach (var eventData in batch)
                {
                    input.Add(new PostEventInput
                    {
                        AppProperties = new AppProperties
                        {
                            ApplicationName = "CMF-Review",
                            EventDefinition = "PostTelemetry",
                            EventTime = DateTime.UtcNow
                        },
                        IgnoreLastServiceId = false,
                        NumberOfRetries = 0,
                        Data = eventData,
                        Priority = 0,
                        AcceptInvalidSchema = false
                    });
                }

                var call = new Dictionary<string, List<PostEventInput>>()
                    {
                        {"IoTEvents", input }
                    };
                var json = JsonSerializer.Serialize(call);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                Log.Debug($"Posting {batch.Count} event(s) to {_clientConfiguration.HostAddress}");

                try
                {
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to Post at least some events to Data Platform", ex);
                }

                // Wait 1 second before next batch (except for the last batch)
                if (i + batchSize < eventsData.Count)
                {
                    await Task.Delay(1000);
                }
            }
        }

        public async Task PostEventAsync(PostTelemetry eventData)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("REST API service not configured. Call Configure first.");
            }

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/DataPlatform/PostEvent");

            // Required headers
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("Cmf_ClientTenantName", _clientConfiguration.ClientTenantName);
            request.Headers.Add("Cmf_HostName", _clientConfiguration.HostAddress);

            var input = new PostEventInput
            {
                AppProperties = new AppProperties
                {
                    ApplicationName = "CMF-Review",
                    EventDefinition = "PostTelemetry",
                    EventTime = DateTime.UtcNow
                },
                IgnoreLastServiceId = false,
                NumberOfRetries = 0,
                Data = eventData,
                Priority = 0,
                AcceptInvalidSchema = false
            };

            var json = JsonSerializer.Serialize(input);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public record Entity(string Name);
    public record Tag(string Key, string Value);

    public record Data(
        string Class,
        string Name,
        List<decimal> NumericValues,
        List<string> StringValues,
        List<long> Timestamps,
        string UnitOfMeasure
    );

    public record ClientConfiguration(string HostAddress, string ClientTenantName, string ClientId, string SecurityAccessToken, bool useSSL);

    public class BaseInput
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public bool IgnoreLastServiceId { get; set; }
        public int NumberOfRetries { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string ServiceComments { get; set; }
        public Dictionary<string, object> ExtraParameters { get; set; } = new();
    }

    public class PostEventInput : BaseInput
    {
        public bool AcceptInvalidSchema { get; set; }
        public AppProperties AppProperties { get; set; }
        public PostTelemetry Data { get; set; }
        public int Priority { get; set; }
    }

    public class AppProperties
    {
        public string ApplicationName { get; set; }
        public string EventDefinition { get; set; }
        public DateTime EventTime { get; set; }
    }

    public class PostTelemetry
    {
        public List<Data> Parameters { get; set; }
        public List<Tag> Tags { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Entity Resource { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Entity Area { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Entity Facility { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Entity Site { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Entity Enterprise { get; set; }

        public PostTelemetry(string projectName, string packageId, string packageVersion, string type, string name, Dictionary<string, string> metadata, List<Tag> tags = null)
        {
            // We will hijack the ISA95 for our context
            this.Enterprise = string.IsNullOrEmpty(projectName) ? null : new Entity(projectName);
            this.Site = string.IsNullOrEmpty(packageId) ? null : new Entity(packageId);
            this.Facility = string.IsNullOrEmpty(packageVersion) ? null : new Entity(packageVersion);
            this.Area = string.IsNullOrEmpty(type) ? null : new Entity(type);
            this.Resource = string.IsNullOrEmpty(name) ? null : new Entity(name);
            this.Tags = tags;

            this.Parameters = new List<Data>();
            foreach (var item in metadata)
            {
                if (item.Value != null)
                {
                    if (decimal.TryParse(item.Value, out decimal numericValue))
                    {
                        this.Parameters.Add(new Data("Property", item.Key, [numericValue], [], [DateTimeOffset.Now.ToUnixTimeSeconds()], ""));
                    }
                    else
                    {
                        this.Parameters.Add(new Data("Property", item.Key, [], [item.Value], [DateTimeOffset.Now.ToUnixTimeSeconds()], ""));
                    }
                }
            }
        }
    }
}
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Base class with common HTTP and JSON parsing logic shared by all provider adapters.
    /// </summary>
    public abstract class BaseScrapingProvider : IScrapingProvider
    {
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly ILogger _logger;

        public abstract string ProviderName { get; }

        protected BaseScrapingProvider(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public abstract Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default);

        /// <summary>
        /// Executes an HTTP request and measures response time.
        /// </summary>
        protected async Task<(HttpResponseMessage response, long elapsedMs, string body)> ExecuteRequestAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("ScrapingClient");
            var sw = Stopwatch.StartNew();

            try
            {
                var response = await client.SendAsync(request, ct);
                sw.Stop();

                var body = await response.Content.ReadAsStringAsync(ct);
                return (response, sw.ElapsedMilliseconds, body);
            }
            catch (TaskCanceledException)
            {
                sw.Stop();
                throw new TimeoutException($"Request to {ProviderName} timed out after {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                throw new HttpRequestException($"Request to {ProviderName} failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Safely extracts a string value from a JToken by path.
        /// </summary>
        protected static string? SafeGetString(JToken? token, params string[] paths)
        {
            if (token == null) return null;
            foreach (var path in paths)
            {
                var value = token.SelectToken(path)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return null;
        }

        /// <summary>
        /// Safely extracts a decimal value from a JToken by path.
        /// </summary>
        protected static decimal? SafeGetDecimal(JToken? token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = token?.SelectToken(path);
                if (t != null)
                {
                    if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                        return t.Value<decimal>();
                    if (decimal.TryParse(t.ToString().Replace("$", "").Replace(",", "").Trim(), out var val))
                        return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Safely extracts an int value from a JToken by path.
        /// </summary>
        protected static int? SafeGetInt(JToken? token, params string[] paths)
        {
            foreach (var path in paths)
            {
                var t = token?.SelectToken(path);
                if (t != null)
                {
                    if (t.Type == JTokenType.Integer)
                        return t.Value<int>();
                    if (int.TryParse(t.ToString().Replace(",", "").Trim(), out var val))
                        return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a Basic Auth header value from username:password.
        /// </summary>
        protected static string BasicAuth(string username, string password = "")
        {
            var bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            return Convert.ToBase64String(bytes);
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Oxylabs Web Scraper API adapter.
    /// POST https://realtime.oxylabs.io/v1/queries with Basic Auth.
    /// Docs: https://developers.oxylabs.io/scraper-apis/web-scraper-api/
    /// </summary>
    public class OxylabsProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Oxylabs";

        public OxylabsProvider(IHttpClientFactory httpClientFactory, ILogger<OxylabsProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                // Oxylabs uses username:password format in ApiKey (e.g. "user:pass")
                var credentials = apiKey.Contains(':') ? apiKey.Split(':', 2) : new[] { apiKey, "" };

                var payload = new
                {
                    source = "universal",
                    url = productUrl,
                    parse = true,
                    render = "html"
                };

                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    BasicAuth(credentials[0], credentials.Length > 1 ? credentials[1] : ""));

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                var json = JToken.Parse(body);
                var results = json["results"];
                var content = results?.First?["content"] ?? json;

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(content, "title", "name", "product_name"),
                    Price = SafeGetDecimal(content, "price", "price_value", "final_price"),
                    Currency = SafeGetString(content, "currency", "price_currency"),
                    Brand = SafeGetString(content, "brand", "manufacturer"),
                    Upc = SafeGetString(content, "upc", "gtin", "ean"),
                    Availability = SafeGetString(content, "availability", "stock"),
                    ImageUrl = SafeGetString(content, "image", "main_image", "images[0]"),
                    Description = SafeGetString(content, "description", "short_description"),
                    Rating = SafeGetDecimal(content, "rating", "stars"),
                    ReviewCount = SafeGetInt(content, "reviews_count", "review_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oxylabs scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

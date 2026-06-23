using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// WebScrapingAPI adapter.
    /// GET https://api.webscrapingapi.com/v1?api_key=...&url=...
    /// Docs: https://docs.webscrapingapi.com/
    /// </summary>
    public class WebScrapingApiProvider : BaseScrapingProvider
    {
        public override string ProviderName => "WebScrapingAPI";

        public WebScrapingApiProvider(IHttpClientFactory httpClientFactory, ILogger<WebScrapingApiProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var url = $"{baseUrl}?api_key={apiKey}&url={HttpUtility.UrlEncode(productUrl)}&render_js=1&extract_rules=%7B%22title%22:%22h1%22,%22price%22:%22.a-price%20.a-offscreen%22%7D";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                JToken? json = null;
                try { json = JToken.Parse(body); } catch { }

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(json, "title", "name"),
                    Price = SafeGetDecimal(json, "price"),
                    Currency = SafeGetString(json, "currency"),
                    Brand = SafeGetString(json, "brand"),
                    Upc = SafeGetString(json, "upc"),
                    Availability = SafeGetString(json, "availability"),
                    ImageUrl = SafeGetString(json, "image"),
                    Description = SafeGetString(json, "description"),
                    Rating = SafeGetDecimal(json, "rating"),
                    ReviewCount = SafeGetInt(json, "reviews_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebScrapingAPI scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

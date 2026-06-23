using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Bright Data Web Scraper API adapter.
    /// Uses POST /scrape endpoint with Bearer token auth.
    /// Docs: https://docs.brightdata.com/scraping
    /// </summary>
    public class BrightDataProvider : BaseScrapingProvider
    {
        public override string ProviderName => "BrightData";

        public BrightDataProvider(IHttpClientFactory httpClientFactory, ILogger<BrightDataProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    url = productUrl,
                    format = "json"
                };

                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                var json = JToken.Parse(body);

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(json, "title", "name", "product_name"),
                    Price = SafeGetDecimal(json, "price", "final_price", "current_price"),
                    Currency = SafeGetString(json, "currency", "currency_code"),
                    Brand = SafeGetString(json, "brand", "manufacturer"),
                    Upc = SafeGetString(json, "upc", "gtin", "barcode"),
                    Availability = SafeGetString(json, "availability", "stock_status", "in_stock"),
                    ImageUrl = SafeGetString(json, "image", "main_image", "images[0]"),
                    Description = SafeGetString(json, "description", "short_description"),
                    Rating = SafeGetDecimal(json, "rating", "stars", "average_rating"),
                    ReviewCount = SafeGetInt(json, "reviews_count", "review_count", "ratings_total")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BrightData scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

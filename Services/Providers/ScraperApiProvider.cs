using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// ScraperAPI adapter.
    /// GET https://api.scraperapi.com?api_key=...&url=...
    /// Docs: https://docs.scraperapi.com/
    /// </summary>
    public class ScraperApiProvider : BaseScrapingProvider
    {
        public override string ProviderName => "ScraperAPI";

        public ScraperApiProvider(IHttpClientFactory httpClientFactory, ILogger<ScraperApiProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                // Use structured data endpoint if available for the platform
                var url = $"{baseUrl}/structured/amazon/product?api_key={apiKey}&url={HttpUtility.UrlEncode(productUrl)}";

                // Fall back to universal endpoint for non-Amazon URLs
                if (!productUrl.Contains("amazon.com", StringComparison.OrdinalIgnoreCase))
                {
                    url = $"{baseUrl}?api_key={apiKey}&url={HttpUtility.UrlEncode(productUrl)}&render=true&autoparse=true";
                }

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
                    Title = SafeGetString(json, "name", "title", "product_name"),
                    Price = SafeGetDecimal(json, "pricing", "price", "final_price"),
                    Currency = SafeGetString(json, "currency"),
                    Brand = SafeGetString(json, "brand", "manufacturer"),
                    Upc = SafeGetString(json, "upc", "gtin"),
                    Availability = SafeGetString(json, "availability", "in_stock"),
                    ImageUrl = SafeGetString(json, "image", "images[0]", "main_image"),
                    Description = SafeGetString(json, "description", "product_description"),
                    Rating = SafeGetDecimal(json, "rating", "stars", "average_rating"),
                    ReviewCount = SafeGetInt(json, "reviews_count", "total_reviews", "ratings_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScraperAPI scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

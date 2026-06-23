using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// ScrapingDog API adapter.
    /// GET https://api.scrapingdog.com/scrape?api_key=...&url=...
    /// Docs: https://docs.scrapingdog.com/
    /// </summary>
    public class ScrapingDogProvider : BaseScrapingProvider
    {
        public override string ProviderName => "ScrapingDog";

        public ScrapingDogProvider(IHttpClientFactory httpClientFactory, ILogger<ScrapingDogProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var url = $"{baseUrl}?api_key={apiKey}&url={HttpUtility.UrlEncode(productUrl)}&dynamic=true";

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
                    Title = SafeGetString(json, "title", "name", "product_title"),
                    Price = SafeGetDecimal(json, "price", "sale_price"),
                    Currency = SafeGetString(json, "currency"),
                    Brand = SafeGetString(json, "brand"),
                    Upc = SafeGetString(json, "upc"),
                    Availability = SafeGetString(json, "availability"),
                    ImageUrl = SafeGetString(json, "image", "main_image"),
                    Description = SafeGetString(json, "description"),
                    Rating = SafeGetDecimal(json, "rating"),
                    ReviewCount = SafeGetInt(json, "reviews_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScrapingDog scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

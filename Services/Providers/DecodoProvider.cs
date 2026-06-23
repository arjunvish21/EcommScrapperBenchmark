using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Decodo (formerly Smartproxy) Web Scraping API adapter.
    /// POST https://scraper-api.decodo.com/v2/scrape with Basic Auth.
    /// Docs: https://help.decodo.com/docs/introduction
    /// </summary>
    public class DecodoProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Decodo";

        public DecodoProvider(IHttpClientFactory httpClientFactory, ILogger<DecodoProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    url = productUrl,
                    target = "universal",
                    locale = "en",
                    headless = "html",
                    parse = true
                };

                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth(apiKey));

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                var json = JToken.Parse(body);
                var content = json["results"]?.First?["content"] ?? json["content"] ?? json;

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(content, "title", "name", "product_title"),
                    Price = SafeGetDecimal(content, "price", "sale_price", "current_price"),
                    Currency = SafeGetString(content, "currency"),
                    Brand = SafeGetString(content, "brand", "manufacturer"),
                    Upc = SafeGetString(content, "upc", "gtin"),
                    Availability = SafeGetString(content, "availability", "stock_status"),
                    ImageUrl = SafeGetString(content, "image", "main_image"),
                    Description = SafeGetString(content, "description"),
                    Rating = SafeGetDecimal(content, "rating", "stars"),
                    ReviewCount = SafeGetInt(content, "reviews_count", "review_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decodo scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

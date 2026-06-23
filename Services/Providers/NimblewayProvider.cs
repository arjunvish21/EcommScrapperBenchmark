using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Nimbleway Web API adapter.
    /// POST https://api.webit.live/api/v1/realtime/web with Bearer auth.
    /// Docs: https://docs.nimbleway.com/
    /// </summary>
    public class NimblewayProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Nimbleway";

        public NimblewayProvider(IHttpClientFactory httpClientFactory, ILogger<NimblewayProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    url = productUrl,
                    render = true,
                    parse = new { enabled = true },
                    country = "US"
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
                var result = json["result"] ?? json["parsing"]?["entities"]?.First ?? json;

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(result, "title", "name", "product_name"),
                    Price = SafeGetDecimal(result, "price", "sale_price"),
                    Currency = SafeGetString(result, "currency"),
                    Brand = SafeGetString(result, "brand"),
                    Upc = SafeGetString(result, "upc", "gtin"),
                    Availability = SafeGetString(result, "availability", "stock"),
                    ImageUrl = SafeGetString(result, "image", "main_image", "image_url"),
                    Description = SafeGetString(result, "description"),
                    Rating = SafeGetDecimal(result, "rating", "stars"),
                    ReviewCount = SafeGetInt(result, "reviews_count", "num_reviews")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nimbleway scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

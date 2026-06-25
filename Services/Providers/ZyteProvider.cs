using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Zyte API adapter.
    /// POST https://api.zyte.com/v1/extract with Basic Auth.
    /// Docs: https://docs.zyte.com/zyte-api/
    /// </summary>
    public class ZyteProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Zyte";

        public ZyteProvider(IHttpClientFactory httpClientFactory, ILogger<ZyteProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    url = productUrl,
                    product = true,
                    productOptions = new { extractFrom = "httpResponseBody" }
                };

                var credentials = apiKey.Contains(':') ? apiKey.Split(':', 2) : new[] { apiKey, "" };
                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicAuth(credentials[0], credentials[1]));

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                var json = JToken.Parse(body);
                var product = json["product"] ?? json;

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(product, "name", "title"),
                    Price = SafeGetDecimal(product, "price"),
                    Currency = SafeGetString(product, "currency", "currencyRaw"),
                    Brand = SafeGetString(product, "brand", "brand.name"),
                    Upc = SafeGetString(product, "gtin[0].value", "gtin", "sku"),
                    Availability = SafeGetString(product, "availability"),
                    ImageUrl = SafeGetString(product, "mainImage", "images[0].url"),
                    Description = SafeGetString(product, "description", "descriptionHtml"),
                    Rating = SafeGetDecimal(product, "aggregateRating.ratingValue"),
                    ReviewCount = SafeGetInt(product, "aggregateRating.reviewCount", "aggregateRating.bestRating")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Zyte scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

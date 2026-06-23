using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Infatica Web Scraper API adapter.
    /// POST with Bearer auth.
    /// Docs: https://docs.infatica.io/
    /// </summary>
    public class InfaticaProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Infatica";

        public InfaticaProvider(IHttpClientFactory httpClientFactory, ILogger<InfaticaProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    url = productUrl,
                    render_js = true,
                    output_format = "json"
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

                JToken? json = null;
                try { json = JToken.Parse(body); } catch { }
                var data = json?["data"] ?? json?["result"] ?? json;

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title = SafeGetString(data, "title", "name"),
                    Price = SafeGetDecimal(data, "price", "sale_price"),
                    Currency = SafeGetString(data, "currency"),
                    Brand = SafeGetString(data, "brand"),
                    Upc = SafeGetString(data, "upc", "gtin"),
                    Availability = SafeGetString(data, "availability"),
                    ImageUrl = SafeGetString(data, "image", "main_image"),
                    Description = SafeGetString(data, "description"),
                    Rating = SafeGetDecimal(data, "rating"),
                    ReviewCount = SafeGetInt(data, "reviews_count")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Infatica scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }
    }
}

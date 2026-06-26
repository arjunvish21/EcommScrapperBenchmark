using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
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

        // Matches ASINs in Amazon URLs
        private static readonly Regex AsinRegex = new(@"(?:/dp/|/gp/product/|/ASIN/)([A-Z0-9]{10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public NimblewayProvider(IHttpClientFactory httpClientFactory, ILogger<NimblewayProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                object payload;
                string requestUrl = baseUrl;

                if (productUrl.Contains("amazon.", StringComparison.OrdinalIgnoreCase))
                {
                    var match = AsinRegex.Match(productUrl);
                    if (match.Success)
                    {
                        var asin = match.Groups[1].Value.ToUpperInvariant();
                        payload = new { agent = "amazon_pdp", localization = true, @params = new { asin = asin, zip_code = "90210" } };
                        requestUrl = "https://sdk.nimbleway.com/v1/agents/run";
                    }
                    else
                    {
                        payload = new { url = productUrl, render = true, parse = new { enabled = true }, country = "US" };
                    }
                }
                else if (productUrl.Contains("walmart.com", StringComparison.OrdinalIgnoreCase))
                {
                    payload = new { vendor = "walmart", url = productUrl, country = "US", zip = "90210", locale = "en" };
                    requestUrl = "https://api.webit.live/api/v1/realtime/ecommerce";
                }
                else
                {
                    payload = new { url = productUrl, render = true, parse = new { enabled = true }, country = "US" };
                }

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
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
                // Walmart returns Product inside parsing.entities.Product array.
                // Amazon agents/run returns data inside data.parsing.
                var result = json["parsing"]?["entities"]?["Product"]?["product"]?.First 
                             ?? json["data"]?["parsing"] 
                             ?? json["result"] 
                             ?? json["parsing"]?["entities"]?.First 
                             ?? json;

                // Price: flat OR nested
                var price = SafeGetDecimal(result, "price", "sale_price", "current_price", "final_price", "web_price")
                            ?? SafeGetDecimal(result["currentPrice"], "price")
                            ?? SafeGetDecimal(result["price"], "amount", "value");

                // Rating: flat OR nested
                var rating = SafeGetDecimal(result, "rating", "stars", "average_rating")
                             ?? SafeGetDecimal(result["rating"], "rating", "value");
                var reviewCount = SafeGetInt(result, "reviews_count", "review_count", "num_reviews", "ratings_total")
                                  ?? SafeGetInt(result["rating"], "count");

                return new ScrapingResponse
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseTimeMs = elapsedMs,
                    RawJson = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title       = SafeGetString(result, "title", "name", "product_name", "product_title", "productName"),
                    Price       = price,
                    Currency    = SafeGetString(result, "currency") ?? SafeGetString(result["currentPrice"], "currencyUnit"),
                    Brand       = SafeGetString(result, "brand", "manufacturer", "brand_name", "brandName"),
                    Upc         = SafeGetString(result, "upc", "gtin", "sku", "UPC"),
                    Availability = SafeGetString(result, "availability", "stock", "stock_status"),
                    ImageUrl    = SafeGetString(result, "image", "main_image", "image_url", "images[0]"),
                    Description = SafeGetString(result, "description", "short_description"),
                    Rating      = rating,
                    ReviewCount = reviewCount
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

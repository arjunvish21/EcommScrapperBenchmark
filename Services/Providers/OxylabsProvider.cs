using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Oxylabs Web Scraper API adapter.
    /// POST https://realtime.oxylabs.io/v1/queries with Basic Auth.
    /// Docs: https://developers.oxylabs.io/scraper-apis/web-scraper-api/
    ///
    /// Source / input-field rules (confirmed):
    ///   Amazon  → source="amazon_product",      query=ASIN  (10-char alphanumeric)
    ///   All other e-commerce URLs (Walmart, eBay, HomeDepot, Target, etc.)
    ///           → source="universal_ecommerce",  url=full product URL
    /// </summary>
    public class OxylabsProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Oxylabs";

        // Matches ASINs in Amazon URLs: /dp/XXXXXXXXXX, /gp/product/XXXXXXXXXX, /ASIN/XXXXXXXXXX
        private static readonly Regex AsinRegex =
            new(@"(?:/dp/|/gp/product/|/ASIN/)([A-Z0-9]{10})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public OxylabsProvider(IHttpClientFactory httpClientFactory, ILogger<OxylabsProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                // Oxylabs uses username:password format in ApiKey (e.g. "user:pass")
                var credentials = apiKey.Contains(':') ? apiKey.Split(':', 2) : new[] { apiKey, "" };

                var payload = BuildPayload(productUrl);

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
                var content = json["results"]?.First?["content"] ?? json;

                // Price: flat decimal (Amazon) OR nested object { value, currency, price } (Walmart universal)
                var price = SafeGetDecimal(content, "price", "price_upper", "price_lower", "price_value", "final_price", "current_price")
                            ?? SafeGetDecimal(content["price"], "price", "value", "amount")
                            ?? SafeGetDecimal(content["pricing"], "price", "current_price");

                // Rating: flat (Amazon) OR nested object { rating, count } (Walmart universal)
                var rating = SafeGetDecimal(content, "rating", "stars", "rating_stars")
                             ?? SafeGetDecimal(content["rating"], "rating", "value", "average")
                             ?? SafeGetDecimal(content["general"], "rating");

                var reviewCount = SafeGetInt(content, "reviews_count", "review_count", "ratings_total")
                                  ?? SafeGetInt(content["rating"], "count", "reviews_count")
                                  ?? SafeGetInt(content["general"], "reviews_count");

                return new ScrapingResponse
                {
                    IsSuccess         = true,
                    HttpStatusCode    = (int)response.StatusCode,
                    ResponseTimeMs    = elapsedMs,
                    RawJson           = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title             = SafeGetString(content, "title", "name", "product_name", "product_title")
                                       ?? SafeGetString(content["general"], "title", "name"),
                    Price             = price,
                    Currency          = SafeGetString(content, "currency", "price_currency")
                                       ?? SafeGetString(content["price"], "currency"),
                    Brand             = SafeGetString(content, "brand", "manufacturer", "brand_name", "vendor")
                                       ?? SafeGetString(content["general"], "brand"),
                    Upc               = SafeGetString(content, "upc", "gtin", "ean", "asin")
                                       ?? SafeGetString(content["meta"], "gtin", "sku")
                                       ?? SafeGetString(content["general"], "upc", "gtin"),
                    Availability      = SafeGetString(content, "availability", "stock", "in_stock")
                                       ?? SafeGetString(content["general"], "availability"),
                    ImageUrl          = SafeGetString(content, "image", "main_image", "images[0]", "image_url")
                                       ?? SafeGetString(content["general"], "image"),
                    Description       = SafeGetString(content, "description", "short_description", "feature_bullets_flat")
                                       ?? SafeGetString(content["general"], "description"),
                    Rating            = rating,
                    ReviewCount       = reviewCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oxylabs scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }

        // -----------------------------------------------------------------------
        // Payload builder
        // -----------------------------------------------------------------------
        private object BuildPayload(string productUrl)
        {
            // Amazon: dedicated source with ASIN in "query"
            if (productUrl.Contains("amazon.", StringComparison.OrdinalIgnoreCase))
            {
                var match = AsinRegex.Match(productUrl);
                if (match.Success)
                {
                    var asin = match.Groups[1].Value.ToUpperInvariant();                    
                    return new { source = "amazon_product", query = asin, parse = true };
                }
            }

            // All other platforms (Walmart, eBay, HomeDepot, Target, etc.) → universal_ecommerce + url            
            return new { source = "universal_ecommerce", url = productUrl, parse = true };
        }
    }
}

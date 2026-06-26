using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Decodo (formerly Smartproxy) Web Scraping API adapter.
    /// POST https://scraper-api.decodo.com/v2/scrape with Basic Auth.
    /// Docs: https://help.decodo.com/docs/introduction
    ///
    /// Each known platform is declared once in <see cref="PlatformRules"/>. The rule captures:
    ///   - Which Decodo target name to use
    ///   - Which JSON field the API expects  (InputField enum: Query | Url | ProductId)
    ///   - A regex to pull the right value out of the product URL
    ///
    /// Decodo input-field requirements per target (from official docs):
    ///   amazon_product      → "query"      (ASIN)
    ///   walmart_product     → "url"        (full product URL)
    ///   target_product      → "product_id" (numeric product ID)
    ///   tiktok_shop_product → "url"        (full product URL)
    ///
    /// Unrecognised URLs fall back to target="universal" with the full URL + headless rendering.
    /// To add a new platform, append one PlatformRule entry — no other code changes needed.
    /// </summary>
    public class DecodoProvider : BaseScrapingProvider
    {
        public override string ProviderName => "Decodo";

        // -----------------------------------------------------------------------
        // Describes which JSON field Decodo expects for a given target
        // -----------------------------------------------------------------------
        private enum InputField { Query, Url, ProductId }

        // -----------------------------------------------------------------------
        // Platform rule table
        // Each entry: (domain hint, Decodo target, which input field, regex for that value)
        // When IdPattern is null the full productUrl is used as-is (InputField.Url only).
        // -----------------------------------------------------------------------
        private sealed record PlatformRule(
            string     DomainHint,
            string     Target,
            InputField Field,
            Regex?     IdPattern = null);

        private static readonly PlatformRule[] PlatformRules =
        [
            // Amazon – ASIN in "query" field
            new("amazon.",
                "amazon_product",
                InputField.Query,
                new Regex(@"(?:/dp/|/gp/product/|/ASIN/)([A-Z0-9]{10})",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled)),

            // Walmart – full URL in "url" field  (no ID extraction needed)
            new("walmart.",
                "walmart",
                InputField.Url),

            // Target – numeric product_id in "product_id" field  (after /p/-/A-)
            new("target.",
                "target_product",
                InputField.ProductId,
                new Regex(@"/A-([0-9]{8})(?:[/?#]|$)", RegexOptions.Compiled)),

            // TikTok Shop – full URL in "url" field
            new("tiktok.",
                "tiktok_shop_product",
                InputField.Url),

            // Home Depot – full URL in "url" field
            new("homedepot.",
                "homedepot",
                InputField.Url),
        ];

        // -----------------------------------------------------------------------

        public DecodoProvider(IHttpClientFactory httpClientFactory, ILogger<DecodoProvider> logger)
            : base(httpClientFactory, logger) { }

        public override async Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl, string apiKey, string baseUrl, CancellationToken ct = default)
        {
            try
            {
                var payload = BuildPayload(productUrl);

                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                };

                // Support both "user:pass" Basic auth and bare Token auth
                if (apiKey.Contains(':'))
                {
                    var parts = apiKey.Split(':', 2);
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Basic", BasicAuth(parts[0], parts[1]));
                }
                else
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey);
                }

                var (response, elapsedMs, body) = await ExecuteRequestAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return ScrapingResponse.Failure(
                        $"HTTP {(int)response.StatusCode}: {body}",
                        (int)response.StatusCode, elapsedMs);
                }

                var json = JToken.Parse(body);
                var content = json["results"]?.First?["content"] ?? json["content"] ?? json;
                
                // Decodo often wraps the actual parsed data inside "results" within the content object
                var data = content["results"] ?? content;

                // Price: flat (Amazon/Walmart) OR nested current_price object
                var price = SafeGetDecimal(data, "price", "sale_price", "current_price", "final_price", "")
                            ?? SafeGetDecimal(data["price"], "price", "amount", "value");

                // Rating: flat OR nested { rating, count } object
                var rating = SafeGetDecimal(data, "rating", "stars", "average_rating")
                             ?? SafeGetDecimal(data["rating"], "rating", "value")
                             ?? SafeGetDecimal(data["general"], "rating");
                var reviewCount = SafeGetInt(data, "reviews_count", "review_count", "ratings_total")
                                  ?? SafeGetInt(data["rating"], "count", "reviews_count")
                                  ?? SafeGetInt(data["general"], "reviews_count");

                return new ScrapingResponse
                {
                    IsSuccess         = true,
                    HttpStatusCode    = (int)response.StatusCode,
                    ResponseTimeMs    = elapsedMs,
                    RawJson           = body,
                    ResponseSizeBytes = Encoding.UTF8.GetByteCount(body),
                    Title       = SafeGetString(data, "title", "name", "product_title", "product_name")
                                 ?? SafeGetString(data["general"], "title", "name"),
                    Price       = price,
                    Currency    = SafeGetString(data, "currency")
                                 ?? SafeGetString(data["price"], "currency"),
                    Brand       = SafeGetString(data, "brand", "manufacturer", "brand_name")
                                 ?? SafeGetString(data["general"], "brand"),
                    Upc         = SafeGetString(data, "upc", "gtin", "product_id", "sku")
                                 ?? SafeGetString(data["general"], "upc", "gtin"),
                    Availability = SafeGetString(data, "availability", "stock_status", "stock")
                                 ?? SafeGetString(data["general"], "availability"),
                    ImageUrl    = SafeGetString(data, "image", "main_image", "images[0]", "thumbnail")
                                 ?? SafeGetString(data["general"], "image"),
                    Description = SafeGetString(data, "description", "short_description")
                                 ?? SafeGetString(data["general"], "description"),
                    Rating      = rating,
                    ReviewCount = reviewCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decodo scraping failed for {Url}", productUrl);
                return ScrapingResponse.Failure(ex.Message);
            }
        }

        // -----------------------------------------------------------------------
        // Payload builder — walks the rule table, falls back to universal target
        // -----------------------------------------------------------------------
        private object BuildPayload(string productUrl)
        {
            foreach (var rule in PlatformRules)
            {
                if (!productUrl.Contains(rule.DomainHint, StringComparison.OrdinalIgnoreCase))
                    continue;

                // If an ID pattern is defined, extract the value; otherwise use the full URL
                string value;
                if (rule.IdPattern != null)
                {
                    var match = rule.IdPattern.Match(productUrl);
                    if (!match.Success) continue;
                    value = match.Groups[1].Value.ToUpperInvariant();
                }
                else
                {
                    value = productUrl;
                }

                _logger.LogInformation(
                    "Decodo: {Platform} URL → target={Target}, {Field}={Value}",
                    rule.DomainHint.TrimEnd('.'), rule.Target, rule.Field, value);

                return rule.Field switch
                {
                    InputField.Query     => (object)new { query      = value, target = rule.Target, parse = true, output_format = "json" },
                    InputField.ProductId =>         new { product_id = value, target = rule.Target, parse = true, output_format = "json" },
                    _  /* Url */         =>         new { url        = value, target = rule.Target, parse = true },
                };
            }

            // No platform rule matched — pass the full URL with the universal target
            _logger.LogInformation("Decodo: No dedicated target for {Url} — falling back to universal", productUrl);
            return new
            {
                url    = productUrl,
                target = "universal",
                parse  = true
            };
        }
    }
}

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Represents the normalized response from any scraping provider.
    /// Each provider adapter parses its API-specific response into this common shape.
    /// </summary>
    public class ScrapingResponse
    {
        public bool IsSuccess { get; set; }
        public int HttpStatusCode { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? RawJson { get; set; }
        public long ResponseSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }

        // Normalized extracted fields
        public string? Title { get; set; }
        public decimal? Price { get; set; }
        public string? Currency { get; set; }
        public string? Brand { get; set; }
        public string? Upc { get; set; }
        public string? Availability { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public decimal? Rating { get; set; }
        public int? ReviewCount { get; set; }

        public static ScrapingResponse Failure(string error, int statusCode = 0, long responseTimeMs = 0)
        {
            return new ScrapingResponse
            {
                IsSuccess = false,
                ErrorMessage = error,
                HttpStatusCode = statusCode,
                ResponseTimeMs = responseTimeMs
            };
        }
    }

    /// <summary>
    /// Common interface for all web scraping API provider adapters.
    /// </summary>
    public interface IScrapingProvider
    {
        /// <summary>
        /// The unique name of this provider (must match ProviderConfig.ProviderName).
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Scrape a product page and return a normalized response.
        /// </summary>
        Task<ScrapingResponse> ScrapeProductAsync(
            string productUrl,
            string apiKey,
            string baseUrl,
            CancellationToken ct = default);
    }
}

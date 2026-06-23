namespace EcommScrapperBenchmark.Models
{
    public class ProviderConfig
    {
        public int Id { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
        public string AuthType { get; set; } = "Bearer";
        public bool IsActive { get; set; }
        public int RateLimitPerMinute { get; set; } = 10;
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

        /// <summary>
        /// A provider is considered configured if it has a non-empty API key.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}

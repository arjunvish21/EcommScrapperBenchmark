namespace EcommScrapperBenchmark.Models
{
    public class BenchmarkResult
    {
        public int Id { get; set; }
        public int RunId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int TestProductId { get; set; }
        public string? ProductUrl { get; set; }

        // Performance metrics
        public int? HttpStatusCode { get; set; }
        public long? ResponseTimeMs { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        // Extracted data (normalized fields)
        public string? ExtractedTitle { get; set; }
        public decimal? ExtractedPrice { get; set; }
        public string? ExtractedCurrency { get; set; }
        public string? ExtractedBrand { get; set; }
        public string? ExtractedUpc { get; set; }
        public string? ExtractedAvailability { get; set; }
        public string? ExtractedImageUrl { get; set; }
        public string? ExtractedDescription { get; set; }
        public decimal? ExtractedRating { get; set; }
        public int? ExtractedReviewCount { get; set; }

        // Raw response
        public string? RawResponseJson { get; set; }
        public long? RawResponseSizeBytes { get; set; }

        // Quality scores
        public decimal? CompletenessScore { get; set; }
        public decimal? AccuracyScore { get; set; }
        public decimal? StructureScore { get; set; }
        public decimal? OverallQualityScore { get; set; }

        public DateTime CreatedOn { get; set; }

        // Navigation properties (populated manually via joins)
        public TestProduct? TestProduct { get; set; }
    }
}

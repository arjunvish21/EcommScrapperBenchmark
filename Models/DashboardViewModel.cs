namespace EcommScrapperBenchmark.Models
{
    public class DashboardViewModel
    {
        public List<ProviderSummary> ProviderSummaries { get; set; } = new();
        public List<PlatformBreakdown> PlatformBreakdowns { get; set; } = new();
        public List<BenchmarkRun> RecentRuns { get; set; } = new();
        public OverallStats Overall { get; set; } = new();
    }

    public class ProviderSummary
    {
        public string ProviderName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public decimal SuccessRate { get; set; }
        public double AvgResponseTimeMs { get; set; }
        public decimal AvgQualityScore { get; set; }
        public decimal AvgCompletenessScore { get; set; }
        public decimal AvgAccuracyScore { get; set; }
        public decimal AvgStructureScore { get; set; }
    }

    public class PlatformBreakdown
    {
        public string ProviderName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public decimal SuccessRate { get; set; }
        public double AvgResponseTimeMs { get; set; }
        public decimal AvgQualityScore { get; set; }
    }

    public class OverallStats
    {
        public int TotalRuns { get; set; }
        public int TotalProvidersTested { get; set; }
        public int TotalRequests { get; set; }
        public decimal OverallSuccessRate { get; set; }
        public double OverallAvgResponseTime { get; set; }
        public decimal OverallAvgQuality { get; set; }
    }
}

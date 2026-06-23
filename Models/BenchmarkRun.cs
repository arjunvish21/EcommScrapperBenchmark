namespace EcommScrapperBenchmark.Models
{
    public class BenchmarkRun
    {
        public int Id { get; set; }
        public Guid RunGuid { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Running";
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public string? Notes { get; set; }

        // Computed properties for display
        public string Duration
        {
            get
            {
                if (CompletedAt == null) return "In progress...";
                var span = CompletedAt.Value - StartedAt;
                if (span.TotalMinutes < 1) return $"{span.Seconds}s";
                return $"{(int)span.TotalMinutes}m {span.Seconds}s";
            }
        }

        public decimal SuccessRate => TotalRequests > 0
            ? Math.Round((decimal)SuccessfulRequests / TotalRequests * 100, 1)
            : 0;
    }
}

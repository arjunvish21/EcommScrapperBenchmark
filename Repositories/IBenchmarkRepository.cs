using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public interface IBenchmarkRepository
    {
        // Runs
        Task<int> CreateRunAsync(BenchmarkRun run);
        Task UpdateRunAsync(BenchmarkRun run);
        Task<BenchmarkRun?> GetRunByIdAsync(int id);
        Task<BenchmarkRun?> GetRunByGuidAsync(Guid runGuid);
        Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync(int limit = 50);
        Task<BenchmarkRun?> GetLatestRunAsync();

        // Results
        Task InsertResultAsync(BenchmarkResult result);
        Task<IEnumerable<BenchmarkResult>> GetResultsByRunIdAsync(int runId);
        Task<IEnumerable<BenchmarkResult>> GetResultsByRunIdAndProviderAsync(int runId, string providerName);

        // Dashboard aggregations
        Task<IEnumerable<ProviderSummary>> GetProviderSummariesAsync(int? runId = null);
        Task<IEnumerable<PlatformBreakdown>> GetPlatformBreakdownsAsync(int? runId = null);
        Task<OverallStats> GetOverallStatsAsync();
    }
}

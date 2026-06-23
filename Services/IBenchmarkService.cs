using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Services
{
    public interface IBenchmarkService
    {
        /// <summary>
        /// Runs a full benchmark across all active, configured providers and test products.
        /// Returns the BenchmarkRun record after completion.
        /// </summary>
        Task<BenchmarkRun> RunBenchmarkAsync(string? notes = null, CancellationToken ct = default);

        /// <summary>
        /// Gets the current status of a running benchmark.
        /// </summary>
        bool IsRunning { get; }
    }
}

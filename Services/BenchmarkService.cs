using EcommScrapperBenchmark.Models;
using EcommScrapperBenchmark.Repositories;
using EcommScrapperBenchmark.Services.Providers;

namespace EcommScrapperBenchmark.Services
{
    /// <summary>
    /// Orchestrates benchmark runs across all active providers and test products.
    /// Uses SemaphoreSlim for concurrency control and respects rate limits.
    /// </summary>
    public class BenchmarkService : IBenchmarkService
    {
        private readonly IProviderConfigRepository _providerRepo;
        private readonly ITestProductRepository _productRepo;
        private readonly IBenchmarkRepository _benchmarkRepo;
        private readonly ProviderFactory _providerFactory;
        private readonly QualityScorer _qualityScorer;
        private readonly ILogger<BenchmarkService> _logger;
        private readonly IConfiguration _configuration;

        private volatile bool _isRunning;
        public bool IsRunning => _isRunning;

        public BenchmarkService(
            IProviderConfigRepository providerRepo,
            ITestProductRepository productRepo,
            IBenchmarkRepository benchmarkRepo,
            ProviderFactory providerFactory,
            QualityScorer qualityScorer,
            ILogger<BenchmarkService> logger,
            IConfiguration configuration)
        {
            _providerRepo = providerRepo;
            _productRepo = productRepo;
            _benchmarkRepo = benchmarkRepo;
            _providerFactory = providerFactory;
            _qualityScorer = qualityScorer;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<BenchmarkRun> RunBenchmarkAsync(string? notes = null, CancellationToken ct = default)
        {
            if (_isRunning)
                throw new InvalidOperationException("A benchmark is already running.");

            _isRunning = true;
            var maxConcurrency = _configuration.GetValue("BenchmarkSettings:MaxConcurrency", 3);
            var timeoutSeconds = _configuration.GetValue("BenchmarkSettings:RequestTimeoutSeconds", 60);

            // Create the benchmark run record
            var run = new BenchmarkRun
            {
                RunGuid = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                Status = "Running",
                Notes = notes
            };
            run.Id = await _benchmarkRepo.CreateRunAsync(run);

            try
            {
                // Get all active, configured providers
                var providers = (await _providerRepo.GetActiveConfiguredProvidersAsync()).ToList();
                if (!providers.Any())
                {
                    _logger.LogWarning("No active, configured providers found. Skipping benchmark.");
                    run.Status = "Completed";
                    run.CompletedAt = DateTime.UtcNow;
                    run.Notes = (run.Notes ?? "") + " | No providers configured.";
                    await _benchmarkRepo.UpdateRunAsync(run);
                    _isRunning = false;
                    return run;
                }

                // Get all active test products
                var products = (await _productRepo.GetActiveAsync()).ToList();
                if (!products.Any())
                {
                    _logger.LogWarning("No active test products found. Skipping benchmark.");
                    run.Status = "Completed";
                    run.CompletedAt = DateTime.UtcNow;
                    run.Notes = (run.Notes ?? "") + " | No test products configured.";
                    await _benchmarkRepo.UpdateRunAsync(run);
                    _isRunning = false;
                    return run;
                }

                _logger.LogInformation(
                    "Starting benchmark run {RunGuid} with {ProviderCount} providers × {ProductCount} products = {TotalRequests} requests",
                    run.RunGuid, providers.Count, products.Count, providers.Count * products.Count);

                int totalRequests = 0;
                int successfulRequests = 0;
                int failedRequests = 0;

                // Process each provider sequentially, but products within a provider with concurrency
                var semaphore = new SemaphoreSlim(maxConcurrency);

                foreach (var providerConfig in providers)
                {
                    var provider = _providerFactory.GetProvider(providerConfig.ProviderName);
                    if (provider == null)
                    {
                        _logger.LogWarning("No adapter found for provider '{ProviderName}', skipping.",
                            providerConfig.ProviderName);
                        continue;
                    }

                    _logger.LogInformation("Benchmarking provider: {ProviderName}", providerConfig.ProviderName);

                    var tasks = products.Select(async product =>
                    {
                        await semaphore.WaitAsync(ct);
                        try
                        {
                            var result = await BenchmarkSingleAsync(
                                run.Id, provider, providerConfig, product, timeoutSeconds, ct);

                            await _benchmarkRepo.InsertResultAsync(result);

                            Interlocked.Increment(ref totalRequests);
                            if (result.IsSuccess)
                                Interlocked.Increment(ref successfulRequests);
                            else
                                Interlocked.Increment(ref failedRequests);
                        }
                        finally
                        {
                            semaphore.Release();
                        }

                        // Respect rate limiting: simple delay between requests
                        var delayMs = 60_000 / Math.Max(1, providerConfig.RateLimitPerMinute);
                        await Task.Delay(delayMs, ct);
                    });

                    await Task.WhenAll(tasks);
                }

                // Update run stats
                run.TotalRequests = totalRequests;
                run.SuccessfulRequests = successfulRequests;
                run.FailedRequests = failedRequests;
                run.Status = "Completed";
                run.CompletedAt = DateTime.UtcNow;
                await _benchmarkRepo.UpdateRunAsync(run);

                _logger.LogInformation(
                    "Benchmark run {RunGuid} completed. Total: {Total}, Success: {Success}, Failed: {Failed}",
                    run.RunGuid, totalRequests, successfulRequests, failedRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Benchmark run {RunGuid} failed.", run.RunGuid);
                run.Status = "Failed";
                run.CompletedAt = DateTime.UtcNow;
                run.Notes = (run.Notes ?? "") + $" | Error: {ex.Message}";
                await _benchmarkRepo.UpdateRunAsync(run);
            }
            finally
            {
                _isRunning = false;
            }

            return run;
        }

        /// <summary>
        /// Benchmarks a single provider+product combination.
        /// </summary>
        private async Task<BenchmarkResult> BenchmarkSingleAsync(
            int runId,
            IScrapingProvider provider,
            ProviderConfig config,
            TestProduct product,
            int timeoutSeconds,
            CancellationToken ct)
        {
            var result = new BenchmarkResult
            {
                RunId = runId,
                ProviderName = config.ProviderName,
                Platform = product.Platform,
                TestProductId = product.Id,
                ProductUrl = product.ProductUrl
            };

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var response = await provider.ScrapeProductAsync(
                    product.ProductUrl,
                    config.ApiKey!,
                    config.BaseUrl ?? "",
                    cts.Token);

                // Map response to result
                result.IsSuccess = response.IsSuccess;
                result.HttpStatusCode = response.HttpStatusCode;
                result.ResponseTimeMs = response.ResponseTimeMs;
                result.ErrorMessage = response.ErrorMessage;
                result.ExtractedTitle = response.Title;
                result.ExtractedPrice = response.Price;
                result.ExtractedCurrency = response.Currency;
                result.ExtractedBrand = response.Brand;
                result.ExtractedUpc = response.Upc;
                result.ExtractedAvailability = response.Availability;
                result.ExtractedImageUrl = response.ImageUrl;
                result.ExtractedDescription = TruncateIfNeeded(response.Description, 4000);
                result.ExtractedRating = response.Rating;
                result.ExtractedReviewCount = response.ReviewCount;
                result.RawResponseJson = TruncateIfNeeded(response.RawJson, 50000);
                result.RawResponseSizeBytes = response.ResponseSizeBytes;

                // Compute quality scores
                if (response.IsSuccess)
                {
                    _qualityScorer.Score(result, response, product);
                }
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Request timed out after {timeoutSeconds} seconds";
                _logger.LogWarning("Timeout: {Provider} for {Url}", config.ProviderName, product.ProductUrl);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error: {Provider} for {Url}", config.ProviderName, product.ProductUrl);
            }

            return result;
        }

        private static string? TruncateIfNeeded(string? value, int maxLength)
        {
            if (value == null || value.Length <= maxLength) return value;
            return value[..maxLength] + "... [TRUNCATED]";
        }
    }
}

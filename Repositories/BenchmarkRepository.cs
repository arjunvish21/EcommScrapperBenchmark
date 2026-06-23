using Dapper;
using EcommScrapperBenchmark.Data;
using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public class BenchmarkRepository : IBenchmarkRepository
    {
        private readonly BenchmarkDbContext _db;

        public BenchmarkRepository(BenchmarkDbContext db)
        {
            _db = db;
        }

        // ───── Runs ─────

        public async Task<int> CreateRunAsync(BenchmarkRun run)
        {
            using var conn = _db.CreateConnection();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO BenchmarkRun (RunGuid, StartedAt, Status, Notes)
                VALUES (@RunGuid, @StartedAt, @Status, @Notes);
                SELECT CAST(SCOPE_IDENTITY() as int);", run);
        }

        public async Task UpdateRunAsync(BenchmarkRun run)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE BenchmarkRun 
                SET CompletedAt = @CompletedAt,
                    Status = @Status,
                    TotalRequests = @TotalRequests,
                    SuccessfulRequests = @SuccessfulRequests,
                    FailedRequests = @FailedRequests,
                    Notes = @Notes
                WHERE Id = @Id", run);
        }

        public async Task<BenchmarkRun?> GetRunByIdAsync(int id)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<BenchmarkRun>(
                "SELECT * FROM BenchmarkRun WHERE Id = @Id", new { Id = id });
        }

        public async Task<BenchmarkRun?> GetRunByGuidAsync(Guid runGuid)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<BenchmarkRun>(
                "SELECT * FROM BenchmarkRun WHERE RunGuid = @RunGuid", new { RunGuid = runGuid });
        }

        public async Task<IEnumerable<BenchmarkRun>> GetAllRunsAsync(int limit = 50)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<BenchmarkRun>(
                "SELECT TOP (@Limit) * FROM BenchmarkRun ORDER BY StartedAt DESC",
                new { Limit = limit });
        }

        public async Task<BenchmarkRun?> GetLatestRunAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<BenchmarkRun>(
                "SELECT TOP 1 * FROM BenchmarkRun WHERE Status = 'Completed' ORDER BY CompletedAt DESC");
        }

        // ───── Results ─────

        public async Task InsertResultAsync(BenchmarkResult result)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"
                INSERT INTO BenchmarkResult 
                (RunId, ProviderName, Platform, TestProductId, ProductUrl,
                 HttpStatusCode, ResponseTimeMs, IsSuccess, ErrorMessage,
                 ExtractedTitle, ExtractedPrice, ExtractedCurrency, ExtractedBrand,
                 ExtractedUpc, ExtractedAvailability, ExtractedImageUrl, ExtractedDescription,
                 ExtractedRating, ExtractedReviewCount,
                 RawResponseJson, RawResponseSizeBytes,
                 CompletenessScore, AccuracyScore, StructureScore, OverallQualityScore)
                VALUES
                (@RunId, @ProviderName, @Platform, @TestProductId, @ProductUrl,
                 @HttpStatusCode, @ResponseTimeMs, @IsSuccess, @ErrorMessage,
                 @ExtractedTitle, @ExtractedPrice, @ExtractedCurrency, @ExtractedBrand,
                 @ExtractedUpc, @ExtractedAvailability, @ExtractedImageUrl, @ExtractedDescription,
                 @ExtractedRating, @ExtractedReviewCount,
                 @RawResponseJson, @RawResponseSizeBytes,
                 @CompletenessScore, @AccuracyScore, @StructureScore, @OverallQualityScore)", result);
        }

        public async Task<IEnumerable<BenchmarkResult>> GetResultsByRunIdAsync(int runId)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<BenchmarkResult>(@"
                SELECT br.*, tp.ProductName as 'TestProduct.ProductName', tp.UpcCode as 'TestProduct.UpcCode'
                FROM BenchmarkResult br
                LEFT JOIN TestProduct tp ON br.TestProductId = tp.Id
                WHERE br.RunId = @RunId
                ORDER BY br.ProviderName, br.Platform", new { RunId = runId });
        }

        public async Task<IEnumerable<BenchmarkResult>> GetResultsByRunIdAndProviderAsync(int runId, string providerName)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<BenchmarkResult>(@"
                SELECT * FROM BenchmarkResult 
                WHERE RunId = @RunId AND ProviderName = @ProviderName
                ORDER BY Platform", new { RunId = runId, ProviderName = providerName });
        }

        // ───── Dashboard Aggregations ─────

        public async Task<IEnumerable<ProviderSummary>> GetProviderSummariesAsync(int? runId = null)
        {
            using var conn = _db.CreateConnection();
            var whereClause = runId.HasValue ? "WHERE br.RunId = @RunId" : "";

            return await conn.QueryAsync<ProviderSummary>($@"
                SELECT 
                    br.ProviderName,
                    COUNT(*) AS TotalRequests,
                    SUM(CASE WHEN br.IsSuccess = 1 THEN 1 ELSE 0 END) AS SuccessfulRequests,
                    SUM(CASE WHEN br.IsSuccess = 0 THEN 1 ELSE 0 END) AS FailedRequests,
                    CAST(SUM(CASE WHEN br.IsSuccess = 1 THEN 1.0 ELSE 0.0 END) / NULLIF(COUNT(*), 0) * 100 AS DECIMAL(5,1)) AS SuccessRate,
                    AVG(CAST(br.ResponseTimeMs AS FLOAT)) AS AvgResponseTimeMs,
                    AVG(ISNULL(br.OverallQualityScore, 0)) AS AvgQualityScore,
                    AVG(ISNULL(br.CompletenessScore, 0)) AS AvgCompletenessScore,
                    AVG(ISNULL(br.AccuracyScore, 0)) AS AvgAccuracyScore,
                    AVG(ISNULL(br.StructureScore, 0)) AS AvgStructureScore
                FROM BenchmarkResult br
                {whereClause}
                GROUP BY br.ProviderName
                ORDER BY SuccessRate DESC, AvgQualityScore DESC",
                new { RunId = runId });
        }

        public async Task<IEnumerable<PlatformBreakdown>> GetPlatformBreakdownsAsync(int? runId = null)
        {
            using var conn = _db.CreateConnection();
            var whereClause = runId.HasValue ? "WHERE br.RunId = @RunId" : "";

            return await conn.QueryAsync<PlatformBreakdown>($@"
                SELECT 
                    br.ProviderName,
                    br.Platform,
                    COUNT(*) AS TotalRequests,
                    SUM(CASE WHEN br.IsSuccess = 1 THEN 1 ELSE 0 END) AS SuccessfulRequests,
                    CAST(SUM(CASE WHEN br.IsSuccess = 1 THEN 1.0 ELSE 0.0 END) / NULLIF(COUNT(*), 0) * 100 AS DECIMAL(5,1)) AS SuccessRate,
                    AVG(CAST(br.ResponseTimeMs AS FLOAT)) AS AvgResponseTimeMs,
                    AVG(ISNULL(br.OverallQualityScore, 0)) AS AvgQualityScore
                FROM BenchmarkResult br
                {whereClause}
                GROUP BY br.ProviderName, br.Platform
                ORDER BY br.ProviderName, br.Platform",
                new { RunId = runId });
        }

        public async Task<OverallStats> GetOverallStatsAsync()
        {
            using var conn = _db.CreateConnection();
            var stats = await conn.QueryFirstOrDefaultAsync<OverallStats>(@"
                SELECT 
                    (SELECT COUNT(*) FROM BenchmarkRun) AS TotalRuns,
                    (SELECT COUNT(DISTINCT ProviderName) FROM BenchmarkResult) AS TotalProvidersTested,
                    COUNT(*) AS TotalRequests,
                    CAST(SUM(CASE WHEN IsSuccess = 1 THEN 1.0 ELSE 0.0 END) / NULLIF(COUNT(*), 0) * 100 AS DECIMAL(5,1)) AS OverallSuccessRate,
                    AVG(CAST(ResponseTimeMs AS FLOAT)) AS OverallAvgResponseTime,
                    AVG(ISNULL(OverallQualityScore, 0)) AS OverallAvgQuality
                FROM BenchmarkResult");

            return stats ?? new OverallStats();
        }
    }
}

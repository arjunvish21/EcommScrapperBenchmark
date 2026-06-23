using Dapper;
using EcommScrapperBenchmark.Data;
using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public class ProviderConfigRepository : IProviderConfigRepository
    {
        private readonly BenchmarkDbContext _db;

        public ProviderConfigRepository(BenchmarkDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<ProviderConfig>> GetAllAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<ProviderConfig>(
                "SELECT * FROM ProviderConfig ORDER BY ProviderName");
        }

        public async Task<ProviderConfig?> GetByNameAsync(string providerName)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<ProviderConfig>(
                "SELECT * FROM ProviderConfig WHERE ProviderName = @ProviderName",
                new { ProviderName = providerName });
        }

        public async Task<ProviderConfig?> GetByIdAsync(int id)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<ProviderConfig>(
                "SELECT * FROM ProviderConfig WHERE Id = @Id",
                new { Id = id });
        }

        public async Task UpdateAsync(ProviderConfig config)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE ProviderConfig 
                SET ApiKey = @ApiKey, 
                    BaseUrl = @BaseUrl, 
                    AuthType = @AuthType,
                    IsActive = @IsActive, 
                    RateLimitPerMinute = @RateLimitPerMinute,
                    UpdatedOn = GETUTCDATE()
                WHERE Id = @Id", config);
        }

        public async Task<IEnumerable<ProviderConfig>> GetActiveConfiguredProvidersAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<ProviderConfig>(@"
                SELECT * FROM ProviderConfig 
                WHERE IsActive = 1 AND ApiKey IS NOT NULL AND ApiKey != ''
                ORDER BY ProviderName");
        }
    }
}

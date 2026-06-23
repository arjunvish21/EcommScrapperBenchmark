using Dapper;
using EcommScrapperBenchmark.Data;
using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public class TestProductRepository : ITestProductRepository
    {
        private readonly BenchmarkDbContext _db;

        public TestProductRepository(BenchmarkDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<TestProduct>> GetAllAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<TestProduct>(
                "SELECT * FROM TestProduct ORDER BY Platform, ProductName");
        }

        public async Task<IEnumerable<TestProduct>> GetActiveAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<TestProduct>(
                "SELECT * FROM TestProduct WHERE IsActive = 1 ORDER BY Platform, ProductName");
        }

        public async Task<TestProduct?> GetByIdAsync(int id)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<TestProduct>(
                "SELECT * FROM TestProduct WHERE Id = @Id", new { Id = id });
        }

        public async Task<int> InsertAsync(TestProduct product)
        {
            using var conn = _db.CreateConnection();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO TestProduct (Platform, ProductUrl, ProductName, UpcCode, ExpectedPrice, ExpectedBrand, IsActive)
                VALUES (@Platform, @ProductUrl, @ProductName, @UpcCode, @ExpectedPrice, @ExpectedBrand, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() as int);", product);
        }

        public async Task UpdateAsync(TestProduct product)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(@"
                UPDATE TestProduct 
                SET Platform = @Platform,
                    ProductUrl = @ProductUrl,
                    ProductName = @ProductName,
                    UpcCode = @UpcCode,
                    ExpectedPrice = @ExpectedPrice,
                    ExpectedBrand = @ExpectedBrand,
                    IsActive = @IsActive,
                    UpdatedOn = GETUTCDATE()
                WHERE Id = @Id", product);
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM TestProduct WHERE Id = @Id", new { Id = id });
        }

        public async Task<IEnumerable<string>> GetDistinctPlatformsAsync()
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<string>(
                "SELECT DISTINCT Platform FROM TestProduct WHERE IsActive = 1 ORDER BY Platform");
        }
    }
}

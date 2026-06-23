using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public interface ITestProductRepository
    {
        Task<IEnumerable<TestProduct>> GetAllAsync();
        Task<IEnumerable<TestProduct>> GetActiveAsync();
        Task<TestProduct?> GetByIdAsync(int id);
        Task<int> InsertAsync(TestProduct product);
        Task UpdateAsync(TestProduct product);
        Task DeleteAsync(int id);
        Task<IEnumerable<string>> GetDistinctPlatformsAsync();
    }
}

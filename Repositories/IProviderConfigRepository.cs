using EcommScrapperBenchmark.Models;

namespace EcommScrapperBenchmark.Repositories
{
    public interface IProviderConfigRepository
    {
        Task<IEnumerable<ProviderConfig>> GetAllAsync();
        Task<ProviderConfig?> GetByNameAsync(string providerName);
        Task<ProviderConfig?> GetByIdAsync(int id);
        Task UpdateAsync(ProviderConfig config);
        Task<IEnumerable<ProviderConfig>> GetActiveConfiguredProvidersAsync();
    }
}

namespace EcommScrapperBenchmark.Services.Providers
{
    /// <summary>
    /// Resolves the correct IScrapingProvider implementation by provider name.
    /// </summary>
    public class ProviderFactory
    {
        private readonly Dictionary<string, IScrapingProvider> _providers;
        private readonly ILogger<ProviderFactory> _logger;

        public ProviderFactory(IEnumerable<IScrapingProvider> providers, ILogger<ProviderFactory> logger)
        {
            _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        public IScrapingProvider? GetProvider(string providerName)
        {
            if (_providers.TryGetValue(providerName, out var provider))
            {
                return provider;
            }

            _logger.LogWarning("No provider adapter found for '{ProviderName}'", providerName);
            return null;
        }

        public IEnumerable<string> GetRegisteredProviderNames()
        {
            return _providers.Keys;
        }
    }
}

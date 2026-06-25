using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommScrapperBenchmark.Data
{
    public class BenchmarkDbContext
    {
        private readonly string _connectionString;
        private readonly ILogger<BenchmarkDbContext> _logger;

        public BenchmarkDbContext(IConfiguration configuration, ILogger<BenchmarkDbContext> logger)
        {
            var baseConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Add TrustServerCertificate for development/self-signed certificates
            var connectionStringBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                TrustServerCertificate = true,
                Encrypt = true
            };
            _connectionString = connectionStringBuilder.ConnectionString;
            _logger = logger;
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Initializes the database schema by running the init script.
        /// Safe to call multiple times (idempotent).
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Scripts", "InitSchema.sql");

                if (!File.Exists(scriptPath))
                {
                    // Try relative path for development
                    scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Scripts", "InitSchema.sql");
                }

                if (!File.Exists(scriptPath))
                {
                    _logger.LogWarning("InitSchema.sql not found. Database schema must be created manually.");
                    return;
                }

                var script = await File.ReadAllTextAsync(scriptPath);

                // Split by GO statements for SQL Server batch execution
                var batches = script.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n" },
                    StringSplitOptions.RemoveEmptyEntries);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                foreach (var batch in batches)
                {
                    var trimmedBatch = batch.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

                    using var command = new SqlCommand(trimmedBatch, connection);
                    command.CommandTimeout = 60;
                    await command.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Database schema initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database schema.");
                throw;
            }
        }
    }
}

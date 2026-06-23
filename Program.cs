using EcommScrapperBenchmark.Data;
using EcommScrapperBenchmark.Repositories;
using EcommScrapperBenchmark.Services;
using EcommScrapperBenchmark.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddSingleton<BenchmarkDbContext>();

// Repositories
builder.Services.AddScoped<IProviderConfigRepository, ProviderConfigRepository>();
builder.Services.AddScoped<ITestProductRepository, TestProductRepository>();
builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();

// HTTP Client for scraping providers
builder.Services.AddHttpClient("ScrapingClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("BenchmarkSettings:RequestTimeoutSeconds", 60));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "EcommScrapperBenchmark/1.0");
});

// Scraping provider adapters (all 12)
builder.Services.AddSingleton<IScrapingProvider, BrightDataProvider>();
builder.Services.AddSingleton<IScrapingProvider, ZyteProvider>();
builder.Services.AddSingleton<IScrapingProvider, OxylabsProvider>();
builder.Services.AddSingleton<IScrapingProvider, DecodoProvider>();
builder.Services.AddSingleton<IScrapingProvider, NimblewayProvider>();
builder.Services.AddSingleton<IScrapingProvider, WebScrapingApiProvider>();
builder.Services.AddSingleton<IScrapingProvider, NetNutProvider>();
builder.Services.AddSingleton<IScrapingProvider, ScrapingBeeProvider>();
builder.Services.AddSingleton<IScrapingProvider, ScraperApiProvider>();
builder.Services.AddSingleton<IScrapingProvider, ScrapingDogProvider>();
builder.Services.AddSingleton<IScrapingProvider, InfaticaProvider>();
builder.Services.AddSingleton<IScrapingProvider, RayobyteProvider>();

// Provider factory and services
builder.Services.AddSingleton<ProviderFactory>();
builder.Services.AddSingleton<QualityScorer>();
builder.Services.AddScoped<IBenchmarkService, BenchmarkService>();

var app = builder.Build();

// Initialize database schema on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
    await dbContext.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using EcommScrapperBenchmark.Models;
using EcommScrapperBenchmark.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EcommScrapperBenchmark.Controllers
{
    public class ProvidersController : Controller
    {
        private readonly IProviderConfigRepository _providerRepo;
        private readonly ILogger<ProvidersController> _logger;

        public ProvidersController(IProviderConfigRepository providerRepo, ILogger<ProvidersController> logger)
        {
            _providerRepo = providerRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var providers = await _providerRepo.GetAllAsync();
            return View(providers);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var provider = await _providerRepo.GetByIdAsync(id);
            if (provider == null) return NotFound();
            return View(provider);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ProviderName))
            {
                ModelState.AddModelError("", "Provider name is required.");
                return View(config);
            }

            await _providerRepo.UpdateAsync(config);
            TempData["SuccessMessage"] = $"Provider '{config.ProviderName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}

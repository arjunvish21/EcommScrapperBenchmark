using EcommScrapperBenchmark.Models;
using EcommScrapperBenchmark.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EcommScrapperBenchmark.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ITestProductRepository _productRepo;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ITestProductRepository productRepo, ILogger<ProductsController> logger)
        {
            _productRepo = productRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productRepo.GetAllAsync();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new TestProduct { IsActive = true });
        }

        [HttpPost]
        public async Task<IActionResult> Create(TestProduct product)
        {
            if (string.IsNullOrWhiteSpace(product.Platform) || string.IsNullOrWhiteSpace(product.ProductUrl))
            {
                ModelState.AddModelError("", "Platform and Product URL are required.");
                return View(product);
            }

            await _productRepo.InsertAsync(product);
            TempData["SuccessMessage"] = "Product added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _productRepo.GetByIdAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(TestProduct product)
        {
            if (string.IsNullOrWhiteSpace(product.Platform) || string.IsNullOrWhiteSpace(product.ProductUrl))
            {
                ModelState.AddModelError("", "Platform and Product URL are required.");
                return View(product);
            }

            await _productRepo.UpdateAsync(product);
            TempData["SuccessMessage"] = "Product updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _productRepo.DeleteAsync(id);
            TempData["SuccessMessage"] = "Product deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

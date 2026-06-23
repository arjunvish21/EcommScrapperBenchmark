using EcommScrapperBenchmark.Models;
using EcommScrapperBenchmark.Repositories;
using EcommScrapperBenchmark.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommScrapperBenchmark.Controllers
{
    public class BenchmarkController : Controller
    {
        private readonly IBenchmarkRepository _benchmarkRepo;
        private readonly IBenchmarkService _benchmarkService;
        private readonly IProviderConfigRepository _providerRepo;
        private readonly ILogger<BenchmarkController> _logger;

        public BenchmarkController(
            IBenchmarkRepository benchmarkRepo,
            IBenchmarkService benchmarkService,
            IProviderConfigRepository providerRepo,
            ILogger<BenchmarkController> logger)
        {
            _benchmarkRepo = benchmarkRepo;
            _benchmarkService = benchmarkService;
            _providerRepo = providerRepo;
            _logger = logger;
        }

        /// <summary>
        /// Main dashboard with aggregated comparison.
        /// </summary>
        public async Task<IActionResult> Dashboard(int? runId = null)
        {
            var viewModel = new DashboardViewModel();

            try
            {
                viewModel.ProviderSummaries = (await _benchmarkRepo.GetProviderSummariesAsync(runId)).ToList();
                viewModel.PlatformBreakdowns = (await _benchmarkRepo.GetPlatformBreakdownsAsync(runId)).ToList();
                viewModel.RecentRuns = (await _benchmarkRepo.GetAllRunsAsync(10)).ToList();
                viewModel.Overall = await _benchmarkRepo.GetOverallStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
            }

            ViewBag.SelectedRunId = runId;
            ViewBag.IsRunning = _benchmarkService.IsRunning;
            return View(viewModel);
        }

        /// <summary>
        /// List of all benchmark runs.
        /// </summary>
        public async Task<IActionResult> Runs()
        {
            var runs = await _benchmarkRepo.GetAllRunsAsync(100);
            ViewBag.IsRunning = _benchmarkService.IsRunning;
            return View(runs);
        }

        /// <summary>
        /// Detail view of a specific run.
        /// </summary>
        public async Task<IActionResult> RunDetail(int id)
        {
            var run = await _benchmarkRepo.GetRunByIdAsync(id);
            if (run == null) return NotFound();

            var results = await _benchmarkRepo.GetResultsByRunIdAsync(id);
            ViewBag.Run = run;
            ViewBag.ProviderSummaries = (await _benchmarkRepo.GetProviderSummariesAsync(id)).ToList();
            return View(results);
        }

        /// <summary>
        /// Start a new benchmark run (fire and forget).
        /// </summary>
        [HttpPost]
        public IActionResult Start(string? notes)
        {
            if (_benchmarkService.IsRunning)
            {
                TempData["ErrorMessage"] = "A benchmark is already running. Please wait for it to complete.";
                return RedirectToAction(nameof(Runs));
            }

            // Fire and forget — run in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _benchmarkService.RunBenchmarkAsync(notes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background benchmark run failed");
                }
            });

            TempData["SuccessMessage"] = "Benchmark run started! Results will appear as they complete.";
            return RedirectToAction(nameof(Runs));
        }

        /// <summary>
        /// JSON API for chart data.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ChartData(int? runId = null)
        {
            var summaries = await _benchmarkRepo.GetProviderSummariesAsync(runId);
            var breakdowns = await _benchmarkRepo.GetPlatformBreakdownsAsync(runId);

            return Json(new
            {
                providers = summaries,
                platforms = breakdowns
            });
        }

        /// <summary>
        /// Check if a benchmark is currently running (for AJAX polling).
        /// </summary>
        [HttpGet]
        public IActionResult Status()
        {
            return Json(new { isRunning = _benchmarkService.IsRunning });
        }
    }
}

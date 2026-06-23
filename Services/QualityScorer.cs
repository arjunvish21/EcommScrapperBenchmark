using EcommScrapperBenchmark.Models;
using EcommScrapperBenchmark.Services.Providers;

namespace EcommScrapperBenchmark.Services
{
    /// <summary>
    /// Computes quality scores for scraped product data.
    /// Scoring: Completeness (40%) + Accuracy (30%) + Structure (30%)
    /// </summary>
    public class QualityScorer
    {
        /// <summary>
        /// Computes all quality scores and sets them on the BenchmarkResult.
        /// </summary>
        public void Score(BenchmarkResult result, ScrapingResponse response, TestProduct product)
        {
            result.CompletenessScore = CalculateCompleteness(response);
            result.AccuracyScore = CalculateAccuracy(response, product);
            result.StructureScore = CalculateStructure(response);

            result.OverallQualityScore = Math.Round(
                (result.CompletenessScore ?? 0) * 0.4m +
                (result.AccuracyScore ?? 0) * 0.3m +
                (result.StructureScore ?? 0) * 0.3m, 2);
        }

        /// <summary>
        /// Completeness: What percentage of expected fields are non-null and non-empty?
        /// Expected fields: Title, Price, Currency, Brand, Description, ImageUrl, Availability, Rating, ReviewCount, Upc
        /// </summary>
        private decimal CalculateCompleteness(ScrapingResponse response)
        {
            var fields = new object?[]
            {
                response.Title,
                response.Price,
                response.Currency,
                response.Brand,
                response.Description,
                response.ImageUrl,
                response.Availability,
                response.Rating,
                response.ReviewCount,
                response.Upc
            };

            int total = fields.Length;
            int filled = 0;

            foreach (var field in fields)
            {
                if (field == null) continue;
                if (field is string s && string.IsNullOrWhiteSpace(s)) continue;
                filled++;
            }

            return Math.Round((decimal)filled / total * 100, 2);
        }

        /// <summary>
        /// Accuracy: How close is the scraped data to known ground truth?
        /// Compares title similarity, price match, and brand match against expected values.
        /// </summary>
        private decimal CalculateAccuracy(ScrapingResponse response, TestProduct product)
        {
            decimal score = 0;
            int checks = 0;

            // Title check: fuzzy match
            if (!string.IsNullOrWhiteSpace(product.ProductName))
            {
                checks++;
                if (!string.IsNullOrWhiteSpace(response.Title))
                {
                    var similarity = CalculateSimilarity(
                        product.ProductName.ToLowerInvariant(),
                        response.Title.ToLowerInvariant());
                    score += (decimal)similarity * 100;
                }
            }

            // Price check: within 10% tolerance
            if (product.ExpectedPrice.HasValue && product.ExpectedPrice > 0)
            {
                checks++;
                if (response.Price.HasValue && response.Price > 0)
                {
                    var priceDiff = Math.Abs(product.ExpectedPrice.Value - response.Price.Value);
                    var tolerance = product.ExpectedPrice.Value * 0.10m;
                    score += priceDiff <= tolerance ? 100 : Math.Max(0, 100 - (priceDiff / product.ExpectedPrice.Value * 100));
                }
            }

            // Brand check: exact or contains match
            if (!string.IsNullOrWhiteSpace(product.ExpectedBrand))
            {
                checks++;
                if (!string.IsNullOrWhiteSpace(response.Brand))
                {
                    var expectedBrand = product.ExpectedBrand.ToLowerInvariant().Trim();
                    var actualBrand = response.Brand.ToLowerInvariant().Trim();

                    if (expectedBrand == actualBrand)
                        score += 100;
                    else if (actualBrand.Contains(expectedBrand) || expectedBrand.Contains(actualBrand))
                        score += 80;
                    else
                        score += 0;
                }
            }

            if (checks == 0) return 50; // No ground truth available, give neutral score
            return Math.Round(score / checks, 2);
        }

        /// <summary>
        /// Structure: Is the response valid JSON? Does it follow expected patterns?
        /// </summary>
        private decimal CalculateStructure(ScrapingResponse response)
        {
            decimal score = 0;

            // Is the raw response valid JSON? (40 points)
            if (!string.IsNullOrWhiteSpace(response.RawJson))
            {
                try
                {
                    Newtonsoft.Json.Linq.JToken.Parse(response.RawJson);
                    score += 40;
                }
                catch
                {
                    score += 0; // Not valid JSON
                }
            }

            // Response size reasonable? (20 points)
            if (response.ResponseSizeBytes > 100 && response.ResponseSizeBytes < 5_000_000)
            {
                score += 20;
            }
            else if (response.ResponseSizeBytes > 0)
            {
                score += 10; // Suspiciously small or too large
            }

            // Has a parseable title? (20 points)
            if (!string.IsNullOrWhiteSpace(response.Title) && response.Title.Length > 3)
            {
                score += 20;
            }

            // Has a numeric price? (20 points)
            if (response.Price.HasValue && response.Price > 0)
            {
                score += 20;
            }

            return Math.Round(score, 2);
        }

        /// <summary>
        /// Simple Jaccard similarity coefficient for two strings.
        /// </summary>
        private double CalculateSimilarity(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;

            var wordsA = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var wordsB = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            var intersection = wordsA.Intersect(wordsB).Count();
            var union = wordsA.Union(wordsB).Count();

            return union == 0 ? 0 : (double)intersection / union;
        }
    }
}

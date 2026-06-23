namespace EcommScrapperBenchmark.Models
{
    public class TestProduct
    {
        public int Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string ProductUrl { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? UpcCode { get; set; }
        public decimal? ExpectedPrice { get; set; }
        public string? ExpectedBrand { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}

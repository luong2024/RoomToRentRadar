namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Filter and search criteria model for US05.
    /// </summary>
    public class RoomFilterCriteria
    {
        public string? Keyword { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinArea { get; set; }
        public double? MaxArea { get; set; }
        public string? RoomType { get; set; }
        public double? MaxDistanceKm { get; set; }
        public string SortBy { get; set; } = "DistanceAsc"; // DistanceAsc, PriceAsc, PriceDesc

        public int ActiveFilterCount
        {
            get
            {
                int count = 0;
                if (!string.IsNullOrWhiteSpace(Keyword)) count++;
                if (MinPrice.HasValue || MaxPrice.HasValue) count++;
                if (MinArea.HasValue || MaxArea.HasValue) count++;
                if (!string.IsNullOrWhiteSpace(RoomType)) count++;
                if (MaxDistanceKm.HasValue) count++;
                return count;
            }
        }

        public void Clear()
        {
            Keyword = null;
            MinPrice = null;
            MaxPrice = null;
            MinArea = null;
            MaxArea = null;
            RoomType = null;
            MaxDistanceKm = null;
            SortBy = "DistanceAsc";
        }
    }
}

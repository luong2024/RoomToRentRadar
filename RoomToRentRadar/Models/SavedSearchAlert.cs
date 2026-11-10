namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Represents a saved search with alert rules and map area coordinates for new matching listings (US06, US08).
    /// </summary>
    public class SavedSearchAlert
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinArea { get; set; }
        public double? MaxDistanceKm { get; set; }
        public string? RoomType { get; set; }

        // Map area search coordinates (US06)
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public double? RadiusKm { get; set; }
        public string PolygonJson { get; set; } = "[]";

        public bool IsEnabled { get; set; } = true;
        public int AlertsSentToday { get; set; }
        public int MaxDailyAlerts { get; set; } = 3; // AC4: Max 3 alerts per day
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }
}

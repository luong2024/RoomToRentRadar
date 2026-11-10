namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Represents versioned house rules and building regulations for a room (US12).
    /// </summary>
    public class HouseRuleVersion
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int VersionNumber { get; set; } = 1;
        public string QuietHours { get; set; } = string.Empty;
        public string GuestPolicy { get; set; } = string.Empty;
        public string CookingRules { get; set; } = string.Empty;
        public string ParkingRules { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
    }
}

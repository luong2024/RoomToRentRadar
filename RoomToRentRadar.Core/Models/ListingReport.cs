namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Represents a student report on a suspicious or inaccurate room listing (US13).
    /// </summary>
    public class ListingReport
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int ReporterUserId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Reviewed, Resolved
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
    }
}

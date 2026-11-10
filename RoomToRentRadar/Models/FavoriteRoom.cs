namespace RoomToRentRadar.Models
{
    /// <summary>
    /// Represents a student's saved favorite room (US07).
    /// </summary>
    public class FavoriteRoom
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RoomId { get; set; }
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");
    }
}

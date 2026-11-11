namespace RoomToRentRadar.Domain.Entities;

public enum RoomStatus
{
    Available = 0,
    Rented = 1,
    Pending = 2,
    UnderMaintenance = 3
}

public class Room
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Area { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
}

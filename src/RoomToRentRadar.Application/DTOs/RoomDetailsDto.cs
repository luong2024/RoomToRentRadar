namespace RoomToRentRadar.Application.DTOs;

public class RoomDetailsDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string FormattedPrice { get; init; } = string.Empty;
    public double Area { get; init; }
    public string FormattedArea { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public string Status { get; init; } = string.Empty;
}

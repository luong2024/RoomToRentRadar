using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

public sealed class Room
{
    private Room()
    {
    }

    private Room(
        string id,
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId,
        bool isAvailable,
        bool isVerified,
        DateTime createdAtUtc)
    {
        Id = id;
        Title = title;
        Description = description;
        Price = price;
        Area = area;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        LandlordId = landlordId;
        IsAvailable = isAvailable;
        IsVerified = isVerified;
        CreatedAtUtc = createdAtUtc;
    }

    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public double Area { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string LandlordId { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Room Create(
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId)
    {
        var room = new Room(
            Guid.NewGuid().ToString("N"),
            title,
            description,
            price,
            area,
            address,
            latitude,
            longitude,
            landlordId,
            isAvailable: true,
            isVerified: true,
            createdAtUtc: DateTime.UtcNow);

        room.EnsureInvariants();
        return room;
    }

    public static Room Reconstitute(
        string id,
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId,
        bool isAvailable,
        bool isVerified,
        DateTime createdAtUtc)
    {
        var room = new Room(
            id,
            title,
            description,
            price,
            area,
            address,
            latitude,
            longitude,
            landlordId,
            isAvailable,
            isVerified,
            createdAtUtc);

        room.EnsureInvariants();
        return room;
    }

    private void EnsureInvariants()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new DomainValidationException("Room Id is required.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new DomainValidationException("Room title is required.");
        }

        if (string.IsNullOrWhiteSpace(LandlordId))
        {
            throw new DomainValidationException("Room landlord is required.");
        }

        if (Price <= 0m)
        {
            throw new DomainValidationException("Room price must be greater than 0.");
        }

        if (Area <= 0)
        {
            throw new DomainValidationException("Room area must be greater than 0.");
        }

        if (double.IsNaN(Latitude) || Latitude < -90 || Latitude > 90)
        {
            throw new DomainValidationException("Room latitude must be between -90 and 90.");
        }

        if (double.IsNaN(Longitude) || Longitude < -180 || Longitude > 180)
        {
            throw new DomainValidationException("Room longitude must be between -180 and 180.");
        }
    }
}

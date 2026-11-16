namespace RoomToRentRadar.Domain.Entities;

using RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// Represents a university campus or branch.
/// </summary>
public sealed class Campus
{
    public string Id { get; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public GeoLocation Location { get; private set; }
    public string Address { get; private set; }

    public Campus(string id, string name, string code, GeoLocation location, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        Id = id;
        Name = name;
        Code = code;
        Location = location;
        Address = address;
    }
}

using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Reference data for the university campuses used as distance origins (BR-CMP-01..03, <c>docs/erd.md</c> §4.1).
/// Not an aggregate: rows are seeded and never edited at runtime.
/// </summary>
public sealed class Campus
{
    public string Id { get; }
    public string Name { get; }
    public string ShortName { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public bool IsDefault { get; }
    public int SortOrder { get; }

    public Campus(
        string id,
        string name,
        string shortName,
        double latitude,
        double longitude,
        bool isDefault,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Campus Id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Campus name is required.");
        }

        if (string.IsNullOrWhiteSpace(shortName))
        {
            throw new DomainValidationException("Campus short name is required.");
        }

        if (!double.IsFinite(latitude) || latitude < -90 || latitude > 90)
        {
            throw new DomainValidationException("Campus latitude must be finite and between -90 and 90.");
        }

        if (!double.IsFinite(longitude) || longitude < -180 || longitude > 180)
        {
            throw new DomainValidationException("Campus longitude must be finite and between -180 and 180.");
        }

        Id = id.Trim();
        Name = name.Trim();
        ShortName = shortName.Trim();
        Latitude = latitude;
        Longitude = longitude;
        IsDefault = isDefault;
        SortOrder = sortOrder;
    }
}

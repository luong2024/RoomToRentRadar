using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// A safety feature declared for a room listing (<c>docs/erd.md</c> §4.6, BR-LST-08).
/// Immutable value object containing the safety feature code and confirmation status.
/// </summary>
public sealed record RoomSafetyFeature
{
    public SafetyFeature Feature { get; }
    public bool IsConfirmed { get; }

    public RoomSafetyFeature(SafetyFeature feature, bool isConfirmed = false)
    {
        if (!Enum.IsDefined(feature))
        {
            throw new DomainValidationException($"Safety feature '{(int)feature}' is not recognized.");
        }

        Feature = feature;
        IsConfirmed = isConfirmed;
    }

    public RoomSafetyFeature WithConfirmed(bool isConfirmed) =>
        new(Feature, isConfirmed);
}

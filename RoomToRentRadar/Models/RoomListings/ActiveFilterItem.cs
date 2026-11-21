using CommunityToolkit.Mvvm.ComponentModel;

namespace RoomToRentRadar.Models.RoomListings;

/// <summary>
/// Model representing an active filter chip tag in the UI.
/// </summary>
public sealed partial class ActiveFilterItem : ObservableObject
{
    public required string Key { get; init; }

    public required string DisplayText { get; init; }

    public string FilterType { get; init; } = string.Empty;

    public object? Value { get; init; }

    public override bool Equals(object? obj) =>
        obj is ActiveFilterItem other && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Key);
}

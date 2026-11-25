using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Services.Search.Core;

public enum ActiveFilterChipKind
{
    Price,
    Area,
    RoomType,
    MaxDistance
}

public sealed record ActiveFilterChipModel(
    ActiveFilterChipKind Kind,
    string Label,
    string RemoveAccessibilityLabel,
    RoomType? RoomType = null);

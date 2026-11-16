using CommunityToolkit.Mvvm.ComponentModel;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Supported sorting options for room listings.
/// </summary>
public enum RoomSortOption
{
    Newest,
    PriceAsc,
    PriceDesc,
    DistanceAsc
}

/// <summary>
/// Represents a selectable sort option pill item for UI binding.
/// </summary>
public sealed partial class RoomSortOptionItem : ObservableObject
{
    public RoomSortOptionItem(RoomSortOption option, string displayName, string apiQueryValue, bool isSelected = false)
    {
        Option = option;
        DisplayName = displayName;
        ApiQueryValue = apiQueryValue;
        IsSelected = isSelected;
    }

    public RoomSortOption Option { get; }

    public string DisplayName { get; }

    public string ApiQueryValue { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public static IReadOnlyList<RoomSortOptionItem> CreateDefaults() =>
    [
        new(RoomSortOption.Newest, "Newest", "newest", isSelected: true),
        new(RoomSortOption.PriceAsc, "Price: Low to High", "price_asc"),
        new(RoomSortOption.PriceDesc, "Price: High to Low", "price_desc"),
        new(RoomSortOption.DistanceAsc, "Nearest to ICTU", "distance_asc")
    ];
}

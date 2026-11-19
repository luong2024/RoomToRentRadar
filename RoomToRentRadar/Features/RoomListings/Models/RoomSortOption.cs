using CommunityToolkit.Mvvm.ComponentModel;
using RoomToRentRadar.Core.Localization;

namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Supported sorting options for room listings.
/// </summary>
public enum RoomSortOption
{
    DistanceAsc,
    PriceAsc,
    PriceDesc,
    Newest
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

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    public string ApiQueryValue { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void UpdateDisplayName()
    {
        DisplayName = Option switch
        {
            RoomSortOption.DistanceAsc => LocalizationResourceManager.Instance["Sort_DistanceAsc"],
            RoomSortOption.PriceAsc => LocalizationResourceManager.Instance["Sort_PriceAsc"],
            RoomSortOption.PriceDesc => LocalizationResourceManager.Instance["Sort_PriceDesc"],
            _ => DisplayName
        };
    }

    public static IReadOnlyList<RoomSortOptionItem> CreateDefaults() =>
    [
        new(RoomSortOption.DistanceAsc, LocalizationResourceManager.Instance["Sort_DistanceAsc"], "distance_asc", isSelected: true),
        new(RoomSortOption.PriceAsc, LocalizationResourceManager.Instance["Sort_PriceAsc"], "price_asc"),
        new(RoomSortOption.PriceDesc, LocalizationResourceManager.Instance["Sort_PriceDesc"], "price_desc")
    ];
}

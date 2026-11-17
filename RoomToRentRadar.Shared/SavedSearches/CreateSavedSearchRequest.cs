using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.SavedSearches;

/// <summary>Body of <c>POST /api/v1/saved-searches</c>.</summary>
public sealed class CreateSavedSearchRequest
{
    [Required]
    [Description("Saved search name, 1-60 characters.")]
    public string? Name { get; init; }

    [Description("Alert state: off, active or paused. Defaults to off.")]
    public SavedSearchAlertState AlertState { get; init; } = SavedSearchAlertState.Off;

    [Required]
    [Description("Drawn search area. The same closed GeoJSON polygon contract as the area search.")]
    public AreaPolygon? Area { get; init; }
}

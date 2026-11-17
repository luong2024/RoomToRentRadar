using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.SavedSearches;

/// <summary>Body of <c>PUT /api/v1/saved-searches/{id}/alert</c>.</summary>
public sealed class SetSavedSearchAlertRequest
{
    [Required]
    [Description("New alert state: off, active or paused.")]
    public SavedSearchAlertState AlertState { get; init; } = SavedSearchAlertState.Off;
}

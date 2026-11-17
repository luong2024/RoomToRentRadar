namespace RoomToRentRadar.Ports.Contracts.Requests.Profile;

/// <summary>
/// Matches <c>UpdateMyProfileRequest</c> in <c>docs/api/openapi.yaml</c> 1:1. Only provided fields are changed;
/// role/isVerified/email are not updatable (GUARDRAILS 1.5.1). PreferredLanguage is a string like UserProfileDto.
/// </summary>
public sealed record UpdateMyProfileRequest(
    string? FullName,
    string? PhoneNumber,
    string? SelectedCampusId,
    string? PreferredLanguage
);

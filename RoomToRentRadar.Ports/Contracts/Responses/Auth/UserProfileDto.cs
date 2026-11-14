using RoomToRentRadar.Domain.Aggregates;

namespace RoomToRentRadar.Ports.Contracts.Responses.Auth;

public record UserProfileDto(
    string Id,
    string Email,
    string FullName,
    string Role,
    string AuthProvider,
    string? PhoneNumber,
    string? StudentCode,
    bool IsVerified,
    DateTime CreatedAtUtc,
    // BR-CMP-02 / BR-I18N-02: defaults keep legacy callers and cached profile JSON working.
    string SelectedCampusId = User.DefaultSelectedCampusId,
    string PreferredLanguage = User.DefaultPreferredLanguage
);


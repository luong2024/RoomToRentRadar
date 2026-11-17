namespace RoomToRentRadar.Ports.Contracts.Responses.Admin;

/// <summary>Matches <c>PendingLandlordDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record PendingLandlordDto(
    string Id,
    string FullName,
    string Email,
    string PhoneNumber,
    DateTime CreatedAtUtc
);
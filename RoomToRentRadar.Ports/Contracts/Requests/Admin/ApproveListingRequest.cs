namespace RoomToRentRadar.Ports.Contracts.Requests.Admin;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ApproveListingRequest</c> in <c>docs/api/openapi.yaml</c> 1:1 (BR-LST-08); empty array = none confirmed.</summary>
public sealed record ApproveListingRequest(
    IReadOnlyList<SafetyFeature> ConfirmedSafetyFeatures
);
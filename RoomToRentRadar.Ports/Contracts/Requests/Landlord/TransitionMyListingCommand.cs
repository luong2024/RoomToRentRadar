namespace RoomToRentRadar.Ports.Contracts.Requests.Landlord;

/// <summary>
/// Input of the landlord state transitions (mark rented/available, unpublish, republish, resubmit —
/// UC-33). <see cref="IfMatch"/> is the raw <c>If-Match</c> header (ADR-012).
/// </summary>
public sealed record TransitionMyListingCommand(
    string LandlordId,
    string RoomId,
    string? IfMatch);

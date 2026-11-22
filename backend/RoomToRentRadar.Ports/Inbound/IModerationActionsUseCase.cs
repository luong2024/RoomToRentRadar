using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Ports.Inbound;

public interface IModerationActionsUseCase
{
    Task<ModerationActionOutcome> ClaimAsync(
        Guid reportId,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<ModerationActionOutcome> ReleaseAsync(
        Guid reportId,
        Guid actorId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<ModerationActionOutcome> ResolveAsync(
        Guid reportId,
        Guid actorId,
        bool isAdmin,
        ModerationResolutionAction action,
        string note,
        CancellationToken cancellationToken);

    Task<ModerationActionOutcome> ApproveListingAsync(
        Guid listingId,
        Guid actorId,
        CancellationToken cancellationToken);
}

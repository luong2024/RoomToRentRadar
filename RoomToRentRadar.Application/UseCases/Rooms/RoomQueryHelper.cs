using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Rooms;

/// <summary>
/// Shared room visibility and campus resolution rules for room detail and travel-time use cases
/// (BR-LST-04a, BR-CMP-01..03).
/// </summary>
internal static class RoomQueryHelper
{
    public static void EnsureViewable(Room room, string requestedRoomId, ICurrentUserContextPort currentUserContext)
    {
        if (room.Status is ListingStatus.Available or ListingStatus.Rented or ListingStatus.Unpublished)
        {
            return;
        }

        var isOwner = currentUserContext.IsAuthenticated &&
                      string.Equals(currentUserContext.UserId, room.LandlordId, StringComparison.Ordinal);
        var isAdmin = currentUserContext.IsAuthenticated &&
                      string.Equals(currentUserContext.Role, "Admin", StringComparison.OrdinalIgnoreCase);

        if (!isOwner && !isAdmin)
        {
            throw new EntityNotFoundException(
                ProblemCodes.RoomNotFound,
                $"Room with ID '{requestedRoomId}' was not found or is not public.");
        }
    }

    public static async Task<Campus> ResolveCampusAsync(
        string? campusId,
        ICampusRepository campusRepository,
        IUserRepository userRepository,
        ICurrentUserContextPort currentUserContext,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(campusId))
        {
            return await campusRepository.GetByIdAsync(campusId.Trim(), ct).ConfigureAwait(false)
                ?? throw new DomainValidationException(
                    $"Campus with ID '{campusId}' was not found.",
                    ProblemCodes.CampusNotFound);
        }

        if (currentUserContext.IsAuthenticated && !string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            var user = await userRepository.GetByIdAsync(currentUserContext.UserId, ct).ConfigureAwait(false);
            if (user is not null && !string.IsNullOrWhiteSpace(user.SelectedCampusId))
            {
                var profileCampus = await campusRepository.GetByIdAsync(user.SelectedCampusId, ct).ConfigureAwait(false);
                if (profileCampus is not null)
                {
                    return profileCampus;
                }
            }
        }

        return await campusRepository.GetDefaultCampusAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Default campus (ICTU) is not configured.");
    }
}

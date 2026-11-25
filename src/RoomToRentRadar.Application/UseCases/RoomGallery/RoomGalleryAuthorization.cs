using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

internal static class RoomGalleryAuthorization
{
    public static void EnsureCanManageGallery(Room room, Guid actorUserId, bool isAdmin)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (actorUserId == Guid.Empty)
        {
            throw new InvalidRoomPhotoException("Authenticated user ID is required.");
        }

        if (isAdmin)
        {
            return;
        }

        if (!room.LandlordId.HasValue || room.LandlordId.Value != actorUserId)
        {
            throw new RoomGalleryAccessDeniedException(
                $"User '{actorUserId}' is not authorized to manage the gallery for room '{room.Id}'.");
        }
    }
}

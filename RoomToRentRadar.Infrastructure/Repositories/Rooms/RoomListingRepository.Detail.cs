using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T03.1 (US03 — Build Room Detail Data Service)
    // Only T03.1 may edit this file. Override GetByIdAsync and GetSimilarRoomsAsync.
    // Acceptance Criteria:
    //   AC1: Details include description, rent, deposit, area, room type, amenities, address, coordinates, travel times.
    //   AC2: Shows listing status and last-updated date.
    //   AC3: Suggests similar rooms when listing is unavailable.
    //   AC4: Landlord phone number is masked for guests and unmasked only for signed-in users.
    // ============================================================================
    public partial class RoomListingRepository
    {
        /// <summary>
        /// T03.1 (US03): Retrieves full room details by ID (AC1, AC2, AC4).
        /// </summary>
        public override async Task<RoomListing?> GetByIdAsync(int roomId)
        {
            if (Database is null)
            {
                return await base.GetByIdAsync(roomId);
            }

            return await base.GetByIdAsync(roomId);
        }

        /// <summary>
        /// T03.1 (US03): Retrieves similar verified rooms excluding current room (AC3).
        /// </summary>
        public override async Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)
        {
            if (Database is null)
            {
                return await base.GetSimilarRoomsAsync(excludeRoomId, ward, targetRent, limit);
            }

            return await base.GetSimilarRoomsAsync(excludeRoomId, ward, targetRent, limit);
        }
    }
}

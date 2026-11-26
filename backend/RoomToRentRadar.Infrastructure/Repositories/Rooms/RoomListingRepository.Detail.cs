using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T03.1 (US03 — Build Room Detail Data Service)
    // Only T03.1 may edit this file. Override GetByIdAsync and GetSimilarRoomsAsync.
    // Acceptance Criteria (data side):
    //   AC1: the full row is returned (description, rent, deposit, area, room type, amenities, address,
    //        landlord contact, house rules) through the shared MapRoomListing mapper.
    //   AC2: Status and UpdatedAt come with the row.
    //   AC3: GetByIdAsync returns the room whatever its status, so the API can say "no longer available";
    //        GetSimilarRoomsAsync suggests Available + Verified rooms in the same ward with the closest rent.
    //   AC4: the real phone number is returned here; masking for guests is done by the API (RoomDetailController).
    // ============================================================================
    public partial class RoomListingRepository
    {
        private const int MaxSimilarRooms = 20;

        /// <summary>Returns one room by id (any status), or null when it does not exist.</summary>
        public override async Task<RoomListing?> GetByIdAsync(int roomId)
        {
            if (Database is null)
            {
                return await base.GetByIdAsync(roomId);
            }

            if (roomId <= 0)
            {
                return null;
            }

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM RoomListing WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", roomId);

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapRoomListing(reader) : null;
        }

        /// <summary>
        /// AC3: up to <paramref name="limit"/> (1..20) Available + Verified rooms other than
        /// <paramref name="excludeRoomId"/>: same ward first, then the closest monthly rent, then nearest to ICTU.
        /// </summary>
        public override async Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)
        {
            if (Database is null)
            {
                return await base.GetSimilarRoomsAsync(excludeRoomId, ward, targetRent, limit);
            }

            int sanitizedLimit = Math.Clamp(limit, 1, MaxSimilarRooms);

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT *
                FROM RoomListing
                WHERE Status = @Status AND IsVerified = 1 AND Id <> @ExcludeId
                ORDER BY
                    CASE WHEN Ward = @Ward COLLATE NOCASE THEN 0 ELSE 1 END,
                    ABS(MonthlyRent - @TargetRent) ASC,
                    DistanceToCampusKm ASC,
                    Id ASC
                LIMIT @Limit;";

            command.Parameters.AddWithValue("@Status", Constants.ListingStatuses.Available);
            command.Parameters.AddWithValue("@ExcludeId", excludeRoomId);
            command.Parameters.AddWithValue("@Ward", ward?.Trim() ?? string.Empty);
            // decimal is bound as TEXT by Microsoft.Data.Sqlite; use a number so ABS() compares numerically.
            command.Parameters.AddWithValue("@TargetRent", (double)targetRent);
            command.Parameters.AddWithValue("@Limit", sanitizedLimit);

            var results = new List<RoomListing>(sanitizedLimit);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapRoomListing(reader));
            }

            return results;
        }
    }
}

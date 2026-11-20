using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T02.1 (US02 — Build Listing API: pagination & sorting)
    // Only T02.1 may edit this file. Override ListVerifiedRoomsAsync using Database.GetOpenConnectionAsync().
    // Acceptance Criteria:
    //   AC1: Sorted by distance from ICTU campus by default (DistanceAsc); supports PriceAsc / PriceDesc.
    //   AC2: Each card model contains Title, MonthlyRent, Area, DistanceToCampusKm, IsVerified.
    //   AC3: < 2 seconds for up to 500 listings: one indexed-friendly SQL query with LIMIT/OFFSET,
    //        no in-memory cache (a cache would show stale rooms after a status change, breaking AC4).
    //   AC4: Only listings with Status == "Available" AND IsVerified == 1 are returned.
    // ============================================================================
    public partial class RoomListingRepository
    {
        public const string SortDistanceAsc = "DistanceAsc";
        public const string SortPriceAsc = "PriceAsc";
        public const string SortPriceDesc = "PriceDesc";

        private const int MaxPageSize = 100;

        // Whitelisted ORDER BY clauses: sortBy never reaches the SQL text directly.
        // "Id ASC" is the final tie-breaker so paging stays stable when distance and rent are equal.
        private static readonly Dictionary<string, string> ListingOrderBy = new(StringComparer.OrdinalIgnoreCase)
        {
            [SortDistanceAsc] = "ORDER BY DistanceToCampusKm ASC, MonthlyRent ASC, Id ASC",
            [SortPriceAsc] = "ORDER BY MonthlyRent ASC, DistanceToCampusKm ASC, Id ASC",
            [SortPriceDesc] = "ORDER BY MonthlyRent DESC, DistanceToCampusKm ASC, Id ASC",
        };

        /// <summary>
        /// Kept for compatibility with existing callers/tests. The listing query is no longer cached
        /// (stale data after UpdateStatusAsync/SaveListingAsync would violate AC4), so this is a no-op.
        /// </summary>
        public static void InvalidateListingCache()
        {
        }

        /// <summary>
        /// Returns one page of Available + Verified rooms.
        /// pageIndex starts at 0 (negative → 0); pageSize is clamped to 1..100;
        /// sortBy is case-insensitive: DistanceAsc (default), PriceAsc, PriceDesc. Unknown values fall back to DistanceAsc.
        /// </summary>
        public override async Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")
        {
            if (Database is null)
            {
                return await base.ListVerifiedRoomsAsync(pageIndex, pageSize, sortBy);
            }

            int sanitizedPage = Math.Max(0, pageIndex);
            int sanitizedSize = Math.Clamp(pageSize, 1, MaxPageSize);
            long offset = (long)sanitizedPage * sanitizedSize; // long: a huge pageIndex must not overflow to a negative offset

            // AC1: distance from ICTU by default; whitelist lookup keeps SQL injection impossible.
            string orderByClause = ListingOrderBy.TryGetValue(sortBy?.Trim() ?? string.Empty, out var clause)
                ? clause
                : ListingOrderBy[SortDistanceAsc];

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();

            // AC4: filter in SQL; ORDER BY runs before LIMIT/OFFSET, so sorting happens before paging.
            command.CommandText = $@"
                SELECT *
                FROM RoomListing
                WHERE Status = @Status AND IsVerified = 1
                {orderByClause}
                LIMIT @Limit OFFSET @Offset;";

            command.Parameters.AddWithValue("@Status", Constants.ListingStatuses.Available);
            command.Parameters.AddWithValue("@Limit", sanitizedSize);
            command.Parameters.AddWithValue("@Offset", offset);

            var results = new List<RoomListing>(sanitizedSize);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // AC2: shared mapper from RoomListingRepository.cs fills every card field
                // (Title, MonthlyRent, Area, DistanceToCampusKm, IsVerified, ...).
                results.Add(MapRoomListing(reader));
            }

            return results;
        }
    }
}

using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Infrastructure.Persistence.Seed;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Infrastructure.Fakes
{
    /// <summary>
    /// In-memory fallback implementation of <see cref="IRoomListingRepository"/> seeded with <see cref="IctuSeedData.Rooms"/>.
    /// Allows UI tasks (T02.2, T03.2, T05.2, T06.2, T09.1, T11.1) to inject <see cref="IRoomListingRepository"/>
    /// directly via constructor DI and work immediately before Backend tasks merge.
    /// </summary>
    public class InMemoryRoomListingRepository : IRoomListingRepository
    {
        protected readonly List<RoomListing> Rooms = IctuSeedData.Rooms();

        public virtual Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")
        {
            IEnumerable<RoomListing> query = Rooms.Where(r =>
                r.IsVerified && r.Status == Constants.ListingStatuses.Available);

            query = ApplySort(query, sortBy);
            var list = query.Skip(Math.Max(0, pageIndex) * Math.Max(1, pageSize)).Take(Math.Max(1, pageSize)).ToList();
            return Task.FromResult(list);
        }

        public virtual Task<RoomListing?> GetByIdAsync(int roomId)
        {
            var room = Rooms.FirstOrDefault(r => r.Id == roomId);
            return Task.FromResult(room);
        }

        public virtual Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)
        {
            var list = Rooms
                .Where(r => r.Id != excludeRoomId && r.IsVerified && r.Status == Constants.ListingStatuses.Available)
                .OrderBy(r => string.Equals(r.Ward, ward, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(r => Math.Abs(r.MonthlyRent - targetRent))
                .Take(Math.Max(1, limit))
                .ToList();
            return Task.FromResult(list);
        }

        public virtual Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria)
        {
            criteria ??= new RoomFilterCriteria();
            IEnumerable<RoomListing> query = Rooms.Where(r =>
                r.IsVerified && r.Status == Constants.ListingStatuses.Available);

            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var kw = criteria.Keyword.Trim();
                query = query.Where(r =>
                    r.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    r.Address.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    r.Ward.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.MinPrice.HasValue)
                query = query.Where(r => r.MonthlyRent >= criteria.MinPrice.Value);
            if (criteria.MaxPrice.HasValue)
                query = query.Where(r => r.MonthlyRent <= criteria.MaxPrice.Value);
            if (criteria.MinArea.HasValue)
                query = query.Where(r => r.Area >= criteria.MinArea.Value);
            if (criteria.MaxArea.HasValue)
                query = query.Where(r => r.Area <= criteria.MaxArea.Value);
            if (!string.IsNullOrWhiteSpace(criteria.RoomType))
                query = query.Where(r => string.Equals(r.RoomType, criteria.RoomType, StringComparison.OrdinalIgnoreCase));
            if (criteria.MaxDistanceKm.HasValue)
                query = query.Where(r => r.DistanceToCampusKm <= criteria.MaxDistanceKm.Value);

            query = ApplySort(query, criteria.SortBy);
            return Task.FromResult(query.ToList());
        }

        public virtual Task<int> SaveListingAsync(RoomListing listing)
        {
            if (listing.Id <= 0)
            {
                listing.Id = Rooms.Count == 0 ? 1 : Rooms.Max(r => r.Id) + 1;
                listing.Status = Constants.ListingStatuses.PendingReview;
                listing.IsVerified = false;
                Rooms.Add(listing);
            }
            else
            {
                var idx = Rooms.FindIndex(r => r.Id == listing.Id);
                if (idx >= 0)
                    Rooms[idx] = listing;
                else
                    Rooms.Add(listing);
            }

            return Task.FromResult(listing.Id);
        }

        public virtual Task<bool> UpdateStatusAsync(int roomId, string status, bool isVerified)
        {
            var room = Rooms.FirstOrDefault(r => r.Id == roomId);
            if (room is null)
                return Task.FromResult(false);

            room.Status = status;
            room.IsVerified = isVerified;
            return Task.FromResult(true);
        }

        protected static IEnumerable<RoomListing> ApplySort(IEnumerable<RoomListing> query, string? sortBy) =>
            sortBy switch
            {
                "PriceAsc" => query.OrderBy(r => r.MonthlyRent),
                "PriceDesc" => query.OrderByDescending(r => r.MonthlyRent),
                _ => query.OrderBy(r => r.DistanceToCampusKm)
            };
    }
}

using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Infrastructure.Fakes;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IRoomListingRepository"/>.
    /// Split into 4 task-owned partial files so T02.1, T03.1, T05.1, and T09.2 never edit the same file:
    /// <list type="bullet">
    ///   <item><c>RoomListingRepository.Listing.cs</c> — T02.1 (ListVerifiedRoomsAsync)</item>
    ///   <item><c>RoomListingRepository.Detail.cs</c>  — T03.1 (GetByIdAsync, GetSimilarRoomsAsync)</item>
    ///   <item><c>RoomListingRepository.Search.cs</c>  — T05.1 (SearchAsync)</item>
    ///   <item><c>RoomListingRepository.Publish.cs</c> — T09.2 (SaveListingAsync, UpdateStatusAsync)</item>
    /// </list>
    /// Inherits from <see cref="InMemoryRoomListingRepository"/> so any method not yet overridden by its task
    /// still works seamlessly with the seeded ICTU sample data.
    /// </summary>
    public partial class RoomListingRepository : InMemoryRoomListingRepository, IRoomListingRepository
    {
        protected readonly RentalDatabaseService Database;

        public RoomListingRepository(RentalDatabaseService database)
        {
            Database = database;
        }
    }
}

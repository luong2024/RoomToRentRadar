using Microsoft.Data.Sqlite;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Infrastructure.Fakes;
using RoomToRentRadar.Models;

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

        /// <summary>
        /// Shared row mapper for SQLite queries across T02.1, T03.1, T05.1, and T09.2.
        /// Safely converts SQLite REAL/INTEGER/TEXT columns to <see cref="RoomListing"/> properties.
        /// </summary>
        protected static RoomListing MapRoomListing(SqliteDataReader reader) => new()
        {
            Id = Convert.ToInt32(reader["Id"]),
            Title = Convert.ToString(reader["Title"]) ?? string.Empty,
            Description = Convert.ToString(reader["Description"]) ?? string.Empty,
            MonthlyRent = Convert.ToDecimal(reader["MonthlyRent"]),
            Deposit = Convert.ToDecimal(reader["Deposit"]),
            Area = Convert.ToDouble(reader["Area"]),
            RoomType = Convert.ToString(reader["RoomType"]) ?? string.Empty,
            Address = Convert.ToString(reader["Address"]) ?? string.Empty,
            Ward = Convert.ToString(reader["Ward"]) ?? string.Empty,
            Latitude = Convert.ToDouble(reader["Latitude"]),
            Longitude = Convert.ToDouble(reader["Longitude"]),
            DistanceToCampusKm = Convert.ToDouble(reader["DistanceToCampusKm"]),
            MotorbikeMinutes = Convert.ToInt32(reader["MotorbikeMinutes"]),
            BusMinutes = Convert.ToInt32(reader["BusMinutes"]),
            WalkMinutes = Convert.ToInt32(reader["WalkMinutes"]),
            AmenitiesCsv = Convert.ToString(reader["AmenitiesCsv"]) ?? string.Empty,
            ThumbnailUrl = Convert.ToString(reader["ThumbnailUrl"]) ?? "dotnet_bot.png",
            PhotosJson = Convert.ToString(reader["PhotosJson"]) ?? "[]",
            Panorama360Url = Convert.ToString(reader["Panorama360Url"]) ?? string.Empty,
            LandlordId = Convert.ToInt32(reader["LandlordId"]),
            LandlordName = Convert.ToString(reader["LandlordName"]) ?? string.Empty,
            LandlordPhone = Convert.ToString(reader["LandlordPhone"]) ?? string.Empty,
            Status = Convert.ToString(reader["Status"]) ?? Constants.ListingStatuses.Available,
            IsVerified = Convert.ToInt32(reader["IsVerified"]) != 0,
            SecurityScore = Convert.ToDouble(reader["SecurityScore"]),
            QuietHours = Convert.ToString(reader["QuietHours"]) ?? string.Empty,
            GuestPolicy = Convert.ToString(reader["GuestPolicy"]) ?? string.Empty,
            CookingRules = Convert.ToString(reader["CookingRules"]) ?? string.Empty,
            ParkingRules = Convert.ToString(reader["ParkingRules"]) ?? string.Empty,
            RulesVersion = Convert.ToInt32(reader["RulesVersion"]),
            UpdatedAt = Convert.ToString(reader["UpdatedAt"]) ?? string.Empty
        };
    }
}

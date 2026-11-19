using Microsoft.Data.Sqlite;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T03.1 (US03 — Build Room Detail Data Service)
    // Only T03.1 may edit this file. Override GetByIdAsync and GetSimilarRoomsAsync.
    // Acceptance Criteria:
    //   AC1: Details include description, rent, deposit, area, room type, amenities, address, coordinates, travel times.
    //   AC2: The screen shows the listing status and last-updated date.
    //   AC3: If the listing is unavailable, the app shows a message and suggests similar rooms.
    //   AC4: Landlord phone number is masked for guests and unmasked only for signed-in users.
    // ============================================================================
    public partial class RoomListingRepository
    {
        /// <summary>
        /// T03.1 (US03 / Phase 2): Retrieves full room details by ID from SQLite database.
        /// Fulfills AC1 (full metadata, media, travel times, amenities), AC2 (status, updated date),
        /// and AC4 (landlord contact details and guest masking via MaskedLandlordPhone).
        /// </summary>
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
            await using var cmd = connection.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    Id, Title, Description, MonthlyRent, Deposit, Area, RoomType, Address, Ward,
                    Latitude, Longitude, DistanceToCampusKm, MotorbikeMinutes, BusMinutes, WalkMinutes,
                    AmenitiesCsv, ThumbnailUrl, PhotosJson, Panorama360Url,
                    LandlordId, LandlordName, LandlordPhone, Status, IsVerified, SecurityScore,
                    QuietHours, GuestPolicy, CookingRules, ParkingRules, RulesVersion, UpdatedAt
                FROM RoomListing
                WHERE Id = @RoomId
                LIMIT 1;";

            cmd.Parameters.AddWithValue("@RoomId", roomId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return ReadDetailListing(reader);
            }

            return null;
        }

        /// <summary>
        /// T03.1 (US03): Retrieves similar verified rooms excluding current room (AC3).
        /// Baseline delegation to base; fully implemented in Phase 3.
        /// </summary>
        public override async Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)
        {
            if (Database is null)
            {
                return await base.GetSimilarRoomsAsync(excludeRoomId, ward, targetRent, limit);
            }

            return await base.GetSimilarRoomsAsync(excludeRoomId, ward, targetRent, limit);
        }

        private static RoomListing ReadDetailListing(SqliteDataReader reader)
        {
            return new RoomListing
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
}

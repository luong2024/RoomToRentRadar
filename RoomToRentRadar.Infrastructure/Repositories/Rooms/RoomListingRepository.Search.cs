using Microsoft.Data.Sqlite;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T05.1 (US05 — Build search API with price, area, and distance filters)
    // Only T05.1 may edit this file. Override SearchAsync(RoomFilterCriteria criteria).
    // Acceptance Criteria:
    //   AC1: Filters include price range, area range, room type, and maximum distance to campus.
    //   AC2: Filters can be combined and results update within 1.5 seconds.
    //   AC3: The result count and active filters are always visible.
    // ============================================================================
    public partial class RoomListingRepository
    {
        /// <summary>
        /// T05.1 (US05 / Phase 2): Multi-filter search query against SQLite database.
        /// Fulfills AC1 (price, area, room type, distance to ICTU filters) and AC2 (combinable filters, fast execution).
        /// </summary>
        public override async Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria)
        {
            if (Database is null)
            {
                return await base.SearchAsync(criteria);
            }

            criteria ??= new RoomFilterCriteria();

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var cmd = connection.CreateCommand();

            var whereClauses = new List<string>
            {
                "Status = @Status",
                "IsVerified = 1"
            };
            cmd.Parameters.AddWithValue("@Status", Constants.ListingStatuses.Available);

            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                var keyword = $"%{criteria.Keyword.Trim()}%";
                whereClauses.Add("(Title LIKE @Keyword OR Description LIKE @Keyword OR Address LIKE @Keyword OR Ward LIKE @Keyword OR AmenitiesCsv LIKE @Keyword)");
                cmd.Parameters.AddWithValue("@Keyword", keyword);
            }

            if (criteria.MinPrice.HasValue)
            {
                whereClauses.Add("MonthlyRent >= @MinPrice");
                cmd.Parameters.AddWithValue("@MinPrice", criteria.MinPrice.Value);
            }

            if (criteria.MaxPrice.HasValue)
            {
                whereClauses.Add("MonthlyRent <= @MaxPrice");
                cmd.Parameters.AddWithValue("@MaxPrice", criteria.MaxPrice.Value);
            }

            if (criteria.MinArea.HasValue)
            {
                whereClauses.Add("Area >= @MinArea");
                cmd.Parameters.AddWithValue("@MinArea", criteria.MinArea.Value);
            }

            if (criteria.MaxArea.HasValue)
            {
                whereClauses.Add("Area <= @MaxArea");
                cmd.Parameters.AddWithValue("@MaxArea", criteria.MaxArea.Value);
            }

            if (!string.IsNullOrWhiteSpace(criteria.RoomType) && !string.Equals(criteria.RoomType, "Tất cả", StringComparison.OrdinalIgnoreCase))
            {
                whereClauses.Add("RoomType = @RoomType COLLATE NOCASE");
                cmd.Parameters.AddWithValue("@RoomType", criteria.RoomType.Trim());
            }

            if (criteria.MaxDistanceKm.HasValue)
            {
                whereClauses.Add("DistanceToCampusKm <= @MaxDistanceKm");
                cmd.Parameters.AddWithValue("@MaxDistanceKm", criteria.MaxDistanceKm.Value);
            }

            string orderByClause = criteria.SortBy switch
            {
                "PriceAsc" => "ORDER BY MonthlyRent ASC, DistanceToCampusKm ASC",
                "PriceDesc" => "ORDER BY MonthlyRent DESC, DistanceToCampusKm ASC",
                _ => "ORDER BY DistanceToCampusKm ASC, MonthlyRent ASC"
            };

            string whereCondition = string.Join(" AND ", whereClauses);
            cmd.CommandText = $@"
                SELECT 
                    Id, Title, Description, MonthlyRent, Deposit, Area, RoomType, Address, Ward,
                    Latitude, Longitude, DistanceToCampusKm, MotorbikeMinutes, BusMinutes, WalkMinutes,
                    AmenitiesCsv, ThumbnailUrl, PhotosJson, Panorama360Url,
                    LandlordId, LandlordName, LandlordPhone, Status, IsVerified, SecurityScore,
                    QuietHours, GuestPolicy, CookingRules, ParkingRules, RulesVersion, UpdatedAt
                FROM RoomListing
                WHERE {whereCondition}
                {orderByClause};";

            var results = new List<RoomListing>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(ReadSearchListing(reader));
            }

            return results;
        }

        private static RoomListing ReadSearchListing(SqliteDataReader reader)
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

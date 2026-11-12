using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    // ============================================================================
    // TASK OWNER: T02.1 (US02 — Build Listing API: pagination & sorting)
    // Only T02.1 may edit this file. Override ListVerifiedRoomsAsync using Database.GetOpenConnectionAsync().
    // Acceptance Criteria:
    //   AC1: Sorted by distance from ICTU campus by default (DistanceAsc); supports PriceAsc / PriceDesc.
    //   AC2: Each card model contains Title, MonthlyRent, Area, DistanceToCampusKm, IsVerified.
    //   AC3: High-performance read-heavy execution (< 2 seconds for up to 500 listings) with in-memory caching.
    //   AC4: Only listings with Status == "Available" AND IsVerified == 1 are returned.
    // ============================================================================
    public partial class RoomListingRepository
    {
        private static readonly ConcurrentDictionary<string, (DateTime ExpiresAt, List<RoomListing> Items)> ListingCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Clears the in-memory cache for verified room listings.
        /// </summary>
        public static void InvalidateListingCache()
        {
            ListingCache.Clear();
        }

        public override async Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")
        {
            if (Database is null)
            {
                return await base.ListVerifiedRoomsAsync(pageIndex, pageSize, sortBy);
            }

            int sanitizedPage = Math.Max(0, pageIndex);
            int sanitizedSize = Math.Max(1, Math.Min(100, pageSize));
            int offset = sanitizedPage * sanitizedSize;
            string effectiveSort = string.IsNullOrWhiteSpace(sortBy) ? "DistanceAsc" : sortBy.Trim();

            // AC3: Fast cache lookup for read-heavy listing screen
            string cacheKey = $"{sanitizedPage}_{sanitizedSize}_{effectiveSort}";
            if (ListingCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            {
                return cached.Items.Select(CloneListing).ToList();
            }

            await using var connection = await Database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();

            // AC1: Sort by distance from ICTU campus by default; support price sorting
            string orderByClause = effectiveSort switch
            {
                "PriceAsc" => "ORDER BY MonthlyRent ASC, DistanceToCampusKm ASC",
                "PriceDesc" => "ORDER BY MonthlyRent DESC, DistanceToCampusKm ASC",
                _ => "ORDER BY DistanceToCampusKm ASC, MonthlyRent ASC"
            };

            // AC4: Only listings with status Available and Verified are returned
            command.CommandText = $@"
                SELECT 
                    Id, Title, Description, MonthlyRent, Deposit, Area, RoomType, Address, Ward,
                    Latitude, Longitude, DistanceToCampusKm, MotorbikeMinutes, BusMinutes, WalkMinutes,
                    AmenitiesCsv, ThumbnailUrl, PhotosJson, Panorama360Url,
                    LandlordId, LandlordName, LandlordPhone, Status, IsVerified, SecurityScore,
                    QuietHours, GuestPolicy, CookingRules, ParkingRules, RulesVersion, UpdatedAt
                FROM RoomListing
                WHERE Status = @Status AND IsVerified = 1
                {orderByClause}
                LIMIT @Limit OFFSET @Offset;";

            command.Parameters.AddWithValue("@Status", Constants.ListingStatuses.Available);
            command.Parameters.AddWithValue("@Limit", sanitizedSize);
            command.Parameters.AddWithValue("@Offset", offset);

            var results = new List<RoomListing>(sanitizedSize);
            await using var reader = await command.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                ListingCache[cacheKey] = (DateTime.UtcNow.Add(CacheDuration), results);
                return results;
            }

            // AC3: Cache ordinals outside the row loop for fast mapping (< 2s for 500+ records)
            int idOrd = reader.GetOrdinal("Id");
            int titleOrd = reader.GetOrdinal("Title");
            int descOrd = reader.GetOrdinal("Description");
            int rentOrd = reader.GetOrdinal("MonthlyRent");
            int depositOrd = reader.GetOrdinal("Deposit");
            int areaOrd = reader.GetOrdinal("Area");
            int typeOrd = reader.GetOrdinal("RoomType");
            int addrOrd = reader.GetOrdinal("Address");
            int wardOrd = reader.GetOrdinal("Ward");
            int latOrd = reader.GetOrdinal("Latitude");
            int lngOrd = reader.GetOrdinal("Longitude");
            int distOrd = reader.GetOrdinal("DistanceToCampusKm");
            int motoOrd = reader.GetOrdinal("MotorbikeMinutes");
            int busOrd = reader.GetOrdinal("BusMinutes");
            int walkOrd = reader.GetOrdinal("WalkMinutes");
            int amenOrd = reader.GetOrdinal("AmenitiesCsv");
            int thumbOrd = reader.GetOrdinal("ThumbnailUrl");
            int photoOrd = reader.GetOrdinal("PhotosJson");
            int panoOrd = reader.GetOrdinal("Panorama360Url");
            int lIdOrd = reader.GetOrdinal("LandlordId");
            int lNameOrd = reader.GetOrdinal("LandlordName");
            int lPhoneOrd = reader.GetOrdinal("LandlordPhone");
            int statusOrd = reader.GetOrdinal("Status");
            int verOrd = reader.GetOrdinal("IsVerified");
            int secOrd = reader.GetOrdinal("SecurityScore");
            int quietOrd = reader.GetOrdinal("QuietHours");
            int guestOrd = reader.GetOrdinal("GuestPolicy");
            int cookOrd = reader.GetOrdinal("CookingRules");
            int parkOrd = reader.GetOrdinal("ParkingRules");
            int rulesVerOrd = reader.GetOrdinal("RulesVersion");
            int updatedOrd = reader.GetOrdinal("UpdatedAt");

            while (await reader.ReadAsync())
            {
                // AC2: Map all card fields (title, rent, area, distance, verified, etc.)
                var item = new RoomListing
                {
                    Id = reader.GetInt32(idOrd),
                    Title = reader.GetString(titleOrd),
                    Description = reader.GetString(descOrd),
                    MonthlyRent = Convert.ToDecimal(reader.GetDouble(rentOrd)),
                    Deposit = Convert.ToDecimal(reader.GetDouble(depositOrd)),
                    Area = reader.GetDouble(areaOrd),
                    RoomType = reader.GetString(typeOrd),
                    Address = reader.GetString(addrOrd),
                    Ward = reader.GetString(wardOrd),
                    Latitude = reader.GetDouble(latOrd),
                    Longitude = reader.GetDouble(lngOrd),
                    DistanceToCampusKm = reader.GetDouble(distOrd),
                    MotorbikeMinutes = reader.GetInt32(motoOrd),
                    BusMinutes = reader.GetInt32(busOrd),
                    WalkMinutes = reader.GetInt32(walkOrd),
                    AmenitiesCsv = reader.GetString(amenOrd),
                    ThumbnailUrl = reader.GetString(thumbOrd),
                    PhotosJson = reader.GetString(photoOrd),
                    Panorama360Url = reader.GetString(panoOrd),
                    LandlordId = reader.GetInt32(lIdOrd),
                    LandlordName = reader.GetString(lNameOrd),
                    LandlordPhone = reader.GetString(lPhoneOrd),
                    Status = reader.GetString(statusOrd),
                    IsVerified = reader.GetInt32(verOrd) == 1,
                    SecurityScore = reader.GetDouble(secOrd),
                    QuietHours = reader.GetString(quietOrd),
                    GuestPolicy = reader.GetString(guestOrd),
                    CookingRules = reader.GetString(cookOrd),
                    ParkingRules = reader.GetString(parkOrd),
                    RulesVersion = reader.GetInt32(rulesVerOrd),
                    UpdatedAt = reader.GetString(updatedOrd)
                };
                results.Add(item);
            }

            ListingCache[cacheKey] = (DateTime.UtcNow.Add(CacheDuration), results);
            return results.Select(CloneListing).ToList();
        }

        private static RoomListing CloneListing(RoomListing original)
        {
            return new RoomListing
            {
                Id = original.Id,
                Title = original.Title,
                Description = original.Description,
                MonthlyRent = original.MonthlyRent,
                Deposit = original.Deposit,
                Area = original.Area,
                RoomType = original.RoomType,
                Address = original.Address,
                Ward = original.Ward,
                Latitude = original.Latitude,
                Longitude = original.Longitude,
                DistanceToCampusKm = original.DistanceToCampusKm,
                MotorbikeMinutes = original.MotorbikeMinutes,
                BusMinutes = original.BusMinutes,
                WalkMinutes = original.WalkMinutes,
                AmenitiesCsv = original.AmenitiesCsv,
                ThumbnailUrl = original.ThumbnailUrl,
                PhotosJson = original.PhotosJson,
                Panorama360Url = original.Panorama360Url,
                LandlordId = original.LandlordId,
                LandlordName = original.LandlordName,
                LandlordPhone = original.LandlordPhone,
                Status = original.Status,
                IsVerified = original.IsVerified,
                SecurityScore = original.SecurityScore,
                QuietHours = original.QuietHours,
                GuestPolicy = original.GuestPolicy,
                CookingRules = original.CookingRules,
                ParkingRules = original.ParkingRules,
                RulesVersion = original.RulesVersion,
                UpdatedAt = original.UpdatedAt,
                IsFavorite = original.IsFavorite
            };
        }
    }
}

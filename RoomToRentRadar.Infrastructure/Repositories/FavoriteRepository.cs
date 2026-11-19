using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Models;
using RoomToRentRadar.Abstractions.Favorites;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// Local SQLite favorites for US07 / T07.1 (consumed by T02.2, T03.2, T07.2).
    /// Uses the shared FavoriteRoom table from RentalDatabaseService as-is:
    /// - "Unfavorite" deletes the row (the table has no soft-delete column).
    /// - The table has no UNIQUE(UserId, RoomId), so duplicates are prevented here
    ///   with a lock + check-then-insert inside one transaction.
    /// Cross-device sync (AC2) is NOT implemented: the schema has no sync columns and the app has no API token yet.
    /// </summary>
    public class FavoriteRepository : IFavoriteRepository
    {
        private readonly RentalDatabaseService _database;
        private readonly ILogger<FavoriteRepository> _logger;

        // Serializes writes so two quick taps on the heart cannot insert the same pair twice.
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public FavoriteRepository(RentalDatabaseService database, ILogger<FavoriteRepository> logger)
        {
            _database = database;
            _logger = logger;
        }

        /// <summary>
        /// Returns the user's favorite rooms, newest first, with full RoomListing data
        /// (MonthlyRent and Status for AC3). Rooms of every status are included so the UI can show "Unavailable".
        /// </summary>
        public async Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId)
        {
            var rooms = new List<RoomListing>();
            if (userId <= 0)
            {
                return rooms;
            }

            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            // GROUP BY collapses any legacy duplicate rows for the same room.
            command.CommandText = @"
                SELECT r.*
                FROM FavoriteRoom f
                INNER JOIN RoomListing r ON r.Id = f.RoomId
                WHERE f.UserId = @userId
                GROUP BY r.Id
                ORDER BY MAX(f.CreatedAt) DESC;";
            command.Parameters.AddWithValue("@userId", userId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var room = MapRoom(reader);
                room.IsFavorite = true;
                rooms.Add(room);
            }

            return rooms;
        }

        /// <summary>
        /// Adds the room to favorites. Idempotent: returns true if the room is a favorite afterwards
        /// (including when it already was). Returns false for invalid ids or a room that does not exist.
        /// </summary>
        public async Task<bool> AddFavoriteAsync(int userId, int roomId)
        {
            if (userId <= 0 || roomId <= 0)
            {
                return false;
            }

            await _writeLock.WaitAsync();
            try
            {
                await using var connection = await _database.GetOpenConnectionAsync();
                await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

                if (!await RoomExistsAsync(connection, transaction, roomId))
                {
                    _logger.LogInformation("Add favorite rejected: room {RoomId} does not exist.", roomId);
                    return false;
                }

                if (await FavoriteExistsAsync(connection, transaction, userId, roomId))
                {
                    return true; // already a favorite: do not insert a second row
                }

                await using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText =
                        "INSERT INTO FavoriteRoom (UserId, RoomId, CreatedAt) VALUES (@userId, @roomId, @createdAt);";
                    insert.Parameters.AddWithValue("@userId", userId);
                    insert.Parameters.AddWithValue("@roomId", roomId);
                    insert.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
                    await insert.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("User {UserId} added room {RoomId} to favorites.", userId, roomId);
                return true;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Failed to add room {RoomId} to favorites of user {UserId}.", roomId, userId);
                throw; // let the caller show a friendly error; never hide database failures
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Removes the room from favorites (hard delete). Returns true if something was removed,
        /// false if it was not a favorite.
        /// </summary>
        public async Task<bool> RemoveFavoriteAsync(int userId, int roomId)
        {
            if (userId <= 0 || roomId <= 0)
            {
                return false;
            }

            await _writeLock.WaitAsync();
            try
            {
                await using var connection = await _database.GetOpenConnectionAsync();
                await using var command = connection.CreateCommand();
                // Deletes every row of the pair, which also cleans up any legacy duplicates.
                command.CommandText = "DELETE FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId;";
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@roomId", roomId);

                var removed = await command.ExecuteNonQueryAsync() > 0;
                if (removed)
                {
                    _logger.LogInformation("User {UserId} removed room {RoomId} from favorites.", userId, roomId);
                }
                return removed;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Failed to remove room {RoomId} from favorites of user {UserId}.", roomId, userId);
                throw;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Flips the favorite state. Returns true if the room IS a favorite after the call,
        /// false if it was just removed (or the ids/room are invalid), so the heart icon can update immediately (AC1).
        /// </summary>
        public async Task<bool> ToggleFavoriteAsync(int userId, int roomId)
        {
            if (userId <= 0 || roomId <= 0)
            {
                return false;
            }

            bool isFavorite;
            await using (var connection = await _database.GetOpenConnectionAsync())
            {
                isFavorite = await FavoriteExistsAsync(connection, null, userId, roomId);
            }

            // Add/Remove take the write lock themselves; a race between the check and the write
            // only means the second tap wins, and duplicates are still prevented by AddFavoriteAsync.
            if (isFavorite)
            {
                await RemoveFavoriteAsync(userId, roomId);
                return false;
            }

            return await AddFavoriteAsync(userId, roomId);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static async Task<bool> RoomExistsAsync(SqliteConnection connection, SqliteTransaction? transaction, int roomId)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(1) FROM RoomListing WHERE Id = @roomId;";
            command.Parameters.AddWithValue("@roomId", roomId);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static async Task<bool> FavoriteExistsAsync(
            SqliteConnection connection, SqliteTransaction? transaction, int userId, int roomId)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "SELECT COUNT(1) FROM FavoriteRoom WHERE UserId = @userId AND RoomId = @roomId;";
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@roomId", roomId);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        /// <summary>Maps a RoomListing row by column name, so it does not depend on column order.</summary>
        private static RoomListing MapRoom(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            Title = r.GetString(r.GetOrdinal("Title")),
            Description = r.GetString(r.GetOrdinal("Description")),
            MonthlyRent = (decimal)r.GetDouble(r.GetOrdinal("MonthlyRent")),
            Deposit = (decimal)r.GetDouble(r.GetOrdinal("Deposit")),
            Area = r.GetDouble(r.GetOrdinal("Area")),
            RoomType = r.GetString(r.GetOrdinal("RoomType")),
            Address = r.GetString(r.GetOrdinal("Address")),
            Ward = r.GetString(r.GetOrdinal("Ward")),
            Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
            Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
            DistanceToCampusKm = r.GetDouble(r.GetOrdinal("DistanceToCampusKm")),
            MotorbikeMinutes = r.GetInt32(r.GetOrdinal("MotorbikeMinutes")),
            BusMinutes = r.GetInt32(r.GetOrdinal("BusMinutes")),
            WalkMinutes = r.GetInt32(r.GetOrdinal("WalkMinutes")),
            AmenitiesCsv = r.GetString(r.GetOrdinal("AmenitiesCsv")),
            ThumbnailUrl = r.GetString(r.GetOrdinal("ThumbnailUrl")),
            PhotosJson = r.GetString(r.GetOrdinal("PhotosJson")),
            Panorama360Url = r.GetString(r.GetOrdinal("Panorama360Url")),
            LandlordId = r.GetInt32(r.GetOrdinal("LandlordId")),
            LandlordName = r.GetString(r.GetOrdinal("LandlordName")),
            LandlordPhone = r.GetString(r.GetOrdinal("LandlordPhone")),
            Status = r.GetString(r.GetOrdinal("Status")),
            IsVerified = r.GetInt64(r.GetOrdinal("IsVerified")) != 0,
            SecurityScore = r.GetDouble(r.GetOrdinal("SecurityScore")),
            QuietHours = r.GetString(r.GetOrdinal("QuietHours")),
            GuestPolicy = r.GetString(r.GetOrdinal("GuestPolicy")),
            CookingRules = r.GetString(r.GetOrdinal("CookingRules")),
            ParkingRules = r.GetString(r.GetOrdinal("ParkingRules")),
            RulesVersion = r.GetInt32(r.GetOrdinal("RulesVersion")),
            UpdatedAt = r.GetString(r.GetOrdinal("UpdatedAt")),
        };
    }
}

using Microsoft.Data.Sqlite;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Data
{
    /// <summary>
    /// Data access for the UserAccount table (US01 / T01.1).
    /// The table schema and demo users are created by RentalDatabaseService; this class only queries it.
    /// All SQL uses parameters to prevent SQL injection.
    /// </summary>
    public class UserRepository
    {
        private const string SelectColumns =
            "Id, FullName, Email, PasswordHash, Phone, Role, SelectedCampus, CreatedAt";

        private readonly RentalDatabaseService _database;

        public UserRepository(RentalDatabaseService database)
        {
            _database = database;
        }

        /// <summary>Finds a user by email (case-insensitive). Returns null if not found.</summary>
        public async Task<UserAccount?> GetByEmailAsync(string email)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT {SelectColumns} FROM UserAccount WHERE Email = @email COLLATE NOCASE LIMIT 1;";
            command.Parameters.AddWithValue("@email", email.Trim());

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapUser(reader) : null;
        }

        /// <summary>Finds a user by id. Returns null if not found.</summary>
        public async Task<UserAccount?> GetByIdAsync(int id)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM UserAccount WHERE Id = @id LIMIT 1;";
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapUser(reader) : null;
        }

        /// <summary>True if an account with this email already exists (case-insensitive).</summary>
        public async Task<bool> EmailExistsAsync(string email)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(1) FROM UserAccount WHERE Email = @email COLLATE NOCASE;";
            command.Parameters.AddWithValue("@email", email.Trim());

            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        /// <summary>
        /// Inserts a new user and returns the generated Id (also written back to user.Id).
        /// PasswordHash must already be hashed by AuthService; this class never sees plain passwords.
        /// </summary>
        public async Task<int> InsertAsync(UserAccount user)
        {
            await using var connection = await _database.GetOpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO UserAccount (FullName, Email, PasswordHash, Phone, Role, SelectedCampus, CreatedAt)
                VALUES (@fullName, @email, @passwordHash, @phone, @role, @campus, @createdAt);
                SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@fullName", user.FullName);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            command.Parameters.AddWithValue("@phone", user.Phone);
            command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@campus", user.SelectedCampus);
            command.Parameters.AddWithValue("@createdAt", user.CreatedAt);

            user.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
            return user.Id;
        }

        private static UserAccount MapUser(SqliteDataReader reader) => new()
        {
            Id = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Phone = reader.GetString(4),
            Role = reader.GetString(5),
            SelectedCampus = reader.GetString(6),
            CreatedAt = reader.GetString(7),
        };
    }
}

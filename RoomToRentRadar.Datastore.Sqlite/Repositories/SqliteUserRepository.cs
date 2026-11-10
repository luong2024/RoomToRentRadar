using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteUserRepository : IUserRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteUserRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            return await ReadUserByPredicateAsync(conn, "WHERE Id = @id", ("@id", id), ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var normalized = normalizedEmail.Trim().ToLowerInvariant();
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            return await ReadUserByPredicateAsync(conn, "WHERE NormalizedEmail = @email", ("@email", normalized), ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT UserId FROM UserRefreshTokens WHERE TokenHash = @tokenHash LIMIT 1;";
            cmd.Parameters.AddWithValue("@tokenHash", refreshTokenHash);

            var userIdObj = await cmd.ExecuteScalarAsync(ct);
            if (userIdObj is not string userId || string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await ReadUserByPredicateAsync(conn, "WHERE Id = @id", ("@id", userId), ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var normalized = normalizedEmail.Trim().ToLowerInvariant();
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Users WHERE NormalizedEmail = @email;";
            cmd.Parameters.AddWithValue("@email", normalized);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            return count > 0;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO Users (
                        Id, Email, NormalizedEmail, FullName, PasswordHash, Role, AuthProvider,
                        SsoSubjectId, PhoneNumber, StudentCode, IsVerified, FailedLoginCount,
                        LockoutEndUtc, CreatedAtUtc, UpdatedAtUtc
                    ) VALUES (
                        @id, @email, @normalizedEmail, @fullName, @passwordHash, @role, @authProvider,
                        @ssoSubjectId, @phoneNumber, @studentCode, @isVerified, @failedLoginCount,
                        @lockoutEndUtc, @createdAtUtc, @updatedAtUtc
                    );
                    """;
                BindUserParameters(cmd, user);
                await cmd.ExecuteNonQueryAsync(ct);

                await ReplaceRefreshTokensAsync(conn, tx, user, ct);
                await tx.CommitAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                await tx.RollbackAsync(ct);
                throw new DuplicateEmailException(user.Email.Value);
            }
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE Users SET
                    Email = @email,
                    NormalizedEmail = @normalizedEmail,
                    FullName = @fullName,
                    PasswordHash = @passwordHash,
                    Role = @role,
                    AuthProvider = @authProvider,
                    SsoSubjectId = @ssoSubjectId,
                    PhoneNumber = @phoneNumber,
                    StudentCode = @studentCode,
                    IsVerified = @isVerified,
                    FailedLoginCount = @failedLoginCount,
                    LockoutEndUtc = @lockoutEndUtc,
                    UpdatedAtUtc = @updatedAtUtc
                WHERE Id = @id;
                """;
            BindUserParameters(cmd, user);
            await cmd.ExecuteNonQueryAsync(ct);

            await ReplaceRefreshTokensAsync(conn, tx, user, ct);
            await tx.CommitAsync(ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static async Task<User?> ReadUserByPredicateAsync(
        SqliteConnection conn,
        string whereClause,
        (string Name, object Value) parameter,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT Id, Email, FullName, PasswordHash, Role, AuthProvider, SsoSubjectId,
                   PhoneNumber, StudentCode, IsVerified, FailedLoginCount, LockoutEndUtc,
                   CreatedAtUtc, UpdatedAtUtc
            FROM Users
            {whereClause}
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var id = reader.GetString(0);
        var email = reader.GetString(1);
        var fullName = reader.GetString(2);
        var passwordHash = reader.IsDBNull(3) ? null : reader.GetString(3);
        var role = Enum.Parse<UserRole>(reader.GetString(4), ignoreCase: true);
        var authProvider = Enum.Parse<AuthProvider>(reader.GetString(5), ignoreCase: true);
        var ssoSubjectId = reader.IsDBNull(6) ? null : reader.GetString(6);
        var phoneNumber = reader.IsDBNull(7) ? null : reader.GetString(7);
        var studentCode = reader.IsDBNull(8) ? null : reader.GetString(8);
        var isVerified = reader.GetInt64(9) != 0;
        var failedLoginCount = reader.GetInt32(10);
        var lockoutEndUtc = reader.IsDBNull(11) ? (DateTime?)null : ParseUtc(reader.GetString(11));
        var createdAtUtc = ParseUtc(reader.GetString(12));
        var updatedAtUtc = ParseUtc(reader.GetString(13));
        await reader.CloseAsync();

        var tokens = await ReadRefreshTokensAsync(conn, id, ct);

        return User.Reconstitute(
            id: id,
            email: email,
            fullName: fullName,
            passwordHash: passwordHash,
            role: role,
            authProvider: authProvider,
            ssoSubjectId: ssoSubjectId,
            phoneNumber: phoneNumber,
            studentCode: studentCode,
            isVerified: isVerified,
            failedLoginCount: failedLoginCount,
            lockoutEndUtc: lockoutEndUtc,
            createdAtUtc: createdAtUtc,
            updatedAtUtc: updatedAtUtc,
            refreshTokens: tokens);
    }

    private static async Task<List<RefreshTokenSession>> ReadRefreshTokensAsync(
        SqliteConnection conn,
        string userId,
        CancellationToken ct)
    {
        var tokens = new List<RefreshTokenSession>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TokenHash, JwtId, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenHash
            FROM UserRefreshTokens
            WHERE UserId = @userId
            ORDER BY CreatedAtUtc ASC;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tokens.Add(new RefreshTokenSession(
                tokenHash: reader.GetString(0),
                jwtId: reader.GetString(1),
                createdAtUtc: ParseUtc(reader.GetString(2)),
                expiresAtUtc: ParseUtc(reader.GetString(3)),
                revokedAtUtc: reader.IsDBNull(4) ? null : ParseUtc(reader.GetString(4)),
                replacedByTokenHash: reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return tokens;
    }

    private static async Task ReplaceRefreshTokensAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        User user,
        CancellationToken ct)
    {
        await using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = "DELETE FROM UserRefreshTokens WHERE UserId = @userId;";
            deleteCmd.Parameters.AddWithValue("@userId", user.Id);
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var token in user.RefreshTokens)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT INTO UserRefreshTokens (
                    TokenHash, UserId, JwtId, CreatedAtUtc, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenHash
                ) VALUES (
                    @tokenHash, @userId, @jwtId, @createdAtUtc, @expiresAtUtc, @revokedAtUtc, @replacedByTokenHash
                );
                """;
            insertCmd.Parameters.AddWithValue("@tokenHash", token.TokenHash);
            insertCmd.Parameters.AddWithValue("@userId", user.Id);
            insertCmd.Parameters.AddWithValue("@jwtId", token.JwtId);
            insertCmd.Parameters.AddWithValue("@createdAtUtc", token.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            insertCmd.Parameters.AddWithValue("@expiresAtUtc", token.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
            insertCmd.Parameters.AddWithValue("@revokedAtUtc", token.RevokedAtUtc.HasValue
                ? token.RevokedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);
            insertCmd.Parameters.AddWithValue("@replacedByTokenHash", (object?)token.ReplacedByTokenHash ?? DBNull.Value);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static void BindUserParameters(SqliteCommand cmd, User user)
    {
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@email", user.Email.Value);
        cmd.Parameters.AddWithValue("@normalizedEmail", user.Email.Value);
        cmd.Parameters.AddWithValue("@fullName", user.FullName);
        cmd.Parameters.AddWithValue("@passwordHash", (object?)user.PasswordHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@role", user.Role.ToString());
        cmd.Parameters.AddWithValue("@authProvider", user.AuthProvider.ToString());
        cmd.Parameters.AddWithValue("@ssoSubjectId", (object?)user.SsoSubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phoneNumber", (object?)user.PhoneNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@studentCode", (object?)user.StudentCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isVerified", user.IsVerified ? 1 : 0);
        cmd.Parameters.AddWithValue("@failedLoginCount", user.FailedLoginCount);
        cmd.Parameters.AddWithValue("@lockoutEndUtc", user.LockoutEndUtc.HasValue
            ? user.LockoutEndUtc.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAtUtc", user.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@updatedAtUtc", user.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}


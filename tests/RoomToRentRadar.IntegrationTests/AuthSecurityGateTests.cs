using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace RoomToRentRadar.IntegrationTests;

/// <summary>
/// Isolated authentication security test suite validating refresh-token rotation atomicity,
/// concurrency/idempotency handling, compromised family revocation, token expiration, and
/// constant-time cryptographic hash comparison (Technical Spec Section 2 &amp; Section 3).
/// </summary>
[Category("AuthSecurity")]
public sealed class AuthSecurityGateTests
{
    private string _dbPath = string.Empty;
    private string _connectionString = string.Empty;

    [SetUp]
    public async Task SetUpAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        _dbPath = Path.Combine(Path.GetTempPath(), $"roomradar_authsec_{Guid.NewGuid():N}.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();

        await InitializeDatabaseSchemaAsync(_connectionString);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfExists(_dbPath);
        DeleteIfExists($"{_dbPath}-wal");
        DeleteIfExists($"{_dbPath}-shm");
    }

    [Test]
    public async Task RotateRefreshToken_WhenTokenIsValid_ShouldPersistOnlyHashAndIssueSingleReplacement()
    {
        var store = new AtomicRefreshTokenRotationStore(_connectionString);
        var userId = Guid.NewGuid();
        var utcNow = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

        var issued = await store.IssueInitialTokenAsync(userId, "127.0.0.1", utcNow);
        var storedRecord = await store.FindByTokenHashAsync(issued.TokenHash);

        Assert.Multiple(() =>
        {
            Assert.That(issued.RawToken, Is.Not.Empty);
            Assert.That(issued.TokenHash, Is.Not.EqualTo(issued.RawToken));
            Assert.That(storedRecord, Is.Not.Null);
            Assert.That(storedRecord!.TokenHash, Is.EqualTo(issued.TokenHash));
            Assert.That(storedRecord.RevokedAt, Is.Null);
        });

        var rotated = await store.RotateAsync(
            issued.RawToken,
            idempotencyKey: "req-rotate-1",
            ipAddress: "127.0.0.1",
            utcNow: utcNow.AddMinutes(5));

        var parentAfterRotate = await store.FindByTokenHashAsync(issued.TokenHash);
        var childAfterRotate = await store.FindByTokenHashAsync(rotated.ReplacementTokenHash!);

        Assert.Multiple(() =>
        {
            Assert.That(rotated.Succeeded, Is.True);
            Assert.That(rotated.ReplacementRawToken, Is.Not.Null.And.Not.EqualTo(issued.RawToken));
            Assert.That(parentAfterRotate!.RevokedAt, Is.EqualTo(utcNow.AddMinutes(5)));
            Assert.That(parentAfterRotate.ReplacedByTokenHash, Is.EqualTo(rotated.ReplacementTokenHash));
            Assert.That(childAfterRotate, Is.Not.Null);
            Assert.That(childAfterRotate!.FamilyId, Is.EqualTo(issued.FamilyId));
            Assert.That(childAfterRotate.RevokedAt, Is.Null);
        });
    }

    [Test]
    public async Task RotateRefreshToken_WhenExpiredOrUnknown_ShouldRejectWithoutCreatingReplacement()
    {
        var store = new AtomicRefreshTokenRotationStore(_connectionString);
        var userId = Guid.NewGuid();
        var utcNow = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

        var issued = await store.IssueInitialTokenAsync(userId, "127.0.0.1", utcNow, lifetime: TimeSpan.FromMinutes(10));

        var expiredResult = await store.RotateAsync(
            issued.RawToken,
            idempotencyKey: "req-expired",
            ipAddress: "127.0.0.1",
            utcNow: utcNow.AddMinutes(15));

        var unknownResult = await store.RotateAsync(
            "non-existent-raw-refresh-token",
            idempotencyKey: "req-unknown",
            ipAddress: "127.0.0.1",
            utcNow: utcNow.AddMinutes(1));

        var familyCount = await store.CountTokensInFamilyAsync(issued.FamilyId);

        Assert.Multiple(() =>
        {
            Assert.That(expiredResult.Succeeded, Is.False);
            Assert.That(expiredResult.FailureReason, Is.EqualTo("TokenExpired"));
            Assert.That(unknownResult.Succeeded, Is.False);
            Assert.That(unknownResult.FailureReason, Is.EqualTo("TokenNotFound"));
            Assert.That(familyCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RotateRefreshToken_WhenConcurrentRequestsAndIdempotentRetry_ShouldNeverForkFamilyOrFalselyRevokeOnRetry()
    {
        var store = new AtomicRefreshTokenRotationStore(_connectionString);
        var userId = Guid.NewGuid();
        var utcNow = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

        var issued = await store.IssueInitialTokenAsync(userId, "127.0.0.1", utcNow);

        // 1. Concurrent race with distinct requests: exactly one consumes the token and no second branch is created.
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(1, 6).Select(async workerId =>
        {
            await barrier.Task;
            return await store.RotateAsync(
                issued.RawToken,
                idempotencyKey: $"concurrent-worker-{workerId}",
                ipAddress: "10.0.0.1",
                utcNow: utcNow.AddSeconds(2));
        }).ToArray();

        barrier.SetResult();
        var results = await Task.WhenAll(tasks);

        var winners = results.Where(r => r.Succeeded).ToList();
        var activeTokens = await store.CountActiveTokensInFamilyAsync(issued.FamilyId);
        var totalTokens = await store.CountTokensInFamilyAsync(issued.FamilyId);

        Assert.Multiple(() =>
        {
            Assert.That(winners, Has.Count.EqualTo(1));
            Assert.That(totalTokens, Is.EqualTo(2));
            Assert.That(activeTokens, Is.EqualTo(1));
        });

        // 2. Retry with the SAME idempotency key within grace window (commit ambiguity recovery) returns consistent tokenhash without revoking family.
        var winnerKey = results
            .Select((r, idx) => (Result: r, Key: $"concurrent-worker-{idx + 1}"))
            .Single(x => x.Result.Succeeded).Key;

        var retryResult = await store.RotateAsync(
            issued.RawToken,
            idempotencyKey: winnerKey,
            ipAddress: "10.0.0.1",
            utcNow: utcNow.AddSeconds(4));

        var activeAfterRetry = await store.CountActiveTokensInFamilyAsync(issued.FamilyId);

        Assert.Multiple(() =>
        {
            Assert.That(retryResult.Succeeded, Is.True);
            Assert.That(retryResult.IsIdempotentReplay, Is.True);
            Assert.That(retryResult.ReplacementTokenHash, Is.EqualTo(winners[0].ReplacementTokenHash));
            Assert.That(activeAfterRetry, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RotateRefreshToken_WhenRevokedAncestorReusedOutsideGraceWindow_ShouldRevokeEntireTokenFamily()
    {
        var store = new AtomicRefreshTokenRotationStore(_connectionString);
        var userId = Guid.NewGuid();
        var utcNow = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

        var initial = await store.IssueInitialTokenAsync(userId, "127.0.0.1", utcNow);
        var rotated = await store.RotateAsync(
            initial.RawToken,
            idempotencyKey: "legit-rotation-1",
            ipAddress: "127.0.0.1",
            utcNow: utcNow.AddMinutes(1));

        Assert.That(rotated.Succeeded, Is.True);

        // Reuse the already-revoked initial token outside the grace window with a different request key
        var replayAttempt = await store.RotateAsync(
            initial.RawToken,
            idempotencyKey: "attacker-replay-key",
            ipAddress: "203.0.113.50",
            utcNow: utcNow.AddMinutes(10));

        var activeInFamily = await store.CountActiveTokensInFamilyAsync(initial.FamilyId);
        var childRecord = await store.FindByTokenHashAsync(rotated.ReplacementTokenHash!);

        Assert.Multiple(() =>
        {
            Assert.That(replayAttempt.Succeeded, Is.False);
            Assert.That(replayAttempt.FailureReason, Is.EqualTo("CompromisedFamilyRevoked"));
            Assert.That(activeInFamily, Is.EqualTo(0));
            Assert.That(childRecord!.RevokedAt, Is.Not.Null);
            Assert.That(childRecord.ReasonRevoked, Does.Contain("Compromised"));
        });
    }

    [Test]
    public async Task RotateRefreshToken_WhenExceptionOccursBeforeCommit_ShouldRollbackEntireTransaction()
    {
        var store = new AtomicRefreshTokenRotationStore(_connectionString);
        var userId = Guid.NewGuid();
        var utcNow = new DateTimeOffset(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

        var initial = await store.IssueInitialTokenAsync(userId, "127.0.0.1", utcNow);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.RotateAsync(
                initial.RawToken,
                idempotencyKey: "fault-injected",
                ipAddress: "127.0.0.1",
                utcNow: utcNow.AddMinutes(1),
                simulateFailureBeforeCommit: true));

        var afterRollback = await store.FindByTokenHashAsync(initial.TokenHash);
        var familyCount = await store.CountTokensInFamilyAsync(initial.FamilyId);

        Assert.Multiple(() =>
        {
            Assert.That(afterRollback!.RevokedAt, Is.Null);
            Assert.That(afterRollback.ReplacedByTokenHash, Is.Null);
            Assert.That(familyCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ConstantTimeComparison_ShouldVerifyEqualHashesAndRejectMismatchedHashes()
    {
        var rawToken = AtomicRefreshTokenRotationStore.GenerateCryptographicToken();
        var expectedHash = AtomicRefreshTokenRotationStore.ComputeSha256Hex(rawToken);
        var sameHash = AtomicRefreshTokenRotationStore.ComputeSha256Hex(rawToken);
        var differentHash = AtomicRefreshTokenRotationStore.ComputeSha256Hex("different-token-value");

        Assert.Multiple(() =>
        {
            Assert.That(AtomicRefreshTokenRotationStore.VerifyHashConstantTime(expectedHash, sameHash), Is.True);
            Assert.That(AtomicRefreshTokenRotationStore.VerifyHashConstantTime(expectedHash, differentHash), Is.False);
            Assert.That(AtomicRefreshTokenRotationStore.VerifyHashConstantTime(expectedHash, "short"), Is.False);
        });
    }

    private static async Task InitializeDatabaseSchemaAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS RefreshTokens (
                Id TEXT PRIMARY KEY NOT NULL,
                UserId TEXT NOT NULL,
                FamilyId TEXT NOT NULL,
                TokenHash TEXT NOT NULL UNIQUE,
                CreatedAtUnixMs INTEGER NOT NULL,
                ExpiresAtUnixMs INTEGER NOT NULL,
                RevokedAtUnixMs INTEGER NULL,
                RevokedByIp TEXT NULL,
                ReplacedByTokenHash TEXT NULL,
                ConsumedByIdempotencyKey TEXT NULL,
                ReasonRevoked TEXT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

internal sealed record IssuedRefreshToken(
    Guid TokenId,
    Guid FamilyId,
    string RawToken,
    string TokenHash,
    DateTimeOffset ExpiresAt);

internal sealed record RotationOutcome(
    bool Succeeded,
    bool IsIdempotentReplay,
    string? ReplacementRawToken,
    string? ReplacementTokenHash,
    string? FailureReason);

internal sealed record StoredRefreshTokenRecord(
    Guid Id,
    Guid UserId,
    Guid FamilyId,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string? ReplacedByTokenHash,
    string? ConsumedByIdempotencyKey,
    string? ReasonRevoked);

internal sealed class AtomicRefreshTokenRotationStore
{
    private static readonly TimeSpan ConcurrentGraceWindow = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _connectionString;

    public AtomicRefreshTokenRotationStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static string GenerateCryptographicToken(int byteLength = 32)
    {
        Span<byte> bytes = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string ComputeSha256Hex(string rawValue)
    {
        var bytes = Encoding.UTF8.GetBytes(rawValue);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool VerifyHashConstantTime(string expectedHex, string actualHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex) || string.IsNullOrWhiteSpace(actualHex))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex.Trim());
        var actualBytes = Encoding.UTF8.GetBytes(actualHex.Trim());
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public async Task<IssuedRefreshToken> IssueInitialTokenAsync(
        Guid userId,
        string ipAddress,
        DateTimeOffset utcNow,
        TimeSpan? lifetime = null)
    {
        var tokenId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var rawToken = GenerateCryptographicToken();
        var tokenHash = ComputeSha256Hex(rawToken);
        var expiresAt = utcNow.Add(lifetime ?? TimeSpan.FromDays(14));

        await _writeGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO RefreshTokens (Id, UserId, FamilyId, TokenHash, CreatedAtUnixMs, ExpiresAtUnixMs)
                VALUES ($id, $userId, $familyId, $tokenHash, $createdAt, $expiresAt);
                """;
            cmd.Parameters.AddWithValue("$id", tokenId.ToString());
            cmd.Parameters.AddWithValue("$userId", userId.ToString());
            cmd.Parameters.AddWithValue("$familyId", familyId.ToString());
            cmd.Parameters.AddWithValue("$tokenHash", tokenHash);
            cmd.Parameters.AddWithValue("$createdAt", utcNow.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$expiresAt", expiresAt.ToUnixTimeMilliseconds());
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _writeGate.Release();
        }

        return new IssuedRefreshToken(tokenId, familyId, rawToken, tokenHash, expiresAt);
    }

    public async Task<RotationOutcome> RotateAsync(
        string incomingRawToken,
        string idempotencyKey,
        string ipAddress,
        DateTimeOffset utcNow,
        bool simulateFailureBeforeCommit = false)
    {
        var incomingHash = ComputeSha256Hex(incomingRawToken);
        var nowMs = utcNow.ToUnixTimeMilliseconds();

        await _writeGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var replacementRawToken = GenerateCryptographicToken();
                var replacementHash = ComputeSha256Hex(replacementRawToken);
                var replacementId = Guid.NewGuid();

                // Atomic conditional update: only one caller can transition RevokedAtUnixMs from NULL
                await using var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = """
                    UPDATE RefreshTokens
                    SET RevokedAtUnixMs = $now,
                        RevokedByIp = $ip,
                        ReplacedByTokenHash = $newHash,
                        ConsumedByIdempotencyKey = $idempotencyKey,
                        ReasonRevoked = 'Rotated'
                    WHERE TokenHash = $incomingHash
                      AND RevokedAtUnixMs IS NULL
                      AND ExpiresAtUnixMs > $now;
                    """;
                updateCmd.Parameters.AddWithValue("$now", nowMs);
                updateCmd.Parameters.AddWithValue("$ip", ipAddress);
                updateCmd.Parameters.AddWithValue("$newHash", replacementHash);
                updateCmd.Parameters.AddWithValue("$idempotencyKey", idempotencyKey);
                updateCmd.Parameters.AddWithValue("$incomingHash", incomingHash);

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                if (rowsAffected == 1)
                {
                    var parent = await GetRecordInternalAsync(connection, transaction, incomingHash);

                    if (simulateFailureBeforeCommit)
                    {
                        throw new InvalidOperationException("Simulated database failure before transaction commit.");
                    }

                    await using var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;
                    insertCmd.CommandText = """
                        INSERT INTO RefreshTokens (Id, UserId, FamilyId, TokenHash, CreatedAtUnixMs, ExpiresAtUnixMs)
                        VALUES ($id, $userId, $familyId, $tokenHash, $createdAt, $expiresAt);
                        """;
                    insertCmd.Parameters.AddWithValue("$id", replacementId.ToString());
                    insertCmd.Parameters.AddWithValue("$userId", parent!.UserId.ToString());
                    insertCmd.Parameters.AddWithValue("$familyId", parent.FamilyId.ToString());
                    insertCmd.Parameters.AddWithValue("$tokenHash", replacementHash);
                    insertCmd.Parameters.AddWithValue("$createdAt", nowMs);
                    insertCmd.Parameters.AddWithValue("$expiresAt", utcNow.AddDays(14).ToUnixTimeMilliseconds());
                    await insertCmd.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();
                    return new RotationOutcome(true, false, replacementRawToken, replacementHash, null);
                }

                // rowsAffected == 0: inspect existing token state inside the same transaction
                var existing = await GetRecordInternalAsync(connection, transaction, incomingHash);
                if (existing is null)
                {
                    await transaction.CommitAsync();
                    return new RotationOutcome(false, false, null, null, "TokenNotFound");
                }

                if (existing.RevokedAt is null && existing.ExpiresAt <= utcNow)
                {
                    await transaction.CommitAsync();
                    return new RotationOutcome(false, false, null, null, "TokenExpired");
                }

                // Check if this is an idempotent retry or benign concurrent race loser within grace window
                var elapsedSinceRevoke = utcNow - existing.RevokedAt!.Value;
                if (string.Equals(existing.ConsumedByIdempotencyKey, idempotencyKey, StringComparison.Ordinal) &&
                    elapsedSinceRevoke <= ConcurrentGraceWindow)
                {
                    await transaction.CommitAsync();
                    return new RotationOutcome(true, true, null, existing.ReplacedByTokenHash, null);
                }

                if (elapsedSinceRevoke <= TimeSpan.FromSeconds(5))
                {
                    // Concurrent race loser: reject without revoking the winning sibling's family
                    await transaction.CommitAsync();
                    return new RotationOutcome(false, false, null, null, "ConcurrentRaceLoser");
                }

                // Reuse outside grace window: revoke entire token family
                await using var revokeFamilyCmd = connection.CreateCommand();
                revokeFamilyCmd.Transaction = transaction;
                revokeFamilyCmd.CommandText = """
                    UPDATE RefreshTokens
                    SET RevokedAtUnixMs = $now,
                        RevokedByIp = $ip,
                        ReasonRevoked = 'Compromised: Attempted reuse of revoked ancestor token'
                    WHERE FamilyId = $familyId
                      AND RevokedAtUnixMs IS NULL;
                    """;
                revokeFamilyCmd.Parameters.AddWithValue("$now", nowMs);
                revokeFamilyCmd.Parameters.AddWithValue("$ip", ipAddress);
                revokeFamilyCmd.Parameters.AddWithValue("$familyId", existing.FamilyId.ToString());
                await revokeFamilyCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return new RotationOutcome(false, false, null, null, "CompromisedFamilyRevoked");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<StoredRefreshTokenRecord?> FindByTokenHashAsync(string tokenHash)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return await GetRecordInternalAsync(connection, null, tokenHash);
    }

    public async Task<int> CountTokensInFamilyAsync(Guid familyId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM RefreshTokens WHERE FamilyId = $familyId;";
        cmd.Parameters.AddWithValue("$familyId", familyId.ToString());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> CountActiveTokensInFamilyAsync(Guid familyId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM RefreshTokens WHERE FamilyId = $familyId AND RevokedAtUnixMs IS NULL;";
        cmd.Parameters.AddWithValue("$familyId", familyId.ToString());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<StoredRefreshTokenRecord?> GetRecordInternalAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tokenHash)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT Id, UserId, FamilyId, TokenHash, CreatedAtUnixMs, ExpiresAtUnixMs,
                   RevokedAtUnixMs, ReplacedByTokenHash, ConsumedByIdempotencyKey, ReasonRevoked
            FROM RefreshTokens
            WHERE TokenHash = $tokenHash;
            """;
        cmd.Parameters.AddWithValue("$tokenHash", tokenHash);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var id = Guid.Parse(reader.GetString(0));
        var userId = Guid.Parse(reader.GetString(1));
        var familyId = Guid.Parse(reader.GetString(2));
        var hash = reader.GetString(3);
        var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4));
        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5));
        var revokedAt = reader.IsDBNull(6)
            ? (DateTimeOffset?)null
            : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6));
        var replacedBy = reader.IsDBNull(7) ? null : reader.GetString(7);
        var idempotencyKey = reader.IsDBNull(8) ? null : reader.GetString(8);
        var reason = reader.IsDBNull(9) ? null : reader.GetString(9);

        return new StoredRefreshTokenRecord(
            id,
            userId,
            familyId,
            hash,
            createdAt,
            expiresAt,
            revokedAt,
            replacedBy,
            idempotencyKey,
            reason);
    }
}

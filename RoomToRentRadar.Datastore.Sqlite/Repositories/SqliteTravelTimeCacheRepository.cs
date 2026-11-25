using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// SQLite persistence for <c>TravelTimeEstimates</c> (ERD §4.15, BR-TRV-03).
/// Joins <see cref="SqliteAmbientTransaction"/> when running inside <c>IUnitOfWork</c>.
/// </summary>
public sealed class SqliteTravelTimeCacheRepository : ITravelTimeCacheRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteTravelTimeCacheRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TravelTimeCacheEntry>> GetByRoomAndCampusAsync(
        string roomId,
        string campusId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(campusId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = """
            SELECT RoomId, CampusId, Mode, DistanceMeters, DurationSeconds, Source, ComputedAtUtc
            FROM TravelTimeEstimates
            WHERE RoomId = @roomId AND CampusId = @campusId;
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId.Trim());
        cmd.Parameters.AddWithValue("@campusId", campusId.Trim());

        var entries = new List<TravelTimeCacheEntry>(3);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            entries.Add(MapEntry(reader));
        }

        return entries;
    }

    public async Task UpsertRangeAsync(IReadOnlyList<TravelTimeCacheEntry> entries, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        SqliteTransaction? localTransaction = lease.Transaction is null
            ? lease.Connection.BeginTransaction()
            : null;

        try
        {
            var activeTransaction = lease.Transaction ?? localTransaction;
            foreach (var entry in entries)
            {
                await using var cmd = lease.Connection.CreateCommand();
                cmd.Transaction = activeTransaction;
                cmd.CommandText = """
                    INSERT INTO TravelTimeEstimates
                        (RoomId, CampusId, Mode, DistanceMeters, DurationSeconds, Source, ComputedAtUtc)
                    VALUES
                        (@roomId, @campusId, @mode, @distanceMeters, @durationSeconds, @source, @computedAtUtc)
                    ON CONFLICT(RoomId, CampusId, Mode) DO UPDATE SET
                        DistanceMeters = excluded.DistanceMeters,
                        DurationSeconds = excluded.DurationSeconds,
                        Source = excluded.Source,
                        ComputedAtUtc = excluded.ComputedAtUtc;
                    """;
                cmd.Parameters.AddWithValue("@roomId", entry.RoomId);
                cmd.Parameters.AddWithValue("@campusId", entry.CampusId);
                cmd.Parameters.AddWithValue("@mode", entry.Mode.ToString());
                cmd.Parameters.AddWithValue("@distanceMeters", entry.DistanceMeters);
                cmd.Parameters.AddWithValue("@durationSeconds", entry.DurationSeconds);
                cmd.Parameters.AddWithValue("@source", entry.Source);
                cmd.Parameters.AddWithValue("@computedAtUtc", FormatUtc(entry.ComputedAtUtc));
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            localTransaction?.Commit();
        }
        catch
        {
            localTransaction?.Rollback();
            throw;
        }
        finally
        {
            localTransaction?.Dispose();
        }
    }

    public async Task InvalidateByRoomIdAsync(string roomId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "DELETE FROM TravelTimeEstimates WHERE RoomId = @roomId;";
        cmd.Parameters.AddWithValue("@roomId", roomId.Trim());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> PruneExpiredAsync(DateTime expiresBeforeUtc, CancellationToken ct = default)
    {
        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "DELETE FROM TravelTimeEstimates WHERE ComputedAtUtc < @cutoffUtc;";
        cmd.Parameters.AddWithValue("@cutoffUtc", FormatUtc(expiresBeforeUtc));
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static TravelTimeCacheEntry MapEntry(SqliteDataReader reader) => new(
        RoomId: reader.GetString(0),
        CampusId: reader.GetString(1),
        Mode: Enum.Parse<TravelMode>(reader.GetString(2)),
        DistanceMeters: reader.GetInt32(3),
        DurationSeconds: reader.GetInt32(4),
        Source: reader.GetString(5),
        ComputedAtUtc: ParseUtc(reader.GetString(6)));

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
}

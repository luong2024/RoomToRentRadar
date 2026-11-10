using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteAuditLogRepository : IAuditLogRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteAuditLogRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task InsertAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO AuditLogs (
                    Id, UserId, UserEmail, Role, Action, EntityType, EntityId,
                    DetailsJson, IpAddress, UserAgent, StatusCode, DurationMs,
                    IsSuccess, ErrorMessage, TimestampUtc
                ) VALUES (
                    @id, @userId, @userEmail, @role, @action, @entityType, @entityId,
                    @detailsJson, @ipAddress, @userAgent, @statusCode, @durationMs,
                    @isSuccess, @errorMessage, @timestampUtc
                );
                """;

            BindParameters(cmd, entry);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<PagedResult<AuditLogEntry>> QueryAsync(AuditLogQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var offset = (pageNumber - 1) * pageSize;

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            var whereBuilder = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                whereBuilder.Append(" AND UserId = @userId");
                parameters.Add(new SqliteParameter("@userId", request.UserId.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(request.Action))
            {
                whereBuilder.Append(" AND Action = @action");
                parameters.Add(new SqliteParameter("@action", request.Action.Trim().ToUpperInvariant()));
            }

            if (!string.IsNullOrWhiteSpace(request.EntityType))
            {
                whereBuilder.Append(" AND EntityType = @entityType");
                parameters.Add(new SqliteParameter("@entityType", request.EntityType.Trim()));
            }

            if (request.FromUtc.HasValue)
            {
                whereBuilder.Append(" AND TimestampUtc >= @fromUtc");
                parameters.Add(new SqliteParameter(
                    "@fromUtc",
                    request.FromUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
            }

            if (request.ToUtc.HasValue)
            {
                whereBuilder.Append(" AND TimestampUtc <= @toUtc");
                parameters.Add(new SqliteParameter(
                    "@toUtc",
                    request.ToUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
            }

            // 1. Count query
            var countSql = $"SELECT COUNT(1) FROM AuditLogs {whereBuilder};";
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = countSql;
            foreach (var p in parameters)
            {
                countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
            }

            var totalCountObj = await countCmd.ExecuteScalarAsync(ct);
            var totalCount = Convert.ToInt32(totalCountObj, CultureInfo.InvariantCulture);

            if (totalCount == 0)
            {
                return new PagedResult<AuditLogEntry>(Array.Empty<AuditLogEntry>(), 0, pageNumber, pageSize);
            }

            // 2. Data query
            var dataSql = $"""
                SELECT Id, UserId, UserEmail, Role, Action, EntityType, EntityId,
                       DetailsJson, IpAddress, UserAgent, StatusCode, DurationMs,
                       IsSuccess, ErrorMessage, TimestampUtc
                FROM AuditLogs
                {whereBuilder}
                ORDER BY TimestampUtc DESC
                LIMIT @pageSize OFFSET @offset;
                """;

            await using var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = dataSql;
            foreach (var p in parameters)
            {
                dataCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
            }
            dataCmd.Parameters.AddWithValue("@pageSize", pageSize);
            dataCmd.Parameters.AddWithValue("@offset", offset);

            var items = new List<AuditLogEntry>();
            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(ReadRow(reader));
            }

            return new PagedResult<AuditLogEntry>(items, totalCount, pageNumber, pageSize);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static void BindParameters(SqliteCommand cmd, AuditLogEntry entry)
    {
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@userId", (object?)entry.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userEmail", (object?)entry.UserEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@role", entry.Role);
        cmd.Parameters.AddWithValue("@action", entry.Action);
        cmd.Parameters.AddWithValue("@entityType", entry.EntityType);
        cmd.Parameters.AddWithValue("@entityId", (object?)entry.EntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@detailsJson", (object?)entry.DetailsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ipAddress", (object?)entry.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@userAgent", (object?)entry.UserAgent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@statusCode", (object?)entry.StatusCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@durationMs", (object?)entry.DurationMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isSuccess", entry.IsSuccess ? 1 : 0);
        cmd.Parameters.AddWithValue("@errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@timestampUtc", entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static AuditLogEntry ReadRow(DbDataReader reader)
    {
        return new AuditLogEntry(
            id: reader.GetString(reader.GetOrdinal("Id")),
            userId: reader.IsDBNull(reader.GetOrdinal("UserId")) ? null : reader.GetString(reader.GetOrdinal("UserId")),
            userEmail: reader.IsDBNull(reader.GetOrdinal("UserEmail")) ? null : reader.GetString(reader.GetOrdinal("UserEmail")),
            role: reader.GetString(reader.GetOrdinal("Role")),
            action: reader.GetString(reader.GetOrdinal("Action")),
            entityType: reader.GetString(reader.GetOrdinal("EntityType")),
            entityId: reader.IsDBNull(reader.GetOrdinal("EntityId")) ? null : reader.GetString(reader.GetOrdinal("EntityId")),
            detailsJson: reader.IsDBNull(reader.GetOrdinal("DetailsJson")) ? null : reader.GetString(reader.GetOrdinal("DetailsJson")),
            ipAddress: reader.IsDBNull(reader.GetOrdinal("IpAddress")) ? null : reader.GetString(reader.GetOrdinal("IpAddress")),
            userAgent: reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent")),
            statusCode: reader.IsDBNull(reader.GetOrdinal("StatusCode")) ? null : reader.GetInt32(reader.GetOrdinal("StatusCode")),
            durationMs: reader.IsDBNull(reader.GetOrdinal("DurationMs")) ? null : reader.GetInt64(reader.GetOrdinal("DurationMs")),
            isSuccess: reader.GetInt32(reader.GetOrdinal("IsSuccess")) == 1,
            errorMessage: reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            timestampUtc: DateTime.Parse(reader.GetString(reader.GetOrdinal("TimestampUtc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}


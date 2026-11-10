using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.CosmosDb.Repositories;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Contracts.Requests.Audit;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SqliteAuditLogRepositoryTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public void DependencyInjection_RegistersExpectedAuditLogRepositoryType()
    {
        var sqliteServices = new ServiceCollection();
        sqliteServices.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var sqliteDescriptor = sqliteServices.Single(d => d.ServiceType == typeof(IAuditLogRepository));

        var cosmosServices = new ServiceCollection();
        cosmosServices.AddCosmosDatastore(new CosmosDbSettings());
        var cosmosDescriptor = cosmosServices.Single(d => d.ServiceType == typeof(IAuditLogRepository));

        Assert.Multiple(() =>
        {
            Assert.That(sqliteDescriptor.ImplementationType, Is.EqualTo(typeof(SqliteAuditLogRepository)));
            Assert.That(cosmosDescriptor.ImplementationType, Is.EqualTo(typeof(CosmosAuditLogRepository)));
        });
    }

    [Test]
    public async Task InsertAndQueryAsync_FiltersByUserIdAndAction_ReturnsExpectedLogs()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IAuditLogRepository repo = new SqliteAuditLogRepository(dbContext);

        var log1 = new AuditLogEntry(
            id: null,
            userId: "user-123",
            userEmail: "student@ictu.edu.vn",
            role: "Student",
            action: "USER_LOGIN",
            entityType: "User",
            entityId: "user-123",
            detailsJson: "{\"ip\":\"127.0.0.1\"}",
            ipAddress: "127.0.0.1",
            userAgent: "Mozilla",
            statusCode: 200,
            durationMs: 45,
            isSuccess: true,
            timestampUtc: FixedNow);

        var log2 = new AuditLogEntry(
            id: null,
            userId: "user-456",
            userEmail: "landlord@gmail.com",
            role: "Landlord",
            action: "ROOM_CREATED",
            entityType: "Room",
            entityId: "room-999",
            detailsJson: "{\"price\":1500000}",
            ipAddress: "127.0.0.1",
            userAgent: "Mozilla",
            statusCode: 201,
            durationMs: 120,
            isSuccess: true,
            timestampUtc: FixedNow.AddMinutes(5));

        await repo.InsertAsync(log1);
        await repo.InsertAsync(log2);

        // 1. Query all
        var all = await repo.QueryAsync(new AuditLogQueryRequest());
        Assert.That(all.TotalCount, Is.EqualTo(2));

        // 2. Query by userId
        var user1Logs = await repo.QueryAsync(new AuditLogQueryRequest(UserId: "user-123"));
        Assert.Multiple(() =>
        {
            Assert.That(user1Logs.TotalCount, Is.EqualTo(1));
            Assert.That(user1Logs.Items[0].UserEmail, Is.EqualTo("student@ictu.edu.vn"));
            Assert.That(user1Logs.Items[0].Action, Is.EqualTo("USER_LOGIN"));
        });

        // 3. Query by action
        var roomCreatedLogs = await repo.QueryAsync(new AuditLogQueryRequest(Action: "ROOM_CREATED"));
        Assert.Multiple(() =>
        {
            Assert.That(roomCreatedLogs.TotalCount, Is.EqualTo(1));
            Assert.That(roomCreatedLogs.Items[0].EntityId, Is.EqualTo("room-999"));
        });
    }

    [Test]
    public async Task QueryAsync_WithPaginationAndDateFilter_PaginatesCorrectly()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IAuditLogRepository repo = new SqliteAuditLogRepository(dbContext);

        for (var i = 1; i <= 5; i++)
        {
            var log = new AuditLogEntry(
                id: null,
                userId: $"user-{i}",
                userEmail: $"user{i}@test.com",
                role: "Student",
                action: "ROOM_SEARCH",
                entityType: "Room",
                timestampUtc: FixedNow.AddMinutes(i));

            await repo.InsertAsync(log);
        }

        // Query page 2 with page size 2
        var pagedResult = await repo.QueryAsync(new AuditLogQueryRequest(PageNumber: 2, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(pagedResult.TotalCount, Is.EqualTo(5));
            Assert.That(pagedResult.TotalPages, Is.EqualTo(3));
            Assert.That(pagedResult.PageNumber, Is.EqualTo(2));
            Assert.That(pagedResult.Items.Count, Is.EqualTo(2));
            Assert.That(pagedResult.HasPreviousPage, Is.True);
            Assert.That(pagedResult.HasNextPage, Is.True);
        });

        // Date filter
        var dateFiltered = await repo.QueryAsync(new AuditLogQueryRequest(
            FromUtc: FixedNow.AddMinutes(2),
            ToUtc: FixedNow.AddMinutes(4)));

        Assert.That(dateFiltered.TotalCount, Is.EqualTo(3));
    }
}


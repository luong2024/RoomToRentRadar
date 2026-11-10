using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class SqlitePragmaTests
{
    private ListingsApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _factory.Dispose();

    [Test]
    public async Task EveryApplicationConnection_HasWalForeignKeysAndBusyTimeout()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.OpenConnectionAsync();

        try
        {
            var connection = dbContext.Database.GetDbConnection();
            var journalMode = await ScalarAsync(connection, "PRAGMA journal_mode;");
            var foreignKeys = await ScalarAsync(connection, "PRAGMA foreign_keys;");
            var busyTimeout = await ScalarAsync(connection, "PRAGMA busy_timeout;");

            Assert.Multiple(() =>
            {
                Assert.That(journalMode, Is.EqualTo("wal"));
                Assert.That(Convert.ToInt64(foreignKeys), Is.EqualTo(1));
                Assert.That(Convert.ToInt64(busyTimeout), Is.EqualTo(5000));
            });
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    [Test]
    public async Task ForeignKeyViolation_IsRejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.ListingPhotos.Add(new ListingPhoto
        {
            Id = Guid.NewGuid(),
            ListingId = Guid.NewGuid(),
            Url = "/media/missing.jpg",
            Width = 100,
            Height = 100,
            Kind = ListingPhotoKind.Photo
        });

        Assert.ThrowsAsync<DbUpdateException>(async () => await dbContext.SaveChangesAsync());
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}

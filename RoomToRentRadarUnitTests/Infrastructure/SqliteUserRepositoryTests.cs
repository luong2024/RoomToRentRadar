using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.CosmosDb.Repositories;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SqliteUserRepositoryTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public void DependencyInjection_SwappingBetweenSqliteAndCosmosDatastore_RegistersExpectedRepositoryType()
    {
        var sqliteServices = new ServiceCollection();
        sqliteServices.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var sqliteDescriptor = sqliteServices.Single(d => d.ServiceType == typeof(IUserRepository));

        var cosmosServices = new ServiceCollection();
        cosmosServices.AddCosmosDatastore(new CosmosDbSettings());
        var cosmosDescriptor = cosmosServices.Single(d => d.ServiceType == typeof(IUserRepository));

        Assert.Multiple(() =>
        {
            Assert.That(sqliteDescriptor.ImplementationType, Is.EqualTo(typeof(SqliteUserRepository)));
            Assert.That(cosmosDescriptor.ImplementationType, Is.EqualTo(typeof(CosmosUserRepository)));
        });
    }

    [Test]
    public async Task SqliteUserRepository_AddGetUpdateAndLookupByRefreshToken_RoundTripsDomainAggregate()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var landlord = User.RegisterLocal(
            email: "landlord@gmail.com",
            passwordHash: "pbkdf2-hash",
            fullName: "Chu Tro SQLite",
            role: UserRole.Landlord,
            phoneNumber: "0987654321",
            studentCode: null,
            utcNow: FixedNow);

        landlord.IssueRefreshToken("token-hash-100", "jti-100", FixedNow, TimeSpan.FromDays(7));
        await repo.AddAsync(landlord);

        var byEmail = await repo.GetByEmailAsync("LANDLORD@GMAIL.COM");
        var byToken = await repo.GetByRefreshTokenHashAsync("token-hash-100");

        Assert.Multiple(() =>
        {
            Assert.That(byEmail, Is.Not.Null);
            Assert.That(byEmail!.Id, Is.EqualTo(landlord.Id));
            Assert.That(byEmail.IsVerified, Is.False);
            Assert.That(byToken, Is.Not.Null);
            Assert.That(byToken!.RefreshTokens, Has.Count.EqualTo(1));
        });

        landlord.ApproveLandlord(FixedNow.AddMinutes(10));
        await repo.UpdateAsync(landlord);

        var afterApproval = await repo.GetByIdAsync(landlord.Id);
        Assert.That(afterApproval!.IsVerified, Is.True);
    }

    [Test]
    public async Task SqliteUserRepository_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user1 = User.RegisterLocal("dup@ictu.edu.vn", "hash1", "User 1", UserRole.Student, null, "DTC1", FixedNow);
        var user2 = User.RegisterLocal("DUP@ictu.edu.vn", "hash2", "User 2", UserRole.Student, null, "DTC2", FixedNow);

        await repo.AddAsync(user1);

        Assert.ThrowsAsync<DuplicateEmailException>(
            async () => await repo.AddAsync(user2));
    }

    [Test]
    public async Task SqliteUserRepository_UpdateWithStaleConcurrencyToken_ThrowsConcurrencyConflictException()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        var loaded1 = await repo.GetByIdAsync(user.Id);
        var loaded2 = await repo.GetByIdAsync(user.Id);

        loaded1!.RecordLoginSuccess(FixedNow.AddMinutes(1));
        await repo.UpdateAsync(loaded1);

        loaded2!.RecordLoginSuccess(FixedNow.AddMinutes(2));
        Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repo.UpdateAsync(loaded2));
    }
}

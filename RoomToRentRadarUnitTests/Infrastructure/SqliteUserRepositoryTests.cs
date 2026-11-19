using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.CosmosDb;
using RoomToRentRadar.Datastore.CosmosDb.Configurations;
using RoomToRentRadar.Datastore.CosmosDb.Repositories;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
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

    [Test]
    public async Task AddGet_DefaultUser_PersistsDefaultsAndVersionOne()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync(user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(loaded.PreferredLanguage, Is.EqualTo("en"));
            Assert.That(loaded.Version, Is.EqualTo(1));
            Assert.That(loaded.ConcurrencyToken, Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task AddGet_NonDefaultCampusAndLanguage_RoundTrips()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = ReconstituteUser("u-tnus", "tnus@ictu.edu.vn", selectedCampusId: "TNUS", preferredLanguage: "vi", version: 7);
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync(user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.SelectedCampusId, Is.EqualTo("TNUS"));
            Assert.That(loaded.PreferredLanguage, Is.EqualTo("vi"));
            Assert.That(loaded.Version, Is.EqualTo(7));
            Assert.That(loaded.ConcurrencyToken, Is.EqualTo("7"));
        });
    }

    [Test]
    public async Task Update_PersistsChangedCampusAndLanguage_AndBumpsVersion()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("campus@ictu.edu.vn", "hash", "Nguyen Van A", UserRole.Student, null, null, FixedNow);
        await repo.AddAsync(user);
        var loaded = await repo.GetByIdAsync(user.Id);

        var changed = ReconstituteUser(
            loaded!.Id,
            loaded.Email.Value,
            selectedCampusId: "SFL",
            preferredLanguage: "vi",
            version: loaded.Version,
            refreshTokens: loaded.RefreshTokens);
        await repo.UpdateAsync(changed);

        Assert.That(changed.Version, Is.EqualTo(2));

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded!.SelectedCampusId, Is.EqualTo("SFL"));
            Assert.That(reloaded.PreferredLanguage, Is.EqualTo("vi"));
            Assert.That(reloaded.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Update_StaleVersion_ThrowsConcurrencyConflict()
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
        var ex = Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repo.UpdateAsync(loaded2));
        Assert.That(ex, Is.Not.Null);

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded!.Version, Is.EqualTo(2));
            Assert.That(reloaded.UpdatedAtUtc, Is.EqualTo(FixedNow.AddMinutes(1)));
        });
    }

    [Test]
    public async Task Update_SecondUpdateOnSameInstance_Succeeds_TokenAdvances()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        user.RecordLoginSuccess(FixedNow.AddMinutes(1));
        await repo.UpdateAsync(user);
        Assert.Multiple(() =>
        {
            Assert.That(user.Version, Is.EqualTo(2));
            Assert.That(user.ConcurrencyToken, Is.EqualTo("2"));
        });

        user.RecordLoginFailure(FixedNow.AddMinutes(2));
        await repo.UpdateAsync(user);
        Assert.Multiple(() =>
        {
            Assert.That(user.Version, Is.EqualTo(3));
            Assert.That(user.ConcurrencyToken, Is.EqualTo("3"));
        });

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.That(reloaded!.Version, Is.EqualTo(3));
    }

    [Test]
    public async Task Update_AfterManyMutations_PersistsFinalStateWithoutFalseConflict()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        for (var i = 0; i < 5; i++)
        {
            user.RecordLoginFailure(FixedNow.AddMinutes(i + 1));
            await repo.UpdateAsync(user);
        }

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded!.Version, Is.EqualTo(6));
            Assert.That(reloaded.FailedLoginCount, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Update_RecordLoginSuccess_BumpsVersionInDatabase()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync(user.Id);
        var originalVersion = loaded!.Version;
        loaded.RecordLoginSuccess(FixedNow.AddMinutes(1));
        await repo.UpdateAsync(loaded);

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.That(reloaded!.Version, Is.EqualTo(originalVersion + 1));
    }

    [Test]
    public async Task Update_UnknownUser_ThrowsConcurrencyConflict()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var unknown = ReconstituteUser("unknown-user", "unknown@ictu.edu.vn");

        var ex = Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repo.UpdateAsync(unknown));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task AddAsync_UnknownCampus_ThrowsSqliteExceptionNotDuplicateEmail()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = ReconstituteUser("u-nope", "nope@ictu.edu.vn", selectedCampusId: "NOPE");

        var ex = Assert.ThrowsAsync<SqliteException>(async () => await repo.AddAsync(user));

        Assert.That(ex!.SqliteExtendedErrorCode, Is.EqualTo(787));
        Assert.That(await repo.ExistsByEmailAsync("nope@ictu.edu.vn"), Is.False);
    }

    [Test]
    public async Task AddAsync_OtherConstraintViolation_PropagatesAndIsNotDuplicateEmail()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        // Shared :memory: connection: must not be disposed here.
        var conn = dbContext.CreateOpenConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE TRIGGER trg_block_users BEFORE INSERT ON Users BEGIN SELECT RAISE(ABORT, 'blocked by test'); END;";
            await cmd.ExecuteNonQueryAsync();
        }

        var user = User.RegisterLocal("blocked@ictu.edu.vn", "hash", "Blocked", UserRole.Student, null, "DTC1", FixedNow);

        var ex = Assert.ThrowsAsync<SqliteException>(async () => await repo.AddAsync(user));

        Assert.That(ex!.SqliteErrorCode, Is.EqualTo(19));
    }

    [Test]
    public async Task AddAsync_DuplicateEmail_StillThrowsDuplicateEmailException()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user1 = User.RegisterLocal("dup@ictu.edu.vn", "hash1", "User 1", UserRole.Student, null, "DTC1", FixedNow);
        var user2 = User.RegisterLocal("DUP@ictu.edu.vn", "hash2", "User 2", UserRole.Student, null, "DTC2", FixedNow);
        await repo.AddAsync(user1);

        var ex = Assert.ThrowsAsync<DuplicateEmailException>(async () => await repo.AddAsync(user2));

        Assert.That(ex, Is.Not.Null);
        Assert.That(await repo.GetByIdAsync(user1.Id), Is.Not.Null);
    }

    [Test]
    public async Task SeedLandlord_HasDefaultCampusLanguageVersion()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:", SeedDemoRooms = true });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var landlord = await repo.GetByIdAsync("seed-landlord");

        Assert.Multiple(() =>
        {
            Assert.That(landlord, Is.Not.Null);
            Assert.That(landlord!.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(landlord.PreferredLanguage, Is.EqualTo("en"));
            Assert.That(landlord.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DefaultCampusConstant_MatchesSeedAndSchemaDefault()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });

        Assert.That(User.DefaultSelectedCampusId, Is.EqualTo(CampusSeedData.DefaultCampusId));

        var conn = dbContext.CreateOpenConnection();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Campuses WHERE IsDefault = 1;";
            var defaultCampusId = (string?)await cmd.ExecuteScalarAsync();
            Assert.That(defaultCampusId, Is.EqualTo(User.DefaultSelectedCampusId));
        }

        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Users);";
            await using var reader = await pragma.ExecuteReaderAsync();
            string? campusDefault = null;
            string? languageDefault = null;
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (columnName == "SelectedCampusId")
                {
                    campusDefault = reader.IsDBNull(4) ? null : reader.GetString(4);
                }
                else if (columnName == "PreferredLanguage")
                {
                    languageDefault = reader.IsDBNull(4) ? null : reader.GetString(4);
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(campusDefault, Is.EqualTo("'" + User.DefaultSelectedCampusId + "'"));
                Assert.That(languageDefault, Is.EqualTo("'" + User.DefaultPreferredLanguage + "'"));
            });
        }
    }

    [Test]
    public async Task Update_DoesNotDeleteRefreshTokens()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var user = User.RegisterLocal("tokens@ictu.edu.vn", "hash", "Token User", UserRole.Student, null, "DTC1", FixedNow);
        user.IssueRefreshToken("hash-a", "jti-a", FixedNow, TimeSpan.FromDays(7));
        user.IssueRefreshToken("hash-b", "jti-b", FixedNow.AddMinutes(1), TimeSpan.FromDays(7));
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync(user.Id);
        loaded!.RecordLoginSuccess(FixedNow.AddMinutes(2));
        await repo.UpdateAsync(loaded);

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.That(reloaded!.RefreshTokens, Has.Count.EqualTo(2));

        var byHashA = await repo.GetByRefreshTokenHashAsync("hash-a");
        var byHashB = await repo.GetByRefreshTokenHashAsync("hash-b");
        Assert.Multiple(() =>
        {
            Assert.That(byHashA, Is.Not.Null);
            Assert.That(byHashA!.Id, Is.EqualTo(user.Id));
            Assert.That(byHashB, Is.Not.Null);
            Assert.That(byHashB!.Id, Is.EqualTo(user.Id));
        });
    }

    private static User ReconstituteUser(
        string id,
        string email,
        string selectedCampusId = User.DefaultSelectedCampusId,
        string preferredLanguage = User.DefaultPreferredLanguage,
        int version = 1,
        IEnumerable<RefreshTokenSession>? refreshTokens = null)
    {
        return User.Reconstitute(
            id: id,
            email: email,
            fullName: "Nguyen Van A",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: null,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: FixedNow,
            updatedAtUtc: FixedNow,
            refreshTokens: refreshTokens,
            selectedCampusId: selectedCampusId,
            preferredLanguage: preferredLanguage,
            version: version);
    }
}

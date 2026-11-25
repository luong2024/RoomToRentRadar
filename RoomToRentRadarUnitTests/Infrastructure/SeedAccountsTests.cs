using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SeedAccountsTests
{
    private readonly Pbkdf2PasswordHasherAdapter _hasher = new();

    [Test]
    public async Task SeedAccounts_Configured_InsertedWithRoleVerifiedAndVerifiablePasswordHash()
    {
        var accounts = new[]
        {
            new SeedAccount(
                Id: "seed-student",
                Email: "student@roomtorentradar.local",
                FullName: "Sinh Viên Demo",
                Role: UserRole.Student,
                IsVerified: true,
                PasswordHash: _hasher.HashPassword("Student@123"),
                StudentCode: "DTC235140001"),
            new SeedAccount(
                Id: "seed-admin",
                Email: "admin@roomtorentradar.local",
                FullName: "Quản Trị Demo",
                Role: UserRole.Admin,
                IsVerified: true,
                PasswordHash: _hasher.HashPassword("Admin@12345")),
            new SeedAccount(
                Id: "seed-landlord-pending",
                Email: "landlord.pending@roomtorentradar.local",
                FullName: "Chủ Trọ Chờ Duyệt",
                Role: UserRole.Landlord,
                IsVerified: false,
                PasswordHash: _hasher.HashPassword("Landlord@123"),
                PhoneNumber: "0912000002")
        };

        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = false,
            SeedAccounts = accounts
        });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var student = await repo.GetByEmailAsync("student@roomtorentradar.local");
        var admin = await repo.GetByEmailAsync("admin@roomtorentradar.local");
        var pendingLandlord = await repo.GetByEmailAsync("landlord.pending@roomtorentradar.local");

        Assert.Multiple(() =>
        {
            Assert.That(student, Is.Not.Null);
            Assert.That(student!.Id, Is.EqualTo("seed-student"));
            Assert.That(student.Role, Is.EqualTo(UserRole.Student));
            Assert.That(student.IsVerified, Is.True);
            Assert.That(student.StudentCode, Is.EqualTo("DTC235140001"));
            Assert.That(_hasher.VerifyPassword("Student@123", student.PasswordHash!), Is.True);
            Assert.That(_hasher.VerifyPassword("WrongPass@123", student.PasswordHash!), Is.False);

            Assert.That(admin, Is.Not.Null);
            Assert.That(admin!.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(admin.IsVerified, Is.True);
            Assert.That(_hasher.VerifyPassword("Admin@12345", admin.PasswordHash!), Is.True);
            Assert.That(_hasher.VerifyPassword("Student@123", admin.PasswordHash!), Is.False);

            Assert.That(pendingLandlord, Is.Not.Null);
            Assert.That(pendingLandlord!.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(pendingLandlord.IsVerified, Is.False);
            Assert.That(pendingLandlord.PhoneNumber, Is.EqualTo("0912000002"));
            Assert.That(_hasher.VerifyPassword("Landlord@123", pendingLandlord.PasswordHash!), Is.True);
        });
    }

    [Test]
    public void SeedAccounts_SameIdOrEmailTwice_IgnoredWithoutThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rtr_seed_acc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "seed_accounts.db");
        var connStr = $"Data Source={dbPath}";

        try
        {
            var accounts = new[]
            {
                new SeedAccount(
                    Id: "seed-student",
                    Email: "student@roomtorentradar.local",
                    FullName: "First",
                    Role: UserRole.Student,
                    IsVerified: true,
                    PasswordHash: _hasher.HashPassword("Student@123")),
                new SeedAccount(
                    Id: "seed-student",
                    Email: "other@roomtorentradar.local",
                    FullName: "Duplicate Id",
                    Role: UserRole.Student,
                    IsVerified: true,
                    PasswordHash: _hasher.HashPassword("Student@123")),
                new SeedAccount(
                    Id: "seed-student-2",
                    Email: "STUDENT@roomtorentradar.local",
                    FullName: "Duplicate Email",
                    Role: UserRole.Student,
                    IsVerified: true,
                    PasswordHash: _hasher.HashPassword("Student@123"))
            };

            using (var firstInit = new SqliteDbContext(new SqliteSettings
            {
                ConnectionString = connStr,
                SeedDemoRooms = false,
                SeedAccounts = accounts
            }))
            {
                firstInit.EnsureSchemaInitialized();
            }

            using (var secondInit = new SqliteDbContext(new SqliteSettings
            {
                ConnectionString = connStr,
                SeedDemoRooms = false,
                SeedAccounts = accounts
            }))
            {
                secondInit.EnsureSchemaInitialized();
                using var conn = secondInit.CreateOpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM Users;";
                var userCount = Convert.ToInt64(cmd.ExecuteScalar());
                Assert.That(userCount, Is.EqualTo(1));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task SeedAccounts_WithSeedDemoRoomsFalse_StillSeedsConfiguredAccounts()
    {
        var adminAccount = new SeedAccount(
            Id: "seed-admin",
            Email: "admin@roomtorentradar.local",
            FullName: "Quản Trị Demo",
            Role: UserRole.Admin,
            IsVerified: true,
            PasswordHash: _hasher.HashPassword("Admin@12345"));

        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = false,
            SeedAccounts = new[] { adminAccount }
        });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var admin = await repo.GetByEmailAsync("admin@roomtorentradar.local");

        using var conn = dbContext.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Rooms;";
        var roomCount = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Multiple(() =>
        {
            Assert.That(admin, Is.Not.Null);
            Assert.That(admin!.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(roomCount, Is.Zero);
        });
    }

    [Test]
    public async Task SeedAccounts_ConfiguredSeedLandlord_WinsOverFallbackRowWithoutPassword()
    {
        var configuredLandlord = new SeedAccount(
            Id: "seed-landlord",
            Email: "seed-landlord@roomtorentradar.local",
            FullName: "Seed Landlord",
            Role: UserRole.Landlord,
            IsVerified: true,
            PasswordHash: _hasher.HashPassword("Landlord@123"),
            PhoneNumber: "0912000001");

        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true,
            SeedAccounts = new[] { configuredLandlord }
        });
        IUserRepository repo = new SqliteUserRepository(dbContext);

        var landlord = await repo.GetByIdAsync("seed-landlord");

        Assert.Multiple(() =>
        {
            Assert.That(landlord, Is.Not.Null);
            Assert.That(landlord!.PasswordHash, Is.Not.Null.And.Not.Empty);
            Assert.That(landlord.PhoneNumber, Is.EqualTo("0912000001"));
            Assert.That(landlord.IsVerified, Is.True);
            Assert.That(_hasher.VerifyPassword("Landlord@123", landlord.PasswordHash!), Is.True);
        });
    }
}

using Microsoft.Extensions.Configuration;
using RoomToRentRadar.Api.Configuration;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Security;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class SeedAccountsLoaderTests
{
    private readonly Pbkdf2PasswordHasherAdapter _hasher = new();

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    [Test]
    public void Loader_Production_IgnoresDemoDataAccounts_ReadsOnlyAdminSeedAccounts()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Admin:SeedAccounts:0:Email"] = "prod.admin@ictu.edu.vn",
            ["Admin:SeedAccounts:0:FullName"] = "Production Admin",
            ["Admin:SeedAccounts:0:Password"] = "Admin@12345",
            ["DemoData:Accounts:0:Id"] = "seed-student",
            ["DemoData:Accounts:0:Email"] = "student@roomtorentradar.local",
            ["DemoData:Accounts:0:FullName"] = "Sinh Viên Demo",
            ["DemoData:Accounts:0:Role"] = "Student",
            ["DemoData:Accounts:0:IsVerified"] = "true",
            ["DemoData:Accounts:0:Password"] = "Student@123"
        });

        var accounts = SeedAccountsLoader.Load(config, isDevelopment: false, _hasher);

        Assert.That(accounts, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(accounts[0].Id, Is.EqualTo("seed-admin-1"));
            Assert.That(accounts[0].Email, Is.EqualTo("prod.admin@ictu.edu.vn"));
            Assert.That(accounts[0].Role, Is.EqualTo(UserRole.Admin));
            Assert.That(accounts[0].IsVerified, Is.True);
            Assert.That(_hasher.VerifyPassword("Admin@12345", accounts[0].PasswordHash), Is.True);
        });
    }

    [Test]
    public void Loader_Development_ReadsAdminAndDemoDataAccounts()
    {
        var values = new Dictionary<string, string?>
        {
            ["Admin:SeedAccounts:0:Id"] = "seed-admin",
            ["Admin:SeedAccounts:0:Email"] = "admin@roomtorentradar.local",
            ["Admin:SeedAccounts:0:FullName"] = "Quản Trị Demo",
            ["Admin:SeedAccounts:0:Password"] = "Admin@12345",
            ["DemoData:Accounts:0:Id"] = "seed-student",
            ["DemoData:Accounts:0:Email"] = "student@roomtorentradar.local",
            ["DemoData:Accounts:0:FullName"] = "Sinh Viên Demo",
            ["DemoData:Accounts:0:Role"] = "Student",
            ["DemoData:Accounts:0:IsVerified"] = "true",
            ["DemoData:Accounts:0:StudentCode"] = "dtc235140001",
            ["DemoData:Accounts:0:Password"] = "Student@123",
            ["DemoData:Accounts:1:Id"] = "seed-landlord",
            ["DemoData:Accounts:1:Email"] = "seed-landlord@roomtorentradar.local",
            ["DemoData:Accounts:1:FullName"] = "Seed Landlord",
            ["DemoData:Accounts:1:Role"] = "Landlord",
            ["DemoData:Accounts:1:IsVerified"] = "true",
            ["DemoData:Accounts:1:PhoneNumber"] = "0912000001",
            ["DemoData:Accounts:1:Password"] = "Landlord@123",
            ["DemoData:Accounts:2:Id"] = "seed-landlord-pending",
            ["DemoData:Accounts:2:Email"] = "landlord.pending@roomtorentradar.local",
            ["DemoData:Accounts:2:FullName"] = "Chủ Trọ Chờ Duyệt",
            ["DemoData:Accounts:2:Role"] = "Landlord",
            ["DemoData:Accounts:2:IsVerified"] = "false",
            ["DemoData:Accounts:2:PhoneNumber"] = "0912000002",
            ["DemoData:Accounts:2:Password"] = "Landlord@123"
        };

        var config = BuildConfig(values);
        var firstLoad = SeedAccountsLoader.Load(config, isDevelopment: true, _hasher);
        var secondLoad = SeedAccountsLoader.Load(config, isDevelopment: true, _hasher);

        Assert.That(firstLoad, Has.Count.EqualTo(4));
        var admin = firstLoad.Single(a => a.Id == "seed-admin");
        var student = firstLoad.Single(a => a.Id == "seed-student");
        var landlord = firstLoad.Single(a => a.Id == "seed-landlord");
        var pending = firstLoad.Single(a => a.Id == "seed-landlord-pending");

        Assert.Multiple(() =>
        {
            Assert.That(admin.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(admin.IsVerified, Is.True);

            Assert.That(student.Role, Is.EqualTo(UserRole.Student));
            Assert.That(student.IsVerified, Is.True);
            Assert.That(student.StudentCode, Is.EqualTo("DTC235140001"));

            Assert.That(landlord.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(landlord.IsVerified, Is.True);
            Assert.That(landlord.PhoneNumber, Is.EqualTo("0912000001"));

            Assert.That(pending.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(pending.IsVerified, Is.False);
            Assert.That(pending.PhoneNumber, Is.EqualTo("0912000002"));

            // Salted PBKDF2 produces distinct hashes across invocations for identical passwords.
            Assert.That(landlord.PasswordHash, Is.Not.EqualTo(pending.PasswordHash));
            Assert.That(
                firstLoad.Single(a => a.Id == "seed-student").PasswordHash,
                Is.Not.EqualTo(secondLoad.Single(a => a.Id == "seed-student").PasswordHash));
        });
    }

    [TestCase("weak")]
    [TestCase("onlylowercase123")]
    [TestCase("")]
    [TestCase(null)]
    public void Loader_WeakOrMissingPassword_ThrowsWithoutEchoingPassword(string? badPassword)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Admin:SeedAccounts:0:Email"] = "admin@roomtorentradar.local",
            ["Admin:SeedAccounts:0:FullName"] = "Admin",
            ["Admin:SeedAccounts:0:Password"] = badPassword
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SeedAccountsLoader.Load(config, isDevelopment: true, _hasher));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("admin@roomtorentradar.local"));
        if (!string.IsNullOrEmpty(badPassword))
        {
            Assert.That(ex.Message, Does.Not.Contain(badPassword));
        }
    }

    [Test]
    public void Loader_DemoDataRoleAdmin_Throws()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["DemoData:Accounts:0:Id"] = "bad-admin",
            ["DemoData:Accounts:0:Email"] = "fakeadmin@roomtorentradar.local",
            ["DemoData:Accounts:0:FullName"] = "Fake Admin",
            ["DemoData:Accounts:0:Role"] = "Admin",
            ["DemoData:Accounts:0:IsVerified"] = "true",
            ["DemoData:Accounts:0:Password"] = "Admin@12345"
        });

        Assert.Throws<InvalidOperationException>(() =>
            SeedAccountsLoader.Load(config, isDevelopment: true, _hasher));
    }

    [Test]
    public void Loader_LandlordWithoutPhone_Throws()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["DemoData:Accounts:0:Id"] = "seed-landlord",
            ["DemoData:Accounts:0:Email"] = "landlord@roomtorentradar.local",
            ["DemoData:Accounts:0:FullName"] = "Landlord",
            ["DemoData:Accounts:0:Role"] = "Landlord",
            ["DemoData:Accounts:0:IsVerified"] = "true",
            ["DemoData:Accounts:0:Password"] = "Landlord@123"
        });

        Assert.Throws<InvalidOperationException>(() =>
            SeedAccountsLoader.Load(config, isDevelopment: true, _hasher));
    }

    [Test]
    public void Loader_DuplicateEmail_Throws()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Admin:SeedAccounts:0:Email"] = "dup@roomtorentradar.local",
            ["Admin:SeedAccounts:0:FullName"] = "Admin",
            ["Admin:SeedAccounts:0:Password"] = "Admin@12345",
            ["DemoData:Accounts:0:Id"] = "seed-student",
            ["DemoData:Accounts:0:Email"] = "DUP@roomtorentradar.local",
            ["DemoData:Accounts:0:FullName"] = "Student",
            ["DemoData:Accounts:0:Role"] = "Student",
            ["DemoData:Accounts:0:IsVerified"] = "true",
            ["DemoData:Accounts:0:Password"] = "Student@123"
        });

        Assert.Throws<InvalidOperationException>(() =>
            SeedAccountsLoader.Load(config, isDevelopment: true, _hasher));
    }
}

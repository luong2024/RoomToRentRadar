using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class InMemoryUserRepositoryTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task AddGet_RoundTripsNewFields()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user = ReconstituteUser("u1", "student@ictu.edu.vn", selectedCampusId: "TNUS", preferredLanguage: "vi", version: 3);
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync("u1");
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.SelectedCampusId, Is.EqualTo("TNUS"));
            Assert.That(loaded.PreferredLanguage, Is.EqualTo("vi"));
            Assert.That(loaded.Version, Is.EqualTo(3));
            Assert.That(loaded.ConcurrencyToken, Is.EqualTo("3"));
        });

        var defaultUser = User.RegisterLocal("default@ictu.edu.vn", "hash", "Default", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(defaultUser);

        var loadedDefault = await repo.GetByIdAsync(defaultUser.Id);
        Assert.Multiple(() =>
        {
            Assert.That(loadedDefault!.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(loadedDefault.PreferredLanguage, Is.EqualTo("en"));
            Assert.That(loadedDefault.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Update_StaleVersion_Throws()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        var loaded1 = await repo.GetByIdAsync(user.Id);
        var loaded2 = await repo.GetByIdAsync(user.Id);

        loaded1!.RecordLoginSuccess(FixedNow.AddMinutes(1));
        await repo.UpdateAsync(loaded1);

        loaded2!.RecordLoginSuccess(FixedNow.AddMinutes(2));
        var ex = Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repo.UpdateAsync(loaded2));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task Update_UnknownUser_Throws()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var unknown = ReconstituteUser("unknown-user", "unknown@ictu.edu.vn");

        var ex = Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repo.UpdateAsync(unknown));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task Update_BumpsVersionAndToken()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);
        Assert.Multiple(() =>
        {
            Assert.That(user.Version, Is.EqualTo(1));
            Assert.That(user.ConcurrencyToken, Is.EqualTo("1"));
        });

        var loaded = await repo.GetByIdAsync(user.Id);
        loaded!.RecordLoginSuccess(FixedNow.AddMinutes(1));
        await repo.UpdateAsync(loaded);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Version, Is.EqualTo(2));
            Assert.That(loaded.ConcurrencyToken, Is.EqualTo("2"));
        });

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.That(reloaded!.Version, Is.EqualTo(2));

        loaded.RecordLoginFailure(FixedNow.AddMinutes(2));
        await repo.UpdateAsync(loaded);
        Assert.Multiple(() =>
        {
            Assert.That(loaded.Version, Is.EqualTo(3));
            Assert.That(loaded.ConcurrencyToken, Is.EqualTo("3"));
        });
    }

    [Test]
    public async Task Clone_IsolatesStoredInstance()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user = User.RegisterLocal("student@ictu.edu.vn", "hash", "Student", UserRole.Student, null, "DTC1", FixedNow);
        await repo.AddAsync(user);

        var first = await repo.GetByIdAsync(user.Id);
        var second = await repo.GetByIdAsync(user.Id);
        Assert.That(first, Is.Not.SameAs(second));

        first!.RecordLoginFailure(FixedNow.AddMinutes(1));
        var third = await repo.GetByIdAsync(user.Id);
        Assert.That(third!.FailedLoginCount, Is.EqualTo(0));

        user.RecordLoginFailure(FixedNow.AddMinutes(2));
        var fourth = await repo.GetByIdAsync(user.Id);
        Assert.That(fourth!.FailedLoginCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Add_DuplicateEmail_Throws()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user1 = User.RegisterLocal("dup@ictu.edu.vn", "hash1", "User 1", UserRole.Student, null, "DTC1", FixedNow);
        var user2 = User.RegisterLocal("DUP@ictu.edu.vn", "hash2", "User 2", UserRole.Student, null, "DTC2", FixedNow);
        await repo.AddAsync(user1);

        var ex = Assert.ThrowsAsync<DuplicateEmailException>(async () => await repo.AddAsync(user2));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task Update_PersistsChangedCampusAndLanguage()
    {
        IUserRepository repo = new InMemoryUserRepository();

        var user = User.RegisterLocal("campus@ictu.edu.vn", "hash", "Nguyen Van A", UserRole.Student, null, null, FixedNow);
        await repo.AddAsync(user);

        var loaded = await repo.GetByIdAsync(user.Id);
        var changed = ReconstituteUser(loaded!.Id, loaded.Email.Value, selectedCampusId: "SFL", preferredLanguage: "vi", version: loaded.Version);
        await repo.UpdateAsync(changed);

        var reloaded = await repo.GetByIdAsync(user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded!.SelectedCampusId, Is.EqualTo("SFL"));
            Assert.That(reloaded.PreferredLanguage, Is.EqualTo("vi"));
            Assert.That(reloaded.Version, Is.EqualTo(2));
        });
    }

    private static User ReconstituteUser(
        string id,
        string email,
        string selectedCampusId = User.DefaultSelectedCampusId,
        string preferredLanguage = User.DefaultPreferredLanguage,
        int version = 1)
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
            selectedCampusId: selectedCampusId,
            preferredLanguage: preferredLanguage,
            version: version);
    }
}
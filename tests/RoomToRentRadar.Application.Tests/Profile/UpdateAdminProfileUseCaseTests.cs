using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using User = RoomToRentRadar.Domain.Entities.User;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class UpdateAdminProfileUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    private FakeAdminUserRepository _userRepository = null!;
    private FakeUserProfileRepository _profileRepository = null!;
    private TimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new FakeAdminUserRepository();
        _profileRepository = new FakeUserProfileRepository();
        _timeProvider = new FixedTimeProvider(FixedNow);
    }

    [Test]
    public async Task ExecuteAsync_WhenAdminUpdatesProfileWithoutAvatarAndBio_ShouldPreserveExistingAvatarUrlAndBio()
    {
        // Arrange
        var adminUser = User.ProvisionAdminUser(
            EmailAddress.Create("admin@roomtorentradar.vn"),
            "hashed_pwd",
            FixedNow);
        var adminId = adminUser.Id;
        _userRepository.Add(adminUser);

        var existingProfile = UserProfile.CreateDefault(adminId, UserRole.Admin, FixedNow);
        existingProfile.UpdateBasicInfo(
            fullName: "Old Admin Name",
            phone: PhoneNumber.CreateOptional("0911111111"),
            avatarUrl: "https://storage.roomtorentradar.vn/avatars/admin_original.png",
            bio: "Original admin biographical description",
            utcNow: FixedNow);
        existingProfile.UpdateAdminDetails(new AdminDetails("STF-001", "IT", "Manager"), FixedNow);
        await _profileRepository.AddAsync(existingProfile);

        var useCase = new UpdateAdminProfileUseCase(_profileRepository, _userRepository, _timeProvider);
        var command = new UpdateAdminCommand(
            FullName: "Updated Admin Full Name",
            PhoneNumber: "0922222222",
            StaffCode: "STF-999",
            Department: "Operations",
            Title: "Chief Operations Officer",
            AvatarUrl: null, // Request does not provide avatar
            Bio: null);      // Request does not provide bio

        // Act
        var result = await useCase.ExecuteAsync(adminId, command);

        // Assert - Data is preserved and NOT wiped out
        Assert.Multiple(() =>
        {
            Assert.That(result.FullName, Is.EqualTo("Updated Admin Full Name"));
            Assert.That(result.PhoneNumber, Is.EqualTo("0922222222"));
            Assert.That(result.AvatarUrl, Is.EqualTo("https://storage.roomtorentradar.vn/avatars/admin_original.png"),
                "Existing AvatarUrl must be preserved when request does not update it.");
            Assert.That(result.Bio, Is.EqualTo("Original admin biographical description"),
                "Existing Bio must be preserved when request does not update it.");
            Assert.That(result.Admin!.StaffCode, Is.EqualTo("STF-999"));
            Assert.That(result.Admin!.Department, Is.EqualTo("Operations"));
            Assert.That(result.Admin!.Title, Is.EqualTo("Chief Operations Officer"));
        });

        var persisted = await _profileRepository.GetByUserIdAsync(adminId);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.AvatarUrl, Is.EqualTo("https://storage.roomtorentradar.vn/avatars/admin_original.png"));
            Assert.That(persisted.Bio, Is.EqualTo("Original admin biographical description"));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenAdminUpdatesProfileWithNewAvatarAndBio_ShouldUpdateThem()
    {
        // Arrange
        var adminUser = User.ProvisionAdminUser(
            EmailAddress.Create("admin.update@roomtorentradar.vn"),
            "hashed_pwd",
            FixedNow);
        var adminId = adminUser.Id;
        _userRepository.Add(adminUser);

        var existingProfile = UserProfile.CreateDefault(adminId, UserRole.Admin, FixedNow);
        existingProfile.UpdateBasicInfo("Old Name", null, "https://old.png", "Old bio", FixedNow);
        await _profileRepository.AddAsync(existingProfile);

        var useCase = new UpdateAdminProfileUseCase(_profileRepository, _userRepository, _timeProvider);
        var command = new UpdateAdminCommand(
            FullName: "New Admin",
            PhoneNumber: null,
            StaffCode: "STF-002",
            Department: "Security",
            Title: "Officer",
            AvatarUrl: "https://storage.roomtorentradar.vn/avatars/new_admin.png",
            Bio: "Updated biographical description");

        // Act
        var result = await useCase.ExecuteAsync(adminId, command);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.AvatarUrl, Is.EqualTo("https://storage.roomtorentradar.vn/avatars/new_admin.png"));
            Assert.That(result.Bio, Is.EqualTo("Updated biographical description"));
        });
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _fixedUtcNow;

    public FixedTimeProvider(DateTimeOffset fixedUtcNow)
    {
        _fixedUtcNow = fixedUtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _fixedUtcNow;
}

internal sealed class FakeAdminUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = new();

    public void Add(User user) => _users[user.Id] = user;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<User?> GetByExternalLoginAsync(string provider, string providerSubject, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public void Update(User user) => _users[user.Id] = user;
}

internal sealed class FakeUserProfileRepository : IUserProfileRepository
{
    private readonly Dictionary<Guid, UserProfile> _profiles = new();

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(userId, out var p);
        return Task.FromResult(p);
    }

    public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.UserId] = profile;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.UserId] = profile;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}

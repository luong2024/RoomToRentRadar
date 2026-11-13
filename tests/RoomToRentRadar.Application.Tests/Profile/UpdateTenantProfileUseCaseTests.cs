using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class UpdateTenantProfileUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExecuteAsync_WhenProfileDoesNotExist_ShouldCallAddAsyncAndNotUpdateAsync()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("Nguyen Van A", "0912345678", null, "Bio", "SV123", "ICTU", "CNTT", new DateOnly(2003, 1, 1), "Male", null, null, 2500000m);
        var result = await useCase.ExecuteAsync(userId, cmd);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("Nguyen Van A"));
            Assert.That(repo.AddCallCount, Is.EqualTo(1), "AddAsync must be called for new profile");
            Assert.That(repo.UpdateCallCount, Is.EqualTo(0), "UpdateAsync must NOT be called for new profile");
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenProfileAlreadyExists_ShouldCallUpdateAsyncAndNotAddAsync()
    {
        var userId = Guid.NewGuid();
        var existing = UserProfile.CreateDefault(userId, UserRole.Student, FixedNow);
        existing.SetVersion("\"initial-etag-123\"");
        var repo = new TrackingUserProfileRepository(existing);
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("Nguyen Van B", "0987654321", null, "Bio Updated", "SV123", "ICTU", "CNTT", new DateOnly(2003, 1, 1), "Male", null, null, 3000000m);
        var result = await useCase.ExecuteAsync(userId, cmd);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.FullName, Is.EqualTo("Nguyen Van B"));
            Assert.That(repo.AddCallCount, Is.EqualTo(0), "AddAsync must NOT be called for existing profile");
            Assert.That(repo.UpdateCallCount, Is.EqualTo(1), "UpdateAsync must be called for existing profile");
            Assert.That(repo.LastUpdatedProfile?.Version, Is.EqualTo("\"initial-etag-123\""), "UpdateAsync must retain loaded ETag");
        });
    }

    [Test]
    public void ExecuteAsync_WhenAddAsyncEncountersConflict_ShouldThrowConcurrencyConflictException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository { ShouldThrowConflictOnAdd = true };
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("Nguyen Van C", null, null, null, null, null, null, null, null, null, null, null);

        Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));
        Assert.That(repo.AddCallCount, Is.EqualTo(1));
        Assert.That(repo.UpdateCallCount, Is.EqualTo(0));
    }

    [Test]
    public void ExecuteAsync_WhenFullNameIsWhitespace_ShouldThrowProfileValidationException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("   ", null, null, null, null, null, null, null, null, null, null, null);

        var ex = Assert.ThrowsAsync<ProfileValidationException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Full name cannot be empty"));
    }

    [Test]
    public void ExecuteAsync_WhenGenderIsInvalidStringNumber_ShouldThrowProfileValidationException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("Nguyen Van D", null, null, null, null, null, null, null, "999", null, null, null);

        var ex = Assert.ThrowsAsync<ProfileValidationException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Invalid gender value '999'"));
    }

    [Test]
    public void ExecuteAsync_WhenDateOfBirthIsInFuture_ShouldThrowProfileValidationException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var futureDob = DateOnly.FromDateTime(FixedNow.DateTime.AddDays(1));
        var cmd = new UpdateTenantCommand("Nguyen Van E", null, null, null, null, null, null, futureDob, "Male", null, null, null);

        var ex = Assert.ThrowsAsync<ProfileValidationException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Date of birth cannot be in the future"));
    }

    [Test]
    public void ExecuteAsync_WhenMaxBudgetIsNegative_ShouldThrowProfileValidationException()
    {
        var userId = Guid.NewGuid();
        var repo = new TrackingUserProfileRepository();
        var useCase = new UpdateTenantProfileUseCase(repo, new FixedTimeProvider(FixedNow));

        var cmd = new UpdateTenantCommand("Nguyen Van F", null, null, null, null, null, null, null, "Male", null, null, -500000m);

        var ex = Assert.ThrowsAsync<ProfileValidationException>(async () =>
            await useCase.ExecuteAsync(userId, cmd));

        Assert.That(ex.Message, Does.Contain("Maximum budget cannot be negative"));
    }
}

internal class TrackingUserProfileRepository : IUserProfileRepository
{
    private UserProfile? _profile;
    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public bool ShouldThrowConflictOnAdd { get; set; }
    public UserProfile? LastUpdatedProfile { get; private set; }

    public TrackingUserProfileRepository(UserProfile? profile = null)
    {
        _profile = profile;
    }

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_profile);

    public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        if (ShouldThrowConflictOnAdd)
        {
            throw new ConcurrencyConflictException($"Profile for user '{profile.UserId}' already exists.");
        }
        _profile = profile;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastUpdatedProfile = profile;
        _profile = profile;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserProfile>>(Array.Empty<UserProfile>());
}

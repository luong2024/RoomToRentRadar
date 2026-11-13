using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class GetMyProfileUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExecuteAsync_WhenConcurrentProfileCreationConflictOccurs_ShouldCatchConflictAndReturnProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var timeProvider = new FixedTimeProvider(FixedNow);
        var concurrentRepo = new ConcurrentlyConflictedProfileRepository(userId, UserRole.Student, FixedNow);
        var useCase = new GetMyProfileUseCase(concurrentRepo, timeProvider: timeProvider);

        // Act - should NOT throw ConcurrencyConflictException or Cosmos 409
        var result = await useCase.ExecuteAsync(userId, "STUDENT");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.UserId, Is.EqualTo(userId));
            Assert.That(result.Role, Is.EqualTo("STUDENT"));
            Assert.That(concurrentRepo.AddAttemptCount, Is.EqualTo(1));
            Assert.That(concurrentRepo.ConflictResolved, Is.True);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenProfileDoesNotExist_CreatesAndReturnsDefaultProfile()
    {
        var userId = Guid.NewGuid();
        var timeProvider = new FixedTimeProvider(FixedNow);
        var repo = new FakeUserProfileRepository();
        var useCase = new GetMyProfileUseCase(repo, timeProvider: timeProvider);

        var result = await useCase.ExecuteAsync(userId, "STUDENT");

        Assert.Multiple(() =>
        {
            Assert.That(result.UserId, Is.EqualTo(userId));
            Assert.That(result.Role, Is.EqualTo("STUDENT"));
            Assert.That(result.Tenant, Is.Not.Null);
        });
    }
}

internal sealed class ConcurrentlyConflictedProfileRepository : IUserProfileRepository
{
    private readonly Guid _targetUserId;
    private readonly UserProfile _winnerProfile;
    public int AddAttemptCount { get; private set; }
    public bool ConflictResolved { get; private set; }

    public ConcurrentlyConflictedProfileRepository(Guid userId, UserRole role, DateTimeOffset now)
    {
        _targetUserId = userId;
        _winnerProfile = UserProfile.CreateDefault(userId, role, now);
    }

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // First read returns null (profile not found), prompting creation
        if (AddAttemptCount == 0)
        {
            return Task.FromResult<UserProfile?>(null);
        }

        // After the conflict occurs, the competing created profile is returned
        ConflictResolved = true;
        return Task.FromResult<UserProfile?>(_winnerProfile);
    }

    public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        AddAttemptCount++;
        // Simulate Cosmos DB 409 conflict translated by repository into ConcurrencyConflictException
        throw new ConcurrencyConflictException($"Profile for user '{profile.UserId}' was created concurrently.");
    }

    public Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}

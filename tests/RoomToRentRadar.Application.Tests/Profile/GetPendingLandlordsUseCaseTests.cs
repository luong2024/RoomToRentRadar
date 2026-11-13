using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Profile;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Tests.Profile;

[TestFixture]
public class GetPendingLandlordsUseCaseTests
{
    [Test]
    public void ExecuteAsync_WhenSkipIsNegative_ShouldThrowRequestValidationException()
    {
        var repo = new StubUserProfileRepository();
        var useCase = new GetPendingLandlordsUseCase(repo);

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await useCase.ExecuteAsync(skip: -1, take: 20));

        Assert.That(ex!.Errors, Does.ContainKey("skip"));
    }

    [Test]
    public void ExecuteAsync_WhenTakeIsZero_ShouldThrowRequestValidationException()
    {
        var repo = new StubUserProfileRepository();
        var useCase = new GetPendingLandlordsUseCase(repo);

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await useCase.ExecuteAsync(skip: 0, take: 0));

        Assert.That(ex!.Errors, Does.ContainKey("take"));
    }

    [Test]
    public void ExecuteAsync_WhenTakeIsGreaterThan100_ShouldThrowRequestValidationException()
    {
        var repo = new StubUserProfileRepository();
        var useCase = new GetPendingLandlordsUseCase(repo);

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await useCase.ExecuteAsync(skip: 0, take: 101));

        Assert.That(ex!.Errors, Does.ContainKey("take"));
    }

    [Test]
    public async Task ExecuteAsync_WhenPaginationIsValid_ShouldPassSkipAndTakeToRepository()
    {
        var repo = new StubUserProfileRepository();
        var useCase = new GetPendingLandlordsUseCase(repo);

        var result = await useCase.ExecuteAsync(skip: 10, take: 25);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(repo.LastSkip, Is.EqualTo(10));
            Assert.That(repo.LastTake, Is.EqualTo(25));
        });
    }

    private sealed class StubUserProfileRepository : IUserProfileRepository
    {
        public int LastSkip { get; private set; }
        public int LastTake { get; private set; }

        public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(null);

        public Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<UserProfile>> GetPendingLandlordsAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        {
            LastSkip = skip;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<UserProfile>>([]);
        }
    }
}

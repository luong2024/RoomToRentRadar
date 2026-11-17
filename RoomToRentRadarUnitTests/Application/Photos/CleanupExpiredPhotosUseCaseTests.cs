using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

/// <summary>
/// BR-MED-03: the hourly sweep removes unattached uploads older than 24 h, deletes their files and
/// returns how many rows really disappeared (a photo attached concurrently is skipped).
/// </summary>
[TestFixture]
public class CleanupExpiredPhotosUseCaseTests
{
    private IRoomPhotoRepository _photoRepository = null!;
    private FakePhotoStorage _storage = null!;
    private CleanupExpiredPhotosUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _photoRepository = Substitute.For<IRoomPhotoRepository>();
        _storage = new FakePhotoStorage();
        _useCase = new CleanupExpiredPhotosUseCase(_photoRepository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_RemovesExpiredRowsAndTheirFiles()
    {
        var expired = new[]
        {
            AppTestData.Photo("photo-a", createdAtUtc: DateTime.UtcNow.AddHours(-30)),
            AppTestData.Photo("photo-b", createdAtUtc: DateTime.UtcNow.AddHours(-25))
        };
        DateTime? cutoff = null;

        _photoRepository
            .ListExpiredUnattachedAsync(Arg.Do<DateTime>(value => cutoff = value), Arg.Any<CancellationToken>())
            .Returns(expired);
        _photoRepository
            .DeleteUnattachedAsync("photo-a", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Deleted, expired[0]));
        _photoRepository
            .DeleteUnattachedAsync("photo-b", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Deleted, expired[1]));

        var removed = await _useCase.ExecuteAsync();

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(cutoff, Is.Not.Null);
        Assert.That(
            cutoff!.Value,
            Is.EqualTo(DateTime.UtcNow - PhotoFileRules.UnattachedPhotoLifetime).Within(TimeSpan.FromMinutes(1)),
            "BR-MED-03: cutoff = now - 24h.");
        Assert.That(_storage.DeletedKeys, Has.Count.EqualTo(6), "Ba file cho mỗi ảnh.");
    }

    [Test]
    public async Task ExecuteAsync_PhotoAttachedInTheMeantime_IsSkipped()
    {
        var photo = AppTestData.Photo("photo-race");
        _photoRepository
            .ListExpiredUnattachedAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new[] { photo });
        _photoRepository
            .DeleteUnattachedAsync("photo-race", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Attached, photo));

        var removed = await _useCase.ExecuteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.Zero);
            Assert.That(_storage.DeletedKeys, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_NothingExpired_ReturnsZero()
    {
        _photoRepository
            .ListExpiredUnattachedAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomToRentRadar.Domain.Entities.RoomPhoto>());

        var removed = await _useCase.ExecuteAsync();

        Assert.That(removed, Is.Zero);
    }
}

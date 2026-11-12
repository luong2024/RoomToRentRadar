using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

// PhotoCleanupService — BR-MED-03.
[TestFixture]
public class CleanupExpiredPhotosUseCaseTests
{
    private const string UserId = "landlord-001";

    private IRoomPhotoRepository _repository = null!;
    private IPhotoStoragePort _storage = null!;
    private CleanupExpiredPhotosUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _storage = Substitute.For<IPhotoStoragePort>();
        _sut = new CleanupExpiredPhotosUseCase(_repository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_WhenExpiredPhotosExist_DeletesMetadataAndDistinctFilesAndReturnsCount()
    {
        var first = CreatePhoto("photo-1");
        var second = CreatePhoto("photo-2");
        _repository.ListExpiredUnattachedAsync(
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { first, second });
        _repository.DeleteUnattachedAsync(first.Id, UserId, Arg.Any<CancellationToken>())
            .Returns(RoomPhotoDeleteOutcome.Deleted);
        _repository.DeleteUnattachedAsync(second.Id, UserId, Arg.Any<CancellationToken>())
            .Returns(RoomPhotoDeleteOutcome.Deleted);

        var deleted = await _sut.ExecuteAsync();

        Assert.That(deleted, Is.EqualTo(2));
        await _storage.Received(1).DeleteAsync(first.OriginalPath, CancellationToken.None);
        await _storage.Received(1).DeleteAsync(first.MediumPath, CancellationToken.None);
        await _storage.Received(1).DeleteAsync(first.ThumbPath, CancellationToken.None);
        await _storage.Received(1).DeleteAsync(second.OriginalPath, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoWasAttachedBeforeDelete_SkipsItsFilesAndContinues()
    {
        var attached = CreatePhoto("photo-1");
        var expired = CreatePhoto("photo-2");
        _repository.ListExpiredUnattachedAsync(
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { attached, expired });
        _repository.DeleteUnattachedAsync(attached.Id, UserId, Arg.Any<CancellationToken>())
            .Returns(RoomPhotoDeleteOutcome.Attached);
        _repository.DeleteUnattachedAsync(expired.Id, UserId, Arg.Any<CancellationToken>())
            .Returns(RoomPhotoDeleteOutcome.Deleted);

        var deleted = await _sut.ExecuteAsync();

        Assert.That(deleted, Is.EqualTo(1));
        await _storage.DidNotReceive().DeleteAsync(attached.OriginalPath, Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync(expired.OriginalPath, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenStorageDeleteFails_StillCountsDeletedAndContinues()
    {
        var photo = CreatePhoto("photo-1");
        _repository.ListExpiredUnattachedAsync(
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { photo });
        _repository.DeleteUnattachedAsync(photo.Id, UserId, Arg.Any<CancellationToken>())
            .Returns(RoomPhotoDeleteOutcome.Deleted);
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("storage offline"));

        var deleted = await _sut.ExecuteAsync();

        Assert.That(deleted, Is.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_WhenNoExpiredPhotos_ReturnsZeroAndDeletesNothing()
    {
        _repository.ListExpiredUnattachedAsync(
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());

        var deleted = await _sut.ExecuteAsync();

        Assert.That(deleted, Is.Zero);
        await _repository.DidNotReceiveWithAnyArgs().DeleteUnattachedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RoomPhoto CreatePhoto(string id)
    {
        var path = $"rooms/2026/11/{id}/original.jpg";
        return new RoomPhoto(
            uploadedByUserId: UserId,
            kind: PhotoKind.Standard,
            contentType: "image/jpeg",
            sizeBytes: 2048,
            width: 800,
            height: 600,
            originalPath: path,
            mediumPath: $"rooms/2026/11/{id}/medium.jpg",
            thumbPath: $"rooms/2026/11/{id}/thumb.jpg",
            id: id,
            createdAtUtc: DateTime.UtcNow.AddHours(-25));
    }
}

using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

[TestFixture]
public class DeletePhotoUseCaseTests
{
    private const string PhotoId = "photo-001";
    private const string UserId = "landlord-001";
    private const string OtherUserId = "intruder-999";
    private const string DefaultPath = "rooms/2026/11/photo-001/original.jpg";

    private IRoomPhotoRepository _repository = null!;
    private IPhotoStoragePort _storage = null!;
    private DeletePhotoUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _storage = Substitute.For<IPhotoStoragePort>();
        _sut = new DeletePhotoUseCase(_repository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoIsNotFound_ReturnsNotFoundAndDeletesNothing()
    {
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.NotFound));
        await AssertNothingDeletedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoIsAttachedToRoom_ReturnsAttachedAndDeletesNothing()
    {
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>())
            .Returns(CreatePhoto(roomId: "room-123"));

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.Attached));
        await AssertNothingDeletedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WhenUnattachedOwnPhoto_DeletesMetadataThenEveryDistinctFile()
    {
        var photo = CreatePhoto(
            originalPath: "rooms/2026/11/photo-001/original.jpg",
            mediumPath: "rooms/2026/11/photo-001/medium.jpg",
            thumbPath: "rooms/2026/11/photo-001/thumb.jpg");
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.DeleteUnattachedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.Deleted));

        // Metadata first, then each distinct storage path in order (D10).
        Received.InOrder(() =>
        {
            _repository.DeleteUnattachedAsync(PhotoId, UserId, Arg.Any<CancellationToken>());
            _storage.DeleteAsync(photo.OriginalPath, Arg.Any<CancellationToken>());
            _storage.DeleteAsync(photo.MediumPath, Arg.Any<CancellationToken>());
            _storage.DeleteAsync(photo.ThumbPath, Arg.Any<CancellationToken>());
        });

        await _storage.Received(3).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storage.Received(1).DeleteAsync(photo.OriginalPath, CancellationToken.None);
        await _storage.Received(1).DeleteAsync(photo.MediumPath, CancellationToken.None);
        await _storage.Received(1).DeleteAsync(photo.ThumbPath, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenAllRenditionsShareOnePath_DeletesFileOnlyOnce()
    {
        var photo = CreatePhoto();
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.DeleteUnattachedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.Deleted));
        await _storage.Received(1).DeleteAsync(DefaultPath, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenMetadataDeleteLosesRace_ReturnsNotFoundAndSkipsStorage()
    {
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(CreatePhoto());
        _repository.DeleteUnattachedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.NotFound));
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenStorageDeleteFails_StillReturnsDeleted()
    {
        var photo = CreatePhoto();
        _repository.GetOwnedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(photo);
        _repository.DeleteUnattachedAsync(PhotoId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("storage offline"));

        var result = await _sut.ExecuteAsync(PhotoId, UserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.Deleted));
        await _storage.Received(1).DeleteAsync(DefaultPath, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoBelongsToAnotherUser_ReturnsNotFoundAndDeletesNothing()
    {
        // The repository already scopes by owner, so another user's photo looks missing.
        _repository.GetOwnedAsync(PhotoId, OtherUserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.ExecuteAsync(PhotoId, OtherUserId);

        Assert.That(result, Is.EqualTo(DeletePhotoResult.NotFound));
        await _repository.Received(1).GetOwnedAsync(PhotoId, OtherUserId, Arg.Any<CancellationToken>());
        await AssertNothingDeletedAsync();
    }

    [TestCase("", UserId)]
    [TestCase("   ", UserId)]
    [TestCase(PhotoId, "")]
    [TestCase(PhotoId, "   ")]
    public async Task ExecuteAsync_WithBlankId_ThrowsAndQueriesNothing(string photoId, string userId)
    {
        Assert.ThrowsAsync<DomainValidationException>(
            async () => await _sut.ExecuteAsync(photoId, userId));

        await _repository.DidNotReceiveWithAnyArgs().GetOwnedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private async Task AssertNothingDeletedAsync()
    {
        await _repository.DidNotReceiveWithAnyArgs().DeleteUnattachedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RoomPhoto CreatePhoto(
        string? roomId = null,
        string originalPath = DefaultPath,
        string? mediumPath = null,
        string? thumbPath = null)
        => new(
            uploadedByUserId: UserId,
            kind: PhotoKind.Standard,
            contentType: "image/jpeg",
            sizeBytes: 2048,
            width: 800,
            height: 600,
            originalPath: originalPath,
            mediumPath: mediumPath ?? originalPath,
            thumbPath: thumbPath ?? originalPath,
            roomId: roomId,
            id: PhotoId);
}

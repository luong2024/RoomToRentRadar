using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

/// <summary>
/// BR-MED-03: <c>deletePhoto</c> is ownership-scoped (404 anti-IDOR), refuses attached photos (409) and
/// removes the physical files only after the row is gone.
/// </summary>
[TestFixture]
public class DeletePhotoUseCaseTests
{
    private IRoomPhotoRepository _photoRepository = null!;
    private FakePhotoStorage _storage = null!;
    private DeletePhotoUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _photoRepository = Substitute.For<IRoomPhotoRepository>();
        _storage = new FakePhotoStorage();
        _useCase = new DeletePhotoUseCase(_photoRepository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_PhotoOfAnotherUploader_ThrowsNotFound()
    {
        _photoRepository
            .DeleteUnattachedAsync("photo-1", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.NotFound, Photo: null));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync("landlord-1", "photo-1"),
            Throws.TypeOf<EntityNotFoundException>().With.Property("ErrorCode").EqualTo("NOT_FOUND"));

        Assert.That(_storage.DeletedKeys, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_AttachedPhoto_ThrowsInvalidStateTransition()
    {
        _photoRepository
            .DeleteUnattachedAsync("photo-1", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Attached, AppTestData.Photo("photo-1", roomId: "room-1")));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync("landlord-1", "photo-1"),
            Throws.TypeOf<InvalidStateTransitionException>()
                .With.Property("ErrorCode").EqualTo("INVALID_STATE_TRANSITION"));

        Assert.That(_storage.DeletedKeys, Is.Empty, "Ảnh đã gắn phòng không được xoá file.");
    }

    [Test]
    public async Task ExecuteAsync_DeletedPhoto_RemovesEveryDistinctFile()
    {
        var photo = AppTestData.Photo("photo-1");
        _photoRepository
            .DeleteUnattachedAsync("photo-1", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Deleted, photo));

        await _useCase.ExecuteAsync("landlord-1", "photo-1");

        Assert.That(_storage.DeletedKeys, Is.EquivalentTo(new[]
        {
            "rooms/2026/11/photo-1/original.jpg",
            "rooms/2026/11/photo-1/medium.jpg",
            "rooms/2026/11/photo-1/thumb.jpg"
        }));
    }

    [Test]
    public async Task ExecuteAsync_StorageDeleteFails_DoesNotFailTheRequest()
    {
        _storage.DeleteFailure = new IOException("disk full");
        _photoRepository
            .DeleteUnattachedAsync("photo-1", "landlord-1", Arg.Any<CancellationToken>())
            .Returns(new PhotoDeleteAttempt(PhotoDeleteOutcome.Deleted, AppTestData.Photo("photo-1")));

        await _useCase.ExecuteAsync("landlord-1", "photo-1");

        Assert.That(_storage.DeletedKeys, Is.Not.Empty, "DB là source of truth; lỗi xoá file là best-effort.");
    }

    [Test]
    public async Task ExecuteAsync_BlankIds_ThrowsDomainValidation()
    {
        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(" ", "photo-1"),
            Throws.TypeOf<DomainValidationException>());

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync("landlord-1", " "),
            Throws.TypeOf<DomainValidationException>());
    }
}

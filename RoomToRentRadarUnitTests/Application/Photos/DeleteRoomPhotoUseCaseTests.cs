using NSubstitute.ReturnsExtensions;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

[TestFixture]
public class DeleteRoomPhotoUseCaseTests
{
    private const string RoomId = "room-123";
    private const string PhotoId = "photo-001";

    private IRoomPhotoRepository _repository = null!;
    private IPhotoStoragePort _storage = null!;
    private DeleteRoomPhotoUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _storage = Substitute.For<IPhotoStoragePort>();
        _sut = new DeleteRoomPhotoUseCase(_repository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoDoesNotExist_ReturnsFalseAndDeletesNothing()
    {
        _repository.GetAsync(RoomId, PhotoId, Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.ExecuteAsync(RoomId, PhotoId);

        Assert.That(result, Is.False);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenPhotoExists_DeletesStoredFileAndMetadataAndReturnsTrue()
    {
        var photo = new RoomPhoto
        {
            Id = PhotoId,
            RoomId = RoomId,
            FileName = "abc123.jpg",
            Url = $"/uploads/rooms/{RoomId}/abc123.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 2048,
            UploadedByUserId = "landlord-001"
        };
        _repository.GetAsync(RoomId, PhotoId, Arg.Any<CancellationToken>()).Returns(photo);

        var result = await _sut.ExecuteAsync(RoomId, PhotoId);

        Assert.That(result, Is.True);
        await _storage.Received(1).DeleteAsync(
            RoomId, photo.FileName, Arg.Any<CancellationToken>());
        await _repository.Received(1).DeleteAsync(
            PhotoId, Arg.Any<CancellationToken>());
    }
}

using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

[TestFixture]
public class GetRoomGalleryUseCaseTests
{
    private const string RoomId = "room-123";

    private IRoomPhotoRepository _repository = null!;
    private GetRoomGalleryUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _sut = new GetRoomGalleryUseCase(_repository);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomHasNoPhotos_ReturnsEmptyGallery()
    {
        _repository.ListByRoomAsync(RoomId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());

        var result = await _sut.ExecuteAsync(RoomId);

        Assert.Multiple(() =>
        {
            Assert.That(result.RoomId, Is.EqualTo(RoomId));
            Assert.That(result.Photos, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.HasMinimumPhotos, Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_WithTwoPhotos_MarksOnlyFirstAsCoverAndMinimumNotReached()
    {
        var photos = CreatePhotos(2);
        _repository.ListByRoomAsync(RoomId, Arg.Any<CancellationToken>()).Returns(photos);

        var result = await _sut.ExecuteAsync(RoomId);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.HasMinimumPhotos, Is.False);
            Assert.That(result.Photos, Has.Count.EqualTo(2));
            Assert.That(result.Photos[0].IsCover, Is.True);
            Assert.That(result.Photos[1].IsCover, Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_WithThreePhotos_ReachesMinimumAndKeepsRepositoryOrder()
    {
        var photos = CreatePhotos(3);
        _repository.ListByRoomAsync(RoomId, Arg.Any<CancellationToken>()).Returns(photos);

        var result = await _sut.ExecuteAsync(RoomId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasMinimumPhotos, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.Photos.Select(p => p.Id), Is.EqualTo(photos.Select(p => p.Id)));
            Assert.That(result.Photos[0].IsCover, Is.True);
            Assert.That(result.Photos[1].IsCover, Is.False);
            Assert.That(result.Photos[2].IsCover, Is.False);
        });
    }

    private static IReadOnlyList<RoomPhoto> CreatePhotos(int count)
    {
        var baseTime = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);
        var photos = new List<RoomPhoto>(count);
        for (var i = 0; i < count; i++)
        {
            photos.Add(new RoomPhoto
            {
                Id = $"photo-{i + 1}",
                RoomId = RoomId,
                FileName = $"photo-{i + 1}.jpg",
                Url = $"/uploads/rooms/{RoomId}/photo-{i + 1}.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                UploadedByUserId = "landlord-001",
                UploadedAtUtc = baseTime.AddMinutes(i)
            });
        }

        return photos;
    }
}

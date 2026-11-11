using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

[TestFixture]
public class UploadRoomPhotosUseCaseTests
{
    private const string ValidRoomId = "room-123";
    private const string UploaderUserId = "landlord-001";

    private static readonly byte[] JpgMagicBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private IRoomPhotoRepository _repository = null!;
    private IPhotoStoragePort _storage = null!;
    private UploadRoomPhotosUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _storage = Substitute.For<IPhotoStoragePort>();
        _sut = new UploadRoomPhotosUseCase(_repository, _storage);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("../x")]
    public void ExecuteAsync_WithInvalidRoomId_ThrowsAndDoesNotTouchStorageOrRepository(string? roomId)
    {
        var request = new UploadRoomPhotosRequest(roomId!, UploaderUserId, new[] { CreateValidJpg() });

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WithNullFiles_ThrowsDomainValidationException()
    {
        var request = new UploadRoomPhotosRequest(ValidRoomId, UploaderUserId, null!);

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_WithEmptyFiles_ThrowsDomainValidationException()
    {
        var request = new UploadRoomPhotosRequest(
            ValidRoomId, UploaderUserId, Array.Empty<PhotoFileInput>());

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));
    }

    [Test]
    public void ExecuteAsync_WithZeroLengthFile_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateEmptyFile());

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WithFileLargerThanTenMegabytes_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateOversizedFile());

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WithPngExtensionButInvalidMagicBytes_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateDisguisedPng());

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WithOneInvalidFileAmongValidOnes_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateValidJpg("first.jpg"), CreateDisguisedPng("second.png"));

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_WhenRoomWouldExceedMaxPhotos_ThrowsAndSavesNothing()
    {
        _repository.ListByRoomAsync(ValidRoomId, Arg.Any<CancellationToken>())
            .Returns(CreateExistingPhotos(ValidRoomId, 9));

        var request = CreateRequest(CreateValidJpg("one.jpg"), CreateValidPng("two.png"));

        Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync(request));

        _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithValidFilesInEmptyRoom_SavesBothAndMarksOnlyFirstAsCover()
    {
        _repository.ListByRoomAsync(ValidRoomId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());

        var savedUrls = new List<string>();
        _storage.SaveAsync(ValidRoomId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var url = $"/uploads/rooms/{ValidRoomId}/{ci.ArgAt<string>(1)}";
                savedUrls.Add(url);
                return url;
            });

        var request = CreateRequest(CreateValidJpg("first.jpg"), CreateValidPng("second.png"));

        var result = await _sut.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].IsCover, Is.True);
            Assert.That(result[1].IsCover, Is.False);
            Assert.That(savedUrls, Has.Count.EqualTo(2));
            Assert.That(result[0].Url, Is.EqualTo(savedUrls[0]));
            Assert.That(result[1].Url, Is.EqualTo(savedUrls[1]));
        });

        await _storage.Received(2).SaveAsync(
            ValidRoomId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _repository.Received(2).AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomAlreadyHasPhoto_NewPhotoIsNotCover()
    {
        _repository.ListByRoomAsync(ValidRoomId, Arg.Any<CancellationToken>())
            .Returns(CreateExistingPhotos(ValidRoomId, 1));
        _storage.SaveAsync(ValidRoomId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"/uploads/rooms/{ValidRoomId}/{ci.ArgAt<string>(1)}");

        var result = await _sut.ExecuteAsync(CreateRequest(CreateValidJpg()));

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].IsCover, Is.False);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenAddAsyncFails_RollsBackSavedFileAndRethrows()
    {
        _repository.ListByRoomAsync(ValidRoomId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());
        _storage.SaveAsync(ValidRoomId, Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"/uploads/rooms/{ValidRoomId}/{ci.ArgAt<string>(1)}");
        _repository.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var request = CreateRequest(CreateValidJpg());

        Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExecuteAsync(request));

        await _storage.Received(1).DeleteAsync(
            ValidRoomId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static UploadRoomPhotosRequest CreateRequest(params PhotoFileInput[] files)
        => new(ValidRoomId, UploaderUserId, files);

    private static PhotoFileInput CreateValidJpg(string fileName = "photo.jpg")
        => CreateFileWithMagicBytes(fileName, JpgMagicBytes);

    private static PhotoFileInput CreateValidPng(string fileName = "photo.png")
        => CreateFileWithMagicBytes(fileName, PngMagicBytes);

    private static PhotoFileInput CreateFileWithMagicBytes(string fileName, byte[] magicBytes)
    {
        var content = new byte[magicBytes.Length + 8];
        magicBytes.CopyTo(content, 0);
        var stream = new MemoryStream(content);
        return new PhotoFileInput(fileName, stream, stream.Length);
    }

    private static PhotoFileInput CreateEmptyFile(string fileName = "empty.jpg")
        => new(fileName, new MemoryStream(Array.Empty<byte>()), 0);

    private static PhotoFileInput CreateOversizedFile(string fileName = "oversized.jpg")
    {
        var length = PhotoFileRules.MaxFileBytes + 1;
        var stream = new MemoryStream();
        stream.Write(JpgMagicBytes);
        stream.SetLength(length);
        stream.Position = 0;
        return new PhotoFileInput(fileName, stream, length);
    }

    private static PhotoFileInput CreateDisguisedPng(string fileName = "fake.png")
    {
        // Tên .png nhưng nội dung toàn byte 0x00 — không phải magic bytes JPG/PNG.
        var stream = new MemoryStream(new byte[16]);
        return new PhotoFileInput(fileName, stream, stream.Length);
    }

    private static IReadOnlyList<RoomPhoto> CreateExistingPhotos(string roomId, int count)
    {
        var baseTime = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc);
        var photos = new List<RoomPhoto>(count);
        for (var i = 0; i < count; i++)
        {
            photos.Add(new RoomPhoto
            {
                RoomId = roomId,
                FileName = $"existing-{i}.jpg",
                Url = $"/uploads/rooms/{roomId}/existing-{i}.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 1024,
                UploadedByUserId = UploaderUserId,
                UploadedAtUtc = baseTime.AddMinutes(i)
            });
        }

        return photos;
    }
}

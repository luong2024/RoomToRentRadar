using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

// uploadPhoto — UC-30, BR-MED-01..03.
[TestFixture]
public class UploadPhotoUseCaseTests
{
    private const string UserId = "landlord-001";
    private const string PublicUrl = "https://cdn.example.test/rooms/photo.jpg";

    private IRoomPhotoRepository _repository = null!;
    private IPhotoStoragePort _storage = null!;
    private UploadPhotoUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IRoomPhotoRepository>();
        _storage = Substitute.For<IPhotoStoragePort>();
        _storage.GetPublicUrl(Arg.Any<string>()).Returns(PublicUrl);
        _sut = new UploadPhotoUseCase(_repository, _storage);
    }

    [Test]
    public async Task ExecuteAsync_WithValidJpeg_SavesFileThenPersistsUnattachedPhotoAndReturnsDto()
    {
        var content = CreateJpegHeader(width: 800, height: 600);
        string? savedPath = null;
        RoomPhoto? addedPhoto = null;
        CaptureSavePath(path => savedPath = path);
        CaptureAddedPhoto(photo => addedPhoto = photo);

        var result = await _sut.ExecuteAsync(CreateRequest(content));

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(result.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(result.ThumbUrl, Is.EqualTo(PublicUrl));
            Assert.That(result.MediumUrl, Is.EqualTo(PublicUrl));
            Assert.That(result.Width, Is.EqualTo(800));
            Assert.That(result.Height, Is.EqualTo(600));
            Assert.That(result.SizeBytes, Is.EqualTo(content.Length));
            Assert.That(savedPath, Does.Match(@"^rooms/\d{4}/\d{2}/[0-9a-f]{32}/original\.jpg$"));
            Assert.That(savedPath, Does.Contain(result.Id));
        });

        Assert.That(addedPhoto, Is.Not.Null);
        var photo = addedPhoto!;
        Assert.Multiple(() =>
        {
            Assert.That(photo.Id, Is.EqualTo(result.Id));
            Assert.That(photo.UploadedByUserId, Is.EqualTo(UserId));
            Assert.That(photo.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(photo.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(photo.SizeBytes, Is.EqualTo(content.Length));
            Assert.That(photo.Width, Is.EqualTo(800));
            Assert.That(photo.Height, Is.EqualTo(600));
            Assert.That(photo.OriginalPath, Is.EqualTo(savedPath));
            Assert.That(photo.MediumPath, Is.EqualTo(savedPath));
            Assert.That(photo.ThumbPath, Is.EqualTo(savedPath));
            Assert.That(photo.RoomId, Is.Null);
            Assert.That(photo.SortOrder, Is.Zero);
        });

        await _storage.Received(1).SaveAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithValidPng_UsesPngContentTypeAndExtension()
    {
        var content = CreatePngHeader(width: 800, height: 600);
        string? savedPath = null;
        RoomPhoto? addedPhoto = null;
        CaptureSavePath(path => savedPath = path);
        CaptureAddedPhoto(photo => addedPhoto = photo);

        var result = await _sut.ExecuteAsync(CreateRequest(content, fileName: "photo.png"));

        Assert.Multiple(() =>
        {
            Assert.That(savedPath, Does.EndWith("/original.png"));
            Assert.That(addedPhoto!.ContentType, Is.EqualTo("image/png"));
            Assert.That(result.Width, Is.EqualTo(800));
            Assert.That(result.Height, Is.EqualTo(600));
        });
    }

    [Test]
    public async Task ExecuteAsync_WithPanoramaAtTwoToOneRatio_Succeeds()
    {
        var content = CreateJpegHeader(width: 4000, height: 2000);

        var result = await _sut.ExecuteAsync(CreateRequest(content, kind: PhotoKind.Panorama));

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo(PhotoKind.Panorama));
            Assert.That(result.Width, Is.EqualTo(4000));
            Assert.That(result.Height, Is.EqualTo(2000));
        });
        await _repository.Received(1).AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithPanoramaNotAtTwoToOneRatio_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateJpegHeader(width: 800, height: 600), kind: PhotoKind.Panorama);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithZeroLength_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateJpegHeader(width: 800, height: 600), length: 0);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithFileLargerThanTenMegabytes_ThrowsFileTooLargeAndSavesNothing()
    {
        var request = CreateRequest(
            CreateJpegHeader(width: 800, height: 600),
            length: PhotoFileRules.MaxFileBytes + 1);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<FileTooLargeException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithForgedJpegMagicBytes_ThrowsUnsupportedMediaTypeAndSavesNothing()
    {
        var forged = new byte[16];
        var request = CreateRequest(forged, fileName: "forged.jpg");

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<UnsupportedMediaTypeException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithJpegMagicBytesButBrokenHeader_ThrowsUnsupportedMediaTypeAndSavesNothing()
    {
        var brokenHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var request = CreateRequest(brokenHeader);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<UnsupportedMediaTypeException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WithUndefinedKind_ThrowsAndSavesNothing()
    {
        var request = CreateRequest(CreateJpegHeader(width: 800, height: 600), kind: (PhotoKind)99);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>());

        await AssertNothingSavedAsync();
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task ExecuteAsync_WithBlankUserId_ThrowsAndSavesNothing(string userId)
    {
        var request = CreateRequest(CreateJpegHeader(width: 800, height: 600), userId: userId);

        await Assert.ThatAsync(
            async () => await _sut.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>());

        await AssertNothingSavedAsync();
    }

    [Test]
    public async Task ExecuteAsync_WhenAddAsyncFails_DeletesSavedFileWithNoTokenAndRethrows()
    {
        var content = CreateJpegHeader(width: 800, height: 600);
        string? savedPath = null;
        CaptureSavePath(path => savedPath = path);
        _repository.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database failure"));

        InvalidOperationException? exception = null;
        await Assert.ThatAsync(
            async () =>
            {
                try
                {
                    await _sut.ExecuteAsync(CreateRequest(content));
                }
                catch (InvalidOperationException ex)
                {
                    exception = ex;
                    throw;
                }
            },
            Throws.TypeOf<InvalidOperationException>());

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Is.EqualTo("database failure"));
            Assert.That(savedPath, Is.Not.Null);
        });
        await _storage.Received(1).DeleteAsync(savedPath!, CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WhenSaveAsyncFails_RethrowsWithoutPersistingMetadata()
    {
        var storageFailure = new IOException("disk full");
        _storage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(storageFailure);

        IOException? exception = null;
        await Assert.ThatAsync(
            async () =>
            {
                try
                {
                    await _sut.ExecuteAsync(CreateRequest(CreateJpegHeader(width: 800, height: 600)));
                }
                catch (IOException ex)
                {
                    exception = ex;
                    throw;
                }
            },
            Throws.TypeOf<IOException>());

        Assert.That(exception, Is.SameAs(storageFailure));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_PersistsMetadataOnlyAfterSavingFile()
    {
        CaptureAddedPhoto(_ => { });

        await _sut.ExecuteAsync(CreateRequest(CreateJpegHeader(width: 800, height: 600)));

        Received.InOrder(() =>
        {
            _storage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            _repository.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
        });
    }

    private void CaptureSavePath(Action<string> capture)
        => _storage.When(s => s.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()))
            .Do(call => capture(call.ArgAt<string>(0)));

    private void CaptureAddedPhoto(Action<RoomPhoto> capture)
        => _repository.When(r => r.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>()))
            .Do(call => capture(call.ArgAt<RoomPhoto>(0)));

    private async Task AssertNothingSavedAsync()
    {
        await _storage.DidNotReceiveWithAnyArgs().SaveAsync(
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(
            Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>());
    }

    private static UploadPhotoRequest CreateRequest(
        byte[] content,
        PhotoKind kind = PhotoKind.Standard,
        string userId = UserId,
        string fileName = "photo.jpg",
        long? length = null)
        => new(userId, kind, fileName, new MemoryStream(content), length ?? content.Length);

    private static byte[] CreateJpegHeader(int width, int height)
        => new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x11, 0x08,
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width
        };

    private static byte[] CreatePngHeader(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        bytes[11] = 0x0D; // IHDR chunk length (13 bytes) in the last length byte.
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        bytes[16] = (byte)(width >> 24);
        bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8);
        bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24);
        bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8);
        bytes[23] = (byte)height;
        bytes[24] = 8; // bit depth
        bytes[25] = 6; // RGBA colour type
        return bytes;
    }
}

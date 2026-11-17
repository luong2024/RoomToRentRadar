using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Infrastructure.Adapters.Media;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Photos;

/// <summary>
/// US08 AC4 / BR-MED-01..04: <c>uploadPhoto</c> validates size, real JPEG/PNG magic bytes, dimensions
/// and the panorama ratio, strips EXIF through the real byte-level adapter and rolls the stored file back
/// when the row cannot be persisted.
/// </summary>
[TestFixture]
public class UploadPhotoUseCaseTests
{
    private IRoomPhotoRepository _photoRepository = null!;
    private FakePhotoStorage _storage = null!;
    private UploadPhotoUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _photoRepository = Substitute.For<IRoomPhotoRepository>();
        _photoRepository.AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _storage = new FakePhotoStorage();
        _useCase = new UploadPhotoUseCase(_photoRepository, _storage, new ByteLevelImageProcessorAdapter());
    }

    [Test]
    public async Task ExecuteAsync_ValidJpeg_StoresFileAndUnattachedRow()
    {
        var bytes = AppTestData.Jpeg(width: 1600, height: 900);

        var result = await _useCase.ExecuteAsync(StandardRequest(bytes));

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.Not.Empty);
            Assert.That(result.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(result.Width, Is.EqualTo(1600));
            Assert.That(result.Height, Is.EqualTo(900));
            Assert.That(result.SizeBytes, Is.EqualTo(bytes.Length));
            Assert.That(result.ThumbUrl, Does.StartWith("https://media.test/rooms/"));
            Assert.That(result.ThumbUrl, Does.EndWith("/original.jpg"));
            Assert.That(result.MediumUrl, Is.EqualTo(result.ThumbUrl),
                "Zero-NuGet: thumb/medium trỏ về đúng file gốc đã strip.");
        });

        Assert.That(_storage.Files.Keys.Single(), Does.Match(@"^rooms/\d{4}/\d{2}/[0-9a-f]{32}/original\.jpg$"),
            "Storage key: rooms/{yyyy}/{MM}/{photoId}/original.{ext}.");
        await _photoRepository.Received(1).AddAsync(
            Arg.Is<RoomPhoto>(photo =>
                photo.UploadedByUserId == "landlord-1"
                && photo.RoomId == null
                && photo.Kind == PhotoKind.Standard
                && photo.ContentType == RoomPhoto.JpegContentType
                && photo.Width == 1600
                && photo.Height == 900),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_ValidPng_DetectsPngContentTypeAndExtension()
    {
        var bytes = AppTestData.Png(width: 800, height: 600);

        var result = await _useCase.ExecuteAsync(StandardRequest(bytes, fileName: "photo.jpg"));

        Assert.That(result.Kind, Is.EqualTo(PhotoKind.Standard));
        Assert.That(_storage.Files.Keys.Single(), Does.EndWith(".png"),
            "BR-MED-02: định dạng lấy từ magic bytes, không lấy từ phần mở rộng tên file.");
        await _photoRepository.Received(1).AddAsync(
            Arg.Is<RoomPhoto>(photo => photo.ContentType == RoomPhoto.PngContentType),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_FileAboveTenMegabytes_ThrowsFileTooLarge()
    {
        var request = new UploadPhotoRequest(
            "landlord-1", PhotoKind.Standard, "big.jpg",
            new MemoryStream(AppTestData.Jpeg()), PhotoFileRules.MaxFileBytes + 1);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(request),
            Throws.TypeOf<FileTooLargeException>());

        Assert.That(_storage.Files, Is.Empty);
        await _photoRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_EmptyFile_ThrowsValidationFailed()
    {
        var request = new UploadPhotoRequest(
            "landlord-1", PhotoKind.Standard, "empty.jpg", new MemoryStream(), Length: 0);

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));
    }

    [Test]
    public async Task ExecuteAsync_JpegWithExif_StoresStrippedBytesAndReportsCleanedSize()
    {
        var jpeg = AppTestData.Jpeg(width: 1600, height: 900);
        var withExif = WithExifSegment(jpeg, payloadLength: 40);

        var result = await _useCase.ExecuteAsync(StandardRequest(withExif));

        var stored = _storage.Files.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Length, Is.LessThan(withExif.Length), "BR-MED-04: APP1/EXIF bị loại bỏ.");
            Assert.That(result.SizeBytes, Is.EqualTo(stored.Length),
                "sizeBytes = kích thước file sau khi strip EXIF, không phải kích thước upload.");
            Assert.That(result.SizeBytes, Is.EqualTo(withExif.Length - (4 + 40)));
        });
    }

    [Test]
    public async Task ExecuteAsync_NotAnImage_ThrowsUnsupportedMediaType()
    {
        var text = "definitely not an image"u8.ToArray();

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(StandardRequest(text, fileName: "fake.jpg")),
            Throws.TypeOf<UnsupportedMediaTypeException>());

        Assert.That(_storage.Files, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_PanoramaWithoutTwoToOneRatio_ThrowsValidationFailed()
    {
        var bytes = AppTestData.Jpeg(width: 1000, height: 1000);
        var request = StandardRequest(bytes) with { Kind = PhotoKind.Panorama };

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>().With.Property("ErrorCode").EqualTo("VALIDATION_FAILED"));

        Assert.That(_storage.Files, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_PanoramaWithTwoToOneRatio_Succeeds()
    {
        var bytes = AppTestData.Jpeg(width: 2000, height: 1000);
        var request = StandardRequest(bytes) with { Kind = PhotoKind.Panorama };

        var result = await _useCase.ExecuteAsync(request);

        Assert.That(result.Kind, Is.EqualTo(PhotoKind.Panorama));
    }

    [Test]
    public async Task ExecuteAsync_RepositoryFails_DeletesTheStoredFile()
    {
        _photoRepository
            .AddAsync(Arg.Any<RoomPhoto>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("db down"));

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(StandardRequest(AppTestData.Jpeg())),
            Throws.TypeOf<InvalidOperationException>());

        Assert.Multiple(() =>
        {
            Assert.That(_storage.Files, Is.Empty, "BR-MED-03: file phải bị xoá khi row không lưu được.");
            Assert.That(_storage.DeletedKeys, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_NonSeekableStream_IsBufferedAndAccepted()
    {
        var bytes = AppTestData.Jpeg(width: 640, height: 480);
        var request = new UploadPhotoRequest(
            "landlord-1", PhotoKind.Standard, "stream.jpg",
            new NonSeekableStream(new MemoryStream(bytes)), bytes.Length);

        var result = await _useCase.ExecuteAsync(request);

        Assert.That(result.Width, Is.EqualTo(640));
        Assert.That(_storage.Files, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_EmptyUploader_ThrowsDomainValidation()
    {
        var request = StandardRequest(AppTestData.Jpeg()) with { UserId = "  " };

        await Assert.ThatAsync(
            async () => await _useCase.ExecuteAsync(request),
            Throws.TypeOf<DomainValidationException>());
    }

    private static UploadPhotoRequest StandardRequest(byte[] bytes, string fileName = "photo.jpg") =>
        new("landlord-1", PhotoKind.Standard, fileName, new MemoryStream(bytes), bytes.Length);

    /// <summary>Inserts one APP1 (EXIF) segment after the JPEG SOI marker so the stripper has work to do.</summary>
    private static byte[] WithExifSegment(byte[] jpeg, int payloadLength)
    {
        var segmentLength = payloadLength + 2;
        var exif = new byte[4 + payloadLength];
        exif[0] = 0xFF;
        exif[1] = 0xE1;
        exif[2] = (byte)(segmentLength >> 8);
        exif[3] = (byte)segmentLength;
        "Exif"u8.CopyTo(exif.AsSpan(4));

        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

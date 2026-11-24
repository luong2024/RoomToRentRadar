namespace RoomToRentRadar.Adapters.Tests.Storage;

using RoomToRentRadar.Adapters.Outbound.Storage;
using SkiaSharp;

public sealed class ListingImageValidatorTests
{
    [TestCase(SKEncodedImageFormat.Jpeg, "cover.JPG", "IMAGE/JPEG", ".jpg")]
    [TestCase(SKEncodedImageFormat.Jpeg, "cover.jpeg", "image/jpeg", ".jpeg")]
    [TestCase(SKEncodedImageFormat.Png, "cover.PNG", "image/png", ".png")]
    public async Task ReadAndValidateAsync_ValidImageFormats_AcceptsImage(
        SKEncodedImageFormat format, string fileName, string contentType, string expectedExtension)
    {
        var bytes = CreateImage(format);
        var result = await ListingImageValidator.ReadAndValidateAsync(
            new MemoryStream(bytes), fileName, contentType, bytes.Length, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Extension, Is.EqualTo(expectedExtension));
            Assert.That(result.ContentType, Is.EqualTo(format == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg"));
        });
    }

    [Test]
    public async Task ReadAndValidateAsync_ExactlyTenMebibytes_AcceptsImage()
    {
        var png = CreateImage(SKEncodedImageFormat.Png);
        var exact = new byte[ListingImageStorageService.MaxImageSizeBytes];
        Buffer.BlockCopy(png, 0, exact, 0, png.Length);

        var result = await ListingImageValidator.ReadAndValidateAsync(
            new MemoryStream(exact), "exact.png", "image/png", exact.Length, CancellationToken.None);

        Assert.That(result.Bytes.LongLength, Is.EqualTo(ListingImageStorageService.MaxImageSizeBytes));
    }

    [Test]
    public async Task ReadAndValidateAsync_OneByteBelowTenMebibytes_AcceptsImage()
    {
        var png = CreateImage(SKEncodedImageFormat.Png);
        var below = new byte[ListingImageStorageService.MaxImageSizeBytes - 1];
        Buffer.BlockCopy(png, 0, below, 0, png.Length);

        var result = await ListingImageValidator.ReadAndValidateAsync(
            new MemoryStream(below), "below.png", "image/png", below.Length, CancellationToken.None);

        Assert.That(result.Bytes.LongLength, Is.EqualTo(ListingImageStorageService.MaxImageSizeBytes - 1));
    }

    [Test]
    public void ReadAndValidateAsync_OneByteOverTenMebibytes_RejectsImage()
    {
        Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RequestValidationException>(async () =>
            await ListingImageValidator.ReadAndValidateAsync(
                new MemoryStream([1]), "large.png", "image/png", ListingImageStorageService.MaxImageSizeBytes + 1, CancellationToken.None));
    }

    [TestCase("photo.webp", "image/jpeg")]
    [TestCase("photo.jpg", "image/png")]
    [TestCase("photo.png", "text/plain")]
    public void ReadAndValidateAsync_ExtensionOrMimeMismatch_RejectsImage(string fileName, string contentType)
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);
        Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RequestValidationException>(async () =>
            await ListingImageValidator.ReadAndValidateAsync(new MemoryStream(bytes), fileName, contentType, bytes.Length, CancellationToken.None));
    }

    [Test]
    public void ReadAndValidateAsync_EmptyOrCorruptImage_RejectsContent()
    {
        Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RequestValidationException>(async () =>
            await ListingImageValidator.ReadAndValidateAsync(Stream.Null, "empty.jpg", "image/jpeg", 0, CancellationToken.None));

        var corrupt = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x00 };
        Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RequestValidationException>(async () =>
            await ListingImageValidator.ReadAndValidateAsync(new MemoryStream(corrupt), "broken.jpg", "image/jpeg", corrupt.Length, CancellationToken.None));
    }

    [Test]
    public void ReadAndValidateAsync_ContentSignatureMismatch_RejectsImage()
    {
        var png = CreateImage(SKEncodedImageFormat.Png);
        Assert.ThrowsAsync<RoomToRentRadar.Application.Exceptions.RequestValidationException>(async () =>
            await ListingImageValidator.ReadAndValidateAsync(new MemoryStream(png), "misleading.jpg", "image/jpeg", png.Length, CancellationToken.None));
    }

    [Test]
    public void BuildBlobName_MaliciousAndDuplicateOriginalNames_UsesSafeUniqueStorageNames()
    {
        var ownerId = Guid.NewGuid();
        var first = ListingImageStorageService.BuildBlobName(ownerId, "listing-safe", ".jpg");
        var second = ListingImageStorageService.BuildBlobName(ownerId, "listing-safe", ".jpg");

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.StartWith($"{ownerId:D}/listing-safe/"));
            Assert.That(first, Does.Not.Contain(".."));
            Assert.That(first, Is.Not.EqualTo(second));
        });
    }

    private static byte[] CreateImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}

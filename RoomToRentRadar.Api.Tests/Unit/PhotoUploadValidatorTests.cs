using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class PhotoUploadValidatorTests
{
    [Test]
    public void TryValidate_NoFiles_ReturnsEmptyUploadProblem()
    {
        var files = Array.Empty<IFormFile>();

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(PhotoProblemFactory.EmptyUpload));
        });
    }

    [Test]
    public void TryValidate_NullFiles_ReturnsEmptyUploadProblem()
    {
        var isValid = PhotoUploadValidator.TryValidate(null, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Extensions["code"], Is.EqualTo(PhotoProblemFactory.EmptyUpload));
        });
    }

    [Test]
    public void TryValidate_TooManyFiles_ReturnsInvalidPhotoProblem()
    {
        var files = Enumerable
            .Range(0, PhotoUploadLimits.MaxFilesPerRequest + 1)
            .Select(_ => CreateFile())
            .ToList();

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(PhotoProblemFactory.InvalidPhoto));
        });
    }

    [Test]
    public void TryValidate_FileTooLarge_ReturnsPhotoTooLargeProblem()
    {
        var files = new[] { CreateFile(PhotoUploadLimits.MaxFileBytes + 1) };

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(problem.Extensions["code"], Is.EqualTo(PhotoProblemFactory.PhotoTooLarge));
        });
    }

    [Test]
    public void TryValidate_ZeroLengthFile_ReturnsInvalidPhotoProblem()
    {
        var files = new[] { CreateFile(0) };

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Extensions["code"], Is.EqualTo(PhotoProblemFactory.InvalidPhoto));
        });
    }

    [Test]
    public void TryValidate_UnsupportedContentType_ReturnsUnsupportedPhotoTypeProblem()
    {
        var files = new[] { CreateFile(contentType: "text/plain") };

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
            Assert.That(problem.Extensions["code"], Is.EqualTo(PhotoProblemFactory.UnsupportedPhotoType));
        });
    }

    [Test]
    public void TryValidate_InvalidKind_ReturnsInvalidPhotoKindProblem()
    {
        var files = new[] { CreateFile() };

        var isValid = PhotoUploadValidator.TryValidate(files, "video", out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(PhotoProblemFactory.InvalidPhotoKind));
        });
    }

    [TestCase(null, ListingPhotoKind.Photo)]
    [TestCase("", ListingPhotoKind.Photo)]
    [TestCase("photo", ListingPhotoKind.Photo)]
    [TestCase("PANORAMA", ListingPhotoKind.Panorama)]
    [TestCase(" panorama ", ListingPhotoKind.Panorama)]
    public void TryValidate_ValidKind_IsParsedCaseInsensitively(string? kind, ListingPhotoKind expectedKind)
    {
        var files = new[] { CreateFile() };

        var isValid = PhotoUploadValidator.TryValidate(files, kind, out var photoKind, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(photoKind, Is.EqualTo(expectedKind));
        });
    }

    [Test]
    public void TryValidate_ContentTypeWithParameters_IsAccepted()
    {
        var files = new[] { CreateFile(contentType: "image/jpeg; charset=binary") };

        var isValid = PhotoUploadValidator.TryValidate(files, null, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(problem, Is.Null);
        });
    }

    private static IFormFile CreateFile(
        long length = 64,
        string contentType = "image/jpeg",
        string fileName = "photo.jpg")
    {
        var headers = new HeaderDictionary
        {
            ["Content-Type"] = contentType
        };

        return new FormFile(new MemoryStream(new byte[(int)length]), 0, length, "files", fileName)
        {
            Headers = headers
        };
    }
}

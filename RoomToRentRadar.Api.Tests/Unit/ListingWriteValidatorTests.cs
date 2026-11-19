using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Photos;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ListingWriteValidatorTests
{
    [Test]
    public void TryValidate_ValidCreate_ReturnsParsedInput()
    {
        var form = CreateForm(photos: CreateFiles(3));

        var isValid = ListingWriteValidator.TryValidate(form, 3, out var input, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(input.Title, Is.EqualTo("Sunny room"));
            Assert.That(input.Address, Is.EqualTo("12 Nguyen Trai"));
            Assert.That(input.Price, Is.EqualTo(5_000_000));
            Assert.That(input.Deposit, Is.EqualTo(4_000_000));
            Assert.That(input.AreaM2, Is.EqualTo(25));
            Assert.That(input.RoomType, Is.EqualTo(RoomType.Studio));
            Assert.That(input.Bedrooms, Is.EqualTo(1));
            Assert.That(input.Description, Is.EqualTo("Bright room"));
            Assert.That(input.Photos, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void TryValidate_EmptyForm_ReturnsInvalidListingWithEveryRequiredField()
    {
        var form = new ListingWriteForm();

        var isValid = ListingWriteValidator.TryValidate(form, 3, out _, out var problem);

        Assert.That(isValid, Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(ListingWriteProblemFactory.InvalidListing));
        });

        var errors = GetErrors(problem);
        Assert.That(
            errors,
            Is.EquivalentTo(new[]
            {
                "title", "address", "price", "deposit", "area_m2", "room_type", "lat", "lng"
            }));
    }

    [Test]
    public void TryValidate_NonPositiveNumbers_ReturnsFieldErrors()
    {
        var form = CreateForm(price: 0, deposit: -1, areaM2: 0, bedrooms: -1, photos: CreateFiles(3));

        var isValid = ListingWriteValidator.TryValidate(form, 3, out _, out var problem);

        Assert.That(isValid, Is.False);
        var errors = GetErrors(problem);
        Assert.That(errors, Is.EquivalentTo(new[] { "price", "deposit", "area_m2", "bedrooms" }));
    }

    [Test]
    public void TryValidate_UnknownRoomType_ReturnsFieldError()
    {
        var form = CreateForm(roomType: "garage", photos: CreateFiles(3));

        var isValid = ListingWriteValidator.TryValidate(form, 3, out _, out var problem);

        Assert.That(isValid, Is.False);
        Assert.That(GetErrors(problem), Does.Contain("room_type"));
    }

    [Test]
    public void TryValidate_TooFewPhotos_ReturnsTooFewPhotosProblem()
    {
        var form = CreateForm(photos: CreateFiles(2));

        var isValid = ListingWriteValidator.TryValidate(form, 3, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(ListingWriteProblemFactory.TooFewPhotos));
        });
    }

    [Test]
    public void TryValidate_UpdateWithoutPhotos_IsAccepted()
    {
        var form = CreateForm(photos: []);

        var isValid = ListingWriteValidator.TryValidate(form, 0, out var input, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(input.Photos, Is.Empty);
        });
    }

    [Test]
    public void TryValidate_TooManyPhotosInOneRequest_ReturnsFieldError()
    {
        var form = CreateForm(photos: CreateFiles(PhotoUploadLimits.MaxFilesPerRequest + 1));

        var isValid = ListingWriteValidator.TryValidate(form, 0, out _, out var problem);

        Assert.That(isValid, Is.False);
        Assert.That(GetErrors(problem), Does.Contain("photos"));
    }

    [Test]
    public void TryValidate_PhotoTooLarge_ReturnsPhotoTooLargeProblem()
    {
        var files = CreateFiles(3);
        files[1] = CreateFile(length: PhotoUploadLimits.MaxFileBytes + 1);

        var isValid = ListingWriteValidator.TryValidate(CreateForm(photos: files), 3, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(problem.Extensions["code"], Is.EqualTo(ListingWriteProblemFactory.PhotoTooLarge));
        });
    }

    [Test]
    public void TryValidate_PhotoExactlyAtSizeLimit_IsAccepted()
    {
        var files = CreateFiles(3);
        files[0] = CreateFile(length: PhotoUploadLimits.MaxFileBytes);

        var isValid = ListingWriteValidator.TryValidate(CreateForm(photos: files), 3, out _, out var problem);

        Assert.That(isValid, Is.True);
        Assert.That(problem, Is.Null);
        Assert.That(PhotoUploadLimits.MaxRequestBodyBytes,
            Is.GreaterThan(PhotoUploadLimits.MaxFilesPerRequest * PhotoUploadLimits.MaxFileBytes));
    }

    [TestCase("image/webp")]
    [TestCase("image/gif")]
    [TestCase("text/plain")]
    public void TryValidate_UnsupportedPhotoType_ReturnsUnsupportedPhotoTypeProblem(string contentType)
    {
        var files = CreateFiles(3);
        files[0] = CreateFile(contentType: contentType);

        var isValid = ListingWriteValidator.TryValidate(CreateForm(photos: files), 3, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
            Assert.That(problem.Extensions["code"], Is.EqualTo(ListingWriteProblemFactory.UnsupportedPhotoType));
        });
    }

    [TestCase("image/jpeg; charset=binary")]
    [TestCase("IMAGE/PNG")]
    public void TryValidate_SupportedContentTypeWithParameters_IsAccepted(string contentType)
    {
        var files = CreateFiles(3);
        files[0] = CreateFile(contentType: contentType);

        var isValid = ListingWriteValidator.TryValidate(CreateForm(photos: files), 3, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(problem, Is.Null);
        });
    }

    [Test]
    public void TryValidate_ZeroLengthPhoto_ReturnsInvalidPhotoProblem()
    {
        var files = CreateFiles(3);
        files[0] = CreateFile(length: 0);

        var isValid = ListingWriteValidator.TryValidate(CreateForm(photos: files), 3, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Extensions["code"], Is.EqualTo(ListingWriteProblemFactory.InvalidPhoto));
        });
    }

    [Test]
    public void TryValidate_TitleTooLong_ReturnsFieldError()
    {
        var form = CreateForm(
            title: new string('a', 201),
            photos: CreateFiles(3));

        var isValid = ListingWriteValidator.TryValidate(form, 3, out _, out var problem);

        Assert.That(isValid, Is.False);
        Assert.That(GetErrors(problem), Does.Contain("title"));
    }

    private static ListingWriteForm CreateForm(
        string title = "Sunny room",
        string address = "12 Nguyen Trai",
        long price = 5_000_000,
        long deposit = 4_000_000,
        double areaM2 = 25,
        string roomType = "studio",
        int bedrooms = 1,
        List<IFormFile>? photos = null) => new()
        {
            Title = title,
            Address = address,
            Price = price,
            Deposit = deposit,
            AreaM2 = areaM2,
            RoomType = roomType,
            Bedrooms = bedrooms,
            Description = "Bright room",
            Lat = 10.76,
            Lng = 106.70,
            Photos = photos ?? []
        };

    private static List<IFormFile> CreateFiles(int count)
        => Enumerable.Range(0, count).Select(_ => CreateFile()).ToList();

    private static IFormFile CreateFile(
        long length = 64,
        string contentType = "image/jpeg",
        string fileName = "photo.jpg")
    {
        var headers = new HeaderDictionary
        {
            ["Content-Type"] = contentType
        };

        return new FormFile(new MemoryStream(new byte[(int)length]), 0, length, "photos", fileName)
        {
            Headers = headers
        };
    }

    private static List<string> GetErrors(Microsoft.AspNetCore.Mvc.ProblemDetails? problem)
    {
        Assert.That(problem, Is.Not.Null);
        var errors = problem!.Extensions["errors"];
        Assert.That(errors, Is.Not.Null);
        return ((Dictionary<string, string[]>)errors!).Keys.ToList();
    }
}

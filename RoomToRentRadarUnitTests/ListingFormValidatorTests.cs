using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingFormValidatorTests
{
    private const string ValidPhotoName = "phong-1.jpg";

    [Test]
    public void Validate_ValidCreateState_ReturnsNoErrors()
    {
        var errors = ListingFormValidator.Validate(CreateValidState(), isCreate: true);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_MissingRequiredFields_ReturnsOneErrorPerField()
    {
        var state = new ListingFormState
        {
            Title = " ",
            Address = string.Empty,
            PriceText = null,
            DepositText = string.Empty,
            AreaText = " ",
            RoomType = null,
            BedroomsText = "1",
            LatText = string.Empty,
            LngText = null,
            NewPhotos = []
        };

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.Multiple(() =>
        {
            Assert.That(errors.Keys, Is.SupersetOf(new[]
            {
                ListingFieldKeys.Title,
                ListingFieldKeys.Address,
                ListingFieldKeys.Price,
                ListingFieldKeys.Deposit,
                ListingFieldKeys.Area,
                ListingFieldKeys.RoomType,
                ListingFieldKeys.Lat,
                ListingFieldKeys.Lng
            }));
            Assert.That(errors[ListingFieldKeys.Title], Is.EqualTo("Vui lòng nhập tiêu đề tin đăng."));
            Assert.That(errors[ListingFieldKeys.Address], Is.EqualTo("Vui lòng nhập địa chỉ phòng trọ."));
            Assert.That(errors[ListingFieldKeys.Price], Is.EqualTo("Vui lòng nhập giá thuê."));
            Assert.That(errors[ListingFieldKeys.Deposit], Is.EqualTo("Vui lòng nhập tiền cọc."));
            Assert.That(errors[ListingFieldKeys.Area], Is.EqualTo("Vui lòng nhập diện tích."));
            Assert.That(errors[ListingFieldKeys.RoomType], Is.EqualTo("Vui lòng chọn loại phòng."));
            Assert.That(errors[ListingFieldKeys.Lat], Is.EqualTo("Vui lòng nhập vĩ độ."));
            Assert.That(errors[ListingFieldKeys.Lng], Is.EqualTo("Vui lòng nhập kinh độ."));
        });
    }

    [TestCase("0", "Giá thuê phải lớn hơn 0.")]
    [TestCase("-1500000", "Giá thuê không hợp lệ.")]
    [TestCase("ba triệu", "Giá thuê không hợp lệ.")]
    public void Validate_InvalidPrice_ReturnsPriceError(string priceText, string expected)
    {
        var state = CreateValidState();
        state.PriceText = priceText;

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Price], Is.EqualTo(expected));
    }

    [Test]
    public void Validate_NegativeDeposit_ReturnsDepositError()
    {
        var state = CreateValidState();
        state.DepositText = "-1";

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Deposit], Is.EqualTo("Tiền cọc không hợp lệ."));
    }

    [Test]
    public void Validate_AreaWithCommaDecimalMarker_IsAccepted()
    {
        var state = CreateValidState();
        state.AreaText = "20,5";

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors, Is.Empty);
    }

    [TestCase("91", "Vĩ độ phải là số trong khoảng -90 đến 90.")]
    [TestCase("abc", "Vĩ độ phải là số trong khoảng -90 đến 90.")]
    [TestCase("-90.1", "Vĩ độ phải là số trong khoảng -90 đến 90.")]
    public void Validate_LatOutOfRange_ReturnsLatError(string latText, string expected)
    {
        var state = CreateValidState();
        state.LatText = latText;

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Lat], Is.EqualTo(expected));
    }

    [Test]
    public void Validate_LngOutOfRange_ReturnsLngError()
    {
        var state = CreateValidState();
        state.LngText = "180.5";

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Lng],
            Is.EqualTo("Kinh độ phải là số trong khoảng -180 đến 180."));
    }

    [Test]
    public void Validate_InvalidBedrooms_ReturnsBedroomsError()
    {
        var state = CreateValidState();
        state.BedroomsText = "hai";

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Bedrooms], Is.EqualTo("Số phòng ngủ không hợp lệ."));
    }

    [Test]
    public void Validate_EmptyBedrooms_IsTreatedAsZero()
    {
        var state = CreateValidState();
        state.BedroomsText = string.Empty;

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.Empty);
            Assert.That(ListingFormValidator.TryReadValues(state, out var values), Is.True);
            Assert.That(values.Bedrooms, Is.EqualTo(0));
        });
    }

    [Test]
    public void Validate_CreateWithFewerThanThreePhotos_ReturnsPhotosError()
    {
        var state = CreateValidState();
        state.NewPhotos = [Photo("a.jpg"), Photo("b.jpg")];

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Photos],
            Is.EqualTo("Cần ít nhất 3 ảnh (JPG hoặc PNG) để gửi duyệt."));
    }

    [Test]
    public void Validate_CreateCountsExistingPhotosTowardsTheMinimum()
    {
        var state = CreateValidState();
        state.ExistingPhotoCount = 2;
        state.NewPhotos = [Photo("a.png")];

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_UpdateWithoutNewPhotos_DoesNotRequireThreePhotos()
    {
        var state = CreateValidState();
        state.ExistingPhotoCount = 1;
        state.NewPhotos = [];

        var errors = ListingFormValidator.Validate(state, isCreate: false);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void Validate_MoreThanTenNewPhotos_ReturnsPhotosError()
    {
        var state = CreateValidState();
        state.ExistingPhotoCount = 10;
        state.NewPhotos = Enumerable.Range(0, ListingPhotoRules.MaxPhotosPerRequest + 1)
            .Select(index => Photo($"anh-{index}.jpg"))
            .ToList();

        var errors = ListingFormValidator.Validate(state, isCreate: false);

        Assert.That(errors[ListingFieldKeys.Photos],
            Is.EqualTo($"Chỉ được thêm tối đa {ListingPhotoRules.MaxPhotosPerRequest} ảnh mỗi lần lưu."));
    }

    [Test]
    public void Validate_DisallowedPhotoType_NamesTheOffendingFile()
    {
        var state = CreateValidState();
        state.NewPhotos = [Photo("a.jpg"), Photo("b.jpg"), Photo("sodo.gif", "image/gif")];

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Photos],
            Is.EqualTo("Ảnh \"sodo.gif\" phải là JPG hoặc PNG."));
    }

    [Test]
    public void Validate_PhotoOverTenMegabytes_NamesTheOffendingFile()
    {
        var state = CreateValidState();
        state.NewPhotos =
        [
            Photo("a.jpg"),
            Photo("b.jpg"),
            Photo("to.jpg", "image/jpeg", ListingPhotoRules.MaxPhotoBytes + 1)
        ];

        var errors = ListingFormValidator.Validate(state, isCreate: true);

        Assert.That(errors[ListingFieldKeys.Photos], Is.EqualTo("Ảnh \"to.jpg\" vượt quá 10MB."));
    }

    [Test]
    public void TryReadValues_ParsesGroupedAmountsAndCommaDecimals()
    {
        var state = CreateValidState();
        state.PriceText = "3.500.000";
        state.DepositText = "0";
        state.AreaText = "20,5";
        state.BedroomsText = "2";
        state.LatText = "10,7721";
        state.LngText = "106.6579";

        var parsed = ListingFormValidator.TryReadValues(state, out var values);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(values.Price, Is.EqualTo(3_500_000));
            Assert.That(values.Deposit, Is.EqualTo(0));
            Assert.That(values.AreaM2, Is.EqualTo(20.5));
            Assert.That(values.Bedrooms, Is.EqualTo(2));
            Assert.That(values.Lat, Is.EqualTo(10.7721));
            Assert.That(values.Lng, Is.EqualTo(106.6579));
        });
    }

    [Test]
    public void TryReadValues_UnparseableText_ReturnsFalse()
    {
        var state = CreateValidState();
        state.PriceText = "không rõ";

        Assert.That(ListingFormValidator.TryReadValues(state, out _), Is.False);
    }

    [TestCase("3500000", 3_500_000L)]
    [TestCase("3.500.000", 3_500_000L)]
    [TestCase("3,500,000", 3_500_000L)]
    [TestCase("2 500 000", 2_500_000L)]
    [TestCase("0", 0L)]
    public void TryParseAmount_IgnoresGroupSeparators(string text, long expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingFormValidator.TryParseAmount(text, out var value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("-5")]
    [TestCase("12a")]
    public void TryParseAmount_RejectsInvalidInput(string? text)
        => Assert.That(ListingFormValidator.TryParseAmount(text, out _), Is.False);

    [TestCase("20.5", 20.5)]
    [TestCase("20,5", 20.5)]
    [TestCase(" 12 ", 12.0)]
    public void TryParseDecimal_AcceptsDotOrCommaSeparator(string text, double expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingFormValidator.TryParseDecimal(text, out var value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        });
    }

    [TestCase("abc")]
    [TestCase("")]
    [TestCase(null)]
    public void TryParseDecimal_RejectsInvalidInput(string? text)
        => Assert.That(ListingFormValidator.TryParseDecimal(text, out _), Is.False);

    [TestCase("", true, 0)]
    [TestCase(" 2 ", true, 2)]
    [TestCase("0", true, 0)]
    [TestCase("-1", false, 0)]
    [TestCase("1.5", false, 0)]
    public void TryParseBedrooms_AcceptsWholeNonNegativeNumbers(string? text, bool expected, int value)
    {
        var parsed = ListingFormValidator.TryParseBedrooms(text, out var bedrooms);

        Assert.That(parsed, Is.EqualTo(expected));

        if (expected)
            Assert.That(bedrooms, Is.EqualTo(value));
    }

    [Test]
    public void TryParseCoordinate_EnforcesInclusiveBounds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ListingFormValidator.TryParseCoordinate("-90", -90, 90, out _), Is.True);
            Assert.That(ListingFormValidator.TryParseCoordinate("90", -90, 90, out _), Is.True);
            Assert.That(ListingFormValidator.TryParseCoordinate("90.01", -90, 90, out _), Is.False);
            Assert.That(ListingFormValidator.TryParseCoordinate("-180", -180, 180, out _), Is.True);
            Assert.That(ListingFormValidator.TryParseCoordinate("180.2", -180, 180, out _), Is.False);
        });
    }

    private static ListingFormState CreateValidState() => new()
    {
        Title = "Phòng trọ gần đại học",
        Address = "12 Nguyễn Văn Cừ, Quận 5",
        PriceText = "3.500.000",
        DepositText = "3.500.000",
        AreaText = "20",
        BedroomsText = "1",
        DescriptionText = "Phòng sạch, gần trường.",
        LatText = "10.7721",
        LngText = "106.6579",
        LandlordNameText = "Chủ trọ",
        LandlordPhoneText = "0900000000",
        RoomType = RoomType.Studio,
        ExistingPhotoCount = 0,
        NewPhotos = [Photo(ValidPhotoName), Photo("phong-2.jpg"), Photo("phong-3.png", "image/png")]
    };

    private static ListingPhotoCandidate Photo(
        string fileName,
        string? contentType = "image/jpeg",
        long? length = 1024)
        => new(fileName, contentType, length);
}

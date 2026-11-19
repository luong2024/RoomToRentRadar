using System.Globalization;
using System.Net;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels.Base;

[TestFixture]
public class ErrorMessageMapperTests
{
    private const string FallbackEn = "An unexpected error occurred, please try again.";

    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Vi = new("vi-VN");

    private static readonly string[] ErrorKeys =
    {
        "Error_VALIDATION_FAILED",
        "Error_INVALID_SORT_OPTION",
        "Error_INVALID_PRICE_RANGE",
        "Error_INVALID_AREA_RANGE",
        "Error_INVALID_DISTANCE_RANGE",
        "Error_CAMPUS_NOT_FOUND",
        "Error_INVALID_STATE_TRANSITION",
        "Error_INVALID_CREDENTIALS",
        "Error_ACCOUNT_LOCKED",
        "Error_EMAIL_TAKEN",
        "Error_LANDLORD_NOT_APPROVED",
        "Error_ROOM_NOT_FOUND",
        "Error_LISTING_NOT_AVAILABLE",
        "Error_ALREADY_REPORTED",
        "Error_LIMIT_EXCEEDED",
        "Error_CONCURRENCY_CONFLICT",
        "Error_ETAG_MISMATCH",
        "Error_RATE_LIMITED",
        "Error_NETWORK_ERROR",
        "Error_LockDuration",
        "Error_Unexpected"
    };

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    private static ApiException Error(string? code) =>
        new("Server rejected the request", HttpStatusCode.BadRequest, code);

    [TestCase("VALIDATION_FAILED", "Please check the values you entered.")]
    [TestCase("INVALID_SORT_OPTION", "That sort option is not supported.")]
    [TestCase("INVALID_PRICE_RANGE", "The price range is not valid.")]
    [TestCase("INVALID_AREA_RANGE", "The area range is not valid.")]
    [TestCase("INVALID_DISTANCE_RANGE", "The distance filter is not valid.")]
    [TestCase("CAMPUS_NOT_FOUND", "That campus could not be found.")]
    [TestCase("INVALID_STATE_TRANSITION", "This action is not allowed in the current state.")]
    [TestCase("INVALID_CREDENTIALS", "Invalid email or password.")]
    [TestCase("ACCOUNT_LOCKED", "Your account is temporarily locked, please try again in 15 minutes.")]
    [TestCase("EMAIL_TAKEN", "This email address is already in use.")]
    [TestCase("LANDLORD_NOT_APPROVED", "Landlord account is pending approval.")]
    [TestCase("ROOM_NOT_FOUND", "Listing does not exist.")]
    [TestCase("LISTING_NOT_AVAILABLE", "Room is no longer available.")]
    [TestCase("ALREADY_REPORTED", "You have already reported this listing.")]
    [TestCase("LIMIT_EXCEEDED", "Limit reached.")]
    [TestCase("CONCURRENCY_CONFLICT", "Data has changed, please refresh.")]
    [TestCase("ETAG_MISMATCH", "Data has changed, please refresh.")]
    [TestCase("RATE_LIMITED", "Too many requests, please try again shortly.")]
    public void Map_MappedCode_ReturnsExpectedTextEn(string code, string expected)
    {
        var text = ErrorMessageMapper.Map(Error(code), En);

        Assert.That(text, Is.EqualTo(expected));
    }

    [TestCase("VALIDATION_FAILED", "Vui lòng kiểm tra lại các giá trị đã nhập.")]
    [TestCase("INVALID_SORT_OPTION", "Kiểu sắp xếp này không được hỗ trợ.")]
    [TestCase("INVALID_PRICE_RANGE", "Khoảng giá không hợp lệ.")]
    [TestCase("INVALID_AREA_RANGE", "Khoảng diện tích không hợp lệ.")]
    [TestCase("INVALID_DISTANCE_RANGE", "Bộ lọc khoảng cách không hợp lệ.")]
    [TestCase("CAMPUS_NOT_FOUND", "Không tìm thấy cơ sở này.")]
    [TestCase("INVALID_STATE_TRANSITION", "Thao tác này không hợp lệ ở trạng thái hiện tại.")]
    [TestCase("INVALID_CREDENTIALS", "Email hoặc mật khẩu không đúng.")]
    [TestCase("ACCOUNT_LOCKED", "Tài khoản tạm thời bị khoá, vui lòng thử lại sau 15 phút.")]
    [TestCase("EMAIL_TAKEN", "Email này đã được sử dụng.")]
    [TestCase("LANDLORD_NOT_APPROVED", "Tài khoản chủ trọ đang chờ duyệt.")]
    [TestCase("ROOM_NOT_FOUND", "Tin đăng không tồn tại.")]
    [TestCase("LISTING_NOT_AVAILABLE", "Phòng không còn trống.")]
    [TestCase("ALREADY_REPORTED", "Bạn đã báo cáo tin đăng này.")]
    [TestCase("LIMIT_EXCEEDED", "Đã đạt giới hạn.")]
    [TestCase("CONCURRENCY_CONFLICT", "Dữ liệu đã thay đổi, vui lòng tải lại.")]
    [TestCase("ETAG_MISMATCH", "Dữ liệu đã thay đổi, vui lòng tải lại.")]
    [TestCase("RATE_LIMITED", "Quá nhiều yêu cầu, vui lòng thử lại sau ít phút.")]
    public void Map_MappedCode_ReturnsExpectedTextVi(string code, string expected)
    {
        var text = ErrorMessageMapper.Map(Error(code), Vi);

        Assert.That(text, Is.EqualTo(expected));
    }

    [TestCase("DUPLICATE_ROOM")]
    [TestCase("INVALID_TOKEN")]
    [TestCase("FORBIDDEN_ROLE")]
    [TestCase("IF_MATCH_REQUIRED")]
    [TestCase("FILE_TOO_LARGE")]
    [TestCase("UNSUPPORTED_MEDIA_TYPE")]
    [TestCase("NOT_FOUND")]
    [TestCase("FORBIDDEN")]
    [TestCase("UNAUTHORIZED")]
    [TestCase("INTERNAL_ERROR")]
    public void Map_TenUnmappedServerCodes_ReturnFallback(string code)
    {
        var text = ErrorMessageMapper.Map(Error(code), En);

        Assert.That(text, Is.EqualTo(FallbackEn));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Map_NullOrEmptyCode_ReturnsFallback(string? code)
    {
        var text = ErrorMessageMapper.Map(Error(code), En);

        Assert.That(text, Is.EqualTo(FallbackEn));
    }

    [Test]
    public void Map_NetworkError_ReturnsNoInternet()
    {
        var exception = new ApiException("Network unreachable", (HttpStatusCode)0, ApiErrorCodes.NetworkError);

        Assert.Multiple(() =>
        {
            Assert.That(ErrorMessageMapper.Map(exception, En), Is.EqualTo("No internet connection."));
            Assert.That(ErrorMessageMapper.Map(exception, Vi), Is.EqualTo("Không có kết nối mạng."));
        });
    }

    [Test]
    public void Map_PlainException_ReturnsFallbackWithoutMessageLeak()
    {
        var exception = new InvalidOperationException("SECRET database connection string leaked");

        var text = ErrorMessageMapper.Map(exception, En);

        Assert.That(text, Is.EqualTo(FallbackEn));
        Assert.That(text, Does.Not.Contain("SECRET"));
    }

    [Test]
    public void Map_AccountLocked_ReplacesTimeWith15Minutes()
    {
        var exception = Error("ACCOUNT_LOCKED");

        Assert.Multiple(() =>
        {
            Assert.That(
                ErrorMessageMapper.Map(exception, En),
                Is.EqualTo("Your account is temporarily locked, please try again in 15 minutes."));
            Assert.That(
                ErrorMessageMapper.Map(exception, Vi),
                Is.EqualTo("Tài khoản tạm thời bị khoá, vui lòng thử lại sau 15 phút."));
            Assert.That(ErrorMessageMapper.Map(exception, En), Does.Not.Contain("{time}"));
        });
    }

    [Test]
    public void AllErrorKeys_ExistInBothResx()
    {
        Assert.Multiple(() =>
        {
            foreach (var key in ErrorKeys)
            {
                var en = AppResources.GetString(key, En);
                var vi = AppResources.GetString(key, Vi);

                Assert.That(en, Is.Not.Empty, $"Empty EN value for {key}");
                Assert.That(vi, Is.Not.Empty, $"Empty VI value for {key}");
                Assert.That(en, Is.Not.EqualTo($"[{key}]"), $"Missing EN value for {key}");
                Assert.That(vi, Is.Not.EqualTo($"[{key}]"), $"Missing VI value for {key}");
                Assert.That(vi, Is.Not.EqualTo(en), $"VI value equals EN for {key}");
            }
        });
    }
}

using FluentAssertions;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Landlord;
using RoomToRentRadar.Api.Services.Moderation;
using RoomToRentRadar.Api.Services.Photos;
using RoomToRentRadar.Shared.Dtos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>US08 / T08.2 — landlord listing API &amp; image upload.</summary>
public class LandlordListingServiceTests
{
    private const string Landlord = "usr-landlord-20";

    private sealed class FakeStorage : IPhotoStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public Task<string> SaveAsync(string listingId, string fileName, byte[] content, CancellationToken ct)
        {
            Files[fileName] = content;
            return Task.FromResult($"/media/listings/{listingId}/{fileName}");
        }

        public Task DeleteAsync(string listingId, string fileName, CancellationToken ct)
        {
            Files.Remove(fileName);
            return Task.CompletedTask;
        }
    }

    private readonly InMemoryListingRepository _repo = new();
    private readonly FakeStorage _storage = new();
    private readonly LandlordListingService _sut;
    private readonly ListingModerationService _moderation;

    public LandlordListingServiceTests()
    {
        var options = Options.Create(new PhotoOptions());
        var photoService = new PhotoService(new InMemoryPhotoRepository(_repo), _storage, new ImageSharpProcessor(options), options);
        _sut = new LandlordListingService(_repo, _repo, photoService, TimeProvider.System);
        _moderation = new ListingModerationService(_repo, new InMemoryModerationAuditRepository());
    }

    private static CreateListingRequestDto ValidRequest() => new()
    {
        CampusId = "ictu-main",
        Title = "Phòng khép kín mới xây gần cổng ICTU",
        Description = "Phòng rộng, có ban công, wifi miễn phí, giờ giấc tự do.",
        Rent = 1_700_000m,
        Deposit = 1_700_000m,
        Area = 20,
        RoomType = "Khép kín",
        Address = "Ngõ 10 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
        Latitude = 21.5870,
        Longitude = 105.8090,
        Amenities = ["Wifi", "wifi", "Balcony"],
        ContactName = "Cô Mai",
        ContactPhone = "0987000111",
        QuietHours = "23:00 - 05:30"
    };

    private static byte[] Png()
    {
        using var img = new Image<Rgba32>(120, 80, new Rgba32(200, 100, 50));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static List<PhotoUploadItem> Photos(int count, string extension = ".png")
    {
        var data = Png();
        return Enumerable.Range(1, count)
            .Select(i => new PhotoUploadItem($"p{i}{extension}", data.Length, new MemoryStream(data), false))
            .ToList();
    }

    [Fact]
    public async Task Create_ValidRequest_IsPendingReviewNotVerifiedWithPhotos()
    {
        var created = await _sut.CreateAsync(Landlord, ValidRequest(), Photos(3));

        created.Status.Should().Be(ListingAvailabilityStatus.PendingReview);
        created.StatusLabel.Should().Be("Đang chờ duyệt (Pending Review)");
        created.ModerationStatus.Should().Be(ListingModerationStatus.PendingReview);
        created.IsVerified.Should().BeFalse();
        created.PhotoCount.Should().Be(3);
        created.CoverPhotoUrl.Should().EndWith("_thumb.jpg");
        created.Amenities.Should().Equal("Wifi", "Balcony");
        created.Latitude.Should().Be(21.5870);
        created.Longitude.Should().Be(105.8090);
        created.DistanceMeters.Should().BeInRange(200, 600);
        _storage.Files.Should().HaveCount(6); // ảnh + thumbnail

        var stored = await _repo.GetByIdAsync(created.Id);
        stored!.Location.Coordinates.Should().Equal(105.8090, 21.5870); // GeoJSON [lng, lat]
        stored.Landlord.Id.Should().Be(Landlord);
    }

    [Fact]
    public async Task Create_PendingListing_IsHiddenFromPublicQueries()
    {
        var created = await _sut.CreateAsync(Landlord, ValidRequest(), Photos(3));

        var publicListings = await _repo.GetPublicListingsAsync("ictu-main");
        publicListings.Should().NotContain(l => l.Id == created.Id);
        (await new ListingDetailService(_repo).GetByIdAsync(created.Id, isAuthenticated: true)).Should().BeNull();
    }

    [Fact]
    public async Task Create_FewerThanThreePhotos_Rejected()
    {
        var act = () => _sut.CreateAsync(Landlord, ValidRequest(), Photos(2));

        var ex = (await act.Should().ThrowAsync<RequestValidationException>()).Which;
        ex.Errors.Should().ContainKey("photos");
        (await _moderation.GetLandlordListingsAsync(Landlord)).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WrongExtension_Rejected()
    {
        var act = () => _sut.CreateAsync(Landlord, ValidRequest(), Photos(3, ".gif"));

        (await act.Should().ThrowAsync<RequestValidationException>()).Which.Errors.Should().ContainKey("photos");
    }

    [Fact]
    public async Task Create_CorruptImage_RollsBackListing()
    {
        var junk = new byte[] { 1, 2, 3, 4 };
        var photos = Enumerable.Range(1, 3)
            .Select(i => new PhotoUploadItem($"p{i}.jpg", junk.Length, new MemoryStream(junk), false))
            .ToList();

        var act = () => _sut.CreateAsync(Landlord, ValidRequest(), photos);

        await act.Should().ThrowAsync<RequestValidationException>();
        (await _moderation.GetLandlordListingsAsync(Landlord)).TotalCount.Should().Be(0);
        _storage.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_OnlyReturnsOwnListing()
    {
        var created = await _sut.CreateAsync(Landlord, ValidRequest(), Photos(3));

        (await _sut.GetAsync(Landlord, created.Id))!.Photos.Should().HaveCount(3);
        (await _sut.GetAsync("usr-landlord-09", created.Id)).Should().BeNull();
    }

    [Fact]
    public void Rules_InvalidFields_ReportCamelCaseKeys()
    {
        var request = ValidRequest();
        request.Title = "ngắn";
        request.Rent = 0;
        request.Area = double.NaN;
        request.ContactPhone = "12345";
        request.Latitude = 0;
        request.Longitude = 0;

        var errors = ListingCreationRules.Validate(request);

        errors.Keys.Should().BeEquivalentTo("title", "rent", "area", "contactPhone", "location");
    }

    [Fact]
    public void Rules_PhotoTooLarge_Rejected()
    {
        var errors = ListingCreationRules.ValidatePhotos(
            [("a.jpg", 1000), ("b.png", 2000), ("c.jpeg", ListingCreationRules.MaxPhotoSizeBytes + 1)]);

        errors.Should().ContainKey("photos");
        errors["photos"].Should().ContainSingle().Which.Should().Contain("c.jpeg");
    }

    [Fact]
    public async Task Create_ThenModeratorApproves_ListingBecomesPublic()
    {
        var created = await _sut.CreateAsync(Landlord, ValidRequest(), Photos(3));

        // T08.3 thấy tin mới trong hàng đợi duyệt + trang quản lý của chủ trọ
        var queue = await _moderation.GetModerationQueueAsync(ListingModerationStatus.PendingReview);
        queue.Items.Should().Contain(i => i.ListingId == created.Id && i.PhotoCount == 3);
        (await _moderation.GetLandlordListingsAsync(Landlord)).Items.Should().ContainSingle(i => i.ListingId == created.Id);

        await _moderation.ModerateListingAsync(created.Id, "usr-mod-01", new ModerationDecisionRequest { Decision = "Approve" });

        var publicListings = await _repo.GetPublicListingsAsync("ictu-main");
        publicListings.Should().Contain(l => l.Id == created.Id && l.IsVerified);
    }

    [Theory]
    [InlineData("0987000111", "0987000111")]
    [InlineData("+84 987 000 111", "0987000111")]
    [InlineData("84987000111", "0987000111")]
    [InlineData("0987.000-111", "0987000111")]
    public void Rules_NormalizePhone_AcceptsCommonFormats(string input, string expected)
    {
        ListingCreationRules.NormalizePhone(input).Should().Be(expected);
    }

    [Fact]
    public async Task Create_CommaSeparatedAmenitiesAndSpacedPhone_AreNormalized()
    {
        var request = ValidRequest();
        request.Amenities = ["Wifi, Điều hòa", "wifi;Ban công"];
        request.ContactPhone = "+84 987 000 111";

        var created = await _sut.CreateAsync(Landlord, request, Photos(3));

        created.Amenities.Should().Equal("Wifi", "Điều hòa", "Ban công");
        created.ContactPhone.Should().Be("0987000111");
    }
}

using System.Net;
using System.Text;
using Moq;
using NUnit.Framework;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class CreateListingServiceTests
{
    private Mock<IApiClient> _mockApiClient = null!;
    private CreateListingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockApiClient = new Mock<IApiClient>();
        _service = new CreateListingService(_mockApiClient.Object);
    }

    [Test]
    public void Constructor_NullApiClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateListingService(null!));
    }

    [Test]
    public void CreateListingAsync_NullRequest_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.CreateListingAsync(null!));
    }

    [Test]
    public async Task CreateListingAsync_Success_WhenApiReturnsListingId()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateListingApiResponse
            {
                ListingId = "real-listing-id-001",
                Status = "Success"
            });

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ListingId, Is.EqualTo("real-listing-id-001"));
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task CreateListingAsync_Success_WhenApiReturnsIdPropertyFallback()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateListingApiResponse
            {
                Id = "backend-guid-7788",
                Status = "Created"
            });

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ListingId, Is.EqualTo("backend-guid-7788"));
    }

    [Test]
    public async Task CreateListingAsync_ReturnsFailure_WhenApiResponseIsNull()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateListingApiResponse?)null);

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ListingId, Is.Null);
        Assert.That(result.ErrorMessage, Does.Contain("Không nhận được mã tin đăng hợp lệ"));
    }

    [Test]
    public async Task CreateListingAsync_ReturnsFailure_WhenApiResponseHasEmptyListingId()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateListingApiResponse
            {
                ListingId = "   ",
                Id = null,
                Message = "Database error persisting listing"
            });

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ListingId, Is.Null);
        Assert.That(result.ErrorMessage, Does.Contain("Database error persisting listing"));
    }

    [Test]
    public async Task CreateListingAsync_ReturnsFailure_WhenHttp404NotFoundOccurs()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound));

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ListingId, Is.Null);
        Assert.That(result.ErrorMessage, Does.Contain("404 Not Found"));
    }

    [Test]
    public async Task CreateListingAsync_ReturnsFailure_WhenGenericHttpErrorOccurs()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ListingId, Is.Null);
        Assert.That(result.ErrorMessage, Does.Contain("500"));
    }

    [Test]
    public async Task CreateListingAsync_ReturnsFailure_WhenGeneralExceptionOccurs()
    {
        var request = CreateSampleRequest();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network connection lost"));

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ListingId, Is.Null);
        Assert.That(result.ErrorMessage, Does.Contain("Network connection lost"));
    }

    [Test]
    public async Task CreateListingAsync_ConstructsMultipartFormDataWithActualPhotoStreams()
    {
        var photoBytes1 = Encoding.UTF8.GetBytes("fake-jpeg-data-1");
        var photoBytes2 = Encoding.UTF8.GetBytes("fake-png-data-2");

        var photos = new List<ListingPhotoItem>
        {
            new()
            {
                FilePath = "photo1.jpg",
                FileName = "photo1.jpg",
                FileSizeBytes = photoBytes1.Length,
                ContentType = "image/jpeg",
                ContentBytes = photoBytes1
            },
            new()
            {
                FilePath = "photo2.png",
                FileName = "photo2.png",
                FileSizeBytes = photoBytes2.Length,
                ContentType = "image/png",
                ContentBytes = photoBytes2
            }
        };

        var request = new CreateListingRequest(
            Title: "Phòng trọ cao cấp gần ĐH Sư Phạm",
            Address: "Số 10 Lương Ngọc Quyến, TP. Thái Nguyên",
            MonthlyRentVnd: 2500000m,
            DepositVnd: 2000000m,
            AreaM2: 30.5,
            RoomType: "Apartment",
            Description: "Phòng mới xây khép kín, an ninh tốt.",
            Photos: photos,
            PhotoUrls: new[] { "https://cdn.example.com/existing.jpg" });

        var capturedPartNames = new List<string>();
        var capturedPhotoFileNames = new List<string>();
        var capturedPhotoContentTypes = new List<string>();
        var capturedPhotoBytes = new List<byte[]>();

        _mockApiClient
            .Setup(a => a.PostMultipartAsync<CreateListingApiResponse>(
                "api/v1/landlord/listings",
                It.IsAny<MultipartFormDataContent>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, MultipartFormDataContent, CancellationToken>(async (_, content, _) =>
            {
                foreach (var part in content)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"') ?? string.Empty;
                    capturedPartNames.Add(name);

                    if (name == "photos")
                    {
                        var fileName = part.Headers.ContentDisposition?.FileName?.Trim('"') ?? string.Empty;
                        capturedPhotoFileNames.Add(fileName);
                        capturedPhotoContentTypes.Add(part.Headers.ContentType?.MediaType ?? string.Empty);
                        var bytes = await part.ReadAsByteArrayAsync();
                        capturedPhotoBytes.Add(bytes);
                    }
                }
            })
            .ReturnsAsync(new CreateListingApiResponse { ListingId = "listing-uploaded-999" });

        var result = await _service.CreateListingAsync(request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ListingId, Is.EqualTo("listing-uploaded-999"));

        Assert.That(capturedPartNames, Does.Contain("title"));
        Assert.That(capturedPartNames, Does.Contain("address"));
        Assert.That(capturedPartNames, Does.Contain("monthlyRentVnd"));
        Assert.That(capturedPartNames, Does.Contain("depositVnd"));
        Assert.That(capturedPartNames, Does.Contain("areaM2"));
        Assert.That(capturedPartNames, Does.Contain("roomType"));
        Assert.That(capturedPartNames, Does.Contain("description"));
        Assert.That(capturedPartNames, Does.Contain("photoUrls"));

        Assert.That(capturedPhotoFileNames, Is.EquivalentTo(new[] { "photo1.jpg", "photo2.png" }));
        Assert.That(capturedPhotoContentTypes, Is.EquivalentTo(new[] { "image/jpeg", "image/png" }));
        Assert.That(capturedPhotoBytes[0], Is.EqualTo(photoBytes1));
        Assert.That(capturedPhotoBytes[1], Is.EqualTo(photoBytes2));
    }

    private static CreateListingRequest CreateSampleRequest()
    {
        return new CreateListingRequest(
            Title: "Phòng trọ sinh viên gần cổng trường",
            Address: "Đường Z115, Thái Nguyên",
            MonthlyRentVnd: 1500000m,
            DepositVnd: 1000000m,
            AreaM2: 20,
            RoomType: "BoardingRoom",
            Description: "Đầy đủ tiện nghi",
            Photos: Array.Empty<ListingPhotoItem>());
    }
}

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// US08 AC4 / BR-MED-02..03: the photo endpoints require a verified landlord, forward the authenticated
/// user (anti-IDOR) and return 201/204 without exposing storage internals.
/// </summary>
[TestFixture]
public class PhotosControllerTests
{
    private IUploadPhotoUseCase _uploadPhotoUseCase = null!;
    private IDeletePhotoUseCase _deletePhotoUseCase = null!;
    private PhotosController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _uploadPhotoUseCase = Substitute.For<IUploadPhotoUseCase>();
        _deletePhotoUseCase = Substitute.For<IDeletePhotoUseCase>();
        _controller = new PhotosController(_uploadPhotoUseCase, _deletePhotoUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = VerifiedLandlordPrincipal() }
            }
        };
    }

    [Test]
    public void PhotosController_RequiresVerifiedLandlordPolicy()
    {
        var classPolicy = typeof(PhotosController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(classPolicy, Is.Not.Null);
            Assert.That(classPolicy!.Policy, Is.EqualTo(AuthorizationPolicies.VerifiedLandlordPolicy));
            Assert.That(
                GetAction(nameof(PhotosController.UploadPhoto)).GetCustomAttribute<AuthorizeAttribute>(),
                Is.Null,
                "The class-level policy already covers every action.");
        });
    }

    [Test]
    public void UploadPhoto_AllowsTransportHeadroomAboveTheTenMegabyteRule()
    {
        var attribute = GetAction(nameof(PhotosController.UploadPhoto))
            .GetCustomAttribute<RequestSizeLimitAttribute>();

        var bytesField = typeof(RequestSizeLimitAttribute)
            .GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Multiple(() =>
        {
            Assert.That(attribute, Is.Not.Null);
            Assert.That(bytesField, Is.Not.Null);
            Assert.That(bytesField!.GetValue(attribute), Is.EqualTo(11L * 1024 * 1024));
        });
    }

    [Test]
    public async Task UploadPhoto_MapsTheMultipartFileAndReturns201()
    {
        var dto = new PhotoUploadDto(
            Id: "photo-1",
            Kind: PhotoKind.Standard,
            ThumbUrl: "http://localhost:5000/media/rooms/photo-1/thumb.jpg",
            MediumUrl: "http://localhost:5000/media/rooms/photo-1/medium.jpg",
            Width: 1600,
            Height: 1200,
            SizeBytes: 2048);
        UploadPhotoRequest? captured = null;

        _uploadPhotoUseCase
            .ExecuteAsync(Arg.Do<UploadPhotoRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(dto);

        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02 };
        await using var content = new MemoryStream(bytes);
        var file = new FormFile(content, 0, content.Length, "file", "room.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await _controller.UploadPhoto(file, PhotoKind.Standard, CancellationToken.None);

        var objectResult = result.Result as ObjectResult;
        Assert.Multiple(() =>
        {
            Assert.That(objectResult, Is.Not.Null);
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
            Assert.That(objectResult.Value, Is.SameAs(dto));
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.UserId, Is.EqualTo("landlord-1"));
            Assert.That(captured.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(captured.FileName, Is.EqualTo("room.jpg"));
            Assert.That(captured.Length, Is.EqualTo(bytes.Length));
            Assert.That(captured.Content, Is.Not.Null);
        });
    }

    [Test]
    public async Task UploadPhoto_WithoutAuthenticatedUser_ThrowsInvalidToken()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var file = new FormFile(content, 0, content.Length, "file", "room.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        await Assert.ThatAsync(
            async () => await _controller.UploadPhoto(file, PhotoKind.Standard, CancellationToken.None),
            Throws.TypeOf<RoomToRentRadar.Domain.Exceptions.InvalidTokenException>());
    }

    [Test]
    public async Task UploadPhoto_WithoutKindField_ThrowsValidationFailedAndDoesNotStore()
    {
        // BR-MED-02 / openapi uploadPhoto `required: [file, kind]`: missing kind must be a
        // 400 VALIDATION_FAILED, not a silent default to PhotoKind.Standard.
        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);
        var file = new FormFile(content, 0, content.Length, "file", "room.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var exception = Assert.ThrowsAsync<DomainValidationException>(
            async () => await _controller.UploadPhoto(file, kind: null, CancellationToken.None));

        Assert.That(exception!.ErrorCode, Is.EqualTo(ProblemCodes.ValidationFailed));
        await _uploadPhotoUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<UploadPhotoRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeletePhoto_ScopesTheDeleteToTheCallerAndReturns204()
    {
        var result = await _controller.DeletePhoto("photo-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
        });

        await _deletePhotoUseCase.Received(1).ExecuteAsync(
            "landlord-1",
            "photo-1",
            Arg.Any<CancellationToken>());
    }

    private static MethodInfo GetAction(string name) =>
        typeof(PhotosController).GetMethod(name)!;

    private static ClaimsPrincipal VerifiedLandlordPrincipal() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "landlord-1"),
                new Claim(ClaimTypes.Role, nameof(UserRole.Landlord)),
                new Claim(AuthorizationPolicies.VerifiedClaimType, "true")
            ],
            authenticationType: "Test"));
}

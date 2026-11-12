using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Authorization;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class AuthControllerAndRbacTests
{
    private IRegisterUserUseCase _registerUseCase = null!;
    private ILoginUserUseCase _loginUseCase = null!;
    private ILoginWithIctuSsoUseCase _ssoUseCase = null!;
    private IRefreshTokenUseCase _refreshUseCase = null!;
    private IRevokeTokenUseCase _revokeUseCase = null!;
    private IGetCurrentUserProfileUseCase _getProfileUseCase = null!;
    private IApproveLandlordUseCase _approveLandlordUseCase = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _registerUseCase = Substitute.For<IRegisterUserUseCase>();
        _loginUseCase = Substitute.For<ILoginUserUseCase>();
        _ssoUseCase = Substitute.For<ILoginWithIctuSsoUseCase>();
        _refreshUseCase = Substitute.For<IRefreshTokenUseCase>();
        _revokeUseCase = Substitute.For<IRevokeTokenUseCase>();
        _getProfileUseCase = Substitute.For<IGetCurrentUserProfileUseCase>();
        _approveLandlordUseCase = Substitute.For<IApproveLandlordUseCase>();

        _controller = new AuthController(
            _registerUseCase,
            _loginUseCase,
            _ssoUseCase,
            _refreshUseCase,
            _revokeUseCase,
            _getProfileUseCase,
            _approveLandlordUseCase);
    }

    [Test]
    public async Task Register_Returns201CreatedWithAuthTokensDto()
    {
        var expected = CreateSampleTokens("user-1", "Student", isVerified: true);
        _registerUseCase.ExecuteAsync(Arg.Any<RegisterUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var actionResult = await _controller.Register(
            new RegisterUserRequest("student@ictu.edu.vn", "StrongPass123", "Student", "Student"),
            CancellationToken.None);

        var createdResult = actionResult.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(createdResult, Is.Not.Null);
            Assert.That(createdResult!.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
            Assert.That(createdResult.Value, Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task Revoke_ExtractsAuthenticatedUserIdAndReturns204NoContent()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("student-123", UserRole.Student, isVerified: true)
            }
        };

        var result = await _controller.Revoke(new RevokeTokenRequest("refresh-token-1"), CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        await _revokeUseCase.Received(1).ExecuteAsync(
            "student-123",
            Arg.Is<RevokeTokenRequest>(r => r.RefreshToken == "refresh-token-1"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ApproveLandlord_RequiresAdminPolicyAttribute_AndDelegatesWithAdminUserId()
    {
        var methodInfo = typeof(AuthController).GetMethod(nameof(AuthController.ApproveLandlord));
        var authorizeAttr = methodInfo?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(authorizeAttr, Is.Not.Null);
        Assert.That(authorizeAttr!.Policy, Is.EqualTo(AuthorizationPolicies.AdminPolicy));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("admin-001", UserRole.Admin, isVerified: true)
            }
        };

        var approvedProfile = new UserProfileDto(
            "landlord-99",
            "landlord@gmail.com",
            "Chu Tro",
            "Landlord",
            "Local",
            "0987654321",
            null,
            IsVerified: true,
            DateTime.UtcNow);

        _approveLandlordUseCase.ExecuteAsync(
                "admin-001",
                Arg.Is<ApproveLandlordRequest>(r => r.LandlordUserId == "landlord-99"),
                Arg.Any<CancellationToken>())
            .Returns(approvedProfile);

        var response = await _controller.ApproveLandlord("landlord-99", CancellationToken.None);
        var okResult = response.Result as OkObjectResult;

        Assert.Multiple(() =>
        {
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.Value, Is.EqualTo(approvedProfile));
        });
    }

    [Test]
    public void MaskLandlordPhoneIfAnonymous_MasksForGuestAndRevealsForAuthenticatedRoles()
    {
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var studentPrincipal = CreatePrincipal("student-1", UserRole.Student, isVerified: true);
        var landlordPrincipal = CreatePrincipal("landlord-1", UserRole.Landlord, isVerified: false);

        Assert.Multiple(() =>
        {
            Assert.That(anonymousPrincipal.MaskLandlordPhoneIfAnonymous("0912345678"), Is.Null);
            Assert.That(studentPrincipal.MaskLandlordPhoneIfAnonymous("0912345678"), Is.EqualTo("0912345678"));
            Assert.That(landlordPrincipal.MaskLandlordPhoneIfAnonymous("0912345678"), Is.EqualTo("0912345678"));
        });
    }

    [Test]
    public void CanPublishListing_EnforcesVerifiedLandlordOrAdminRule()
    {
        var guest = new ClaimsPrincipal(new ClaimsIdentity());
        var student = CreatePrincipal("student-1", UserRole.Student, isVerified: true);
        var unapprovedLandlord = CreatePrincipal("landlord-1", UserRole.Landlord, isVerified: false);
        var approvedLandlord = CreatePrincipal("landlord-2", UserRole.Landlord, isVerified: true);
        var admin = CreatePrincipal("admin-1", UserRole.Admin, isVerified: true);

        Assert.Multiple(() =>
        {
            Assert.That(guest.CanPublishListing(), Is.False);
            Assert.That(student.CanPublishListing(), Is.False);
            Assert.That(unapprovedLandlord.CanPublishListing(), Is.False);
            Assert.That(approvedLandlord.CanPublishListing(), Is.True);
            Assert.That(admin.CanPublishListing(), Is.True);
        });
    }

    [TestCase(typeof(DomainValidationException), StatusCodes.Status400BadRequest)]
    [TestCase(typeof(InvalidCredentialsException), StatusCodes.Status401Unauthorized)]
    [TestCase(typeof(InvalidTokenException), StatusCodes.Status401Unauthorized)]
    [TestCase(typeof(AccountLockedException), StatusCodes.Status403Forbidden)]
    [TestCase(typeof(ForbiddenRoleException), StatusCodes.Status403Forbidden)]
    [TestCase(typeof(LandlordNotApprovedException), StatusCodes.Status403Forbidden)]
    [TestCase(typeof(UserNotFoundException), StatusCodes.Status404NotFound)]
    [TestCase(typeof(DuplicateEmailException), StatusCodes.Status409Conflict)]
    [TestCase(typeof(ConcurrencyConflictException), StatusCodes.Status409Conflict)]
    public async Task AuthExceptionHandlingMiddleware_MapsDomainExceptionsToExpectedProblemDetails(
        Type exceptionType,
        int expectedStatusCode)
    {
        var exception = CreateExceptionInstance(exceptionType);
        var middleware = new AuthExceptionHandlingMiddleware(_ => throw exception);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/auth/login";
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(httpContext.Response.Body);

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(expectedStatusCode));
            Assert.That(problem.Detail, Is.Not.Null.And.Not.Empty);
        });
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, UserRole role, bool isVerified)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("role", role.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(AuthorizationPolicies.VerifiedClaimType, isVerified ? "true" : "false")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static AuthTokensDto CreateSampleTokens(string userId, string role, bool isVerified)
    {
        var now = DateTime.UtcNow;
        return new AuthTokensDto(
            AccessToken: "jwt-token",
            RefreshToken: "refresh-token",
            AccessTokenExpiresAtUtc: now.AddMinutes(30),
            RefreshTokenExpiresAtUtc: now.AddDays(7),
            User: new UserProfileDto(
                userId,
                "user@ictu.edu.vn",
                "Test User",
                role,
                "Local",
                "0912345678",
                "DTC01",
                isVerified,
                now));
    }

    private static Exception CreateExceptionInstance(Type exceptionType)
    {
        if (exceptionType == typeof(AccountLockedException))
        {
            return new AccountLockedException(DateTime.UtcNow.AddMinutes(15));
        }

        if (exceptionType == typeof(DomainValidationException))
        {
            // DomainValidationException was widened with an optional errorCode (ADR-012), so it no
            // longer exposes a single-string constructor for reflection.
            return new DomainValidationException("Test error detail");
        }

        return (Exception)Activator.CreateInstance(exceptionType, "Test error detail")!;
    }
}


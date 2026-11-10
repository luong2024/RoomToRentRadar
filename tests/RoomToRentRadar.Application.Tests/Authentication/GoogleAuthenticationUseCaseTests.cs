using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Authentication;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests.Authentication;

public class GoogleAuthenticationUseCaseTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private InMemoryFakeUserRepository _userRepository = null!;
    private InMemoryFakeRefreshTokenRepository _refreshTokenRepository = null!;
    private FakeGoogleIdentityVerifier _googleVerifier = null!;
    private FakeTokenService _tokenService = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private FixedTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _userRepository = new InMemoryFakeUserRepository();
        _refreshTokenRepository = new InMemoryFakeRefreshTokenRepository();
        _googleVerifier = new FakeGoogleIdentityVerifier();
        _tokenService = new FakeTokenService();
        _unitOfWork = new FakeUnitOfWork();
        _timeProvider = new FixedTimeProvider(FixedNow);
    }

    [Test]
    public async Task LoginWithGoogle_WhenFirstTimeUserWithRole_ShouldCreateUserAndExternalIdentity()
    {
        _googleVerifier.SetVerifiedIdentity("valid-google-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-101",
            Email: "NewStudent@Gmail.com",
            EmailVerified: true));

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await useCase.ExecuteAsync(new LoginWithGoogleCommand("valid-google-token", "STUDENT"));

        var savedUser = await _userRepository.GetByExternalLoginAsync(UserExternalLogin.GoogleProvider, "google-sub-101");

        Assert.Multiple(() =>
        {
            Assert.That(result.Email, Is.EqualTo("newstudent@gmail.com"));
            Assert.That(result.Role, Is.EqualTo("STUDENT"));
            Assert.That(result.AccessToken, Is.EqualTo("access-token-1"));
            Assert.That(savedUser, Is.Not.Null);
            Assert.That(savedUser!.HasLocalPassword, Is.False);
            Assert.That(savedUser.ExternalLogins.Single().ProviderSubject, Is.EqualTo("google-sub-101"));
        });
    }

    [Test]
    public async Task LoginWithGoogle_WhenExistingGoogleUser_ShouldLoginBySubjectEvenWithoutRoleInRequest()
    {
        var existingUser = User.RegisterGoogleUser(
            EmailAddress.Create("landlord@gmail.com"),
            "google-sub-202",
            UserRole.Landlord,
            FixedNow);
        await _userRepository.AddAsync(existingUser);

        _googleVerifier.SetVerifiedIdentity("returning-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-202",
            Email: "landlord@gmail.com",
            EmailVerified: true));

        var loginTime = FixedNow.AddHours(2);
        _timeProvider.AdvanceTo(loginTime);

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        var result = await useCase.ExecuteAsync(new LoginWithGoogleCommand("returning-token", Role: null));

        Assert.Multiple(() =>
        {
            Assert.That(result.UserId, Is.EqualTo(existingUser.Id));
            Assert.That(result.Role, Is.EqualTo("LANDLORD"));
            Assert.That(existingUser.LastLoginAt, Is.EqualTo(loginTime));
        });
    }

    [Test]
    public async Task LoginWithGoogle_WhenEmailConflictsWithExistingLocalAccount_ShouldThrowExternalIdentityConflictException()
    {
        var localUser = User.RegisterLocalUser(
            EmailAddress.Create("conflict@example.com"),
            "local-hash",
            UserRole.Student,
            FixedNow);
        await _userRepository.AddAsync(localUser);

        _googleVerifier.SetVerifiedIdentity("conflict-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-303",
            Email: "Conflict@Example.com",
            EmailVerified: true));

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<ExternalIdentityConflictException>(async () =>
            await useCase.ExecuteAsync(new LoginWithGoogleCommand("conflict-token", "STUDENT")));
    }

    [Test]
    public void LoginWithGoogle_WhenFirstTimeUserAttemptsAdminRole_ShouldThrowAdminSelfRegistrationForbiddenException()
    {
        _googleVerifier.SetVerifiedIdentity("admin-attempt-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-404",
            Email: "attacker@gmail.com",
            EmailVerified: true));

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<AdminSelfRegistrationForbiddenException>(async () =>
            await useCase.ExecuteAsync(new LoginWithGoogleCommand("admin-attempt-token", "ADMIN")));
    }

    [Test]
    public void LoginWithGoogle_WhenFirstTimeUserOmitsRole_ShouldThrowRequestValidationException()
    {
        _googleVerifier.SetVerifiedIdentity("no-role-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-505",
            Email: "newuser@gmail.com",
            EmailVerified: true));

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<RequestValidationException>(async () =>
            await useCase.ExecuteAsync(new LoginWithGoogleCommand("no-role-token", null)));
    }

    [Test]
    public void LoginWithGoogle_WhenTokenInvalid_ShouldThrowInvalidGoogleTokenException()
    {
        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<InvalidGoogleTokenException>(async () =>
            await useCase.ExecuteAsync(new LoginWithGoogleCommand("forged-google-token", "STUDENT")));
    }

    [Test]
    public async Task LoginWithGoogle_WhenExistingGoogleUserIsInactive_ShouldThrowUserInactiveException()
    {
        var inactiveGoogleUser = User.RegisterGoogleUser(
            EmailAddress.Create("inactive@gmail.com"),
            "google-sub-606",
            UserRole.Student,
            FixedNow);
        inactiveGoogleUser.Deactivate(FixedNow.AddMinutes(5));
        await _userRepository.AddAsync(inactiveGoogleUser);

        _googleVerifier.SetVerifiedIdentity("inactive-user-token", new VerifiedGoogleIdentity(
            Subject: "google-sub-606",
            Email: "inactive@gmail.com",
            EmailVerified: true));

        var useCase = new LoginWithGoogleUseCase(
            _userRepository,
            _refreshTokenRepository,
            _googleVerifier,
            _tokenService,
            _unitOfWork,
            _timeProvider);

        Assert.ThrowsAsync<UserInactiveException>(async () =>
            await useCase.ExecuteAsync(new LoginWithGoogleCommand("inactive-user-token")));
    }
}

internal sealed class FakeGoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly Dictionary<string, VerifiedGoogleIdentity> _tokens = new(StringComparer.Ordinal);

    public void SetVerifiedIdentity(string idToken, VerifiedGoogleIdentity identity)
    {
        _tokens[idToken] = identity;
    }

    public Task<VerifiedGoogleIdentity> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(idToken, out var identity))
        {
            return Task.FromResult(identity);
        }

        throw new InvalidGoogleTokenException("Invalid Google ID token.");
    }
}

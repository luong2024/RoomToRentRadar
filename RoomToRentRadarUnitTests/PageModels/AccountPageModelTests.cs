using System.Net;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class AccountPageModelTests
{
    private IAuthGateway _authGateway = null!;
    private ISessionStore _sessionStore = null!;

    [SetUp]
    public void SetUp()
    {
        _authGateway = Substitute.For<IAuthGateway>();
        _sessionStore = Substitute.For<ISessionStore>();
    }

    [TearDown]
    public void TearDown()
    {
        // Reset culture to en-US after each test to ensure test isolation
        RoomToRentRadar.Resources.Strings.LocalizedStrings.Instance.SetCulture(new System.Globalization.CultureInfo("en-US"));
    }

    [Test]
    public async Task InitializeAsync_WhenCachedUserExists_AppliesProfileAndSetsNotGuest()
    {
        // Arrange
        var cachedUser = new UserProfileDto(
            Id: Guid.NewGuid().ToString(),
            Email: "student@ictu.edu.vn",
            FullName: "Lê Minh",
            Role: "Student",
            AuthProvider: "IctuSso",
            PhoneNumber: null,
            StudentCode: "DTC123456",
            IsVerified: true,
            CreatedAtUtc: DateTime.UtcNow,
            SelectedCampusId: "ictu",
            PreferredLanguage: "en-US");

        _sessionStore.GetCachedUserProfileAsync().Returns(cachedUser);
        _sessionStore.GetAccessTokenAsync().Returns((string?)null);

        var model = new AccountPageModel(_authGateway, _sessionStore);

        // Act
        await model.InitializeAsync();

        // Assert
        Assert.That(model.IsGuest, Is.False);
        Assert.That(model.DisplayName, Is.EqualTo("Lê Minh"));
        Assert.That(model.Email, Is.EqualTo("student@ictu.edu.vn"));
        Assert.That(model.Initials, Is.EqualTo("LM"));
        Assert.That(model.RoleBadgeText, Is.EqualTo("Student · signed in with ICTU"));
    }

    [Test]
    public async Task InitializeAsync_WhenNoCachedUser_KeepsGuestState()
    {
        // Arrange
        _sessionStore.GetCachedUserProfileAsync().Returns((UserProfileDto?)null);
        _sessionStore.GetAccessTokenAsync().Returns((string?)null);

        var model = new AccountPageModel(_authGateway, _sessionStore);

        // Act
        await model.InitializeAsync();

        // Assert
        Assert.That(model.IsGuest, Is.True);
        Assert.That(model.RoleBadgeText, Is.EqualTo("Guest · not signed in"));
    }

    [Test]
    public async Task InitializeAsync_WhenBackendReturnsProfile_RefreshesProfileData()
    {
        // Arrange
        var cachedUser = new UserProfileDto(
            Id: Guid.NewGuid().ToString(),
            Email: "landlord@gmail.com",
            FullName: "Nguyễn Văn Tuân",
            Role: "Landlord",
            AuthProvider: "EmailPassword",
            PhoneNumber: "0912345678",
            StudentCode: null,
            IsVerified: false,
            CreatedAtUtc: DateTime.UtcNow,
            SelectedCampusId: "ictu",
            PreferredLanguage: "vi-VN");

        var updatedUser = cachedUser with { IsVerified = true };

        _sessionStore.GetCachedUserProfileAsync().Returns(cachedUser);
        _sessionStore.GetAccessTokenAsync().Returns("valid-jwt-token");
        _authGateway.GetCurrentUserProfileAsync("valid-jwt-token").Returns(updatedUser);

        var model = new AccountPageModel(_authGateway, _sessionStore);

        // Act
        await model.InitializeAsync();

        // Assert
        Assert.That(model.IsGuest, Is.False);
        Assert.That(model.RoleBadgeText, Is.EqualTo("Landlord · verified"));
    }

    [Test]
    public async Task SignOutAsync_WhenCalled_RevokesTokenOnBackendAndClearsSessionAndNavigatesToExplore()
    {
        // T00.15 AC-4: sign-out returns to the guest tab set //explore (previously //login).
        // Arrange
        _sessionStore.GetRefreshTokenAsync().Returns("sample-refresh-token");
        _sessionStore.GetAccessTokenAsync().Returns("sample-access-token");

        string? navigatedRoute = null;
        var model = new AccountPageModel(_authGateway, _sessionStore)
        {
            NavigateOverride = route =>
            {
                navigatedRoute = route;
                return Task.CompletedTask;
            }
        };

        // Act
        await model.SignOutCommand.ExecuteAsync(null);

        // Assert
        await _authGateway.Received(1).RevokeTokenAsync(
            Arg.Is<RevokeTokenRequest>(r => r.RefreshToken == "sample-refresh-token"),
            "sample-access-token");

        await _sessionStore.Received(1).ClearSessionAsync();
        Assert.That(navigatedRoute, Is.EqualTo("//explore"));
        Assert.That(model.IsGuest, Is.True);
        Assert.That(model.DisplayName, Is.Empty);
    }

    [Test]
    public async Task SignOutAsync_WhenBackendThrowsException_StillClearsSessionAndNavigatesToExplore()
    {
        // Arrange
        _sessionStore.GetRefreshTokenAsync().Returns("sample-refresh-token");
        _sessionStore.GetAccessTokenAsync().Returns("sample-access-token");

        _authGateway.RevokeTokenAsync(Arg.Any<RevokeTokenRequest>(), Arg.Any<string>())
            .ThrowsAsync(new ApiException("Server error", HttpStatusCode.InternalServerError, "INTERNAL_ERROR"));

        string? navigatedRoute = null;
        var model = new AccountPageModel(_authGateway, _sessionStore)
        {
            NavigateOverride = route =>
            {
                navigatedRoute = route;
                return Task.CompletedTask;
            }
        };

        // Act
        await model.SignOutCommand.ExecuteAsync(null);

        // Assert
        await _sessionStore.Received(1).ClearSessionAsync();
        Assert.That(navigatedRoute, Is.EqualTo("//explore"));
        Assert.That(model.IsGuest, Is.True);
    }

    [Test]
    public async Task SignOutAsync_WithNavigationService_CallsSignOutToGuest()
    {
        // Arrange
        _sessionStore.GetRefreshTokenAsync().Returns("sample-refresh-token");
        _sessionStore.GetAccessTokenAsync().Returns("sample-access-token");
        var navigation = Substitute.For<INavigationService>();

        var model = new AccountPageModel(_authGateway, _sessionStore, navigation);

        // Act
        await model.SignOutCommand.ExecuteAsync(null);

        // Assert
        await navigation.Received(1).SignOutToGuestAsync();
        await _sessionStore.Received(1).ClearSessionAsync();
        Assert.That(model.IsGuest, Is.True);
    }

    [Test]
    public async Task Account_NoNavigationService_StillUsesNavigateOverride()
    {
        // Arrange
        string? navigatedRoute = null;
        var model = new AccountPageModel(_authGateway, _sessionStore)
        {
            NavigateOverride = route =>
            {
                navigatedRoute = route;
                return Task.CompletedTask;
            }
        };

        // Act
        await model.NavigateSavedSearchesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(navigatedRoute, Is.EqualTo("saved-searches"));
    }

    [Test]
    public async Task NavigateLanguageAsync_WhenVietnameseSelected_UpdatesLanguageNameAndBadge()
    {
        // Arrange
        var model = new AccountPageModel(_authGateway, _sessionStore)
        {
            DisplayActionSheetOverride = (title, cancel, destruction, buttons) =>
                Task.FromResult("Tiếng Việt (vi-VN)")
        };

        // Act
        await model.NavigateLanguageCommand.ExecuteAsync(null);

        // Assert
        Assert.That(model.LanguageName, Is.EqualTo("Tiếng Việt"));
        Assert.That(model.RoleBadgeText, Is.EqualTo("Khách · chưa đăng nhập"));
    }

    [Test]
    public void SwitchLanguage_WhenAuthenticatedLandlord_UpdatesRoleBadgeToVietnamese()
    {
        // Arrange
        var user = new UserProfileDto(
            Id: Guid.NewGuid().ToString(),
            Email: "landlord@gmail.com",
            FullName: "Nguyễn Văn Tuân",
            Role: "Landlord",
            AuthProvider: "EmailPassword",
            PhoneNumber: "0912345678",
            StudentCode: null,
            IsVerified: true,
            CreatedAtUtc: DateTime.UtcNow,
            SelectedCampusId: "ictu",
            PreferredLanguage: "en-US");

        var model = new AccountPageModel(_authGateway, _sessionStore);
        model.ApplyUserProfile(user);

        // Act
        model.SwitchLanguage("vi-VN");

        // Assert
        Assert.That(model.LanguageName, Is.EqualTo("Tiếng Việt"));
        Assert.That(model.RoleBadgeText, Is.EqualTo("Chủ trọ · đã xác minh"));
    }

    // ----- T13.4: campus row (SCR-ACC-02, BR-CMP-02) -----

    private static ICampusSelectionService CampusService(string id)
    {
        var service = Substitute.For<ICampusSelectionService>();
        service.SelectedCampusId.Returns(id);
        return service;
    }

    [Test]
    public async Task NavigateCampus_NavigatesToAccountCampusRoute_NoAlert()
    {
        string? navigatedRoute = null;
        var alerts = 0;
        var model = new AccountPageModel(_authGateway, _sessionStore)
        {
            NavigateOverride = route =>
            {
                navigatedRoute = route;
                return Task.CompletedTask;
            },
            DisplayAlertOverride = (_, _, _) =>
            {
                alerts++;
                return Task.CompletedTask;
            }
        };

        await model.NavigateCampusCommand.ExecuteAsync(null);

        Assert.That(navigatedRoute, Is.EqualTo(AppRoutes.AccountCampus));
        Assert.That(alerts, Is.Zero);
    }

    [Test]
    public void CampusName_ShowsSelectedShortName()
    {
        var service = CampusService("TNUT");
        service.SelectedCampus.Returns(new CampusDto("TNUT", "Name", "TNUT-short", 21.5, 105.8, false));

        var model = new AccountPageModel(_authGateway, _sessionStore, campusSelection: service);

        Assert.That(model.CampusName, Is.EqualTo("TNUT-short"));
    }

    [Test]
    public void CampusName_UpdatesWhenSelectionChanged()
    {
        var service = CampusService("ICTU");
        var model = new AccountPageModel(_authGateway, _sessionStore, campusSelection: service);

        service.SelectedCampusId.Returns("TUMP");
        service.SelectedCampusChanged += Raise.Event();

        Assert.That(model.CampusName, Is.EqualTo("TUMP"));
        GC.KeepAlive(model);
    }

    [Test]
    public async Task Initialize_SynchronizesCampusAndLoadsNames()
    {
        var service = CampusService("ICTU");
        service.SynchronizeAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            service.SelectedCampusId.Returns("TNUE");
            return Task.CompletedTask;
        });
        var model = new AccountPageModel(_authGateway, _sessionStore, campusSelection: service);

        await model.InitializeAsync();

        await service.Received(1).SynchronizeAsync(Arg.Any<CancellationToken>());
        await service.Received(1).GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.That(model.CampusName, Is.EqualTo("TNUE"));
    }

    [Test]
    public async Task Initialize_CampusLoadThrows_IsSwallowed()
    {
        var service = CampusService("TNUT");
        service.GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException("offline", 0));
        var model = new AccountPageModel(_authGateway, _sessionStore, campusSelection: service);

        await model.InitializeAsync();

        Assert.That(model.CampusName, Is.EqualTo("TNUT"));
    }

    [Test]
    public async Task NoCampusService_StillWorks()
    {
        var model = new AccountPageModel(_authGateway, _sessionStore);

        await model.InitializeAsync();

        Assert.That(model.CampusName, Is.EqualTo("ICTU"));
    }
}

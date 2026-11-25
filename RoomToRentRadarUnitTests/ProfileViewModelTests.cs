using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Profile;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ProfileViewModelTests
{
    private static readonly Guid UserId = Guid.Parse("0c7a1f42-3b5e-4a8d-9c61-7d2f90a1b2c3");
    private static readonly DateTime CreatedAt = new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_StartsInLoadingState()
    {
        var viewModel = CreateViewModel(new FakeProfileClient());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Loading));
            Assert.That(viewModel.IsLoadingState, Is.True);
            Assert.That(viewModel.CanEditProfile, Is.False);
            Assert.That(viewModel.MenuItems, Is.Empty);
        });
    }

    [Test]
    public async Task Load_WhenSignedOut_ShowsSignedOutWithoutCallingClient()
    {
        var client = new FakeProfileClient();
        var viewModel = CreateViewModel(client, new FakeTokenStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.SignedOut));
            Assert.That(viewModel.IsSignedOutState, Is.True);
            Assert.That(client.GetCalls, Is.Zero);
        });
    }

    [Test]
    public async Task Load_WhenStudent_ShowsRoleAdaptiveProfileAndMenu()
    {
        var client = new FakeProfileClient
        {
            ProfileResult = Profile("Minh Anh", "student", "minh@example.test")
        };
        var store = new InMemoryProfileDetailsStore();
        var authService = CreateAuthService(SignedInAs(AppUserRole.Student));
        var viewModel = CreateViewModel(client, authService: authService, store: store);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsContentState, Is.True);
            Assert.That(viewModel.DisplayName, Is.EqualTo("Minh Anh"));
            Assert.That(viewModel.Email, Is.EqualTo("minh@example.test"));
            Assert.That(viewModel.RoleText, Is.EqualTo("Sinh viên"));
            Assert.That(viewModel.Initials, Is.EqualTo("MA"));
            Assert.That(viewModel.MemberSinceText, Is.EqualTo($"Thành viên từ {CreatedAt.ToLocalTime():dd/MM/yyyy}"));
            Assert.That(viewModel.IsStudent, Is.True);
            Assert.That(viewModel.IsLandlord, Is.False);
            Assert.That(viewModel.IsStaff, Is.False);
            Assert.That(viewModel.CanEditProfile, Is.True);
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Họ và tên"), Is.EqualTo("Minh Anh"));
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Số điện thoại"), Is.EqualTo("0901 234 567"));
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Trường đại học"), Is.EqualTo("Đại học Bách khoa TP.HCM"));
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Email"), Is.EqualTo("minh@example.test"));
            Assert.That(viewModel.RentalPreferenceRows, Has.Count.EqualTo(9));
            Assert.That(ValueOf(viewModel.RentalPreferenceRows, "Ngân sách"), Is.EqualTo("3 – 5 triệu/tháng"));
            Assert.That(viewModel.BusinessInfoRows, Is.Empty);
            Assert.That(viewModel.VerificationItems, Is.Empty);
            Assert.That(viewModel.MenuSectionTitle, Is.EqualTo("Tiện ích của bạn"));
            Assert.That(viewModel.MenuItems, Has.Count.EqualTo(6));
            Assert.That(viewModel.MenuItems[0].AutomationId, Is.EqualTo("ProfileMenuFavourites"));
            Assert.That(viewModel.MenuItems[0].IsComingSoon, Is.False);
            Assert.That(viewModel.MenuItems.Count(item => item.IsComingSoon), Is.EqualTo(5));
            Assert.That(store.GetStudentDetails(UserId, string.Empty).FullName, Is.EqualTo("Minh Anh"));
            Assert.That(authService.CurrentDisplayName, Is.EqualTo("Minh Anh"));
        });
    }

    [Test]
    public async Task Load_WhenLandlord_ShowsStatsVerificationAndLandlordMenu()
    {
        var client = new FakeProfileClient
        {
            ProfileResult = Profile("Nhà trọ Bình An", "landlord", "landlord@example.test")
        };
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Landlord)),
            store: new InMemoryProfileDetailsStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsLandlord, Is.True);
            Assert.That(viewModel.IsStudent, Is.False);
            Assert.That(viewModel.RoleText, Is.EqualTo("Chủ phòng"));
            Assert.That(ValueOf(viewModel.BusinessInfoRows, "Tên cơ sở"), Is.EqualTo("Nhà trọ Bình An"));
            Assert.That(ValueOf(viewModel.BusinessInfoRows, "Địa chỉ liên hệ"), Is.EqualTo("12 Hòa Hảo, Quận 10, TP.HCM"));
            Assert.That(viewModel.VerificationItems, Has.Count.EqualTo(4));
            Assert.That(viewModel.VerificationItems[0].IsVerified, Is.True);
            Assert.That(viewModel.VerificationItems[2].IsPending, Is.True);
            Assert.That(viewModel.VerificationItems[3].IsUnverified, Is.True);
            Assert.That(viewModel.TotalRooms, Is.EqualTo(6));
            Assert.That(viewModel.AvailableRooms, Is.EqualTo(3));
            Assert.That(viewModel.OccupiedRooms, Is.EqualTo(2));
            Assert.That(viewModel.PendingRooms, Is.EqualTo(1));
            Assert.That(viewModel.AverageRatingText,
                Is.EqualTo(4.6.ToString("0.0", CultureInfo.GetCultureInfo("vi-VN"))));
            Assert.That(viewModel.TotalReviewsText, Is.EqualTo("28 đánh giá"));
            Assert.That(ValueOf(viewModel.ReputationRows, "Tỷ lệ phản hồi"), Is.EqualTo("92%"));
            Assert.That(viewModel.MenuSectionTitle, Is.EqualTo("Quản lý cho thuê"));
            Assert.That(viewModel.MenuItems[0].AutomationId, Is.EqualTo("ProfileMenuMyListings"));
            Assert.That(viewModel.MenuItems[0].IsComingSoon, Is.False);
            Assert.That(viewModel.MenuItems.Any(item => item.AutomationId == "ProfileMenuFavourites"), Is.False);
        });
    }

    [Test]
    public async Task Load_WhenModerator_ShowsStaffWithoutEditableProfile()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient { ProfileResult = Profile("Kiểm Duyệt Viên", "moderator") },
            authService: CreateAuthService(SignedInAs(AppUserRole.Moderator)));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsStaff, Is.True);
            Assert.That(viewModel.IsStudent, Is.False);
            Assert.That(viewModel.IsLandlord, Is.False);
            Assert.That(viewModel.CanEditProfile, Is.False);
            Assert.That(viewModel.RoleText, Is.EqualTo("Kiểm duyệt viên"));
            Assert.That(viewModel.PersonalInfoRows, Is.Empty);
            Assert.That(viewModel.BusinessInfoRows, Is.Empty);
            Assert.That(viewModel.MenuItems, Is.Empty);
        });
    }

    [Test]
    public async Task Load_WhenStoreHasSavedDetails_ShowsSavedValues()
    {
        var store = new InMemoryProfileDetailsStore();
        store.SaveStudentDetails(UserId, new StudentProfileDetails
        {
            FullName = "Minh Anh",
            University = "Đại học Quốc gia TP.HCM",
            Phone = "0912345678"
        });
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: store);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Trường đại học"), Is.EqualTo("Đại học Quốc gia TP.HCM"));
            Assert.That(ValueOf(viewModel.PersonalInfoRows, "Số điện thoại"), Is.EqualTo("0912345678"));
        });
    }

    [Test]
    public async Task Load_WhenApiReturnsUnauthorized_ShowsSignedOut()
    {
        var client = new FakeProfileClient
        {
            GetException = new ApiException(HttpStatusCode.Unauthorized, "Sign in required.", "unauthorized")
        };
        var viewModel = CreateViewModel(client, authService: CreateAuthService(SignedInAs(AppUserRole.Student)));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.SignedOut));
            Assert.That(viewModel.DisplayName, Is.Empty);
            Assert.That(viewModel.CanEditProfile, Is.False);
        });
    }

    [Test]
    public async Task Load_WhenApiFails_ShowsErrorAndReportsIt()
    {
        var client = new FakeProfileClient { GetException = new HttpRequestException("API offline") };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            errorHandler: errorHandler);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Error));
            Assert.That(viewModel.IsErrorState, Is.True);
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task EditProfile_NavigatesToEditProfilePage()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            navigation: navigation);

        await viewModel.EditProfileCommand.ExecuteAsync(null);

        Assert.That(navigation.EditProfileCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task OpenSettings_NavigatesToAccountSettingsPage()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            navigation: navigation);

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.That(navigation.AccountSettingsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task MenuFavourites_NavigatesToFavouritesTab()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        var favourites = viewModel.MenuItems.Single(item => item.AutomationId == "ProfileMenuFavourites");
        await ((IAsyncRelayCommand)favourites.Command).ExecuteAsync(null);

        Assert.That(navigation.FavouritesCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task MenuMyListings_NavigatesToMyListingsTab()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient { ProfileResult = Profile("Chủ Trọ", "landlord") },
            authService: CreateAuthService(SignedInAs(AppUserRole.Landlord)),
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        var myListings = viewModel.MenuItems.Single(item => item.AutomationId == "ProfileMenuMyListings");
        await ((IAsyncRelayCommand)myListings.Command).ExecuteAsync(null);

        Assert.That(navigation.MyListingsCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task MenuComingSoon_ShowsComingSoonAlertAndDoesNotNavigate()
    {
        var prompt = new FakeAlertPrompt();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            prompt: prompt,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        var comingSoon = viewModel.MenuItems.Single(item => item.AutomationId == "ProfileMenuSearchHistory");
        await ((IAsyncRelayCommand)comingSoon.Command).ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Shown, Has.Count.EqualTo(1));
            Assert.That(prompt.Shown[0].Title, Is.EqualTo("Sắp ra mắt"));
            Assert.That(prompt.Shown[0].Message, Does.Contain("Lịch sử tìm kiếm"));
            Assert.That(navigation.FavouritesCalls, Is.Zero);
            Assert.That(navigation.MyListingsCalls, Is.Zero);
        });
    }

    [Test]
    public async Task Notifications_ShowsComingSoonAlert()
    {
        var prompt = new FakeAlertPrompt();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            prompt: prompt);

        await viewModel.ShowNotificationsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Shown, Has.Count.EqualTo(1));
            Assert.That(prompt.Shown[0].Title, Is.EqualTo("Sắp ra mắt"));
        });
    }

    [Test]
    public async Task SignOut_WhenConfirmed_ClearsSessionAndNavigatesHome()
    {
        var tokenStore = SignedInAs(AppUserRole.Landlord);
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient { ProfileResult = Profile("Chủ Trọ", "landlord") },
            tokenStore,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.SignedOut));
            Assert.That(viewModel.DisplayName, Is.Empty);
            Assert.That(viewModel.Email, Is.Empty);
            Assert.That(viewModel.MenuItems, Is.Empty);
            Assert.That(navigation.RoleRootRoles, Is.EqualTo(new AppUserRole?[] { null }));
        });
    }

    [Test]
    public async Task SignOut_WhenCancelled_KeepsProfile()
    {
        var tokenStore = SignedInAs(AppUserRole.Student);
        var prompt = new FakeAlertPrompt { ConfirmResult = false };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            tokenStore,
            prompt: prompt,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.ConfirmCalls, Is.EqualTo(1));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("test-token"));
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.DisplayName, Is.EqualTo("Minh Anh"));
            Assert.That(navigation.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task SignIn_NavigatesToLogin()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeProfileClient(), new FakeTokenStore(), navigation: navigation);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigation.GoToLoginCount, Is.EqualTo(1));
    }

    private static string ValueOf(IReadOnlyList<ProfileInfoRow> rows, string label)
        => rows.Single(row => row.Label == label).Value;

    private static ProfileViewModel CreateViewModel(
        FakeProfileClient client,
        FakeTokenStore? tokenStore = null,
        FakeAlertPrompt? prompt = null,
        FakeNavigationService? navigation = null,
        FakeErrorHandler? errorHandler = null,
        AuthService? authService = null,
        InMemoryProfileDetailsStore? store = null)
        => new(
            client,
            authService ?? CreateAuthService(tokenStore ?? new FakeTokenStore()),
            navigation ?? new FakeNavigationService(),
            prompt ?? new FakeAlertPrompt(),
            errorHandler ?? new FakeErrorHandler(),
            store ?? new InMemoryProfileDetailsStore());

    private static AuthService CreateAuthService(FakeTokenStore tokenStore)
        => new(null!, tokenStore);

    private static FakeTokenStore SignedInAs(AppUserRole role)
        => new() { AccessToken = "test-token", Role = role };

    private static UserProfileDto Profile(
        string displayName = "Minh Anh",
        string role = "student",
        string email = "minh@example.test")
        => new(UserId, email, displayName, role, CreatedAt);

    private sealed class FakeProfileClient : IProfileClient
    {
        public UserProfileDto ProfileResult { get; set; } = Profile();

        public Exception? GetException { get; set; }

        public Exception? UpdateException { get; set; }

        public int GetCalls { get; private set; }

        public List<string> UpdateRequests { get; } = [];

        public Task<UserProfileDto> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            GetCalls++;

            return GetException is { } exception
                ? Task.FromException<UserProfileDto>(exception)
                : Task.FromResult(ProfileResult);
        }

        public Task<UserProfileDto> UpdateDisplayNameAsync(
            string displayName,
            CancellationToken cancellationToken = default)
        {
            UpdateRequests.Add(displayName);

            if (UpdateException is { } exception)
            {
                return Task.FromException<UserProfileDto>(exception);
            }

            ProfileResult = ProfileResult with { DisplayName = displayName };
            return Task.FromResult(ProfileResult);
        }
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> Errors { get; } = [];

        public void HandleError(Exception ex) => Errors.Add(ex);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; set; }

        public AppUserRole? Role { get; set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AccessToken);

        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Role);

        public Task SaveAsync(
            string accessToken,
            DateTime expiresAt,
            AppUserRole role,
            CancellationToken cancellationToken = default)
        {
            AccessToken = accessToken;
            Role = role;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            AccessToken = null;
            Role = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAlertPrompt : IAlertPrompt
    {
        public bool ConfirmResult { get; set; } = true;

        public int ConfirmCalls { get; private set; }

        public List<(string Title, string Message)> Shown { get; } = [];

        public Task ShowAsync(
            string title,
            string message,
            string dismissText = "OK",
            CancellationToken cancellationToken = default)
        {
            Shown.Add((title, message));
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string acceptText,
            string cancelText,
            CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            return Task.FromResult(ConfirmResult);
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public int GoToLoginCount { get; private set; }

        public int EditProfileCalls { get; private set; }

        public int AccountSettingsCalls { get; private set; }

        public int FavouritesCalls { get; private set; }

        public int MyListingsCalls { get; private set; }

        public List<AppUserRole?> RoleRootRoles { get; } = [];

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToEditProfileAsync()
        {
            EditProfileCalls++;
            return Task.CompletedTask;
        }

        public Task GoToAccountSettingsAsync()
        {
            AccountSettingsCalls++;
            return Task.CompletedTask;
        }

        public Task GoToFavouritesAsync()
        {
            FavouritesCalls++;
            return Task.CompletedTask;
        }

        public Task GoToMyListingsAsync()
        {
            MyListingsCalls++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The profile screen does not open the listing editor.");

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The profile screen does not open room details.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The profile screen does not open the registration screen.");

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The profile screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The profile screen does not open room comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The profile screen does not navigate back.");
    }
}

using System.Net;
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
public class AccountSettingsViewModelTests
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
            Assert.That(viewModel.NotificationsEnabled, Is.False);
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
            Assert.That(client.GetCalls, Is.Zero);
        });
    }

    [Test]
    public async Task Load_WhenSignedIn_ShowsEmailAndDefaultSettings()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: new InMemoryProfileDetailsStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsContentState, Is.True);
            Assert.That(viewModel.Email, Is.EqualTo("minh@example.test"));
            Assert.That(viewModel.NotificationsEnabled, Is.True);
            Assert.That(viewModel.NewListingsEnabled, Is.True);
            Assert.That(viewModel.ShowPhoneEnabled, Is.False);
            Assert.That(viewModel.AllowMessagesEnabled, Is.True);
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

        Assert.That(viewModel.State, Is.EqualTo(ProfileState.SignedOut));
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
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ToggleSettings_PersistsEachChangeToStore()
    {
        var store = new InMemoryProfileDetailsStore();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: store);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.NotificationsEnabled = false;
        viewModel.NewListingsEnabled = false;
        viewModel.ShowPhoneEnabled = true;
        viewModel.AllowMessagesEnabled = false;

        var saved = store.GetSettings(UserId);
        Assert.Multiple(() =>
        {
            Assert.That(saved.NotificationsEnabled, Is.False);
            Assert.That(saved.NewListingsEnabled, Is.False);
            Assert.That(saved.ShowPhoneEnabled, Is.True);
            Assert.That(saved.AllowMessagesEnabled, Is.False);
        });
    }

    [Test]
    public async Task Load_WhenStoreHasSavedSettings_UsesStoredValues()
    {
        var store = new InMemoryProfileDetailsStore();
        store.SaveSettings(UserId, new UserSettings
        {
            NotificationsEnabled = false,
            NewListingsEnabled = false,
            ShowPhoneEnabled = true,
            AllowMessagesEnabled = false
        });
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Landlord)),
            store: store);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.NotificationsEnabled, Is.False);
            Assert.That(viewModel.NewListingsEnabled, Is.False);
            Assert.That(viewModel.ShowPhoneEnabled, Is.True);
            Assert.That(viewModel.AllowMessagesEnabled, Is.False);
        });
    }

    [Test]
    public async Task SignOut_WhenConfirmed_ClearsSessionAndNavigatesHome()
    {
        var tokenStore = SignedInAs(AppUserRole.Landlord);
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            tokenStore,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.SignOutCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(tokenStore.AccessToken, Is.Null);
            Assert.That(navigation.RoleRootRoles, Is.EqualTo(new AppUserRole?[] { null }));
        });
    }

    [Test]
    public async Task SignOut_WhenCancelled_KeepsSession()
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
            Assert.That(navigation.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task PlaceholderActions_ShowComingSoonAlerts()
    {
        var prompt = new FakeAlertPrompt();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            prompt: prompt);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.ChangePasswordCommand.ExecuteAsync(null);
        await viewModel.AccountSecurityCommand.ExecuteAsync(null);
        await viewModel.HelpCenterCommand.ExecuteAsync(null);
        await viewModel.ReportProblemCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Shown, Has.Count.EqualTo(4));
            Assert.That(
                prompt.Shown.Select(alert => alert.Title),
                Is.EqualTo(new[] { "Đổi mật khẩu", "Bảo mật tài khoản", "Trung tâm trợ giúp", "Báo cáo sự cố" }));
            Assert.That(
                prompt.Shown.All(alert => alert.Message.EndsWith("sẽ có trong bản cập nhật tới.", StringComparison.Ordinal)),
                Is.True);
        });
    }

    [Test]
    public async Task DeleteAccount_ShowsNoticeWithoutClearingSession()
    {
        var tokenStore = SignedInAs(AppUserRole.Student);
        var prompt = new FakeAlertPrompt();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            tokenStore,
            prompt: prompt,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.DeleteAccountCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Shown, Has.Count.EqualTo(1));
            Assert.That(prompt.Shown[0].Title, Is.EqualTo("Chưa hỗ trợ"));
            Assert.That(prompt.Shown[0].Message, Does.Contain("vẫn được giữ nguyên"));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("test-token"));
            Assert.That(navigation.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task GoBack_NavigatesBack()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeProfileClient(), navigation: navigation);

        await viewModel.GoBackCommand.ExecuteAsync(null);

        Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task SignIn_NavigatesToLogin()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeProfileClient(), navigation: navigation);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigation.GoToLoginCount, Is.EqualTo(1));
    }

    private static AccountSettingsViewModel CreateViewModel(
        FakeProfileClient client,
        FakeTokenStore? tokenStore = null,
        AuthService? authService = null,
        InMemoryProfileDetailsStore? store = null,
        FakeAlertPrompt? prompt = null,
        FakeNavigationService? navigation = null,
        FakeErrorHandler? errorHandler = null)
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

        public int GetCalls { get; private set; }

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
            => throw new NotSupportedException("The settings screen does not update the display name.");
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

        public int GoBackCalls { get; private set; }

        public List<AppUserRole?> RoleRootRoles { get; } = [];

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCalls++;
            return Task.CompletedTask;
        }

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The settings screen does not open room details.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The settings screen does not open registration.");

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The settings screen does not open the listing editor.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The settings screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The settings screen does not open comparison.");
    }
}

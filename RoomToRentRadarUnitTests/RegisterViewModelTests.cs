using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.Pagination;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RegisterViewModelTests
{
    [Test]
    public async Task Register_WhenFormValid_SavesSessionAndNavigatesToRoleRoot()
    {
        var apiClient = new FakeApiClient
        {
            RegisterResult = new RegisterResponse(
                Guid.NewGuid(),
                "landlord@example.test",
                "New Landlord",
                AppUserRoleWire.Landlord),
            LoginResult = new LoginResponse(
                "register-token",
                DateTime.UtcNow.AddDays(7),
                "New Landlord",
                AppUserRoleWire.Landlord)
        };
        var tokenStore = new FakeTokenStore();
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, tokenStore, navigationService);
        viewModel.DisplayName = "  New Landlord  ";
        viewModel.Email = "  landlord@example.test  ";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";
        viewModel.SelectLandlordCommand.Execute(null);

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(viewModel.Password, Is.Empty);
            Assert.That(viewModel.ConfirmPassword, Is.Empty);
            Assert.That(apiClient.Registrations, Has.Count.EqualTo(1));
            Assert.That(apiClient.Registrations[0].DisplayName, Is.EqualTo("New Landlord"));
            Assert.That(apiClient.Registrations[0].Email, Is.EqualTo("landlord@example.test"));
            Assert.That(apiClient.Registrations[0].Password, Is.EqualTo("Password-123"));
            Assert.That(apiClient.Registrations[0].Role, Is.EqualTo(AppUserRoleWire.Landlord));
            Assert.That(tokenStore.AccessToken, Is.EqualTo("register-token"));
            Assert.That(tokenStore.Role, Is.EqualTo(AppUserRole.Landlord));
            Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { AppUserRole.Student }));
        });
    }

    [Test]
    public async Task Register_AsStudent_DefaultsToTheStudentRole()
    {
        var apiClient = new FakeApiClient
        {
            RegisterResult = new RegisterResponse(
                Guid.NewGuid(),
                "student@example.test",
                "New Student",
                AppUserRoleWire.Student),
            LoginResult = new LoginResponse(
                "register-token",
                DateTime.UtcNow.AddDays(7),
                "New Student",
                AppUserRoleWire.Student)
        };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedRole, Is.EqualTo(AppUserRole.Student));
            Assert.That(apiClient.Registrations[0].Role, Is.EqualTo(AppUserRoleWire.Student));
            Assert.That(navigationService.RoleRootRoles, Is.EqualTo(new[] { AppUserRole.Student }));
        });
    }

    [Test]
    public async Task Register_WhenFormInvalid_ShowsValidationErrorWithoutCallingApi()
    {
        var apiClient = new FakeApiClient();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("họ tên"));
            Assert.That(apiClient.Registrations, Is.Empty);
        });
    }

    [Test]
    public async Task Register_WhenPasswordsMismatch_ShowsMismatchError()
    {
        var apiClient = new FakeApiClient();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-124";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Mật khẩu nhập lại không khớp."));
            Assert.That(apiClient.Registrations, Is.Empty);
        });
    }

    [Test]
    public async Task Register_WhenEmailAlreadyRegistered_ShowsDuplicateEmailMessage()
    {
        var apiClient = new FakeApiClient
        {
            RegisterException = new ApiException(
                HttpStatusCode.Conflict,
                "Email already registered.",
                "email_already_registered")
        };
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), navigationService);
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(
                viewModel.ErrorMessage,
                Is.EqualTo("Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác."));
            Assert.That(
                viewModel.EmailError,
                Is.EqualTo("Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác."));
            Assert.That(viewModel.HasEmailError, Is.True);
            Assert.That(navigationService.RoleRootRoles, Is.Empty);
        });
    }

    [Test]
    public async Task Register_WhenMultipleFieldsInvalid_PopulatesInlineFieldErrorsAndClearsOnEdit()
    {
        var apiClient = new FakeApiClient();
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.DisplayName = string.Empty;
        viewModel.Email = "invalid-email";
        viewModel.Password = "lettersonly";
        viewModel.ConfirmPassword = "other";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasDisplayNameError, Is.True);
            Assert.That(viewModel.HasEmailError, Is.True);
            Assert.That(viewModel.HasPasswordError, Is.True);
            Assert.That(viewModel.HasConfirmPasswordError, Is.True);
            Assert.That(apiClient.Registrations, Is.Empty);
        });

        viewModel.Email = "valid@example.test";
        viewModel.Password = "Room1234";

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasEmailError, Is.False);
            Assert.That(viewModel.HasPasswordError, Is.False);
            Assert.That(viewModel.HasMinPasswordLength, Is.True);
            Assert.That(viewModel.HasPasswordLetter, Is.True);
            Assert.That(viewModel.HasPasswordDigit, Is.True);
        });
    }

    [Test]
    public async Task Register_WhenRateLimited_ShowsFriendlyMessageWithoutErrorHandler()
    {
        var apiClient = new FakeApiClient
        {
            RegisterException = new ApiException(
                HttpStatusCode.TooManyRequests,
                "Too many requests.",
                "rate_limit_exceeded")
        };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            apiClient,
            new FakeTokenStore(),
            new FakeNavigationService(),
            errorHandler);
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasError, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain("quá nhanh"));
            Assert.That(errorHandler.HandledExceptions, Is.Empty);
        });
    }

    [Test]
    public async Task Register_WhenApiRejectsPayload_ShowsInvalidDataMessage()
    {
        var apiClient = new FakeApiClient
        {
            RegisterException = new ApiException(
                HttpStatusCode.BadRequest,
                "Invalid registration.",
                "invalid_registration_request")
        };
        var viewModel = CreateViewModel(apiClient, new FakeTokenStore(), new FakeNavigationService());
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.That(
            viewModel.ErrorMessage,
            Is.EqualTo("Thông tin đăng ký chưa hợp lệ. Kiểm tra lại họ tên, email và mật khẩu."));
    }

    [Test]
    public async Task Register_WhenApiThrows_ShowsConnectionErrorAndReportsException()
    {
        var exception = new HttpRequestException("network down");
        var apiClient = new FakeApiClient { RegisterException = exception };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            apiClient,
            new FakeTokenStore(),
            new FakeNavigationService(),
            errorHandler);
        viewModel.DisplayName = "New Student";
        viewModel.Email = "student@example.test";
        viewModel.Password = "Password-123";
        viewModel.ConfirmPassword = "Password-123";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Không thể đăng ký. Kiểm tra kết nối và thử lại."));
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(errorHandler.HandledExceptions[0], Is.SameAs(exception));
        });
    }

    [Test]
    public void SelectLandlord_UpdatesTheSelectedRole()
    {
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), new FakeNavigationService());

        viewModel.SelectLandlordCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedRole, Is.EqualTo(AppUserRole.Landlord));
            Assert.That(viewModel.IsLandlord, Is.True);
            Assert.That(viewModel.IsStudent, Is.False);
        });

        viewModel.SelectStudentCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedRole, Is.EqualTo(AppUserRole.Student));
            Assert.That(viewModel.IsStudent, Is.True);
            Assert.That(viewModel.IsLandlord, Is.False);
        });
    }

    [Test]
    public void ApplyQueryAttributes_WithLandlordRole_PreselectsLandlordAndIgnoresAdmin()
    {
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), new FakeNavigationService());

        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["role"] = AppUserRoleWire.Landlord });
        Assert.That(viewModel.SelectedRole, Is.EqualTo(AppUserRole.Landlord));

        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["role"] = AppUserRoleWire.Admin });
        Assert.That(viewModel.SelectedRole, Is.EqualTo(AppUserRole.Landlord));
    }

    [Test]
    public async Task Back_WhenInvoked_NavigatesBack()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(new FakeApiClient(), new FakeTokenStore(), navigationService);

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.That(navigationService.GoBackCount, Is.EqualTo(1));
    }

    private static RegisterViewModel CreateViewModel(
        FakeApiClient apiClient,
        FakeTokenStore tokenStore,
        FakeNavigationService navigationService,
        FakeErrorHandler? errorHandler = null)
        => new(
            new AuthService(apiClient, tokenStore),
            errorHandler ?? new FakeErrorHandler(),
            navigationService);

    private sealed class FakeApiClient : IApiClient
    {
        public List<RegisterRequest> Registrations { get; } = new();

        public RegisterResponse? RegisterResult { get; set; }

        public LoginResponse? LoginResult { get; set; }

        public Exception? RegisterException { get; set; }

        public Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        {
            Registrations.Add(request);

            return RegisterException is { } exception
                ? Task.FromException<RegisterResponse>(exception)
                : Task.FromResult(RegisterResult!);
        }

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not load listings.");

        public Task<ListingDetailFetchResult> GetListingDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not load listing details.");

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not load similar listings.");

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not load listing contacts.");

        public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(LoginResult);

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not use the demo login.");

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not sign out.");

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not search by area.");

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not save searches.");

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not change alerts.");

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not delete saved searches.");

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The registration screen does not load notifications.");
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public string? AccessToken { get; private set; }

        public AppUserRole? Role { get; private set; }

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

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public List<AppUserRole?> RoleRootRoles { get; } = new();

        public int GoBackCount { get; private set; }

        public Task GoToRoleRootAsync(AppUserRole? role)
        {
            RoleRootRoles.Add(role);
            return Task.CompletedTask;
        }

        public Task GoBackAsync()
        {
            GoBackCount++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The registration screen does not open the listing editor.");

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The registration screen does not open room details.");

        public Task GoToLoginAsync()
            => throw new NotSupportedException("The registration screen does not open the login screen.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The registration screen does not navigate to itself.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The registration screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The registration screen does not open room comparison.");
    }
}

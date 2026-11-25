using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Media;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Profile;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class EditProfileViewModelTests
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
            Assert.That(viewModel.HasAvatar, Is.False);
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
    public async Task Load_WhenStudent_PopulatesFieldsFromStore()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: new InMemoryProfileDetailsStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsStudent, Is.True);
            Assert.That(viewModel.IsLandlord, Is.False);
            Assert.That(viewModel.IsStaff, Is.False);
            Assert.That(viewModel.CanEditProfile, Is.True);
            Assert.That(viewModel.Email, Is.EqualTo("minh@example.test"));
            Assert.That(viewModel.FullName, Is.EqualTo("Minh Anh"));
            Assert.That(viewModel.Phone, Is.EqualTo("0901 234 567"));
            Assert.That(viewModel.DateOfBirth, Is.EqualTo(new DateTime(2003, 5, 12)));
            Assert.That(viewModel.Gender, Is.EqualTo("Nam"));
            Assert.That(viewModel.University, Is.EqualTo("Đại học Bách khoa TP.HCM"));
            Assert.That(viewModel.Major, Is.EqualTo("Khoa học máy tính"));
            Assert.That(viewModel.AcademicYear, Is.EqualTo("Năm 3"));
            Assert.That(viewModel.Budget, Is.EqualTo("3 – 5 triệu/tháng"));
            Assert.That(viewModel.PreferredLocation, Is.EqualTo("Quận 10, TP.HCM"));
            Assert.That(viewModel.MoveInDate.Date, Is.EqualTo(DateTime.Today.AddDays(30)));
            Assert.That(viewModel.HasPets, Is.False);
            Assert.That(viewModel.HasFullNameError, Is.False);
            Assert.That(viewModel.HasPhoneError, Is.False);
        });
    }

    [Test]
    public async Task Load_WhenLandlord_PopulatesBusinessFields()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient { ProfileResult = Profile("Chủ Trọ", "landlord") },
            authService: CreateAuthService(SignedInAs(AppUserRole.Landlord)),
            store: new InMemoryProfileDetailsStore());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsLandlord, Is.True);
            Assert.That(viewModel.FullName, Is.EqualTo("Chủ Trọ"));
            Assert.That(viewModel.Phone, Is.EqualTo("0987 654 321"));
            Assert.That(viewModel.BusinessName, Is.EqualTo("Nhà trọ Bình An"));
            Assert.That(viewModel.ContactAddress, Is.EqualTo("12 Hòa Hảo, Quận 10, TP.HCM"));
            Assert.That(viewModel.Introduction, Is.Not.Empty);
        });
    }

    [Test]
    public async Task Load_WhenModerator_ShowsStaffWithoutEditableProfile()
    {
        var client = new FakeProfileClient { ProfileResult = Profile("Kiểm Duyệt Viên", "moderator") };
        var prompt = new FakeAlertPrompt();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Moderator)),
            prompt: prompt);

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
            Assert.That(viewModel.IsStaff, Is.True);
            Assert.That(viewModel.CanEditProfile, Is.False);
            Assert.That(client.UpdateRequests, Is.Empty);
            Assert.That(prompt.Shown, Is.Empty);
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
            Assert.That(viewModel.IsErrorState, Is.True);
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Save_Student_UpdatesDisplayNameAndPersistsDetails()
    {
        var client = new FakeProfileClient();
        var store = new InMemoryProfileDetailsStore();
        var prompt = new FakeAlertPrompt();
        var navigation = new FakeNavigationService();
        var authService = CreateAuthService(SignedInAs(AppUserRole.Student));
        var viewModel = CreateViewModel(client, authService: authService, store: store, prompt: prompt, navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.FullName = "  Tên Mới  ";
        viewModel.Phone = "0912 345 678";
        viewModel.University = "  Đại học Quốc gia  ";
        viewModel.Introduction = "  Giới thiệu mới  ";
        viewModel.HasPets = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = store.GetStudentDetails(UserId, string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(client.UpdateRequests, Is.EqualTo(new[] { "Tên Mới" }));
            Assert.That(saved.FullName, Is.EqualTo("Tên Mới"));
            Assert.That(saved.Phone, Is.EqualTo("0912345678"));
            Assert.That(saved.University, Is.EqualTo("Đại học Quốc gia"));
            Assert.That(saved.Introduction, Is.EqualTo("Giới thiệu mới"));
            Assert.That(saved.HasPets, Is.True);
            Assert.That(saved.DateOfBirth, Is.EqualTo(new DateTime(2003, 5, 12)));
            Assert.That(authService.CurrentDisplayName, Is.EqualTo("Tên Mới"));
            Assert.That(prompt.Shown, Has.Count.EqualTo(1));
            Assert.That(prompt.Shown[0].Title, Is.EqualTo("Đã lưu thay đổi"));
            Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
            Assert.That(viewModel.IsSaving, Is.False);
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
        });
    }

    [Test]
    public async Task Save_WithUnchangedName_DoesNotCallClientButPersistsAndReturns()
    {
        var client = new FakeProfileClient();
        var store = new InMemoryProfileDetailsStore();
        var prompt = new FakeAlertPrompt();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: store,
            prompt: prompt,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.Phone = "0912345678";
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.UpdateRequests, Is.Empty);
            Assert.That(store.GetStudentDetails(UserId, string.Empty).Phone, Is.EqualTo("0912345678"));
            Assert.That(prompt.Shown, Has.Count.EqualTo(1));
            Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Save_WithInvalidFullName_ShowsFieldErrorWithoutCallingClient()
    {
        var client = new FakeProfileClient();
        var prompt = new FakeAlertPrompt();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            prompt: prompt);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FullName = "   ";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.FullNameError, Is.Not.Empty);
            Assert.That(viewModel.HasFullNameError, Is.True);
            Assert.That(client.UpdateRequests, Is.Empty);
            Assert.That(prompt.Shown, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(ProfileState.Content));
        });
    }

    [Test]
    public async Task Save_WithInvalidPhone_ShowsPhoneErrorWithoutCallingClient()
    {
        var client = new FakeProfileClient();
        var viewModel = CreateViewModel(client, authService: CreateAuthService(SignedInAs(AppUserRole.Student)));
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Phone = "12345";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PhoneError, Is.Not.Empty);
            Assert.That(viewModel.HasPhoneError, Is.True);
            Assert.That(client.UpdateRequests, Is.Empty);
        });
    }

    [Test]
    public async Task Save_WithFutureBirthDate_ShowsBirthDateErrorForStudent()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)));
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.DateOfBirth = DateTime.Today.AddDays(1);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DateOfBirthError, Is.Not.Empty);
            Assert.That(viewModel.HasDateOfBirthError, Is.True);
        });
    }

    [Test]
    public async Task Save_WhenServerRejectsDisplayName_ShowsFieldErrorAndKeepsStore()
    {
        var client = new FakeProfileClient
        {
            UpdateException = new ApiException(HttpStatusCode.BadRequest, "Invalid display name.", "invalid_display_name")
        };
        var store = new InMemoryProfileDetailsStore();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: store,
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FullName = "Tên Mới";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.FullNameError, Is.Not.Empty);
            Assert.That(store.GetStudentDetails(UserId, string.Empty).FullName, Is.EqualTo("Minh Anh"));
            Assert.That(navigation.GoBackCalls, Is.Zero);
            Assert.That(viewModel.IsSaving, Is.False);
        });
    }

    [Test]
    public async Task Save_WhenUnauthorized_ShowsSessionExpiredMessage()
    {
        var client = new FakeProfileClient
        {
            UpdateException = new ApiException(HttpStatusCode.Unauthorized, "Sign in required.", "unauthorized")
        };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            navigation: navigation);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FullName = "Tên Mới";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Message, Does.Contain("Phiên đăng nhập"));
            Assert.That(viewModel.HasMessage, Is.True);
            Assert.That(navigation.GoBackCalls, Is.Zero);
        });
    }

    [Test]
    public async Task Save_WhenApiFails_ShowsMessageAndReportsIt()
    {
        var client = new FakeProfileClient { UpdateException = new HttpRequestException("API offline") };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            errorHandler: errorHandler);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FullName = "Tên Mới";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
            Assert.That(viewModel.IsSaving, Is.False);
        });
    }

    [Test]
    public async Task Save_Landlord_PreservesVerificationStatsAndReputation()
    {
        var client = new FakeProfileClient { ProfileResult = Profile("Chủ Trọ", "landlord") };
        var store = new InMemoryProfileDetailsStore();
        var viewModel = CreateViewModel(
            client,
            authService: CreateAuthService(SignedInAs(AppUserRole.Landlord)),
            store: store,
            prompt: new FakeAlertPrompt(),
            navigation: new FakeNavigationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.BusinessName = "Nhà trọ Mới";
        viewModel.Phone = "0987.654.321";
        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = store.GetLandlordDetails(UserId, string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(client.UpdateRequests, Is.Empty);
            Assert.That(saved.FullName, Is.EqualTo("Chủ Trọ"));
            Assert.That(saved.BusinessName, Is.EqualTo("Nhà trọ Mới"));
            Assert.That(saved.Phone, Is.EqualTo("0987654321"));
            Assert.That(saved.PhoneVerification, Is.EqualTo(ProfileVerificationStatus.Verified));
            Assert.That(saved.EmailVerification, Is.EqualTo(ProfileVerificationStatus.Verified));
            Assert.That(saved.IdentityVerification, Is.EqualTo(ProfileVerificationStatus.Pending));
            Assert.That(saved.PropertyOwnershipVerification, Is.EqualTo(ProfileVerificationStatus.Unverified));
            Assert.That(saved.RoomStats.Total, Is.EqualTo(6));
            Assert.That(saved.Reputation.AverageRating, Is.EqualTo(4.6));
        });
    }

    [Test]
    public async Task PickAvatar_WithSelection_SetsPathAndPersistsOnSave()
    {
        var picker = new FakeAvatarPicker { ResultPath = "/data/avatars/minh.jpg" };
        var store = new InMemoryProfileDetailsStore();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            store: store,
            prompt: new FakeAlertPrompt(),
            navigation: new FakeNavigationService(),
            avatarPicker: picker);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.PickAvatarCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(picker.LastUserId, Is.EqualTo(UserId));
            Assert.That(viewModel.AvatarPath, Is.EqualTo("/data/avatars/minh.jpg"));
            Assert.That(viewModel.HasAvatar, Is.True);
        });

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.That(store.GetStudentDetails(UserId, string.Empty).AvatarPath,
            Is.EqualTo("/data/avatars/minh.jpg"));
    }

    [Test]
    public async Task PickAvatar_WhenPickerFails_ShowsMessageAndReportsIt()
    {
        var picker = new FakeAvatarPicker { Exception = new InvalidOperationException("Picker offline") };
        var errorHandler = new FakeErrorHandler();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            errorHandler: errorHandler,
            avatarPicker: picker);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.PickAvatarCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(viewModel.AvatarPath, Is.Empty);
            Assert.That(errorHandler.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task EditingField_ClearsItsValidationError()
    {
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)));
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.FullName = "   ";
        viewModel.Phone = "123";
        viewModel.DateOfBirth = DateTime.Today.AddDays(1);
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.That(viewModel.HasFullNameError && viewModel.HasPhoneError && viewModel.HasDateOfBirthError, Is.True);

        viewModel.FullName = "Tên Mới";
        viewModel.Phone = "0901234567";
        viewModel.DateOfBirth = new DateTime(2003, 5, 12);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasFullNameError, Is.False);
            Assert.That(viewModel.HasPhoneError, Is.False);
            Assert.That(viewModel.HasDateOfBirthError, Is.False);
        });
    }

    [Test]
    public async Task Cancel_NavigatesBack()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(
            new FakeProfileClient(),
            authService: CreateAuthService(SignedInAs(AppUserRole.Student)),
            navigation: navigation);

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
    }

    private static EditProfileViewModel CreateViewModel(
        FakeProfileClient client,
        FakeTokenStore? tokenStore = null,
        AuthService? authService = null,
        InMemoryProfileDetailsStore? store = null,
        FakeAlertPrompt? prompt = null,
        FakeNavigationService? navigation = null,
        FakeErrorHandler? errorHandler = null,
        FakeAvatarPicker? avatarPicker = null)
        => new(
            client,
            authService ?? CreateAuthService(tokenStore ?? new FakeTokenStore()),
            navigation ?? new FakeNavigationService(),
            prompt ?? new FakeAlertPrompt(),
            errorHandler ?? new FakeErrorHandler(),
            store ?? new InMemoryProfileDetailsStore(),
            avatarPicker ?? new FakeAvatarPicker());

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

    private sealed class FakeAvatarPicker : IAvatarPicker
    {
        public string? ResultPath { get; set; }

        public Exception? Exception { get; set; }

        public Guid? LastUserId { get; private set; }

        public Task<string?> PickAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Exception is { } exception
                ? Task.FromException<string?>(exception)
                : Task.FromResult(ResultPath);
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
            => Task.FromResult(true);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public int GoBackCalls { get; private set; }

        public Task GoBackAsync()
        {
            GoBackCalls++;
            return Task.CompletedTask;
        }

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The edit profile screen does not open room details.");

        public Task GoToLoginAsync()
            => throw new NotSupportedException("The edit profile screen does not open login.");

        public Task GoToRegisterAsync()
            => throw new NotSupportedException("The edit profile screen does not open registration.");

        public Task GoToRoleRootAsync(AppUserRole? role)
            => throw new NotSupportedException("The edit profile screen does not reset the root.");

        public Task GoToListingEditorAsync(Guid? listingId = null)
            => throw new NotSupportedException("The edit profile screen does not open the listing editor.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The edit profile screen does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The edit profile screen does not open comparison.");
    }
}

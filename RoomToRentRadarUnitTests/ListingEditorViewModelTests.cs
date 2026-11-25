using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Models;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingEditorViewModelTests
{
    private readonly List<string> _temporaryPhotos = [];

    [TearDown]
    public void DeleteTemporaryPhotos()
    {
        foreach (var path in _temporaryPhotos) File.Delete(path);
        _temporaryPhotos.Clear();
    }

    [Test]
    public async Task SaveCommand_ValidNewListing_ShowsPendingReviewAndReplacesSelectionsWithSavedPhotos()
    {
        var client = new FakeClient();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, navigation: navigation);
        PopulateForm(viewModel);
        AddPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.CreateCalls, Is.EqualTo(1));
            Assert.That(client.LastSubmission!.Deposit, Is.Zero);
            Assert.That(client.LastSubmission.Photos, Has.Count.EqualTo(3));
            Assert.That(viewModel.StatusText, Is.EqualTo(ListingStatusText.For(ListingStatus.PendingReview)));
            Assert.That(viewModel.SelectedPhotos, Is.Empty);
            Assert.That(viewModel.ExistingPhotos, Has.Count.EqualTo(3));
            Assert.That(viewModel.CanUnpublish, Is.True);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(_temporaryPhotos.All(path => !File.Exists(path)), Is.True);
            Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task UnpublishCommand_ConfirmationDeclined_DoesNotCallApi()
    {
        var client = new FakeClient();
        var alerts = new FakeAlertPrompt { ConfirmResult = false };
        var viewModel = CreateViewModel(client, alerts: alerts);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.UnpublishCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.UnpublishCalls, Is.Zero);
        });
    }

    [Test]
    public async Task SaveCommand_FailureRetainsFieldsAndPhotos_AndRetrySucceeds()
    {
        var client = new FakeClient { CreateError = new HttpRequestException("Offline") };
        var viewModel = CreateViewModel(client);
        PopulateForm(viewModel);
        AddPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TitleField.Text, Is.EqualTo("Room near campus"));
            Assert.That(viewModel.DepositField.Text, Is.EqualTo("0"));
            Assert.That(viewModel.SelectedPhotos, Has.Count.EqualTo(3));
            Assert.That(_temporaryPhotos.All(File.Exists), Is.True);
            Assert.That(viewModel.Message, Is.Not.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
        });

        client.CreateError = null;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.That(client.CreateCalls, Is.EqualTo(2));
        Assert.That(viewModel.SelectedPhotos, Is.Empty);
    }

    [Test]
    public async Task SaveCommand_EmptyForm_ShowsRequiredErrorsWithoutCallingApi()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TitleField.Error, Is.Not.Empty);
            Assert.That(viewModel.DepositField.Error, Is.Not.Empty);
            Assert.That(viewModel.RoomTypeError, Is.Not.Empty);
            Assert.That(viewModel.PhotoError, Is.Not.Empty);
            Assert.That(client.CreateCalls, Is.Zero);
        });
    }

    [Test]
    public async Task LoadCommand_ExistingListing_PopulatesFormAndSaveRetainsExistingPhotos()
    {
        var client = new FakeClient();
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, navigation: navigation);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TitleField.Text, Is.EqualTo(client.Saved.Title));
            Assert.That(viewModel.AddressField.Text, Is.EqualTo(client.Saved.Address));
            Assert.That(viewModel.RentField.Text, Is.EqualTo("2500000"));
            Assert.That(viewModel.DepositField.Text, Is.EqualTo("0"));
            Assert.That(viewModel.SelectedRoomType!.Value, Is.EqualTo(RoomType.Studio));
            Assert.That(viewModel.ExistingPhotos, Has.Count.EqualTo(3));
            Assert.That(viewModel.SelectedPhotos, Is.Empty);
        });

        viewModel.TitleField.Text = "Updated room";
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.CreateCalls, Is.Zero);
            Assert.That(client.UpdateCalls, Is.EqualTo(1));
            Assert.That(client.LastSubmission!.Title, Is.EqualTo("Updated room"));
            Assert.That(client.LastSubmission.Photos, Is.Empty);
            Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LoadCommand_ExistingListingWithHouseRules_PrefillsRulesAndVersion()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEditMode, Is.True);
            Assert.That(viewModel.QuietHoursField.Text, Is.EqualTo("22:00 - 06:00"));
            Assert.That(viewModel.GuestPolicyField.Text, Is.EqualTo("Guests leave before 22:00"));
            Assert.That(viewModel.CookingRulesField.Text, Is.EqualTo("Cook in the shared kitchen"));
            Assert.That(viewModel.ParkingRulesField.Text, Is.EqualTo("Park on the ground floor"));
            Assert.That(viewModel.HouseRulesVersion, Is.EqualTo(2));
            Assert.That(viewModel.HouseRulesVersionText, Does.StartWith("Phiên bản 2"));
        });
    }

    [Test]
    public async Task SaveHouseRulesCommand_ValidRules_SendsTrimmedValuesAndShowsNextVersion()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.QuietHoursField.Text = "  23:00 - 05:00  ";
        viewModel.GuestPolicyField.Text = "No overnight guests";
        viewModel.CookingRulesField.Text = "Kitchen closes at 22:00";
        viewModel.ParkingRulesField.Text = "Basement B1";

        await viewModel.SaveHouseRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.HouseRulesCalls, Is.EqualTo(1));
            Assert.That(client.LastHouseRulesRequest!.QuietHours, Is.EqualTo("23:00 - 05:00"));
            Assert.That(client.LastHouseRulesRequest.GuestPolicy, Is.EqualTo("No overnight guests"));
            Assert.That(client.LastHouseRulesRequest.ParkingRules, Is.EqualTo("Basement B1"));
            Assert.That(viewModel.HouseRulesVersion, Is.EqualTo(3));
            Assert.That(viewModel.HouseRulesVersionText, Does.StartWith("Phiên bản 3"));
            Assert.That(viewModel.Message, Does.Contain("phiên bản 3"));
            Assert.That(viewModel.QuietHoursField.Error, Is.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task SaveHouseRulesCommand_MissingOrTooLongFields_ShowsErrorsWithoutCallingApi()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.QuietHoursField.Text = "   ";
        viewModel.GuestPolicyField.Text = new string('a', ListingEditorViewModel.HouseRulesMaxLength + 1);
        viewModel.CookingRulesField.Text = "Hợp lệ";
        viewModel.ParkingRulesField.Text = "Hợp lệ";

        await viewModel.SaveHouseRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.QuietHoursField.Error, Is.Not.Empty);
            Assert.That(viewModel.GuestPolicyField.Error, Does.Contain("300"));
            Assert.That(viewModel.CookingRulesField.Error, Is.Empty);
            Assert.That(client.HouseRulesCalls, Is.Zero);
        });
    }

    [Test]
    public async Task SaveHouseRulesCommand_ServerValidationErrors_MapToFields()
    {
        var client = new FakeClient
        {
            HouseRulesError = new ListingManagementException(
                HttpStatusCode.BadRequest,
                "Invalid house rules",
                "invalid_house_rules",
                new Dictionary<string, string[]>
                {
                    ["quiet_hours"] = ["This field is required."],
                    ["parking_rules"] = ["This field is required."]
                })
        };
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.QuietHoursField.Text = "23:00";
        viewModel.GuestPolicyField.Text = "Policy";
        viewModel.CookingRulesField.Text = "Cooking";
        viewModel.ParkingRulesField.Text = "Parking";

        await viewModel.SaveHouseRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.QuietHoursField.Error, Is.EqualTo("This field is required."));
            Assert.That(viewModel.ParkingRulesField.Error, Is.EqualTo("This field is required."));
            Assert.That(viewModel.GuestPolicyField.Error, Is.Empty);
            Assert.That(viewModel.Message, Is.EqualTo("Invalid house rules"));
        });
    }

    [Test]
    public async Task SaveHouseRulesCommand_NewListing_DoesNothing()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client);

        await viewModel.SaveHouseRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEditMode, Is.False);
            Assert.That(client.HouseRulesCalls, Is.Zero);
        });
    }

    [Test]
    public async Task SaveHouseRulesCommand_WhenEditLoadFailed_DoesNotCallApi()
    {
        var client = new FakeClient { LoadError = new HttpRequestException("offline") };
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.QuietHoursField.Text = "23:00";
        viewModel.GuestPolicyField.Text = "Policy";
        viewModel.CookingRulesField.Text = "Cooking";
        viewModel.ParkingRulesField.Text = "Parking";

        await viewModel.SaveHouseRulesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.HouseRulesCalls, Is.Zero);
            Assert.That(viewModel.Message, Is.EqualTo("Tải lại thông tin tin đăng trước khi lưu nội quy."));
        });
    }

    [Test]
    public async Task LoadCommand_FailedEditLoad_PreventsSavingUntilLoaded()
    {
        var client = new FakeClient { LoadError = new HttpRequestException("Offline") };
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });

        await viewModel.LoadCommand.ExecuteAsync(null);
        PopulateForm(viewModel);
        AddPhotos(viewModel);
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.UpdateCalls, Is.Zero);
            Assert.That(client.CreateCalls, Is.Zero);
            Assert.That(viewModel.Message, Is.Not.Empty);
        });
    }

    [Test]
    public async Task SaveCommand_NotSignedIn_DoesNotSendListing()
    {
        var client = new FakeClient();
        var viewModel = CreateViewModel(client, signedIn: false);
        PopulateForm(viewModel);
        AddPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.That(client.CreateCalls, Is.Zero);
        Assert.That(viewModel.IsAuthenticated, Is.False);
    }

    [Test]
    public async Task ReleaseSelectedPhotos_PendingSave_CancelsOperationAndCleansCacheAfterItFinishes()
    {
        var client = new FakeClient();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<ManagedListingDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.CreateOverride = async cancellationToken =>
        {
            using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
            started.TrySetResult();
            return await pending.Task;
        };
        var viewModel = CreateViewModel(client);
        PopulateForm(viewModel);
        AddPhotos(viewModel);
        var save = viewModel.SaveCommand.ExecuteAsync(null);
        await started.Task;

        viewModel.ReleaseSelectedPhotos();
        await save;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.SelectedPhotos, Is.Empty);
            Assert.That(_temporaryPhotos.All(path => !File.Exists(path)), Is.True);
            Assert.That(viewModel.Message, Is.Empty);
        });
    }

    [Test]
    public async Task ReleaseSelectedPhotos_PendingLoad_CancelsWithoutShowingConnectionError()
    {
        var client = new FakeClient();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<ManagedListingDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.LoadOverride = async cancellationToken =>
        {
            using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
            started.TrySetResult();
            return await pending.Task;
        };
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        var load = viewModel.LoadCommand.ExecuteAsync(null);
        await started.Task;

        viewModel.ReleaseSelectedPhotos();
        await load;

        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.Message, Is.Empty);
    }

    [Test]
    public async Task PublishCommand_HiddenListing_TransitionsToPendingReviewAndEnablesUnpublish()
    {
        // Arrange
        var client = new FakeClient();
        client.Saved = client.Saved with { Status = ListingStatus.Hidden };
        var viewModel = CreateViewModel(client);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.PublishCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.PublishCalls, Is.EqualTo(1));
            Assert.That(viewModel.StatusText, Is.EqualTo(ListingStatusText.For(ListingStatus.PendingReview)));
            Assert.That(viewModel.CanPublish, Is.False);
            Assert.That(viewModel.CanUnpublish, Is.True);
            Assert.That(viewModel.CanSave, Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Đã gửi duyệt lại tin đăng. Tin đang chờ duyệt trước khi hiển thị công khai."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task DeleteCommand_ConfirmationDeclined_DoesNotCallApiOrNavigateBack()
    {
        // Arrange
        var client = new FakeClient();
        var alerts = new FakeAlertPrompt { ConfirmResult = false };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, alerts: alerts, navigation: navigation);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.DeleteCalls, Is.Zero);
            Assert.That(navigation.GoBackCalls, Is.Zero);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task DeleteCommand_Confirmed_DeletesListingCleansTemporaryPhotosAndNavigatesBack()
    {
        // Arrange
        var client = new FakeClient();
        var alerts = new FakeAlertPrompt { ConfirmResult = true };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(client, alerts: alerts, navigation: navigation);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });
        await viewModel.LoadCommand.ExecuteAsync(null);
        AddPhotos(viewModel);

        // Act
        await viewModel.DeleteCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(client.DeleteCalls, Is.EqualTo(1));
            Assert.That(navigation.GoBackCalls, Is.EqualTo(1));
            Assert.That(viewModel.SelectedPhotos, Is.Empty);
            Assert.That(_temporaryPhotos.All(path => !File.Exists(path)), Is.True);
            Assert.That(viewModel.Message, Is.EqualTo("Đã xóa tin đăng."));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task LoadCommand_RemovedListing_DisablesSavingPublishingAndUnpublishing()
    {
        // Arrange
        var client = new FakeClient();
        client.Saved = client.Saved with { Status = ListingStatus.Removed };
        var alerts = new FakeAlertPrompt { ConfirmResult = true };
        var viewModel = CreateViewModel(client, alerts: alerts);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = client.Saved.Id.ToString() });

        // Act
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.PublishCommand.ExecuteAsync(null);
        await viewModel.UnpublishCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusText, Is.EqualTo(ListingStatusText.For(ListingStatus.Removed)));
            Assert.That(viewModel.CanSave, Is.False);
            Assert.That(viewModel.CanSaveListing, Is.False);
            Assert.That(viewModel.SaveCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.CanPublish, Is.False);
            Assert.That(viewModel.CanUnpublish, Is.False);
            Assert.That(client.UpdateCalls, Is.Zero);
            Assert.That(client.PublishCalls, Is.Zero);
            Assert.That(client.UnpublishCalls, Is.Zero);
            Assert.That(alerts.ConfirmCalls, Is.Zero);
        });
    }

    [Test]
    public async Task UseCurrentLocationCommand_Success_PopulatesSixDecimalCoordinatesShowsAccuracyAndRaisesResolvedEvent()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Arrange
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var locationService = new FakeDeviceLocationService
            {
                Result = new DeviceLocationResult(DeviceLocationStatus.Success, 10.7768894, 106.7008062, 12.4)
            };
            var viewModel = CreateViewModel(new FakeClient(), locationService: locationService);
            viewModel.LatitudeField.Error = "Vĩ độ không hợp lệ.";
            viewModel.LongitudeField.Error = "Kinh độ không hợp lệ.";
            var resolvedCoordinates = new List<(double Latitude, double Longitude)>();
            viewModel.CurrentLocationResolved += (_, coordinates) => resolvedCoordinates.Add(coordinates);

            // Act
            await viewModel.UseCurrentLocationCommand.ExecuteAsync(null);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(locationService.GetLocationCalls, Is.EqualTo(1));
                Assert.That(locationService.OpenSettingsCalls, Is.Zero);
                Assert.That(viewModel.LatitudeField.Text, Is.EqualTo("10.776889"));
                Assert.That(viewModel.LongitudeField.Text, Is.EqualTo("106.700806"));
                Assert.That(viewModel.LatitudeField.Error, Is.Empty);
                Assert.That(viewModel.LongitudeField.Error, Is.Empty);
                Assert.That(viewModel.LocationStatusMessage, Is.EqualTo("Đã chọn vị trí hiện tại (độ chính xác ±12 m)."));
                Assert.That(resolvedCoordinates, Is.EqualTo(new[] { (10.7768894, 106.7008062) }));
                Assert.That(viewModel.IsLocating, Is.False);
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task UseCurrentLocationCommand_LocationDisabledAndConfirmed_PromptsAndOpensLocationSettings()
    {
        // Arrange
        var locationService = new FakeDeviceLocationService
        {
            Result = new DeviceLocationResult(DeviceLocationStatus.LocationDisabled)
        };
        var alerts = new FakeAlertPrompt { ConfirmResult = true };
        var viewModel = CreateViewModel(new FakeClient(), alerts: alerts, locationService: locationService);

        // Act
        await viewModel.UseCurrentLocationCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(locationService.GetLocationCalls, Is.EqualTo(1));
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(alerts.LastConfirmTitle, Is.EqualTo("Bật định vị GPS?"));
            Assert.That(locationService.OpenSettingsCalls, Is.EqualTo(1));
            Assert.That(viewModel.LocationStatusMessage, Is.EqualTo("GPS / Dịch vụ vị trí đang tắt. Hãy bật Vị trí rồi bấm định vị lại."));
            Assert.That(viewModel.LatitudeField.Text, Is.Empty);
            Assert.That(viewModel.LongitudeField.Text, Is.Empty);
            Assert.That(viewModel.IsLocating, Is.False);
        });
    }

    [Test]
    public async Task UseCurrentLocationCommand_PermissionDeniedAndDeclined_ShowsMessageWithoutOpeningSettings()
    {
        // Arrange
        var locationService = new FakeDeviceLocationService
        {
            Result = new DeviceLocationResult(DeviceLocationStatus.PermissionDenied)
        };
        var alerts = new FakeAlertPrompt { ConfirmResult = false };
        var viewModel = CreateViewModel(new FakeClient(), alerts: alerts, locationService: locationService);

        // Act
        await viewModel.UseCurrentLocationCommand.ExecuteAsync(null);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(locationService.GetLocationCalls, Is.EqualTo(1));
            Assert.That(alerts.ConfirmCalls, Is.EqualTo(1));
            Assert.That(alerts.LastConfirmTitle, Is.EqualTo("Cấp quyền Vị trí?"));
            Assert.That(locationService.OpenSettingsCalls, Is.Zero);
            Assert.That(viewModel.LocationStatusMessage, Is.EqualTo("Chưa có quyền truy cập vị trí chính xác. Cấp quyền Vị trí trong Cài đặt rồi thử lại."));
            Assert.That(viewModel.LatitudeField.Text, Is.Empty);
            Assert.That(viewModel.LongitudeField.Text, Is.Empty);
            Assert.That(viewModel.IsLocating, Is.False);
        });
    }

    private static ListingEditorViewModel CreateViewModel(
        FakeClient client,
        bool signedIn = true,
        FakeAlertPrompt? alerts = null,
        FakeNavigationService? navigation = null,
        FakeDeviceLocationService? locationService = null)
        => new(
            client,
            new AuthService(null!, new FakeTokenStore(signedIn)),
            navigation ?? new FakeNavigationService(),
            alerts ?? new FakeAlertPrompt(),
            locationService);

    private static void PopulateForm(ListingEditorViewModel viewModel)
    {
        viewModel.TitleField.Text = "Room near campus";
        viewModel.AddressField.Text = "10 Campus Road";
        viewModel.RentField.Text = "2500000";
        viewModel.DepositField.Text = "0";
        viewModel.AreaField.Text = "20.5";
        viewModel.LatitudeField.Text = "10.75";
        viewModel.LongitudeField.Text = "106.7";
        viewModel.SelectedRoomType = viewModel.RoomTypes.Single(option => option.Value == RoomType.Studio);
    }

    private void AddPhotos(ListingEditorViewModel viewModel)
    {
        for (var index = 0; index < 3; index++)
        {
            var path = Path.GetTempFileName();
            _temporaryPhotos.Add(path);
            viewModel.SelectedPhotos.Add(new SelectedListingPhoto($"room-{index}.png", "image/png", path, new RelayCommand(() => { })));
        }
    }

    private sealed class FakeTokenStore(bool signedIn) : ITokenStore
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(signedIn ? "test-token" : null);
        public Task<AppUserRole?> GetRoleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<AppUserRole?>(AppUserRole.Landlord);
        public Task SaveAsync(string accessToken, DateTime expiresAt, AppUserRole role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public int GoBackCalls { get; private set; }

        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;
        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;
        public Task GoToLoginAsync() => Task.CompletedTask;
        public Task GoToRegisterAsync() => Task.CompletedTask;
        public Task GoToRoleRootAsync(AppUserRole? role) => Task.CompletedTask;
        public Task GoToDirectionsAsync(double destinationLat, double destinationLng, double? originLat, double? originLng) => Task.CompletedTask;
        public Task GoToComparisonAsync() => Task.CompletedTask;
        public Task GoBackAsync()
        {
            GoBackCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAlertPrompt : IAlertPrompt
    {
        public bool ConfirmResult { get; set; } = true;
        public int ConfirmCalls { get; private set; }
        public string? LastConfirmTitle { get; private set; }

        public Task ShowAsync(
            string title,
            string message,
            string dismissText = "OK",
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string acceptText,
            string cancelText,
            CancellationToken cancellationToken = default)
        {
            ConfirmCalls++;
            LastConfirmTitle = title;
            return Task.FromResult(ConfirmResult);
        }
    }

    private sealed class FakeDeviceLocationService : IDeviceLocationService
    {
        public DeviceLocationResult Result { get; set; } = new(DeviceLocationStatus.Success, 10.776889, 106.700806, 8);
        public bool OpenSettingsResult { get; set; } = true;
        public int GetLocationCalls { get; private set; }
        public int OpenSettingsCalls { get; private set; }

        public Task<DeviceLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
        {
            GetLocationCalls++;
            return Task.FromResult(Result);
        }

        public Task<bool> OpenLocationSettingsAsync()
        {
            OpenSettingsCalls++;
            return Task.FromResult(OpenSettingsResult);
        }
    }

    private sealed class FakeClient : IListingManagementClient
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int PublishCalls { get; private set; }
        public int UnpublishCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int HouseRulesCalls { get; private set; }
        public ListingFormSubmission? LastSubmission { get; private set; }
        public UpdateHouseRulesRequest? LastHouseRulesRequest { get; private set; }
        public Exception? CreateError { get; set; }
        public Exception? LoadError { get; set; }
        public Exception? PublishError { get; set; }
        public Exception? DeleteError { get; set; }
        public Exception? HouseRulesError { get; set; }
        public Func<CancellationToken, Task<ManagedListingDto>>? CreateOverride { get; set; }
        public Func<CancellationToken, Task<ManagedListingDto>>? LoadOverride { get; set; }
        public ManagedListingDto Saved { get; set; } = CreateListing();
        public HouseRulesDto? HouseRules { get; private set; }

        public FakeClient()
        {
            HouseRules = Saved.HouseRules;
        }

        public Task<IReadOnlyList<ManagedListingDto>> GetMineAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ManagedListingDto>>([Saved]);
        public Task<ManagedListingDto> GetManagedAsync(Guid id, CancellationToken cancellationToken = default)
            => LoadOverride?.Invoke(cancellationToken) ?? (LoadError is { } error ? Task.FromException<ManagedListingDto>(error) : Task.FromResult(Saved));
        public Task<ManagedListingDto> CreateAsync(ListingFormSubmission form, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastSubmission = form;
            return CreateOverride?.Invoke(cancellationToken) ?? (CreateError is { } error ? Task.FromException<ManagedListingDto>(error) : Task.FromResult(Saved));
        }
        public Task<ManagedListingDto> UpdateAsync(Guid id, ListingFormSubmission form, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastSubmission = form;
            return Task.FromResult(Saved);
        }
        public Task<ManagedListingDto> PublishAsync(Guid id, CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            return PublishError is { } error
                ? Task.FromException<ManagedListingDto>(error)
                : Task.FromResult(Saved with { Status = ListingStatus.PendingReview });
        }
        public Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
        {
            UnpublishCalls++;
            return Task.FromResult(Saved with { Status = ListingStatus.Hidden });
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return DeleteError is { } error
                ? Task.FromException(error)
                : Task.CompletedTask;
        }
        public Task<HouseRulesDto> UpdateHouseRulesAsync(
            Guid id, UpdateHouseRulesRequest request, CancellationToken cancellationToken = default)
        {
            HouseRulesCalls++;
            LastHouseRulesRequest = request;
            if (HouseRulesError is { } error) return Task.FromException<HouseRulesDto>(error);
            HouseRules = new HouseRulesDto(
                request.QuietHours ?? string.Empty,
                request.GuestPolicy ?? string.Empty,
                request.CookingRules ?? string.Empty,
                request.ParkingRules ?? string.Empty,
                (HouseRules?.Version ?? 0) + 1,
                new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc));
            return Task.FromResult(HouseRules);
        }

        private static ManagedListingDto CreateListing()
        {
            var id = Guid.NewGuid();
            var photos = Enumerable.Range(0, 3).Select(index => new ListingPhotoDto(Guid.NewGuid(), id,
                $"/photos/{index}.png", $"/photos/{index}-thumb.png", 100, 100, ListingPhotoKind.Photo, null, index, index == 0)).ToArray();
            return new ManagedListingDto(id, "Room near campus", "Near campus", 2500000, 0, 20.5, RoomType.Studio,
                1, "10 Campus Road", 10.75, 106.7, ListingStatus.PendingReview,
                new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc), [], [], "Landlord", photos,
                new HouseRulesDto("22:00 - 06:00", "Guests leave before 22:00",
                    "Cook in the shared kitchen", "Park on the ground floor", 2,
                    new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)));
        }
    }
}

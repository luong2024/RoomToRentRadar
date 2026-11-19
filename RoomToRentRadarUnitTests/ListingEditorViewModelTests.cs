using System.Net;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Features.ListingManagement.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
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
        var viewModel = CreateViewModel(client);
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
        var viewModel = CreateViewModel(client);
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

    private static ListingEditorViewModel CreateViewModel(FakeClient client, bool signedIn = true)
        => new(client, new AuthService(null!, new FakeTokenStore(signedIn)), new FakeNavigationService());

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
        public Task SaveAsync(string accessToken, DateTime expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;
        public Task GoToLoginAsync() => Task.CompletedTask;
        public Task GoToDirectionsAsync(double destinationLat, double destinationLng, double? originLat, double? originLng) => Task.CompletedTask;
        public Task GoBackAsync() => Task.CompletedTask;
    }

    private sealed class FakeClient : IListingManagementClient
    {
        public int CreateCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int HouseRulesCalls { get; private set; }
        public ListingFormSubmission? LastSubmission { get; private set; }
        public UpdateHouseRulesRequest? LastHouseRulesRequest { get; private set; }
        public Exception? CreateError { get; set; }
        public Exception? LoadError { get; set; }
        public Exception? HouseRulesError { get; set; }
        public Func<CancellationToken, Task<ManagedListingDto>>? CreateOverride { get; set; }
        public Func<CancellationToken, Task<ManagedListingDto>>? LoadOverride { get; set; }
        public ManagedListingDto Saved { get; } = CreateListing();
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
        public Task<ManagedListingDto> UnpublishAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved with { Status = ListingStatus.Hidden });
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

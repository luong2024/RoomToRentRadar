using System.Net;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Features.ListingManagement.ViewModels;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;
using ListingSubmission = RoomToRentRadar.Models.ListingSubmission;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingEditorViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");

    [Test]
    public void Constructor_StartsInCreateModeWithSensibleDefaults()
    {
        var viewModel = CreateViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsCreateMode, Is.True);
            Assert.That(viewModel.PageTitle, Is.EqualTo("Đăng tin mới"));
            Assert.That(viewModel.SubmitText, Is.EqualTo("Gửi duyệt"));
            Assert.That(viewModel.BedroomsText, Is.EqualTo("1"));
            Assert.That(viewModel.LatText, Is.EqualTo("10.7721"));
            Assert.That(viewModel.LngText, Is.EqualTo("106.6579"));
            Assert.That(viewModel.SelectedRoomType, Is.Null);
            Assert.That(viewModel.RoomTypeOptions, Has.Count.EqualTo(5));
            Assert.That(viewModel.RoomTypeOptions.Select(option => option.Value), Is.EqualTo(new[]
            {
                RoomType.SingleRoom,
                RoomType.SharedRoom,
                RoomType.Studio,
                RoomType.Apartment,
                RoomType.House
            }));
            Assert.That(viewModel.PhotoCountText, Is.EqualTo("0/3 ảnh"));
            Assert.That(viewModel.HasPhotos, Is.False);
        });
    }

    [Test]
    public void ApplyQueryAttributes_WithoutId_KeepsCreateMode()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsCreateMode, Is.True);
            Assert.That(viewModel.PageTitle, Is.EqualTo("Đăng tin mới"));
            Assert.That(viewModel.LoadCommand.ExecutionTask, Is.Null);
        });
    }

    [Test]
    public async Task ApplyQueryAttributes_WithId_LoadsAndPrefillsTheEditor()
    {
        var managed = CreateManagedListing();
        var apiClient = new FakeApiClient { ManagedListing = managed };
        var viewModel = CreateViewModel(apiClient);

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ListingEditorViewModel.ListingIdQueryKey] = ListingId.ToString("D")
        });
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsCreateMode, Is.False);
            Assert.That(viewModel.PageTitle, Is.EqualTo("Sửa tin đăng"));
            Assert.That(viewModel.SubmitText, Is.EqualTo("Lưu thay đổi"));
            Assert.That(viewModel.Title, Is.EqualTo(managed.Title));
            Assert.That(viewModel.Address, Is.EqualTo(managed.Address));
            Assert.That(viewModel.PriceText, Is.EqualTo("2500000"));
            Assert.That(viewModel.DepositText, Is.EqualTo("2500000"));
            Assert.That(viewModel.AreaText, Is.EqualTo("20"));
            Assert.That(viewModel.BedroomsText, Is.EqualTo("2"));
            Assert.That(viewModel.DescriptionText, Is.EqualTo("Mô tả phòng"));
            Assert.That(viewModel.LandlordNameText, Is.EqualTo("Chủ trọ"));
            Assert.That(viewModel.SelectedRoomType, Is.EqualTo(RoomType.Studio));
            Assert.That(viewModel.RoomTypeOptions.Single(option => option.IsSelected).Value,
                Is.EqualTo(RoomType.Studio));
            Assert.That(viewModel.ExistingPhotoCount, Is.EqualTo(3));
            Assert.That(viewModel.Photos, Has.Count.EqualTo(3));
            Assert.That(viewModel.Photos.All(photo => !photo.IsRemovable), Is.True);
            Assert.That(viewModel.PhotoCountText, Is.EqualTo("3/3 ảnh"));
            Assert.That(apiClient.GetManagedRequestedIds, Is.EqualTo(new[] { ListingId }));
        });
    }

    [Test]
    public async Task LoadAsync_WhenTheListingIsGone_ShowsTheNotFoundMessage()
    {
        var apiClient = new FakeApiClient
        {
            ManagedException = new ApiException(HttpStatusCode.NotFound, "gone", "listing_not_found")
        };
        var viewModel = CreateViewModel(apiClient);

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ListingEditorViewModel.ListingIdQueryKey] = ListingId.ToString("D")
        });
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Is.EqualTo("Tin đăng không còn tồn tại."));
            Assert.That(viewModel.IsSignInRequired, Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_WhenUnauthorized_AsksTheLandlordToSignInAgain()
    {
        var apiClient = new FakeApiClient
        {
            ManagedException = new ApiException(HttpStatusCode.Unauthorized, "expired")
        };
        var viewModel = CreateViewModel(apiClient);

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ListingEditorViewModel.ListingIdQueryKey] = ListingId.ToString("D")
        });
        await AwaitAutoStartedLoadAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInRequired, Is.True);
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
        });
    }

    [Test]
    public async Task SaveAsync_WhenTheFormIsEmpty_MarksTheFieldsAndSkipsTheApi()
    {
        var apiClient = new FakeApiClient();
        var viewModel = CreateViewModel(apiClient);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TitleError, Is.EqualTo("Vui lòng nhập tiêu đề tin đăng."));
            Assert.That(viewModel.AddressError, Is.EqualTo("Vui lòng nhập địa chỉ phòng trọ."));
            Assert.That(viewModel.PriceError, Is.EqualTo("Vui lòng nhập giá thuê."));
            Assert.That(viewModel.RoomTypeError, Is.EqualTo("Vui lòng chọn loại phòng."));
            Assert.That(viewModel.PhotosError,
                Is.EqualTo("Cần ít nhất 3 ảnh (JPG hoặc PNG) để gửi duyệt."));
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Vui lòng kiểm tra lại các thông tin được đánh dấu."));
            Assert.That(viewModel.HasTitleError, Is.True);
            Assert.That(apiClient.CreateSubmissions, Is.Empty);
            Assert.That(apiClient.UpdateCalls, Is.Empty);
        });
    }

    [Test]
    public async Task SaveAsync_WhenCreating_SendsTheSubmissionAndReturnsToTheList()
    {
        var apiClient = new FakeApiClient { Created = CreateManagedListing(ListingStatus.PendingReview) };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, navigation: navigation);
        viewModel.SuccessMessageDelay = TimeSpan.Zero;
        FillValidForm(viewModel);
        AddNewPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.CreateSubmissions, Has.Count.EqualTo(1));

            var submission = apiClient.CreateSubmissions[0];
            Assert.That(submission.Title, Is.EqualTo("Phòng trọ gần đại học"));
            Assert.That(submission.Address, Is.EqualTo("12 Nguyễn Văn Cừ, Quận 5"));
            Assert.That(submission.Price, Is.EqualTo(3_500_000));
            Assert.That(submission.Deposit, Is.EqualTo(3_500_000));
            Assert.That(submission.AreaM2, Is.EqualTo(20));
            Assert.That(submission.RoomType, Is.EqualTo(RoomType.Studio));
            Assert.That(submission.Bedrooms, Is.EqualTo(1));
            Assert.That(submission.Lat, Is.EqualTo(10.7721));
            Assert.That(submission.Lng, Is.EqualTo(106.6579));
            Assert.That(submission.Photos.Select(photo => photo.FileName),
                Is.EqualTo(new[] { "phong-0.jpg", "phong-1.jpg", "phong-2.jpg" }));
            Assert.That(submission.Photos.All(photo => photo.ContentType == "image/jpeg"), Is.True);

            Assert.That(apiClient.UpdateCalls, Is.Empty);
            Assert.That(viewModel.SuccessMessage,
                Is.EqualTo("Tin đã được gửi duyệt. Tin sẽ hiển thị với sinh viên sau khi được kiểm duyệt."));
            Assert.That(viewModel.HasError, Is.False);
            Assert.That(navigation.BackRequests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SaveAsync_WhenEditing_UpdatesTheLoadedListing()
    {
        var apiClient = new FakeApiClient
        {
            ManagedListing = CreateManagedListing(),
            Updated = CreateManagedListing(ListingStatus.PendingReview)
        };
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(apiClient, navigation: navigation);
        viewModel.SuccessMessageDelay = TimeSpan.Zero;

        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ListingEditorViewModel.ListingIdQueryKey] = ListingId.ToString("D")
        });
        await AwaitAutoStartedLoadAsync(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(apiClient.UpdateCalls, Has.Count.EqualTo(1));
            Assert.That(apiClient.UpdateCalls[0].Id, Is.EqualTo(ListingId));
            Assert.That(apiClient.UpdateCalls[0].Submission.Title, Is.EqualTo("Phòng đẹp"));
            Assert.That(apiClient.UpdateCalls[0].Submission.Photos, Is.Empty);
            Assert.That(apiClient.CreateSubmissions, Is.Empty);
            Assert.That(viewModel.SuccessMessage,
                Is.EqualTo("Đã lưu thay đổi. Tin sẽ được kiểm duyệt lại trước khi hiển thị."));
            Assert.That(navigation.BackRequests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SaveAsync_WhenTheServerMarksFields_ShowsThemOnTheForm()
    {
        var apiClient = new FakeApiClient
        {
            CreateException = new ListingSaveException(
                HttpStatusCode.UnprocessableEntity,
                "Invalid listing",
                "invalid_listing",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    [ListingFieldKeys.Title] = ["Tiêu đề quá dài."],
                    [ListingFieldKeys.Photos] = ["Ảnh thứ 2 không hợp lệ."]
                })
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.SuccessMessageDelay = TimeSpan.Zero;
        FillValidForm(viewModel);
        AddNewPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TitleError, Is.EqualTo("Tiêu đề quá dài."));
            Assert.That(viewModel.PhotosError, Is.EqualTo("Ảnh thứ 2 không hợp lệ."));
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Vui lòng kiểm tra lại các thông tin được đánh dấu."));
            Assert.That(viewModel.SuccessMessage, Is.Empty);
        });
    }

    [Test]
    public async Task SaveAsync_WhenTheServerRejectsThePhotos_ShowsTheReason()
    {
        var apiClient = new FakeApiClient
        {
            CreateException = new ListingSaveException(
                HttpStatusCode.UnprocessableEntity,
                "Too few photos",
                "too_few_photos")
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.SuccessMessageDelay = TimeSpan.Zero;
        FillValidForm(viewModel);
        AddNewPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.That(viewModel.ErrorMessage, Is.EqualTo("Cần ít nhất 3 ảnh để gửi duyệt."));
    }

    [Test]
    public async Task SaveAsync_WhenUnauthorized_AsksTheLandlordToSignInAgain()
    {
        var apiClient = new FakeApiClient
        {
            CreateException = new ApiException(HttpStatusCode.Unauthorized, "expired")
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.SuccessMessageDelay = TimeSpan.Zero;
        FillValidForm(viewModel);
        AddNewPhotos(viewModel);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSignInRequired, Is.True);
            Assert.That(viewModel.ErrorMessage,
                Is.EqualTo("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
        });
    }

    [Test]
    public async Task PickPhotosAsync_KeepsSupportedPhotosAndReportsSkippedOnes()
    {
        var picker = new FakePhotoPicker
        {
            Photos =
            [
                Picked("hop-le.jpg", "image/jpeg", 1024),
                Picked("sodo.gif", "image/gif", 1024),
                Picked("qua-to.png", "image/png", ListingPhotoRules.MaxPhotoBytes + 1),
                Picked("hop-le-2.png", "image/png", 2048)
            ]
        };
        var viewModel = CreateViewModel(photoPicker: picker);

        await viewModel.PickPhotosCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Photos, Has.Count.EqualTo(2));
            Assert.That(viewModel.Photos.Select(photo => photo.FileName),
                Is.EqualTo(new[] { "hop-le.jpg", "hop-le-2.png" }));
            Assert.That(viewModel.NewPhotoCount, Is.EqualTo(2));
            Assert.That(viewModel.HasPhotos, Is.True);
            Assert.That(viewModel.PhotoCountText, Is.EqualTo("2/3 ảnh"));
            Assert.That(viewModel.PhotosError, Is.EqualTo(
                "Đã bỏ qua 2 ảnh: chỉ nhận JPG hoặc PNG, tối đa 10MB mỗi ảnh và 10 ảnh mới mỗi lần lưu."));
        });
    }

    [Test]
    public async Task PickPhotosAsync_WhenThePickerThrows_ShowsThePickerError()
    {
        var picker = new FakePhotoPicker { Exception = new InvalidOperationException("denied") };
        var viewModel = CreateViewModel(photoPicker: picker);

        await viewModel.PickPhotosCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.PhotosError,
                Is.EqualTo("Không mở được thư viện ảnh. Vui lòng thử lại."));
            Assert.That(viewModel.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task RemovePhoto_DeletesNewPhotosButKeepsUploadedOnes()
    {
        var apiClient = new FakeApiClient { ManagedListing = CreateManagedListing() };
        var viewModel = CreateViewModel(apiClient);
        viewModel.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [ListingEditorViewModel.ListingIdQueryKey] = ListingId.ToString("D")
        });
        await AwaitAutoStartedLoadAsync(viewModel);
        AddNewPhotos(viewModel, count: 1);

        var uploaded = viewModel.Photos[0];
        viewModel.RemovePhotoCommand.Execute(uploaded);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Photos, Has.Count.EqualTo(4));
            Assert.That(uploaded.IsRemovable, Is.False);
        });

        var fresh = viewModel.Photos.Single(photo => photo.IsNew);
        viewModel.RemovePhotoCommand.Execute(fresh);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Photos, Has.Count.EqualTo(3));
            Assert.That(viewModel.NewPhotoCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GoBackCommand_ReturnsToThePreviousPage()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(navigation: navigation);

        await viewModel.GoBackCommand.ExecuteAsync(null);

        Assert.That(navigation.BackRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task SignInCommand_OpensTheLoginPage()
    {
        var navigation = new FakeNavigationService();
        var viewModel = CreateViewModel(navigation: navigation);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.That(navigation.LoginRequests, Is.EqualTo(1));
    }

    private static ListingEditorViewModel CreateViewModel(
        FakeApiClient? apiClient = null,
        FakePhotoPicker? photoPicker = null,
        FakeNavigationService? navigation = null)
        => new(apiClient ?? new FakeApiClient(), photoPicker ?? new FakePhotoPicker(), navigation ?? new FakeNavigationService());

    private static void FillValidForm(ListingEditorViewModel viewModel)
    {
        viewModel.Title = "Phòng trọ gần đại học";
        viewModel.Address = "12 Nguyễn Văn Cừ, Quận 5";
        viewModel.PriceText = "3.500.000";
        viewModel.DepositText = "3.500.000";
        viewModel.AreaText = "20";
        viewModel.BedroomsText = "1";
        viewModel.SelectedRoomType = RoomType.Studio;
    }

    private static void AddNewPhotos(ListingEditorViewModel viewModel, int count = 3)
    {
        for (var index = 0; index < count; index++)
        {
            viewModel.Photos.Add(new EditorPhoto(
                $"phong-{index}.jpg",
                isNew: true,
                photoId: null,
                preview: null,
                openReadAsync: () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
                contentType: "image/jpeg",
                length: 3));
        }
    }

    private static PickedPhoto Picked(string fileName, string contentType, long length)
        => new(fileName, contentType, length, () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

    private static ManagedListingDto CreateManagedListing(
        ListingStatus status = ListingStatus.Published,
        int photoCount = 3)
    {
        var photos = Enumerable.Range(0, photoCount)
            .Select(index => new ListingPhotoDto(
                Guid.NewGuid(),
                ListingId,
                $"/uploads/phong-{index}.jpg",
                null,
                800,
                600,
                ListingPhotoKind.Photo,
                null,
                index,
                IsCover: index == 0))
            .ToList();

        return new ManagedListingDto(
            ListingId,
            "Phòng đẹp",
            "Mô tả phòng",
            2_500_000,
            2_500_000,
            20,
            RoomType.Studio,
            2,
            "12 Nguyễn Văn Cừ, Quận 5",
            10.7721,
            106.6579,
            status,
            new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc),
            [],
            [],
            "Chủ trọ",
            photos);
    }

    // A valid id starts the load inside ApplyQueryAttributes, so the test awaits the auto-started
    // execution instead of invoking the command a second time (which would issue a second call).
    private static Task AwaitAutoStartedLoadAsync(ListingEditorViewModel viewModel)
    {
        var executionTask = viewModel.LoadCommand.ExecutionTask;

        Assert.That(executionTask, Is.Not.Null, "Applying a valid listing id should start the load automatically.");

        return executionTask!;
    }

    private sealed class FakePhotoPicker : IPhotoPicker
    {
        public IReadOnlyList<PickedPhoto> Photos { get; set; } = [];

        public Exception? Exception { get; set; }

        public Task<IReadOnlyList<PickedPhoto>> PickPhotosAsync()
            => Exception is not null
                ? Task.FromException<IReadOnlyList<PickedPhoto>>(Exception)
                : Task.FromResult(Photos);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public int LoginRequests { get; private set; }

        public int BackRequests { get; private set; }

        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;

        public Task GoToLoginAsync()
        {
            LoginRequests++;
            return Task.CompletedTask;
        }

        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The listing editor does not open directions.");

        public Task GoBackAsync()
        {
            BackRequests++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApiClient : IApiClient
    {
        public ManagedListingDto? ManagedListing { get; set; }

        public Exception? ManagedException { get; set; }

        public ManagedListingDto? Created { get; set; }

        public ManagedListingDto? Updated { get; set; }

        public Exception? CreateException { get; set; }

        public List<ListingSubmission> CreateSubmissions { get; } = new();

        public List<Guid> GetManagedRequestedIds { get; } = new();

        public List<(Guid Id, ListingSubmission Submission)> UpdateCalls { get; } = new();

        public Task<ManagedListingDto> GetManagedListingAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            GetManagedRequestedIds.Add(id);
            return ManagedException is not null
                ? Task.FromException<ManagedListingDto>(ManagedException)
                : Task.FromResult(ManagedListing!);
        }

        public Task<ManagedListingDto> CreateListingAsync(
            ListingSubmission submission,
            CancellationToken cancellationToken = default)
        {
            CreateSubmissions.Add(submission);
            return CreateException is not null
                ? Task.FromException<ManagedListingDto>(CreateException)
                : Task.FromResult(Created!);
        }

        public Task<ManagedListingDto> UpdateListingAsync(
            Guid id,
            ListingSubmission submission,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls.Add((id, submission));
            return Task.FromResult(Updated!);
        }

        public Task<IReadOnlyList<ManagedListingDto>> GetMyListingsAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ManagedListingDto> UnpublishListingAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingSearchResultDto> GetListingsAsync(
            int limit,
            string? cursor = null,
            ActiveListingFiltersDto? filters = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingDetailFetchResult> GetListingDetailAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ListingSummaryDto>> GetSimilarListingsAsync(
            Guid id,
            int limit = 4,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ListingContactFetchResult> GetListingContactAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LoginResponse?> DemoLoginAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SearchResultDto<ListingSummaryDto>> SearchByAreaAsync(
            AreaSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto> CreateSavedSearchAsync(
            CreateSavedSearchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SavedSearchDto?> SetSavedSearchAlertAsync(
            Guid id,
            SavedSearchAlertState alertState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteSavedSearchAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

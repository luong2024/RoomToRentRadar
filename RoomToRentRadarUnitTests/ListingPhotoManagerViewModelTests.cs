using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Media;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ListingPhotoManagerViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("2f1c1d4c-9b3a-4c49-9f5e-6a0d8f7c1234");

    [Test]
    public async Task LoadAsync_WhenPhotosExist_FillsTheGalleryAndCountsPhotos()
    {
        var client = new FakePhotosApiClient();
        client.FetchResult = new ListingPhotosFetchResult(ListingPhotosFetchOutcome.Found,
        [
            CreatePhoto(ListingPhotoKind.Photo, isCover: true),
            CreatePhoto(ListingPhotoKind.Panorama)
        ]);
        var viewModel = CreateViewModel(client);
        ApplyListingId(viewModel);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Photos, Has.Count.EqualTo(2));
            Assert.That(viewModel.PhotoCountText, Is.EqualTo("2/20 ảnh"));
            Assert.That(viewModel.HasPhotos, Is.True);
            Assert.That(viewModel.IsEmpty, Is.False);
            Assert.That(viewModel.Lightbox.Photos, Has.Count.EqualTo(2));
            Assert.That(viewModel.IsLoading, Is.False);
            Assert.That(viewModel.ErrorMessage, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WhenTheListingIsMissing_ShowsAnError()
    {
        var client = new FakePhotosApiClient
        {
            FetchResult = new ListingPhotosFetchResult(ListingPhotosFetchOutcome.NotFound, [])
        };
        var viewModel = CreateViewModel(client);
        ApplyListingId(viewModel);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Does.Contain("Không tìm thấy"));
            Assert.That(viewModel.Photos, Is.Empty);
            Assert.That(viewModel.IsEmpty, Is.True);
        });
    }

    [Test]
    public async Task PickPanoramaAsync_WhenUploadSucceeds_AppendsPhotosAndDeletesStagedFiles()
    {
        var client = new FakePhotosApiClient
        {
            UploadResult = new ListingPhotosUploadResult(ListingPhotosUploadOutcome.Uploaded,
                [CreatePhoto(ListingPhotoKind.Panorama)])
        };
        var stagedPath = CreateStagedFile();
        var picker = new FakePhotoPicker([new PickedListingPhoto("360.jpg", "image/jpeg", stagedPath)]);
        var viewModel = CreateViewModel(client, picker);
        ApplyListingId(viewModel);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.PickPanoramaCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.Uploads, Has.Count.EqualTo(1));
            Assert.That(client.Uploads[0].ListingId, Is.EqualTo(ListingId));
            Assert.That(client.Uploads[0].Kind, Is.EqualTo(ListingPhotoKind.Panorama));
            Assert.That(client.Uploads[0].Files, Has.Count.EqualTo(1));
            Assert.That(client.Uploads[0].Files[0].FileName, Is.EqualTo("360.jpg"));
            Assert.That(viewModel.Photos, Has.Count.EqualTo(1));
            Assert.That(viewModel.Photos[0].IsPanorama, Is.True);
            Assert.That(viewModel.StatusMessage, Does.Contain("360°"));
            Assert.That(viewModel.ErrorMessage, Is.Empty);
            Assert.That(File.Exists(stagedPath), Is.False);
        });
    }

    [Test]
    public async Task PickPhotosAsync_WhenTheAccountIsForbidden_ShowsTheOwnershipMessage()
    {
        var client = new FakePhotosApiClient
        {
            UploadResult = new ListingPhotosUploadResult(ListingPhotosUploadOutcome.Forbidden, [])
        };
        var picker = new FakePhotoPicker([new PickedListingPhoto("room.jpg", "image/jpeg", CreateStagedFile())]);
        var viewModel = CreateViewModel(client, picker);
        ApplyListingId(viewModel);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.PickPhotosCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.Uploads, Has.Count.EqualTo(1));
            Assert.That(client.Uploads[0].Kind, Is.EqualTo(ListingPhotoKind.Photo));
            Assert.That(viewModel.ErrorMessage, Does.Contain("chủ phòng"));
            Assert.That(viewModel.Photos, Is.Empty);
        });
    }

    [Test]
    public async Task PickPhotosAsync_WhenTheListingHasTwentyPhotos_DisablesUpload()
    {
        var client = new FakePhotosApiClient();
        client.FetchResult = new ListingPhotosFetchResult(
            ListingPhotosFetchOutcome.Found,
            Enumerable.Range(0, 20).Select(_ => CreatePhoto(ListingPhotoKind.Photo)).ToList());
        var picker = new FakePhotoPicker([new PickedListingPhoto("room.jpg", "image/jpeg", CreateStagedFile())]);
        var viewModel = CreateViewModel(client, picker);
        ApplyListingId(viewModel);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanUpload, Is.False);
            Assert.That(viewModel.PickPhotosCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.PickPanoramaCommand.CanExecute(null), Is.False);
            Assert.That(picker.CallCount, Is.Zero);
        });

        await viewModel.PickPhotosCommand.ExecuteAsync(null);
        Assert.That(client.Uploads, Is.Empty);
    }

    [Test]
    public async Task PickPhotosAsync_WhenTheSelectionExceedsTheLimit_UploadsOnlyTheRemainingSlots()
    {
        var client = new FakePhotosApiClient();
        client.FetchResult = new ListingPhotosFetchResult(
            ListingPhotosFetchOutcome.Found,
            Enumerable.Range(0, 18).Select(_ => CreatePhoto(ListingPhotoKind.Photo)).ToList());
        client.UploadResult = new ListingPhotosUploadResult(ListingPhotosUploadOutcome.Uploaded,
            [CreatePhoto(ListingPhotoKind.Panorama), CreatePhoto(ListingPhotoKind.Panorama)]);
        var picker = new FakePhotoPicker(Enumerable.Range(0, 4)
            .Select(index => new PickedListingPhoto($"pano-{index}.jpg", "image/jpeg", CreateStagedFile()))
            .ToList());
        var viewModel = CreateViewModel(client, picker);
        ApplyListingId(viewModel);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.PickPhotosCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.Uploads, Has.Count.EqualTo(1));
            Assert.That(client.Uploads[0].Files, Has.Count.EqualTo(2));
            Assert.That(viewModel.Photos, Has.Count.EqualTo(20));
            Assert.That(viewModel.StatusMessage, Does.Contain("Bỏ qua 2 ảnh"));
            Assert.That(viewModel.CanUpload, Is.False);
        });
    }

    [Test]
    public async Task OpenPhoto_OpensTheLightboxAtTheChosenPhoto()
    {
        var client = new FakePhotosApiClient();
        client.FetchResult = new ListingPhotosFetchResult(ListingPhotosFetchOutcome.Found,
        [
            CreatePhoto(ListingPhotoKind.Photo, isCover: true),
            CreatePhoto(ListingPhotoKind.Panorama)
        ]);
        var viewModel = CreateViewModel(client);
        ApplyListingId(viewModel);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.OpenPhotoCommand.Execute(viewModel.Photos[1]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Lightbox.IsOpen, Is.True);
            Assert.That(viewModel.Lightbox.CurrentIndex, Is.EqualTo(1));
            Assert.That(viewModel.Lightbox.IsPanoramaMode, Is.True);
        });
    }

    [Test]
    public async Task ApplyQueryAttributes_WithoutAValidId_ShowsAnError()
    {
        var viewModel = CreateViewModel(new FakePhotosApiClient());

        viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = "not-a-guid" });
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ErrorMessage, Does.Contain("Không xác định"));
            Assert.That(viewModel.Photos, Is.Empty);
        });
    }

    private static ListingPhotoManagerViewModel CreateViewModel(
        FakePhotosApiClient client,
        FakePhotoPicker? picker = null)
        => new(client, picker ?? new FakePhotoPicker([]), new FakeNavigationService());

    private static void ApplyListingId(ListingPhotoManagerViewModel viewModel)
        => viewModel.ApplyQueryAttributes(new Dictionary<string, object> { ["id"] = ListingId.ToString() });

    private static string CreateStagedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rtrr-test-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]);
        return path;
    }

    private static ListingPhotoDto CreatePhoto(ListingPhotoKind kind, bool isCover = false)
        => new(
            Guid.NewGuid(),
            ListingId,
            "/media/photo.jpg",
            "/media/photo-thumb.jpg",
            kind == ListingPhotoKind.Panorama ? 4000 : 1600,
            kind == ListingPhotoKind.Panorama ? 2000 : 1067,
            kind,
            null,
            0,
            isCover);

    private sealed class FakePhotoPicker(IReadOnlyList<PickedListingPhoto> photos) : IListingPhotoPicker
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<PickedListingPhoto>?> PickPhotosAsync(
            string title,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<PickedListingPhoto>?>(photos);
        }
    }

    private sealed class FakePhotosApiClient : IPhotosApiClient
    {
        public ListingPhotosFetchResult FetchResult { get; set; } =
            new(ListingPhotosFetchOutcome.Found, []);

        public ListingPhotosUploadResult UploadResult { get; set; } =
            new(ListingPhotosUploadOutcome.Uploaded, []);

        public List<(Guid ListingId, IReadOnlyList<ListingPhotoUpload> Files, ListingPhotoKind Kind)> Uploads { get; } = [];

        public Task<ListingPhotosFetchResult> GetPhotosAsync(
            Guid listingId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(FetchResult);

        public Task<ListingPhotosUploadResult> UploadPhotosAsync(
            Guid listingId,
            IReadOnlyList<ListingPhotoUpload> files,
            ListingPhotoKind kind,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add((listingId, files, kind));
            return Task.FromResult(UploadResult);
        }

        public Task<byte[]?> GetMediaBytesAsync(
            string absoluteUrl,
            CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToRoomDetailAsync(Guid listingId) => Task.CompletedTask;

        public Task GoToLoginAsync() => Task.CompletedTask;

        public Task GoToRegisterAsync() => Task.CompletedTask;

        public Task GoToRoleRootAsync(RoomToRentRadar.Models.AppUserRole? role) => Task.CompletedTask;

        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;

        public Task GoToDirectionsAsync(double destinationLat, double destinationLng, double? originLat, double? originLng)
            => Task.CompletedTask;

        public Task GoToComparisonAsync() => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;
    }
}

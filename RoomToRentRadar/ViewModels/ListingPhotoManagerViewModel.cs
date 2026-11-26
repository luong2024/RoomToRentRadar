using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Media;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.ViewModels;

public partial class ListingPhotoManagerViewModel(
    IPhotosApiClient photosApiClient,
    IListingPhotoPicker photoPicker,
    INavigationService navigation) : ObservableObject, IQueryAttributable
{
    // Mirrors PhotoUploadLimits on the backend; the API re-validates every request.
    private const int MaxFilesPerUpload = 10;
    private const int MaxPhotosPerListing = 20;

    private Guid? _listingId;

    public ObservableCollection<GalleryPhotoItem> Photos { get; } = [];

    public PhotoLightboxViewModel Lightbox { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickPhotosCommand), nameof(PickPanoramaCommand))]
    public partial bool IsUploading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool HasPhotos => Photos.Count > 0;

    public bool IsEmpty => !IsLoading && Photos.Count == 0;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool CanUpload => !IsUploading && Photos.Count < MaxPhotosPerListing;

    public string PhotoCountText => $"{Photos.Count}/{MaxPhotosPerListing} ảnh";

    public string UploadHintText =>
        $"Ảnh JPG hoặc PNG, tối đa 10 MB mỗi ảnh. Mỗi lần tải lên tối đa {MaxFilesPerUpload} ảnh, " +
        $"mỗi tin tối đa {MaxPhotosPerListing} ảnh.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _listingId = query.TryGetValue("id", out var value)
            && Guid.TryParse(value?.ToString(), out var id)
                ? id
                : null;

        if (_listingId is null)
        {
            ErrorMessage = "Không xác định được tin đăng cần quản lý ảnh.";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_listingId is not { } listingId || IsLoading)
            return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await photosApiClient.GetPhotosAsync(listingId, cancellationToken);
            Photos.Clear();

            switch (result.Outcome)
            {
                case ListingPhotosFetchOutcome.NotFound:
                    ErrorMessage = "Không tìm thấy tin đăng này.";
                    break;

                case ListingPhotosFetchOutcome.Unavailable:
                    ErrorMessage = "Tin đăng hiện không còn khả dụng.";
                    break;

                default:
                    foreach (var photo in result.Photos)
                    {
                        Photos.Add(new GalleryPhotoItem(photo));
                    }

                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ErrorMessage = "Chưa tải được thư viện ảnh. Kiểm tra kết nối mạng rồi thử lại.";
        }
        finally
        {
            IsLoading = false;
            RefreshPhotoState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpload), AllowConcurrentExecutions = false)]
    private Task PickPhotosAsync(CancellationToken cancellationToken)
        => PickAndUploadAsync(ListingPhotoKind.Photo, "Chọn ảnh thường", cancellationToken);

    [RelayCommand(CanExecute = nameof(CanUpload), AllowConcurrentExecutions = false)]
    private Task PickPanoramaAsync(CancellationToken cancellationToken)
        => PickAndUploadAsync(ListingPhotoKind.Panorama, "Chọn ảnh 360°", cancellationToken);

    [RelayCommand]
    private void OpenPhoto(GalleryPhotoItem? photo)
    {
        if (Photos.Count == 0)
            return;

        var index = photo is null ? 0 : Photos.IndexOf(photo);
        Lightbox.OpenCommand.Execute(index < 0 ? 0 : index);
    }

    [RelayCommand]
    private Task GoBackAsync() => navigation.GoBackAsync();

    private async Task PickAndUploadAsync(
        ListingPhotoKind kind,
        string pickerTitle,
        CancellationToken cancellationToken)
    {
        if (_listingId is not { } listingId || IsUploading)
            return;

        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        IReadOnlyList<PickedListingPhoto>? picked;
        try
        {
            picked = await photoPicker.PickPhotosAsync(pickerTitle, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (InvalidDataException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể chọn ảnh. Kiểm tra quyền truy cập và thử lại.";
            return;
        }

        if (picked is null || picked.Count == 0)
            return;

        var remaining = MaxPhotosPerListing - Photos.Count;
        var accepted = picked
            .Take(Math.Min(MaxFilesPerUpload, Math.Max(remaining, 0)))
            .ToList();
        var skipped = picked.Count - accepted.Count;

        try
        {
            if (accepted.Count == 0)
            {
                ErrorMessage = $"Tin đăng đã đạt tối đa {MaxPhotosPerListing} ảnh.";
                return;
            }

            IsUploading = true;

            var files = accepted
                .Select(photo => new ListingPhotoUpload(
                    photo.FileName,
                    photo.ContentType,
                    () => Task.FromResult<Stream>(File.OpenRead(photo.Path))))
                .ToList();

            var result = await photosApiClient.UploadPhotosAsync(
                listingId,
                files,
                kind,
                cancellationToken);

            if (result.Outcome != ListingPhotosUploadOutcome.Uploaded)
            {
                ErrorMessage = DescribeUploadFailure(result.Outcome);
                return;
            }

            foreach (var photo in result.Photos)
            {
                Photos.Add(new GalleryPhotoItem(photo));
            }

            StatusMessage = kind == ListingPhotoKind.Panorama
                ? $"Đã tải lên {result.Photos.Count} ảnh 360°."
                : $"Đã tải lên {result.Photos.Count} ảnh.";

            if (skipped > 0)
            {
                StatusMessage += $" Bỏ qua {skipped} ảnh vượt giới hạn {MaxPhotosPerListing} ảnh mỗi tin.";
            }

            RefreshPhotoState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ErrorMessage = "Tải ảnh thất bại. Vui lòng thử lại.";
        }
        finally
        {
            foreach (var photo in picked)
            {
                TryDelete(photo.Path);
            }

            IsUploading = false;
        }
    }

    private void RefreshPhotoState()
    {
        Lightbox.SetPhotos(Photos.ToList());
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(CanUpload));
        OnPropertyChanged(nameof(PhotoCountText));
        PickPhotosCommand.NotifyCanExecuteChanged();
        PickPanoramaCommand.NotifyCanExecuteChanged();
    }

    private static string DescribeUploadFailure(ListingPhotosUploadOutcome outcome) => outcome switch
    {
        ListingPhotosUploadOutcome.Unauthorized => "Phiên đăng nhập đã hết hạn. Đăng nhập lại để tải ảnh.",
        ListingPhotosUploadOutcome.Forbidden => "Chỉ tài khoản chủ phòng mới được tải ảnh lên.",
        ListingPhotosUploadOutcome.ListingNotFound => "Không tìm thấy tin đăng hoặc bạn không phải chủ tin.",
        ListingPhotosUploadOutcome.ListingUnavailable => "Tin đăng hiện không còn khả dụng.",
        ListingPhotosUploadOutcome.InvalidPhoto => "Ảnh không hợp lệ. Chỉ nhận ảnh JPG hoặc PNG nguyên khung hình.",
        ListingPhotosUploadOutcome.PhotoLimitExceeded => $"Tin đăng đã đạt tối đa {MaxPhotosPerListing} ảnh.",
        ListingPhotosUploadOutcome.PhotoTooLarge => "Mỗi ảnh chỉ được tối đa 10 MB.",
        ListingPhotosUploadOutcome.UnsupportedType => "Chỉ hỗ trợ ảnh JPG hoặc PNG.",
        _ => "Tải ảnh thất bại. Vui lòng thử lại."
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));
}

using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

/// <summary>
/// Create and edit screen for a landlord listing (US08 AC1, AC2, AC4). All
/// platform work (file picker, navigation) is injected so the class is
/// unit-testable.
/// </summary>
public partial class ListingEditorViewModel : ObservableObject, IQueryAttributable
{
    public const string ListingIdQueryKey = "id";

    public const string CreatePageTitle = "Đăng tin mới";

    public const string EditPageTitle = "Sửa tin đăng";

    private const string DefaultLatText = "10.7721";
    private const string DefaultLngText = "106.6579";

    private readonly IApiClient _apiClient;
    private readonly IPhotoPicker _photoPicker;
    private readonly INavigationService _navigationService;

    private Guid? _listingId;
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubmitText))]
    public partial bool IsCreateMode { get; set; }

    [ObservableProperty]
    public partial string PageTitle { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; }

    [ObservableProperty]
    public partial string PriceText { get; set; }

    [ObservableProperty]
    public partial string DepositText { get; set; }

    [ObservableProperty]
    public partial string AreaText { get; set; }

    [ObservableProperty]
    public partial string BedroomsText { get; set; }

    [ObservableProperty]
    public partial string DescriptionText { get; set; }

    [ObservableProperty]
    public partial string LatText { get; set; }

    [ObservableProperty]
    public partial string LngText { get; set; }

    [ObservableProperty]
    public partial string LandlordNameText { get; set; }

    [ObservableProperty]
    public partial string LandlordPhoneText { get; set; }

    [ObservableProperty]
    public partial RoomType? SelectedRoomType { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitleError))]
    public partial string TitleError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddressError))]
    public partial string AddressError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPriceError))]
    public partial string PriceError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDepositError))]
    public partial string DepositError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAreaError))]
    public partial string AreaError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomTypeError))]
    public partial string RoomTypeError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLatError))]
    public partial string LatError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLngError))]
    public partial string LngError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotosError))]
    public partial string PhotosError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccessMessage))]
    public partial string SuccessMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSignInRequired { get; set; }

    public ListingEditorViewModel(
        IApiClient apiClient,
        IPhotoPicker photoPicker,
        INavigationService navigationService)
    {
        _apiClient = apiClient;
        _photoPicker = photoPicker;
        _navigationService = navigationService;

        PageTitle = CreatePageTitle;
        Title = string.Empty;
        Address = string.Empty;
        PriceText = string.Empty;
        DepositText = string.Empty;
        AreaText = string.Empty;
        BedroomsText = "1";
        DescriptionText = string.Empty;
        LatText = DefaultLatText;
        LngText = DefaultLngText;
        LandlordNameText = string.Empty;
        LandlordPhoneText = string.Empty;
        TitleError = string.Empty;
        AddressError = string.Empty;
        PriceError = string.Empty;
        DepositError = string.Empty;
        AreaError = string.Empty;
        RoomTypeError = string.Empty;
        LatError = string.Empty;
        LngError = string.Empty;
        PhotosError = string.Empty;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        IsCreateMode = true;

        RoomTypeOptions = BuildRoomTypeOptions();

        foreach (var option in RoomTypeOptions)
            option.IsSelected = option.Value == SelectedRoomType;

        Photos.CollectionChanged += (_, _) => UpdatePhotoState();
        UpdatePhotoState();
    }

    public ObservableCollection<EditorPhoto> Photos { get; } = new();

    public IReadOnlyList<RoomTypeOption> RoomTypeOptions { get; }

    /// <summary>Photos already stored on the server; they cannot be deleted here.</summary>
    public int ExistingPhotoCount { get; private set; }

    /// <summary>Delay before leaving the page so the success note stays readable.</summary>
    public TimeSpan SuccessMessageDelay { get; set; } = TimeSpan.FromMilliseconds(900);

    public string SubmitText => IsCreateMode ? "Gửi duyệt" : "Lưu thay đổi";

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value) => PickPhotosCommand.NotifyCanExecuteChanged();

    public bool HasTitleError => !string.IsNullOrEmpty(TitleError);

    public bool HasAddressError => !string.IsNullOrEmpty(AddressError);

    public bool HasPriceError => !string.IsNullOrEmpty(PriceError);

    public bool HasDepositError => !string.IsNullOrEmpty(DepositError);

    public bool HasAreaError => !string.IsNullOrEmpty(AreaError);

    public bool HasRoomTypeError => !string.IsNullOrEmpty(RoomTypeError);

    public bool HasLatError => !string.IsNullOrEmpty(LatError);

    public bool HasLngError => !string.IsNullOrEmpty(LngError);

    public bool HasPhotosError => !string.IsNullOrEmpty(PhotosError);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasSuccessMessage => !string.IsNullOrEmpty(SuccessMessage);

    public bool HasPhotos => Photos.Count > 0;

    public int NewPhotoCount => Photos.Count(photo => photo.IsNew);

    public string PhotoCountText =>
        $"{ExistingPhotoCount + NewPhotoCount}/{ListingPhotoRules.MinPhotosOnCreate} ảnh";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.TryGetValue(ListingIdQueryKey, out var value)
            && Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var listingId))
        {
            _listingId = listingId;
            IsCreateMode = false;
            PageTitle = EditPageTitle;

            // Shell does not guarantee that query attributes arrive before
            // OnAppearing, so an already completed load is restarted here.
            _isLoaded = false;
            LoadCommand.Execute(null);
            return;
        }

        _listingId = null;
        IsCreateMode = true;
        PageTitle = CreatePageTitle;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || _isLoaded)
            return;

        _isLoaded = true;

        // A new listing has nothing to load from the server.
        if (_listingId is not { } listingId)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var listing = await _apiClient.GetManagedListingAsync(listingId, cancellationToken);
            Prefill(listing);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsSignInRequired = true;
            ErrorMessage = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (ApiException ex) when (ex.Code == "listing_not_found")
        {
            ErrorMessage = "Tin đăng không còn tồn tại.";
        }
        catch (Exception)
        {
            ErrorMessage = "Không tải được tin đăng. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanPickPhotos))]
    private async Task PickPhotosAsync()
    {
        PhotosError = string.Empty;
        ErrorMessage = string.Empty;

        IReadOnlyList<PickedPhoto> picked;

        try
        {
            picked = await _photoPicker.PickPhotosAsync();
        }
        catch (Exception)
        {
            PhotosError = "Không mở được thư viện ảnh. Vui lòng thử lại.";
            return;
        }

        if (picked.Count == 0)
            return;

        var skipped = 0;

        foreach (var photo in picked)
        {
            if (NewPhotoCount >= ListingPhotoRules.MaxPhotosPerRequest)
            {
                skipped++;
                continue;
            }

            if (!ListingPhotoRules.IsAllowedType(photo.ContentType, photo.FileName)
                || !ListingPhotoRules.IsWithinSizeLimit(photo.Length))
            {
                skipped++;
                continue;
            }

            Photos.Add(new EditorPhoto(
                photo.FileName,
                isNew: true,
                photoId: null,
                CreatePreview(photo.OpenReadAsync),
                photo.OpenReadAsync,
                photo.ContentType,
                photo.Length,
                candidate => RemovePhoto(candidate)));
        }

        if (skipped > 0)
        {
            PhotosError = $"Đã bỏ qua {skipped} ảnh: chỉ nhận JPG hoặc PNG, tối đa 10MB mỗi ảnh "
                          + $"và {ListingPhotoRules.MaxPhotosPerRequest} ảnh mới mỗi lần lưu.";
        }

        UpdatePhotoState();
    }

    private bool CanPickPhotos() => !IsBusy && NewPhotoCount < ListingPhotoRules.MaxPhotosPerRequest;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        ClearFieldErrors();
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        var state = BuildFormState();
        var errors = ListingFormValidator.Validate(state, IsCreateMode);

        if (errors.Count > 0)
        {
            ApplyFieldErrors(errors);
            ErrorMessage = "Vui lòng kiểm tra lại các thông tin được đánh dấu.";
            return;
        }

        if (!ListingFormValidator.TryReadValues(state, out var values)
            || state.RoomType is not { } roomType)
        {
            ErrorMessage = "Vui lòng kiểm tra lại các thông tin được đánh dấu.";
            return;
        }

        var submission = new ListingSubmission(
            state.Title!.Trim(),
            state.Address!.Trim(),
            values.Price,
            values.Deposit,
            values.AreaM2,
            roomType,
            values.Bedrooms,
            string.IsNullOrWhiteSpace(state.DescriptionText) ? null : state.DescriptionText.Trim(),
            values.Lat,
            values.Lng,
            string.IsNullOrWhiteSpace(state.LandlordNameText) ? null : state.LandlordNameText.Trim(),
            string.IsNullOrWhiteSpace(state.LandlordPhoneText) ? null : state.LandlordPhoneText.Trim(),
            BuildPhotoUploads());

        IsBusy = true;

        try
        {
            var wasCreate = IsCreateMode || _listingId is null;

            if (wasCreate)
                await _apiClient.CreateListingAsync(submission, cancellationToken);
            else
                await _apiClient.UpdateListingAsync(_listingId!.Value, submission, cancellationToken);

            SuccessMessage = wasCreate
                ? "Tin đã được gửi duyệt. Tin sẽ hiển thị với sinh viên sau khi được kiểm duyệt."
                : "Đã lưu thay đổi. Tin sẽ được kiểm duyệt lại trước khi hiển thị.";

            if (SuccessMessageDelay > TimeSpan.Zero)
                await Task.Delay(SuccessMessageDelay, cancellationToken);

            await _navigationService.GoBackAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ListingSaveException ex)
        {
            ApplyServerErrors(ex);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsSignInRequired = true;
            ErrorMessage = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (Exception)
        {
            ErrorMessage = "Không lưu được tin đăng. Vui lòng thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigationService.GoBackAsync();

    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            await _navigationService.GoToLoginAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Không mở được trang đăng nhập.";
        }
    }

    [RelayCommand]
    private void RemovePhoto(EditorPhoto? photo)
    {
        // Photos already stored on the server stay read-only (US08 scope).
        if (photo is null || !photo.IsRemovable)
            return;

        Photos.Remove(photo);
        UpdatePhotoState();
    }

    private void SelectRoomType(RoomTypeOption option)
    {
        SelectedRoomType = option.Value;

        foreach (var candidate in RoomTypeOptions)
            candidate.IsSelected = candidate == option;

        RoomTypeError = string.Empty;
    }

    private IReadOnlyList<RoomTypeOption> BuildRoomTypeOptions()
    {
        var labels = new (string Label, RoomType Value)[]
        {
            ("Phòng đơn", RoomType.SingleRoom),
            ("Ở ghép", RoomType.SharedRoom),
            ("Studio", RoomType.Studio),
            ("Căn hộ", RoomType.Apartment),
            ("Nhà", RoomType.House)
        };

        return labels
            .Select(pair => new RoomTypeOption(pair.Label, pair.Value, SelectRoomType))
            .ToList();
    }

    private void Prefill(ManagedListingDto listing)
    {
        Title = listing.Title;
        Address = listing.Address;
        PriceText = listing.Price.ToString(CultureInfo.InvariantCulture);
        DepositText = listing.Deposit.ToString(CultureInfo.InvariantCulture);
        AreaText = listing.AreaM2.ToString("0.##", CultureInfo.InvariantCulture);
        BedroomsText = listing.Bedrooms.ToString(CultureInfo.InvariantCulture);
        DescriptionText = listing.Description ?? string.Empty;
        LatText = double.IsFinite(listing.Lat)
            ? listing.Lat.ToString("0.####", CultureInfo.InvariantCulture)
            : DefaultLatText;
        LngText = double.IsFinite(listing.Lng)
            ? listing.Lng.ToString("0.####", CultureInfo.InvariantCulture)
            : DefaultLngText;
        LandlordNameText = listing.LandlordName ?? string.Empty;
        SelectedRoomType = listing.RoomType;

        foreach (var option in RoomTypeOptions)
            option.IsSelected = option.Value == listing.RoomType;

        Photos.Clear();

        foreach (var photo in listing.Photos)
        {
            Photos.Add(new EditorPhoto(
                fileName: $"Ảnh {photo.SortOrder + 1}",
                isNew: false,
                photo.Id,
                CreatePreview(photo),
                openReadAsync: null,
                contentType: null,
                length: null));
        }

        ExistingPhotoCount = listing.Photos.Count;
        UpdatePhotoState();
    }

    private ListingFormState BuildFormState() => new()
    {
        Title = Title,
        Address = Address,
        PriceText = PriceText,
        DepositText = DepositText,
        AreaText = AreaText,
        BedroomsText = BedroomsText,
        DescriptionText = DescriptionText,
        LatText = LatText,
        LngText = LngText,
        LandlordNameText = LandlordNameText,
        LandlordPhoneText = LandlordPhoneText,
        RoomType = SelectedRoomType,
        ExistingPhotoCount = ExistingPhotoCount,
        NewPhotos = Photos
            .Where(photo => photo.IsNew)
            .Select(photo => photo.ToCandidate())
            .ToList()
    };

    private IReadOnlyList<ListingPhotoUploadModel> BuildPhotoUploads()
        => Photos
            .Where(photo => photo is { IsNew: true, OpenReadAsync: not null })
            .Select(photo => new ListingPhotoUploadModel(
                photo.FileName,
                ListingPhotoRules.Normalize(photo.ContentType)
                    ?? ListingPhotoRules.FromExtension(photo.FileName)
                    ?? ListingPhotoRules.Jpeg,
                photo.Length,
                photo.OpenReadAsync!))
            .ToList();

    private void UpdatePhotoState()
    {
        OnPropertyChanged(nameof(NewPhotoCount));
        OnPropertyChanged(nameof(PhotoCountText));
        OnPropertyChanged(nameof(HasPhotos));
        PickPhotosCommand.NotifyCanExecuteChanged();
    }

    private void ClearFieldErrors()
    {
        TitleError = string.Empty;
        AddressError = string.Empty;
        PriceError = string.Empty;
        DepositError = string.Empty;
        AreaError = string.Empty;
        RoomTypeError = string.Empty;
        LatError = string.Empty;
        LngError = string.Empty;
        PhotosError = string.Empty;
    }

    private void ApplyFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        TitleError = Read(errors, ListingFieldKeys.Title);
        AddressError = Read(errors, ListingFieldKeys.Address);
        PriceError = Read(errors, ListingFieldKeys.Price);
        DepositError = Read(errors, ListingFieldKeys.Deposit);
        AreaError = Read(errors, ListingFieldKeys.Area);
        RoomTypeError = Read(errors, ListingFieldKeys.RoomType);
        LatError = Read(errors, ListingFieldKeys.Lat);
        LngError = Read(errors, ListingFieldKeys.Lng);
        PhotosError = Read(errors, ListingFieldKeys.Photos);

        if (errors.TryGetValue(ListingFieldKeys.Bedrooms, out var bedrooms) && !string.IsNullOrEmpty(bedrooms))
            ErrorMessage = bedrooms;
    }

    private void ApplyServerErrors(ListingSaveException exception)
    {
        var errors = exception.FieldErrors.ToDictionary(
            pair => pair.Key,
            pair => string.Join(" ", pair.Value),
            StringComparer.OrdinalIgnoreCase);

        ApplyFieldErrors(errors);

        if (string.IsNullOrEmpty(TitleError)
            && string.IsNullOrEmpty(AddressError)
            && string.IsNullOrEmpty(PriceError)
            && string.IsNullOrEmpty(DepositError)
            && string.IsNullOrEmpty(AreaError)
            && string.IsNullOrEmpty(RoomTypeError)
            && string.IsNullOrEmpty(LatError)
            && string.IsNullOrEmpty(LngError)
            && string.IsNullOrEmpty(PhotosError))
        {
            ErrorMessage = exception.Code switch
            {
                "too_few_photos" => "Cần ít nhất 3 ảnh để gửi duyệt.",
                "invalid_listing_photo" => "Ảnh không hợp lệ. Vui lòng chọn ảnh JPG hoặc PNG khác.",
                "photo_too_large" => "Mỗi ảnh phải nhỏ hơn hoặc bằng 10MB.",
                "unsupported_photo_type" => "Chỉ nhận ảnh JPG hoặc PNG.",
                "photo_limit_exceeded" => "Tin đăng đã đạt số ảnh tối đa.",
                "invalid_listing_transition" => "Không thể cập nhật tin đăng ở trạng thái hiện tại.",
                "listing_not_found" => "Tin đăng không còn tồn tại.",
                _ => exception.Message
            };
        }
        else
        {
            ErrorMessage = "Vui lòng kiểm tra lại các thông tin được đánh dấu.";
        }
    }

    private static string Read(IReadOnlyDictionary<string, string> errors, string key)
        => errors.TryGetValue(key, out var message) ? message : string.Empty;

    private static ImageSource? CreatePreview(Func<Task<Stream>> openReadAsync)
    {
        try
        {
            return ImageSource.FromStream(() => openReadAsync().GetAwaiter().GetResult());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ImageSource? CreatePreview(ListingPhotoDto photo)
    {
        var url = ListingPhotoUrl.Resolve(photo.ThumbUrl) ?? ListingPhotoUrl.Resolve(photo.Url);

        return url is not null && Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? ImageSource.FromUri(absolute)
            : null;
    }
}

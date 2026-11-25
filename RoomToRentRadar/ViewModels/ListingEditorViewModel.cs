using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Models;
using RoomToRentRadar.Validations;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.ViewModels;

public partial class ListingEditorViewModel(IListingManagementClient client, AuthService auth,
    INavigationService navigation, IAlertPrompt alerts,
    IDeviceLocationService? locationService = null) : ObservableObject, IQueryAttributable
{
    private const string LocatingProgressMessage = "Đang lấy vị trí GPS chính xác…";
    private Guid? _listingId;
    private bool _loaded;
    private bool _released;
    private bool _confirming;

    public event EventHandler<(double Latitude, double Longitude)>? CurrentLocationResolved;
    public ListingFormField TitleField { get; } = new();
    public ListingFormField AddressField { get; } = new();
    public ListingFormField RentField { get; } = new();
    public ListingFormField DepositField { get; } = new();
    public ListingFormField AreaField { get; } = new();
    public ListingFormField LatitudeField { get; } = new();
    public ListingFormField LongitudeField { get; } = new();
    public ListingFormField DescriptionField { get; } = new();
    public ListingFormField QuietHoursField { get; } = new();
    public ListingFormField GuestPolicyField { get; } = new();
    public ListingFormField CookingRulesField { get; } = new();
    public ListingFormField ParkingRulesField { get; } = new();
    public ObservableCollection<SelectedListingPhoto> SelectedPhotos { get; } = [];
    public ObservableCollection<ExistingListingPhoto> ExistingPhotos { get; } = [];
    public IReadOnlyList<RoomTypeOption> RoomTypes { get; } =
    [new(RoomType.SingleRoom, "Phòng riêng"), new(RoomType.SharedRoom, "Phòng ở ghép"),
     new(RoomType.Studio, "Studio"), new(RoomType.Apartment, "Căn hộ"), new(RoomType.House, "Nhà nguyên căn")];

    [ObservableProperty] public partial RoomTypeOption? SelectedRoomType { get; set; }
    [ObservableProperty] public partial string RoomTypeError { get; set; } = string.Empty;
    [ObservableProperty] public partial string PhotoError { get; set; } = string.Empty;
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = "Tin mới sẽ được gửi chờ duyệt.";
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy), nameof(CanSaveListing), nameof(CanDeleteWhenIdle), nameof(CanUseCurrentLocation))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(PickPhotosCommand), nameof(UnpublishCommand), nameof(PublishCommand), nameof(DeleteCommand), nameof(UseCurrentLocationCommand))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveListing), nameof(CanUseCurrentLocation))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(UseCurrentLocationCommand))]
    public partial bool CanSave { get; set; } = true;
    [ObservableProperty] public partial bool CanUnpublish { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    public partial bool CanPublish { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLocating), nameof(CanUseCurrentLocation))]
    [NotifyCanExecuteChangedFor(nameof(UseCurrentLocationCommand))]
    public partial bool IsLocating { get; set; }
    [ObservableProperty] public partial string LocationStatusMessage { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HouseRulesVersionText))]
    public partial long? HouseRulesVersion { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HouseRulesVersionText))]
    public partial DateTime? HouseRulesUpdatedAt { get; set; }
    public bool IsNotBusy => !IsBusy;
    public bool IsNotLocating => !IsLocating;
    public bool CanSaveListing => IsNotBusy && CanSave;
    public bool CanUseCurrentLocation => IsNotBusy && !IsLocating && CanSave;
    public bool IsEditMode => _listingId is not null;
    public bool CanDelete => IsEditMode && _loaded;
    public bool CanDeleteWhenIdle => IsNotBusy && CanDelete;
    public string PageTitle => _listingId is null ? "Đăng tin cho thuê" : "Chỉnh sửa tin";
    public string PhotoCountText => $"{ExistingPhotos.Count + SelectedPhotos.Count} ảnh · cần ít nhất 3 ảnh";
    public const int HouseRulesMaxLength = 300;
    public string HouseRulesVersionText => HouseRulesVersion is { } version
        ? HouseRulesUpdatedAt is { } updatedAt
            ? $"Phiên bản {version} · cập nhật {updatedAt.ToLocalTime():dd/MM/yyyy HH:mm}"
            : $"Phiên bản {version}"
        : "Chưa thiết lập nội quy — lần lưu đầu tiên sẽ tạo phiên bản 1.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
            _listingId = id;
        NotifyEditStateChanged();
    }

    [RelayCommand]
    private Task SignInAsync() => navigation.GoToLoginAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || _released || _confirming) return;
        IsBusy = true;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            if (!IsAuthenticated) { SetLoaded(false); return; }
            if (_loaded || _listingId is null) return;
            var listing = await client.GetManagedAsync(_listingId.Value, cancellationToken);
            TitleField.Text = listing.Title;
            AddressField.Text = listing.Address;
            RentField.Text = listing.Price.ToString(CultureInfo.InvariantCulture);
            DepositField.Text = listing.Deposit.ToString(CultureInfo.InvariantCulture);
            AreaField.Text = listing.AreaM2.ToString(CultureInfo.InvariantCulture);
            LatitudeField.Text = listing.Lat.ToString(CultureInfo.InvariantCulture);
            LongitudeField.Text = listing.Lng.ToString(CultureInfo.InvariantCulture);
            DescriptionField.Text = listing.Description;
            SelectedRoomType = RoomTypes.First(option => option.Value == listing.RoomType);
            ApplySavedListing(listing);
            ApplyHouseRules(listing.HouseRules);
            SetLoaded(true);
            Message = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task PickPhotosAsync()
    {
        if (_released || _confirming) return;
        IsBusy = true;
        PhotoError = string.Empty;
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn ảnh JPG hoặc PNG",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["image/jpeg", "image/png"],
                    [DevicePlatform.iOS] = ["public.jpeg", "public.png"],
                    [DevicePlatform.MacCatalyst] = ["public.jpeg", "public.png"],
                    [DevicePlatform.WinUI] = [".jpg", ".jpeg", ".png"]
                })
            });
            if (files is null || _released) return;
            foreach (var file in files)
            {
                if (_released) break;
                if (file is null) continue;
                if (SelectedPhotos.Count >= 10 || ExistingPhotos.Count + SelectedPhotos.Count >= 20)
                {
                    PhotoError = "Chọn tối đa 10 ảnh mới mỗi lần lưu, tổng cộng tối đa 20 ảnh.";
                    break;
                }
                var fileName = file.FileName ?? string.Empty;
                var extension = Path.GetExtension(fileName);
                var path = Path.Combine(FileSystem.CacheDirectory, $"listing-{Guid.NewGuid():N}{extension}");
                try
                {
                    string contentType;
                    await using (var input = await file.OpenReadAsync())
                    await using (var output = File.Create(path))
                        contentType = await ListingPhotoSelectionValidator.CopyValidatedAsync(fileName, input, output);
                    SelectedListingPhoto? photo = null;
                    photo = new SelectedListingPhoto(fileName, contentType, path, new RelayCommand(() =>
                    {
                        if (IsBusy || photo is null) return;
                        SelectedPhotos.Remove(photo);
                        TryDelete(path);
                        OnPropertyChanged(nameof(PhotoCountText));
                    }));
                    SelectedPhotos.Add(photo);
                    OnPropertyChanged(nameof(PhotoCountText));
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    TryDelete(path);
                    PhotoError = $"{file.FileName}: {ex.Message}";
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { PhotoError = "Không thể chọn ảnh. Kiểm tra quyền truy cập và thử lại."; }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(CanSaveListing), AllowConcurrentExecutions = false)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_released || !CanSave || _confirming) return;
        Message = string.Empty;
        if (_listingId is not null && !_loaded)
        {
            Message = "Tải lại thông tin tin đăng trước khi lưu thay đổi.";
            return;
        }
        var fields = new ListingFormFields(TitleField.Text, AddressField.Text, RentField.Text, DepositField.Text,
            AreaField.Text, SelectedRoomType?.Value, LatitudeField.Text, LongitudeField.Text, DescriptionField.Text);
        var errors = ListingFormValidator.Validate(fields, ExistingPhotos.Count + SelectedPhotos.Count);
        SetErrors(errors);
        if (errors.Count > 0) { Message = "Kiểm tra các trường được đánh dấu bên dưới."; return; }
        ListingFormValidator.TryMoney(fields.Rent, out var rent);
        ListingFormValidator.TryMoney(fields.Deposit, out var deposit);
        ListingFormValidator.TryNumber(fields.Area, out var area);
        ListingFormValidator.TryNumber(fields.Latitude, out var latitude);
        ListingFormValidator.TryNumber(fields.Longitude, out var longitude);
        var submission = new ListingFormSubmission(fields.Title.Trim(), fields.Address.Trim(), rent, deposit,
            area, fields.RoomType!.Value, latitude, longitude, SelectedPhotos.Select(photo => photo.ToUpload()).ToArray(),
            fields.Description.Trim());
        IsBusy = true;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            if (!IsAuthenticated) { SetLoaded(false); Message = "Đăng nhập để lưu tin."; return; }
            var saved = _listingId is { } id
                ? await client.UpdateAsync(id, submission, cancellationToken)
                : await client.CreateAsync(submission, cancellationToken);
            _listingId = saved.Id;
            SetLoaded(true);
            ClearSelectedPhotos();
            ApplySavedListing(saved);
            _released = true;
            Message = saved.Status == ListingStatus.PendingReview
                ? "Đã lưu tin. Tin đang chờ duyệt trước khi hiển thị công khai."
                : "Đã lưu thay đổi.";
            NotifyEditStateChanged();
            await navigation.GoBackAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task UnpublishAsync(CancellationToken cancellationToken)
    {
        if (_released || _listingId is null || !CanUnpublish || IsBusy || _confirming) return;
        bool confirmed;
        _confirming = true;
        try
        {
            confirmed = await alerts.ConfirmAsync(
                "Gỡ đăng tin?",
                "Tin sẽ không còn hiển thị công khai. Bạn vẫn có thể chỉnh sửa tin.",
                "Gỡ đăng",
                "Hủy",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _confirming = false;
        }

        if (!confirmed || _released || IsBusy) return;
        IsBusy = true;
        try
        {
            ApplySavedListing(await client.UnpublishAsync(_listingId.Value, cancellationToken));
            Message = "Đã gỡ đăng tin.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (_released || _listingId is null || !CanPublish || _confirming) return;
        IsBusy = true;
        try
        {
            ApplySavedListing(await client.PublishAsync(_listingId.Value, cancellationToken));
            Message = "Đã gửi duyệt lại tin đăng. Tin đang chờ duyệt trước khi hiển thị công khai.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteWhenIdle), AllowConcurrentExecutions = false)]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (_released || _listingId is null || IsBusy || _confirming) return;
        if (!_loaded)
        {
            Message = "Tải lại thông tin tin đăng trước khi xóa.";
            return;
        }

        bool confirmed;
        _confirming = true;
        try
        {
            confirmed = await alerts.ConfirmAsync(
                "Xóa phòng cho thuê?",
                "Tin đăng và toàn bộ hình ảnh sẽ bị xóa vĩnh viễn. Thao tác này không thể hoàn tác.",
                "Xóa tin",
                "Hủy",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _confirming = false;
        }

        if (!confirmed || _released || IsBusy) return;
        IsBusy = true;
        try
        {
            await client.DeleteAsync(_listingId.Value, cancellationToken);
            _released = true;
            ClearSelectedPhotos();
            Message = "Đã xóa tin đăng.";
            await navigation.GoBackAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(CanUseCurrentLocation), AllowConcurrentExecutions = false)]
    private async Task UseCurrentLocationAsync(CancellationToken cancellationToken)
    {
        if (_released || !CanSave || IsBusy || _confirming) return;
        if (locationService is null)
        {
            LocationStatusMessage = "Thiết bị này chưa hỗ trợ định vị GPS.";
            return;
        }

        IsLocating = true;
        LatitudeField.Error = string.Empty;
        LongitudeField.Error = string.Empty;
        LocationStatusMessage = LocatingProgressMessage;
        try
        {
            var result = await locationService.GetCurrentLocationAsync(cancellationToken);
            if (_released) return;

            switch (result.Status)
            {
                case DeviceLocationStatus.Success when result.Latitude is { } lat && result.Longitude is { } lng:
                    LatitudeField.Text = lat.ToString("F6", CultureInfo.InvariantCulture);
                    LongitudeField.Text = lng.ToString("F6", CultureInfo.InvariantCulture);
                    LocationStatusMessage = result.AccuracyMeters is { } accuracy && accuracy > 0
                        ? $"Đã chọn vị trí hiện tại (độ chính xác ±{Math.Max(1, Math.Round(accuracy)):0} m)."
                        : "Đã chọn vị trí GPS hiện tại.";
                    CurrentLocationResolved?.Invoke(this, (lat, lng));
                    break;

                case DeviceLocationStatus.LocationDisabled:
                    LocationStatusMessage = "GPS / Dịch vụ vị trí đang tắt. Hãy bật Vị trí rồi bấm định vị lại.";
                    IsLocating = false;
                    await PromptOpenLocationSettingsAsync(
                        "Bật định vị GPS?",
                        "Dịch vụ Vị trí (GPS) trên thiết bị đang tắt. Bạn có muốn mở Cài đặt để bật GPS ngay không?",
                        "Bật GPS",
                        "Để sau",
                        cancellationToken);
                    break;

                case DeviceLocationStatus.PermissionDenied:
                    LocationStatusMessage = "Chưa có quyền truy cập vị trí chính xác. Cấp quyền Vị trí trong Cài đặt rồi thử lại.";
                    IsLocating = false;
                    await PromptOpenLocationSettingsAsync(
                        "Cấp quyền Vị trí?",
                        "Room Radar cần quyền Vị trí chính xác để tự động điền tọa độ phòng. Mở Cài đặt ngay?",
                        "Mở Cài đặt",
                        "Đóng",
                        cancellationToken);
                    break;

                case DeviceLocationStatus.NotSupported:
                    LocationStatusMessage = "Thiết bị này không hỗ trợ định vị GPS. Hãy chạm bản đồ hoặc nhập tọa độ thủ công.";
                    break;

                default:
                    LocationStatusMessage = "Không thể xác định vị trí hiện tại. Kiểm tra tín hiệu GPS / mạng rồi thử lại.";
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (LocationStatusMessage == LocatingProgressMessage)
            {
                LocationStatusMessage = string.Empty;
            }
        }
        catch (Exception)
        {
            LocationStatusMessage = "Không thể xác định vị trí hiện tại. Kiểm tra tín hiệu GPS / mạng rồi thử lại.";
        }
        finally
        {
            IsLocating = false;
        }
    }

    private async Task PromptOpenLocationSettingsAsync(
        string title,
        string message,
        string acceptText,
        string cancelText,
        CancellationToken cancellationToken)
    {
        if (_released || _confirming || locationService is null) return;
        bool confirmed;
        _confirming = true;
        try
        {
            confirmed = await alerts.ConfirmAsync(title, message, acceptText, cancelText, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _confirming = false;
        }

        if (confirmed && !_released)
        {
            await locationService.OpenLocationSettingsAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task SaveHouseRulesAsync(CancellationToken cancellationToken)
    {
        if (_released || _listingId is not { } listingId || _confirming) return;
        Message = string.Empty;
        if (!_loaded)
        {
            Message = "Tải lại thông tin tin đăng trước khi lưu nội quy.";
            return;
        }
        ListingFormField[] fields = [QuietHoursField, GuestPolicyField, CookingRulesField, ParkingRulesField];
        foreach (var field in fields) ValidateHouseRuleField(field);
        if (fields.Any(field => field.Error.Length > 0))
        {
            Message = "Kiểm tra các trường nội quy được đánh dấu.";
            return;
        }

        IsBusy = true;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            if (!IsAuthenticated) { SetLoaded(false); Message = "Đăng nhập để lưu nội quy."; return; }
            var saved = await client.UpdateHouseRulesAsync(listingId, new UpdateHouseRulesRequest
            {
                QuietHours = QuietHoursField.Text.Trim(),
                GuestPolicy = GuestPolicyField.Text.Trim(),
                CookingRules = CookingRulesField.Text.Trim(),
                ParkingRules = ParkingRulesField.Text.Trim()
            }, cancellationToken);
            ApplyHouseRules(saved);
            Message = $"Đã lưu nội quy — phiên bản {saved.Version}.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleHouseRulesFailure(ex); }
        finally { FinishOperation(); }
    }

    private static void ValidateHouseRuleField(ListingFormField field)
    {
        var length = field.Text.Trim().Length;
        field.Error = length == 0
            ? "Vui lòng nhập thông tin này."
            : length > HouseRulesMaxLength
                ? $"Tối đa {HouseRulesMaxLength} ký tự."
                : string.Empty;
    }

    private void ApplyHouseRules(HouseRulesDto? rules)
    {
        HouseRulesVersion = rules?.Version;
        HouseRulesUpdatedAt = rules?.UpdatedAt;
        QuietHoursField.Text = rules?.QuietHours ?? string.Empty;
        GuestPolicyField.Text = rules?.GuestPolicy ?? string.Empty;
        CookingRulesField.Text = rules?.CookingRules ?? string.Empty;
        ParkingRulesField.Text = rules?.ParkingRules ?? string.Empty;
        QuietHoursField.Error = string.Empty;
        GuestPolicyField.Error = string.Empty;
        CookingRulesField.Error = string.Empty;
        ParkingRulesField.Error = string.Empty;
    }

    private void HandleHouseRulesFailure(Exception exception)
    {
        if (exception is ListingManagementException problem)
        {
            QuietHoursField.Error = ReadError(problem, "quiet_hours");
            GuestPolicyField.Error = ReadError(problem, "guest_policy");
            CookingRulesField.Error = ReadError(problem, "cooking_rules");
            ParkingRulesField.Error = ReadError(problem, "parking_rules");
            if (problem.StatusCode == HttpStatusCode.Unauthorized) { IsAuthenticated = false; SetLoaded(false); }
            Message = problem.Message;
        }
        else Message = "Không thể kết nối. Nội quy vẫn được giữ; hãy thử lại.";
    }

    private static string ReadError(ListingManagementException problem, string key)
        => problem.Errors.TryGetValue(key, out var messages) ? string.Join(" ", messages) : string.Empty;

    private void ApplySavedListing(ManagedListingDto listing)
    {
        ExistingPhotos.Clear();
        foreach (var photo in listing.Photos) ExistingPhotos.Add(new ExistingListingPhoto(photo));
        StatusText = ListingStatusText.For(listing.Status);
        CanSave = listing.Status != ListingStatus.Removed;
        CanUnpublish = listing.Status is ListingStatus.Draft or ListingStatus.PendingReview or ListingStatus.Published;
        CanPublish = listing.Status == ListingStatus.Hidden;
        OnPropertyChanged(nameof(PhotoCountText));
    }

    private void SetErrors(IReadOnlyDictionary<string, string> errors)
    {
        TitleField.Error = errors.GetValueOrDefault("title", "");
        AddressField.Error = errors.GetValueOrDefault("address", "");
        RentField.Error = errors.GetValueOrDefault("price", "");
        DepositField.Error = errors.GetValueOrDefault("deposit", "");
        AreaField.Error = errors.GetValueOrDefault("area_m2", "");
        LatitudeField.Error = errors.GetValueOrDefault("lat", "");
        LongitudeField.Error = errors.GetValueOrDefault("lng", "");
        DescriptionField.Error = errors.GetValueOrDefault("description", "");
        RoomTypeError = errors.GetValueOrDefault("room_type", "");
        PhotoError = errors.GetValueOrDefault("photos", "");
    }

    private void HandleFailure(Exception exception)
    {
        if (exception is ListingManagementException problem)
        {
            SetErrors(problem.Errors.ToDictionary(pair => pair.Key, pair => string.Join(" ", pair.Value)));
            if (problem.StatusCode == HttpStatusCode.Unauthorized) { IsAuthenticated = false; SetLoaded(false); }
            Message = problem.Message;
        }
        else Message = "Không thể kết nối. Dữ liệu vẫn được giữ; hãy thử lại.";
    }

    private void SetLoaded(bool loaded)
    {
        if (_loaded == loaded) return;
        _loaded = loaded;
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanDeleteWhenIdle));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void NotifyEditStateChanged()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanDeleteWhenIdle));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void ReleaseSelectedPhotos()
    {
        _released = true;
        LoadCommand.Cancel();
        SaveCommand.Cancel();
        SaveHouseRulesCommand.Cancel();
        UnpublishCommand.Cancel();
        PublishCommand.Cancel();
        DeleteCommand.Cancel();
        UseCurrentLocationCommand.Cancel();
        if (!IsBusy) ClearSelectedPhotos();
    }

    private void FinishOperation()
    {
        IsBusy = false;
        if (_released) ClearSelectedPhotos();
    }

    private void ClearSelectedPhotos()
    {
        foreach (var photo in SelectedPhotos) TryDelete(photo.Path);
        SelectedPhotos.Clear();
    }
}

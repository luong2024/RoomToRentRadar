using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Infrastructure.Auth;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

public partial class ListingEditorViewModel(IListingManagementClient client, AuthService auth,
    INavigationService navigation) : ObservableObject, IQueryAttributable
{
    private Guid? _listingId;
    private bool _loaded;
    private bool _released;
    public ListingFormField TitleField { get; } = new();
    public ListingFormField AddressField { get; } = new();
    public ListingFormField RentField { get; } = new();
    public ListingFormField DepositField { get; } = new();
    public ListingFormField AreaField { get; } = new();
    public ListingFormField LatitudeField { get; } = new();
    public ListingFormField LongitudeField { get; } = new();
    public ListingFormField DescriptionField { get; } = new();
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
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(PickPhotosCommand), nameof(UnpublishCommand))]
    public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool CanUnpublish { get; set; }
    public bool IsNotBusy => !IsBusy;
    public string PageTitle => _listingId is null ? "Đăng tin cho thuê" : "Chỉnh sửa tin";
    public string PhotoCountText => $"{ExistingPhotos.Count + SelectedPhotos.Count} ảnh · cần ít nhất 3 ảnh";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
            _listingId = id;
        OnPropertyChanged(nameof(PageTitle));
    }

    [RelayCommand]
    private Task SignInAsync() => navigation.GoToLoginAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || _released) return;
        IsBusy = true;
        try
        {
            IsAuthenticated = await auth.IsSignedInAsync(cancellationToken);
            if (!IsAuthenticated) { _loaded = false; return; }
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
            _loaded = true;
            Message = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task PickPhotosAsync()
    {
        if (_released) return;
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
                if (SelectedPhotos.Count >= 10 || ExistingPhotos.Count + SelectedPhotos.Count >= 20)
                {
                    PhotoError = "Chọn tối đa 10 ảnh mới mỗi lần lưu, tổng cộng tối đa 20 ảnh.";
                    break;
                }
                var path = Path.Combine(FileSystem.CacheDirectory, $"listing-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
                try
                {
                    string contentType;
                    await using (var input = await file.OpenReadAsync())
                    await using (var output = File.Create(path))
                        contentType = await ListingPhotoSelectionValidator.CopyValidatedAsync(file.FileName, input, output);
                    SelectedListingPhoto? photo = null;
                    photo = new SelectedListingPhoto(file.FileName, contentType, path, new RelayCommand(() =>
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

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_released) return;
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
            if (!IsAuthenticated) { _loaded = false; Message = "Đăng nhập để lưu tin."; return; }
            var saved = _listingId is { } id
                ? await client.UpdateAsync(id, submission, cancellationToken)
                : await client.CreateAsync(submission, cancellationToken);
            _listingId = saved.Id;
            _loaded = true;
            foreach (var photo in SelectedPhotos) TryDelete(photo.Path);
            SelectedPhotos.Clear();
            ApplySavedListing(saved);
            Message = saved.Status == ListingStatus.PendingReview
                ? "Đã lưu tin. Tin đang chờ duyệt trước khi hiển thị công khai."
                : "Đã lưu thay đổi.";
            OnPropertyChanged(nameof(PageTitle));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy), AllowConcurrentExecutions = false)]
    private async Task UnpublishAsync(CancellationToken cancellationToken)
    {
        if (_released || _listingId is null || !CanUnpublish) return;
        IsBusy = true;
        try
        {
            if (!await Shell.Current.DisplayAlertAsync("Gỡ đăng tin?", "Tin sẽ không còn hiển thị công khai. Bạn vẫn có thể chỉnh sửa tin.", "Gỡ đăng", "Hủy") || _released) return;
            ApplySavedListing(await client.UnpublishAsync(_listingId.Value, cancellationToken));
            Message = "Đã gỡ đăng tin.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { HandleFailure(ex); }
        finally { FinishOperation(); }
    }

    private void ApplySavedListing(ManagedListingDto listing)
    {
        ExistingPhotos.Clear();
        foreach (var photo in listing.Photos) ExistingPhotos.Add(new ExistingListingPhoto(photo));
        StatusText = ListingStatusText.For(listing.Status);
        CanUnpublish = listing.Status is not (ListingStatus.Hidden or ListingStatus.Removed);
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
            if (problem.StatusCode == HttpStatusCode.Unauthorized) { IsAuthenticated = false; _loaded = false; }
            Message = problem.Message;
        }
        else Message = "Không thể kết nối. Dữ liệu vẫn được giữ; hãy thử lại.";
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
        UnpublishCommand.Cancel();
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

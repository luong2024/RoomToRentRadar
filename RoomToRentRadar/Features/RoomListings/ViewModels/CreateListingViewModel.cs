using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;

namespace RoomToRentRadar.Features.RoomListings.ViewModels;

public partial class CreateListingViewModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    public const long MaxPhotoSizeBytes = 10 * 1024 * 1024; // 10 MB per BR-MED-02
    public const int MinPhotosCount = 3;
    public const int MaxPhotosCount = 20;

    private readonly ICreateListingService _createListingService;
    private readonly IMediaPickerService _mediaPickerService;

    public CreateListingViewModel(
        ICreateListingService createListingService,
        IMediaPickerService mediaPickerService)
    {
        _createListingService = createListingService ?? throw new ArgumentNullException(nameof(createListingService));
        _mediaPickerService = mediaPickerService ?? throw new ArgumentNullException(nameof(mediaPickerService));

        RoomTypeOptions = RoomTypeOption.DefaultOptions;
        Photos = new ObservableCollection<ListingPhotoItem>();
        Photos.CollectionChanged += (_, _) => OnPhotosCollectionChanged();
    }

    public IReadOnlyList<RoomTypeOption> RoomTypeOptions { get; }

    public ObservableCollection<ListingPhotoItem> Photos { get; }

    public Func<Task>? NavigationBackHandler { get; set; }

    // Form Fields
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleCharacterCountText))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? TitleError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBoardingRoomSelected))]
    [NotifyPropertyChangedFor(nameof(IsMiniApartmentSelected))]
    [NotifyPropertyChangedFor(nameof(IsWholeHouseSelected))]
    [NotifyPropertyChangedFor(nameof(IsDormitorySelected))]
    public partial string? SelectedRoomType { get; set; }

    [ObservableProperty]
    public partial string? RoomTypeError { get; set; }

    public bool IsBoardingRoomSelected => SelectedRoomType == "BoardingRoom";
    public bool IsMiniApartmentSelected => SelectedRoomType == "MiniApartment";
    public bool IsWholeHouseSelected => SelectedRoomType == "WholeHouse";
    public bool IsDormitorySelected => SelectedRoomType == "Dormitory";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedAreaPreview))]
    public partial string AreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AreaError { get; set; }

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AddressError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedPricePreview))]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PriceError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedDepositPreview))]
    public partial string DepositText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DepositError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionCharacterCountText))]
    public partial string? Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DescriptionError { get; set; }

    [ObservableProperty]
    public partial string? PhotosError { get; set; }

    // Status properties
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial bool IsSubmitting { get; set; }

    public bool CanSubmit => !IsSubmitting;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSuccess { get; set; }

    [ObservableProperty]
    public partial string? SuccessMessage { get; set; }

    // Live formatted previews
    public string TitleCharacterCountText => $"{Title?.Length ?? 0} / 100";

    public string DescriptionCharacterCountText => $"{Description?.Length ?? 0} / 2000";

    public string PhotosCountText => $"Selected: {Photos.Count} / {MinPhotosCount} min photos";

    public bool HasMinimumPhotos => Photos.Count >= MinPhotosCount;

    public string FormattedPricePreview
    {
        get
        {
            if (TryParsePrice(PriceText, out var price) && price > 0)
            {
                return string.Format(ViCulture, "₫ {0:N0} / tháng", price);
            }
            return string.Empty;
        }
    }

    public string FormattedDepositPreview
    {
        get
        {
            if (TryParseDeposit(DepositText, out var deposit))
            {
                if (deposit == 0)
                {
                    return "Không yêu cầu đặt cọc (0 ₫)";
                }
                return string.Format(ViCulture, "₫ {0:N0}", deposit);
            }
            return string.Empty;
        }
    }

    public string FormattedAreaPreview
    {
        get
        {
            if (TryParseArea(AreaText, out var area) && area > 0)
            {
                return $"{area:0.#} m²";
            }
            return string.Empty;
        }
    }

    private void OnPhotosCollectionChanged()
    {
        OnPropertyChanged(nameof(PhotosCountText));
        OnPropertyChanged(nameof(HasMinimumPhotos));

        // Update cover status: first photo is cover
        for (int i = 0; i < Photos.Count; i++)
        {
            Photos[i].IsCover = i == 0;
        }

        if (Photos.Count >= MinPhotosCount && Photos.Count <= MaxPhotosCount)
        {
            PhotosError = null;
        }
    }

    [RelayCommand]
    public void SelectRoomType(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        SelectedRoomType = code;
        RoomTypeError = null;
    }

    [RelayCommand]
    public async Task PickPhotosAsync(CancellationToken cancellationToken)
    {
        if (IsSubmitting) return;

        try
        {
            var pickedPhotos = await _mediaPickerService.PickPhotosAsync(cancellationToken);
            if (pickedPhotos == null || pickedPhotos.Count == 0)
            {
                return;
            }

            foreach (var photo in pickedPhotos)
            {
                if (Photos.Count >= MaxPhotosCount)
                {
                    PhotosError = $"Chỉ được chọn tối đa {MaxPhotosCount} hình ảnh.";
                    break;
                }

                // Check extension
                var ext = Path.GetExtension(photo.FileName)?.ToLowerInvariant();
                if (ext is not ".jpg" and not ".jpeg" and not ".png")
                {
                    PhotosError = $"Ảnh '{photo.FileName}' không đúng định dạng JPG hoặc PNG.";
                    continue;
                }

                // Check size (<= 10MB)
                if (photo.FileSizeBytes > MaxPhotoSizeBytes)
                {
                    PhotosError = $"Ảnh '{photo.FileName}' vượt quá dung lượng cho phép (tối đa 10 MB).";
                    continue;
                }

                // Avoid duplicate file paths
                if (!Photos.Any(p => string.Equals(p.FilePath, photo.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    photo.IsCover = Photos.Count == 0;
                    Photos.Add(photo);
                }
            }

            if (Photos.Count >= MinPhotosCount && Photos.Count <= MaxPhotosCount)
            {
                PhotosError = null;
            }
            else if (Photos.Count < MinPhotosCount)
            {
                PhotosError = $"Vui lòng chọn ít nhất {MinPhotosCount} hình ảnh phòng.";
            }
        }
        catch (Exception ex)
        {
            PhotosError = $"Không thể tải hình ảnh: {ex.Message}";
        }
    }

    [RelayCommand]
    public void RemovePhoto(ListingPhotoItem? photo)
    {
        if (photo == null || IsSubmitting) return;

        Photos.Remove(photo);

        if (Photos.Count < MinPhotosCount)
        {
            PhotosError = $"Vui lòng chọn ít nhất {MinPhotosCount} hình ảnh phòng.";
        }
        else if (Photos.Count <= MaxPhotosCount)
        {
            PhotosError = null;
        }
    }

    [RelayCommand]
    public async Task SubmitListingAsync(CancellationToken cancellationToken)
    {
        if (IsSubmitting) return;

        ClearStatusMessages();

        if (!ValidateForm())
        {
            HasError = true;
            ErrorMessage = "Vui lòng kiểm tra lại các trường thông tin có đánh dấu lỗi.";
            return;
        }

        try
        {
            IsSubmitting = true;

            TryParsePrice(PriceText, out var price);
            TryParseDeposit(DepositText, out var deposit);
            TryParseArea(AreaText, out var area);

            var photoPaths = Photos.Select(p => p.FilePath).ToList();

            var request = new CreateListingRequest(
                Title: Title.Trim(),
                Address: Address.Trim(),
                MonthlyRentVnd: price,
                DepositVnd: deposit,
                AreaM2: area,
                RoomType: SelectedRoomType!,
                Description: string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                PhotoFilePaths: photoPaths);

            var result = await _createListingService.CreateListingAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                IsSuccess = true;
                SuccessMessage = "Tin đăng của bạn đã được gửi thành công và đang chờ quản trị viên duyệt!";
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Không thể gửi tin đăng. Vui lòng thử lại sau.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Đã xảy ra lỗi khi gửi tin đăng: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        if (NavigationBackHandler != null)
        {
            await NavigationBackHandler.Invoke();
            return;
        }

        if (Shell.Current != null)
        {
            if (Shell.Current.Navigation?.NavigationStack?.Count > 1)
            {
                try
                {
                    await Shell.Current.Navigation.PopAsync();
                    return;
                }
                catch
                {
                    try
                    {
                        await Shell.Current.GoToAsync("..");
                        return;
                    }
                    catch
                    {
                        // Fall back to tab navigation below
                    }
                }
            }

            try
            {
                await Shell.Current.GoToAsync($"//{Core.Constants.AppRoutes.RoomListings}");
            }
            catch
            {
                await Shell.Current.GoToAsync($"//{Core.Constants.AppRoutes.Home}");
            }
        }
    }

    public bool ValidateForm()
    {
        var isValid = true;

        // 1. Title validation (10 - 100 chars, BR-LST-01)
        if (string.IsNullOrWhiteSpace(Title))
        {
            TitleError = "Vui lòng nhập tiêu đề bài đăng.";
            isValid = false;
        }
        else if (Title.Trim().Length < 10 || Title.Trim().Length > 100)
        {
            TitleError = "Tiêu đề bài đăng phải từ 10 đến 100 ký tự.";
            isValid = false;
        }
        else
        {
            TitleError = null;
        }

        // 2. Room Type validation
        if (string.IsNullOrWhiteSpace(SelectedRoomType))
        {
            RoomTypeError = "Vui lòng chọn loại hình phòng trọ.";
            isValid = false;
        }
        else if (RoomTypeOptions.All(o => o.Code != SelectedRoomType))
        {
            RoomTypeError = "Loại hình phòng trọ không hợp lệ.";
            isValid = false;
        }
        else
        {
            RoomTypeError = null;
        }

        // 3. Area validation (5 - 200 m², BR-LST-01)
        if (string.IsNullOrWhiteSpace(AreaText))
        {
            AreaError = "Vui lòng nhập diện tích phòng.";
            isValid = false;
        }
        else if (!TryParseArea(AreaText, out var area))
        {
            AreaError = "Diện tích phòng không đúng định dạng số.";
            isValid = false;
        }
        else if (area < 5 || area > 200)
        {
            AreaError = "Diện tích phòng phải từ 5 m² đến 200 m².";
            isValid = false;
        }
        else
        {
            AreaError = null;
        }

        // 4. Address validation (<= 200 chars, BR-LST-01)
        if (string.IsNullOrWhiteSpace(Address))
        {
            AddressError = "Vui lòng nhập địa chỉ phòng trọ.";
            isValid = false;
        }
        else if (Address.Trim().Length > 200)
        {
            AddressError = "Địa chỉ phòng trọ không được vượt quá 200 ký tự.";
            isValid = false;
        }
        else
        {
            AddressError = null;
        }

        // 5. Price validation (100,000 - 50,000,000 VND, BR-LST-01)
        var hasValidPrice = false;
        decimal parsedPrice = 0;

        if (string.IsNullOrWhiteSpace(PriceText))
        {
            PriceError = "Vui lòng nhập giá thuê phòng.";
            isValid = false;
        }
        else if (!TryParsePrice(PriceText, out parsedPrice))
        {
            PriceError = "Giá thuê không đúng định dạng số.";
            isValid = false;
        }
        else if (parsedPrice < 100_000 || parsedPrice > 50_000_000)
        {
            PriceError = "Giá thuê phải từ 100.000 ₫ đến 50.000.000 ₫/tháng.";
            isValid = false;
        }
        else
        {
            PriceError = null;
            hasValidPrice = true;
        }

        // 6. Deposit validation (>= 0 and <= 3 * rent, BR-LST-01)
        if (string.IsNullOrWhiteSpace(DepositText))
        {
            DepositError = "Vui lòng nhập tiền đặt cọc (nhập 0 nếu không cần cọc).";
            isValid = false;
        }
        else if (!TryParseDeposit(DepositText, out var parsedDeposit))
        {
            DepositError = "Tiền đặt cọc không đúng định dạng số.";
            isValid = false;
        }
        else if (parsedDeposit < 0)
        {
            DepositError = "Tiền đặt cọc không được là số âm.";
            isValid = false;
        }
        else if (hasValidPrice && parsedDeposit > 3 * parsedPrice)
        {
            DepositError = "Tiền đặt cọc không được vượt quá 3 lần tiền thuê tháng.";
            isValid = false;
        }
        else
        {
            DepositError = null;
        }

        // 7. Photos validation (Min 3, Max 20, <= 10MB each, BR-MED-01..02)
        if (Photos.Count < MinPhotosCount)
        {
            PhotosError = $"Vui lòng chọn ít nhất {MinPhotosCount} hình ảnh phòng.";
            isValid = false;
        }
        else if (Photos.Count > MaxPhotosCount)
        {
            PhotosError = $"Chỉ được chọn tối đa {MaxPhotosCount} hình ảnh phòng.";
            isValid = false;
        }
        else
        {
            var oversizedPhoto = Photos.FirstOrDefault(p => p.FileSizeBytes > MaxPhotoSizeBytes);
            if (oversizedPhoto != null)
            {
                PhotosError = $"Ảnh '{oversizedPhoto.FileName}' vượt quá dung lượng cho phép (tối đa 10 MB).";
                isValid = false;
            }
            else
            {
                PhotosError = null;
            }
        }

        // 8. Description validation (<= 2000 chars, BR-LST-02)
        if (!string.IsNullOrWhiteSpace(Description) && Description.Trim().Length > 2000)
        {
            DescriptionError = "Mô tả chi tiết không được vượt quá 2.000 ký tự.";
            isValid = false;
        }
        else
        {
            DescriptionError = null;
        }

        return isValid;
    }

    public void ClearStatusMessages()
    {
        HasError = false;
        ErrorMessage = null;
        IsSuccess = false;
        SuccessMessage = null;
    }

    private static bool TryParsePrice(string? input, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var cleaned = input.Replace(".", string.Empty).Replace(",", string.Empty).Replace(" ", string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDeposit(string? input, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var cleaned = input.Replace(".", string.Empty).Replace(",", string.Empty).Replace(" ", string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseArea(string? input, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var normalized = input.Replace(",", ".").Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

public class ListingPhotoDraftModel : ObservableObject
{
    private bool _is360;
    private int _order;

    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public string LocalPathOrUrl { get; set; } = string.Empty;

    public bool Is360
    {
        get => _is360;
        set
        {
            if (SetProperty(ref _is360, value))
            {
                OnPropertyChanged(nameof(Badge360Text));
                OnPropertyChanged(nameof(Badge360BackgroundHex));
                OnPropertyChanged(nameof(Badge360TextColorHex));
            }
        }
    }

    public int Order
    {
        get => _order;
        set
        {
            if (SetProperty(ref _order, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string DisplayTitle => $"#{Order} • {FileName}";

    public string FormattedSize => SizeBytes >= 1024 * 1024
        ? string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:0.0} MB", SizeBytes / (1024.0 * 1024.0))
        : string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:0} KB", Math.Max(1, SizeBytes / 1024.0));

    public string Badge360Text => Is360 ? "🌐 Ảnh 360°" : "📷 Ảnh thường";

    public string Badge360BackgroundHex => Is360 ? "#512BD4" : "#E2E8F0";

    public string Badge360TextColorHex => Is360 ? "#FFFFFF" : "#334155";
}

public class SelectableAmenityModel : ObservableObject
{
    private bool _isSelected;

    public string Code { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayText => $"{Icon} {Label}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ChipBackgroundHex));
                OnPropertyChanged(nameof(ChipTextColorHex));
                OnPropertyChanged(nameof(ChipStrokeHex));
            }
        }
    }

    public string ChipBackgroundHex => IsSelected ? "#512BD4" : "#F1F5F9";
    public string ChipTextColorHex => IsSelected ? "#FFFFFF" : "#334155";
    public string ChipStrokeHex => IsSelected ? "#512BD4" : "#CBD5E1";
}

public class ListingRoomTypeOptionModel : ObservableObject
{
    private bool _isSelected;

    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string DisplayName => $"{Icon} {Value}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ChipBackgroundHex));
                OnPropertyChanged(nameof(ChipTextColorHex));
            }
        }
    }

    public string ChipBackgroundHex => IsSelected ? "#512BD4" : "#F1F5F9";
    public string ChipTextColorHex => IsSelected ? "#FFFFFF" : "#334155";
}

/// <summary>
/// ViewModel màn đăng tin phòng trọ & kiểm tra dữ liệu đầu vào (US08 / T08.1).
/// </summary>
public partial class CreateListingViewModel : ObservableObject
{
    public const int MinimumRequiredPhotos = 3;
    public const long MaxPhotoSizeBytes = 10L * 1024L * 1024L;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private static readonly string[] AllowedMimeTypes = ["image/jpeg", "image/jpg", "image/png"];

    private readonly ILandlordListingService _landlordListingService;

    private string _title = string.Empty;
    private string _titleError = string.Empty;
    private string _description = string.Empty;
    private string _descriptionError = string.Empty;
    private string _rentText = string.Empty;
    private string _rentError = string.Empty;
    private string _depositText = string.Empty;
    private string _depositError = string.Empty;
    private string _areaText = string.Empty;
    private string _areaError = string.Empty;
    private string _address = string.Empty;
    private string _addressError = string.Empty;
    private string _selectedRoomType = "Phòng khép kín";
    private string _landlordName = string.Empty;
    private string _landlordNameError = string.Empty;
    private string _landlordPhone = string.Empty;
    private string _landlordPhoneError = string.Empty;
    private string _quietHours = "23:00 - 05:30";
    private string _guestPolicy = "Khách về trước 22:30, qua đêm báo trước chủ trọ";
    private string _cookingRules = "Được nấu ăn bằng bếp từ, giữ vệ sinh chung";
    private string _parkingRules = "Để xe đúng vạch tầng 1, khóa cổ xe";
    private string _photosError = string.Empty;
    private string _formErrorSummary = string.Empty;

    private bool _isSubmitting;
    private bool _isSubmittedPendingReview;
    private string _submittedListingId = string.Empty;
    private string _submittedStatus = "PendingReview";
    private bool _submittedIsVerified;
    private string _submissionStatusText = string.Empty;
    private bool _isFromLiveApi;

    public CreateListingViewModel(ILandlordListingService? landlordListingService = null)
    {
        _landlordListingService = landlordListingService ?? new MockLandlordListingService();

        PickPhotosCommand = new AsyncRelayCommand(PickPhotosAsync);
        AddSamplePhotosCommand = new RelayCommand(AddSamplePhotos);
        RemovePhotoCommand = new RelayCommand<ListingPhotoDraftModel>(RemovePhoto);
        TogglePhoto360Command = new RelayCommand<ListingPhotoDraftModel>(TogglePhoto360);
        ToggleAmenityCommand = new RelayCommand<SelectableAmenityModel>(ToggleAmenity);
        SelectRoomTypeCommand = new RelayCommand<ListingRoomTypeOptionModel>(SelectRoomType);
        SubmitListingCommand = new AsyncRelayCommand(SubmitListingAsync);
        ResetFormCommand = new RelayCommand(ResetForm);

        InitializeOptions();
    }

    public event Func<Task>? PickPhotosRequested;

    public ObservableCollection<ListingPhotoDraftModel> Photos { get; } = [];
    public ObservableCollection<SelectableAmenityModel> Amenities { get; } = [];
    public ObservableCollection<ListingRoomTypeOptionModel> RoomTypeOptions { get; } = [];

    public IAsyncRelayCommand PickPhotosCommand { get; }
    public IRelayCommand AddSamplePhotosCommand { get; }
    public IRelayCommand<ListingPhotoDraftModel> RemovePhotoCommand { get; }
    public IRelayCommand<ListingPhotoDraftModel> TogglePhoto360Command { get; }
    public IRelayCommand<SelectableAmenityModel> ToggleAmenityCommand { get; }
    public IRelayCommand<ListingRoomTypeOptionModel> SelectRoomTypeCommand { get; }
    public IAsyncRelayCommand SubmitListingCommand { get; }
    public IRelayCommand ResetFormCommand { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                TitleError = string.Empty;
            }
        }
    }

    public string TitleError
    {
        get => _titleError;
        private set
        {
            if (SetProperty(ref _titleError, value))
            {
                OnPropertyChanged(nameof(HasTitleError));
            }
        }
    }

    public bool HasTitleError => !string.IsNullOrEmpty(TitleError);

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                DescriptionError = string.Empty;
            }
        }
    }

    public string DescriptionError
    {
        get => _descriptionError;
        private set
        {
            if (SetProperty(ref _descriptionError, value))
            {
                OnPropertyChanged(nameof(HasDescriptionError));
            }
        }
    }

    public bool HasDescriptionError => !string.IsNullOrEmpty(DescriptionError);

    public string RentText
    {
        get => _rentText;
        set
        {
            if (SetProperty(ref _rentText, value))
            {
                RentError = string.Empty;
            }
        }
    }

    public string RentError
    {
        get => _rentError;
        private set
        {
            if (SetProperty(ref _rentError, value))
            {
                OnPropertyChanged(nameof(HasRentError));
            }
        }
    }

    public bool HasRentError => !string.IsNullOrEmpty(RentError);

    public string DepositText
    {
        get => _depositText;
        set
        {
            if (SetProperty(ref _depositText, value))
            {
                DepositError = string.Empty;
            }
        }
    }

    public string DepositError
    {
        get => _depositError;
        private set
        {
            if (SetProperty(ref _depositError, value))
            {
                OnPropertyChanged(nameof(HasDepositError));
            }
        }
    }

    public bool HasDepositError => !string.IsNullOrEmpty(DepositError);

    public string AreaText
    {
        get => _areaText;
        set
        {
            if (SetProperty(ref _areaText, value))
            {
                AreaError = string.Empty;
            }
        }
    }

    public string AreaError
    {
        get => _areaError;
        private set
        {
            if (SetProperty(ref _areaError, value))
            {
                OnPropertyChanged(nameof(HasAreaError));
            }
        }
    }

    public bool HasAreaError => !string.IsNullOrEmpty(AreaError);

    public string Address
    {
        get => _address;
        set
        {
            if (SetProperty(ref _address, value))
            {
                AddressError = string.Empty;
            }
        }
    }

    public string AddressError
    {
        get => _addressError;
        private set
        {
            if (SetProperty(ref _addressError, value))
            {
                OnPropertyChanged(nameof(HasAddressError));
            }
        }
    }

    public bool HasAddressError => !string.IsNullOrEmpty(AddressError);

    public string SelectedRoomType
    {
        get => _selectedRoomType;
        set
        {
            if (SetProperty(ref _selectedRoomType, value))
            {
                foreach (var opt in RoomTypeOptions)
                {
                    opt.IsSelected = string.Equals(opt.Value, value, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    public string LandlordName
    {
        get => _landlordName;
        set
        {
            if (SetProperty(ref _landlordName, value))
            {
                LandlordNameError = string.Empty;
            }
        }
    }

    public string LandlordNameError
    {
        get => _landlordNameError;
        private set
        {
            if (SetProperty(ref _landlordNameError, value))
            {
                OnPropertyChanged(nameof(HasLandlordNameError));
            }
        }
    }

    public bool HasLandlordNameError => !string.IsNullOrEmpty(LandlordNameError);

    public string LandlordPhone
    {
        get => _landlordPhone;
        set
        {
            if (SetProperty(ref _landlordPhone, value))
            {
                LandlordPhoneError = string.Empty;
            }
        }
    }

    public string LandlordPhoneError
    {
        get => _landlordPhoneError;
        private set
        {
            if (SetProperty(ref _landlordPhoneError, value))
            {
                OnPropertyChanged(nameof(HasLandlordPhoneError));
            }
        }
    }

    public bool HasLandlordPhoneError => !string.IsNullOrEmpty(LandlordPhoneError);

    public string QuietHours
    {
        get => _quietHours;
        set => SetProperty(ref _quietHours, value);
    }

    public string GuestPolicy
    {
        get => _guestPolicy;
        set => SetProperty(ref _guestPolicy, value);
    }

    public string CookingRules
    {
        get => _cookingRules;
        set => SetProperty(ref _cookingRules, value);
    }

    public string ParkingRules
    {
        get => _parkingRules;
        set => SetProperty(ref _parkingRules, value);
    }

    public string PhotosError
    {
        get => _photosError;
        private set
        {
            if (SetProperty(ref _photosError, value))
            {
                OnPropertyChanged(nameof(HasPhotosError));
            }
        }
    }

    public bool HasPhotosError => !string.IsNullOrEmpty(PhotosError);

    public bool HasPhotos => Photos.Count > 0;

    public string PhotoCountSummaryText => Photos.Count >= MinimumRequiredPhotos
        ? $"✓ Đã chọn {Photos.Count} ảnh hợp lệ (đạt tối thiểu {MinimumRequiredPhotos} ảnh)"
        : $"Đã chọn {Photos.Count}/{MinimumRequiredPhotos} ảnh bắt buộc (JPG/PNG ≤ 10MB)";

    public string FormErrorSummary
    {
        get => _formErrorSummary;
        private set
        {
            if (SetProperty(ref _formErrorSummary, value))
            {
                OnPropertyChanged(nameof(HasFormErrorSummary));
            }
        }
    }

    public bool HasFormErrorSummary => !string.IsNullOrEmpty(FormErrorSummary);

    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set => SetProperty(ref _isSubmitting, value);
    }

    public bool IsSubmittedPendingReview
    {
        get => _isSubmittedPendingReview;
        private set => SetProperty(ref _isSubmittedPendingReview, value);
    }

    public string SubmittedListingId
    {
        get => _submittedListingId;
        private set => SetProperty(ref _submittedListingId, value);
    }

    public string SubmittedStatus
    {
        get => _submittedStatus;
        private set => SetProperty(ref _submittedStatus, value);
    }

    public bool SubmittedIsVerified
    {
        get => _submittedIsVerified;
        private set => SetProperty(ref _submittedIsVerified, value);
    }

    public string SubmissionStatusText
    {
        get => _submissionStatusText;
        private set => SetProperty(ref _submissionStatusText, value);
    }

    public string ModerationStatusBadgeText => "⏳ PendingReview • Chờ kiểm duyệt";

    public bool IsFromLiveApi
    {
        get => _isFromLiveApi;
        private set
        {
            if (SetProperty(ref _isFromLiveApi, value))
            {
                OnPropertyChanged(nameof(DataSourceBadgeText));
            }
        }
    }

    public string DataSourceBadgeText => IsFromLiveApi
        ? "🟢 API T08.2 trực tuyến"
        : "⚡ Chế độ kiểm thử Offline (PendingReview)";

    private void InitializeOptions()
    {
        RoomTypeOptions.Clear();
        RoomTypeOptions.Add(new ListingRoomTypeOptionModel { Value = "Phòng khép kín", Icon = "🚪", IsSelected = true });
        RoomTypeOptions.Add(new ListingRoomTypeOptionModel { Value = "Studio", Icon = "✨", IsSelected = false });
        RoomTypeOptions.Add(new ListingRoomTypeOptionModel { Value = "Gác xép", Icon = "🪜", IsSelected = false });
        RoomTypeOptions.Add(new ListingRoomTypeOptionModel { Value = "Chung cư mini", Icon = "🏢", IsSelected = false });

        Amenities.Clear();
        Amenities.Add(new SelectableAmenityModel { Code = "Wifi", Icon = "📶", Label = "Wifi tốc độ cao", IsSelected = true });
        Amenities.Add(new SelectableAmenityModel { Code = "PrivateWC", Icon = "🚽", Label = "WC khép kín", IsSelected = true });
        Amenities.Add(new SelectableAmenityModel { Code = "WaterHeater", Icon = "🚿", Label = "Bình nóng lạnh", IsSelected = false });
        Amenities.Add(new SelectableAmenityModel { Code = "AirConditioner", Icon = "❄️", Label = "Điều hòa", IsSelected = false });
        Amenities.Add(new SelectableAmenityModel { Code = "FingerprintLock", Icon = "🔐", Label = "Khóa vân tay", IsSelected = false });
        Amenities.Add(new SelectableAmenityModel { Code = "Balcony", Icon = "🌤️", Label = "Ban công thoáng", IsSelected = false });
        Amenities.Add(new SelectableAmenityModel { Code = "Parking", Icon = "🛵", Label = "Chỗ để xe máy", IsSelected = false });
    }

    public static (bool IsValid, string? ErrorMessage) ValidatePhotoFile(string fileName, string? contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "Tên tệp ảnh không hợp lệ.");
        }

        var extension = Path.GetExtension(fileName.Trim()).ToLowerInvariant();
        var mime = (contentType ?? string.Empty).Trim().ToLowerInvariant();

        bool extensionValid = !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);
        bool mimeValid = string.IsNullOrEmpty(mime) || AllowedMimeTypes.Contains(mime);

        if (!extensionValid || !mimeValid)
        {
            return (false, $"Tệp \"{fileName}\" không đúng định dạng JPG hoặc PNG.");
        }

        if (sizeBytes <= 0)
        {
            return (false, $"Tệp \"{fileName}\" có dung lượng không hợp lệ.");
        }

        if (sizeBytes > MaxPhotoSizeBytes)
        {
            return (false, $"Ảnh \"{fileName}\" vượt quá giới hạn tối đa 10 MB.");
        }

        return (true, null);
    }

    public (bool Success, string? ErrorMessage) TryAddPhoto(
        string fileName,
        string contentType,
        long sizeBytes,
        string localPathOrUrl,
        bool is360 = false)
    {
        var (isValid, errorMessage) = ValidatePhotoFile(fileName, contentType, sizeBytes);
        if (!isValid)
        {
            PhotosError = errorMessage ?? "Ảnh không hợp lệ.";
            return (false, errorMessage);
        }

        var normalizedMime = string.IsNullOrWhiteSpace(contentType)
            ? (Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg")
            : contentType.Trim();

        Photos.Add(new ListingPhotoDraftModel
        {
            Id = $"photo-{Guid.NewGuid():N}",
            FileName = fileName.Trim(),
            ContentType = normalizedMime,
            SizeBytes = sizeBytes,
            LocalPathOrUrl = localPathOrUrl,
            Is360 = is360,
            Order = Photos.Count + 1
        });

        RenumberPhotos();
        PhotosError = Photos.Count >= MinimumRequiredPhotos ? string.Empty : PhotosError;
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(PhotoCountSummaryText));
        return (true, null);
    }

    public bool ValidateForm()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length < 10)
        {
            TitleError = "Tiêu đề phòng trọ cần ít nhất 10 ký tự.";
            errors.Add(TitleError);
        }
        else
        {
            TitleError = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.Trim().Length < 20)
        {
            DescriptionError = "Mô tả chi tiết cần ít nhất 20 ký tự.";
            errors.Add(DescriptionError);
        }
        else
        {
            DescriptionError = string.Empty;
        }

        if (!TryParsePositiveDecimal(RentText, out var rentValue) || rentValue < 300_000m)
        {
            RentError = "Giá thuê phải là số hợp lệ từ 300.000 đ/tháng trở lên.";
            errors.Add(RentError);
        }
        else
        {
            RentError = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(DepositText) && (!TryParseNonNegativeDecimal(DepositText, out _)))
        {
            DepositError = "Tiền cọc phải là số hợp lệ (>= 0).";
            errors.Add(DepositError);
        }
        else
        {
            DepositError = string.Empty;
        }

        if (!TryParseArea(AreaText, out var areaValue) || areaValue is < 8 or > 200)
        {
            AreaError = "Diện tích phải nằm trong khoảng 8 – 200 m².";
            errors.Add(AreaError);
        }
        else
        {
            AreaError = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Address) || Address.Trim().Length < 8)
        {
            AddressError = "Vui lòng nhập địa chỉ cụ thể (ít nhất 8 ký tự).";
            errors.Add(AddressError);
        }
        else
        {
            AddressError = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(LandlordName))
        {
            LandlordNameError = "Vui lòng nhập tên chủ trọ / người liên hệ.";
            errors.Add(LandlordNameError);
        }
        else
        {
            LandlordNameError = string.Empty;
        }

        var phoneDigits = new string(LandlordPhone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length != 10 || !phoneDigits.StartsWith('0'))
        {
            LandlordPhoneError = "Số điện thoại liên hệ phải gồm 10 chữ số bắt đầu bằng số 0.";
            errors.Add(LandlordPhoneError);
        }
        else
        {
            LandlordPhoneError = string.Empty;
        }

        if (Photos.Count < MinimumRequiredPhotos)
        {
            PhotosError = $"Cần tối thiểu {MinimumRequiredPhotos} ảnh thực tế (hiện có {Photos.Count} ảnh).";
            errors.Add(PhotosError);
        }
        else
        {
            var invalidPhoto = Photos
                .Select(p => ValidatePhotoFile(p.FileName, p.ContentType, p.SizeBytes))
                .FirstOrDefault(r => !r.IsValid);

            if (!string.IsNullOrEmpty(invalidPhoto.ErrorMessage))
            {
                PhotosError = invalidPhoto.ErrorMessage;
                errors.Add(PhotosError);
            }
            else
            {
                PhotosError = string.Empty;
            }
        }

        FormErrorSummary = errors.Count > 0
            ? $"Vui lòng kiểm tra {errors.Count} lỗi trước khi gửi: {string.Join(" • ", errors)}"
            : string.Empty;

        return errors.Count == 0;
    }

    private async Task PickPhotosAsync()
    {
        if (PickPhotosRequested is not null)
        {
            await PickPhotosRequested.Invoke();
            return;
        }

        AddSamplePhotos();
    }

    private void AddSamplePhotos()
    {
        var samples = new (string FileName, string Mime, long Size, string Url, bool Is360)[]
        {
            ("phong_chinh_ictu.jpg", "image/jpeg", 1_450_000, "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80", false),
            ("ban_cong_z115.png", "image/png", 1_820_000, "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80", false),
            ("toan_canh_360.jpg", "image/jpeg", 2_150_000, "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=700&q=80", true)
        };

        foreach (var sample in samples)
        {
            if (Photos.Any(p => string.Equals(p.FileName, sample.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            TryAddPhoto(sample.FileName, sample.Mime, sample.Size, sample.Url, sample.Is360);
        }
    }

    private void RemovePhoto(ListingPhotoDraftModel? photo)
    {
        if (photo is null)
        {
            return;
        }

        Photos.Remove(photo);
        RenumberPhotos();
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(PhotoCountSummaryText));
    }

    private void TogglePhoto360(ListingPhotoDraftModel? photo)
    {
        if (photo is null)
        {
            return;
        }

        photo.Is360 = !photo.Is360;
    }

    private void ToggleAmenity(SelectableAmenityModel? amenity)
    {
        if (amenity is null)
        {
            return;
        }

        amenity.IsSelected = !amenity.IsSelected;
    }

    private void SelectRoomType(ListingRoomTypeOptionModel? option)
    {
        if (option is null)
        {
            return;
        }

        SelectedRoomType = option.Value;
    }

    private async Task SubmitListingAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        if (!ValidateForm())
        {
            IsSubmittedPendingReview = false;
            return;
        }

        TryParsePositiveDecimal(RentText, out var rentValue);
        TryParseNonNegativeDecimal(DepositText, out var depositValue);
        TryParseArea(AreaText, out var areaValue);

        var request = new CreateListingRequestContract
        {
            CampusId = "ictu-main",
            Title = Title.Trim(),
            Description = Description.Trim(),
            Rent = rentValue,
            Deposit = string.IsNullOrWhiteSpace(DepositText) ? rentValue : depositValue,
            Area = areaValue,
            RoomType = SelectedRoomType,
            Address = Address.Trim(),
            Amenities = Amenities.Where(a => a.IsSelected).Select(a => a.Code).ToList(),
            HouseRules = new CreateListingHouseRulesPayload
            {
                QuietHours = QuietHours.Trim(),
                GuestPolicy = GuestPolicy.Trim(),
                CookingRules = CookingRules.Trim(),
                ParkingRules = ParkingRules.Trim()
            },
            LandlordName = LandlordName.Trim(),
            LandlordPhone = new string(LandlordPhone.Where(char.IsDigit).ToArray()),
            Photos = Photos.Select(p => new CreateListingPhotoPayload
            {
                FileName = p.FileName,
                ContentType = p.ContentType,
                SizeBytes = p.SizeBytes,
                LocalPathOrUrl = p.LocalPathOrUrl,
                Is360 = p.Is360,
                Order = p.Order
            }).ToList()
        };

        try
        {
            IsSubmitting = true;
            FormErrorSummary = string.Empty;

            var submitResult = await _landlordListingService.SubmitListingAsync(request);
            IsFromLiveApi = submitResult.IsFromLiveApi;

            var response = submitResult.Response;
            SubmittedListingId = response.ListingId;
            SubmittedStatus = string.IsNullOrWhiteSpace(response.Status) ? "PendingReview" : response.Status;
            SubmittedIsVerified = response.IsVerified;
            SubmissionStatusText = string.IsNullOrWhiteSpace(response.Message)
                ? $"Tin #{SubmittedListingId} đã lưu ở trạng thái {SubmittedStatus}, đang chờ kiểm duyệt."
                : response.Message;
            IsSubmittedPendingReview = true;
        }
        catch (Exception ex)
        {
            IsSubmittedPendingReview = false;
            FormErrorSummary = $"Không thể gửi tin đăng: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void ResetForm()
    {
        Title = string.Empty;
        Description = string.Empty;
        RentText = string.Empty;
        DepositText = string.Empty;
        AreaText = string.Empty;
        Address = string.Empty;
        LandlordName = string.Empty;
        LandlordPhone = string.Empty;
        SelectedRoomType = "Phòng khép kín";
        Photos.Clear();

        TitleError = string.Empty;
        DescriptionError = string.Empty;
        RentError = string.Empty;
        DepositError = string.Empty;
        AreaError = string.Empty;
        AddressError = string.Empty;
        LandlordNameError = string.Empty;
        LandlordPhoneError = string.Empty;
        PhotosError = string.Empty;
        FormErrorSummary = string.Empty;

        IsSubmittedPendingReview = false;
        SubmittedListingId = string.Empty;
        SubmittedStatus = "PendingReview";
        SubmittedIsVerified = false;
        SubmissionStatusText = string.Empty;

        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(PhotoCountSummaryText));
    }

    private void RenumberPhotos()
    {
        for (var i = 0; i < Photos.Count; i++)
        {
            Photos[i].Order = i + 1;
        }
    }

    private static bool TryParsePositiveDecimal(string input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static bool TryParseNonNegativeDecimal(string input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length > 0
               && decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value >= 0;
    }

    private static bool TryParseArea(string input, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

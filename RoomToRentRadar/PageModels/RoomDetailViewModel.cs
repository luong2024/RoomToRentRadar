using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

public class AmenityChipItem
{
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayText => $"{Icon}  {Name}";
}

public class RoomPhotoItem : ObservableObject
{
    private bool _isSelected;

    public string ImageUrl { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(BorderStrokeColorHex));
            }
        }
    }

    public string BorderStrokeColorHex => IsSelected ? "#FFFFFF" : "#44FFFFFF";
}

public class SimilarRoomItemDto : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public bool IsVerified { get; set; } = true;
    public string AccentColorHex { get; set; } = "#512BD4";
    public string ImageUrl { get; set; } = string.Empty;
    public string PhotoBadgeText { get; set; } = "📷 6 ảnh • 360°";

    public string FormattedRent => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ/tháng", Rent);
    public string FormattedSpecs => $"{Area:0.#} m² • {RoomType} • Cách ICTU {DistanceMeters}m";
}

public class HouseRulesDetailModel : ObservableObject
{
    public string QuietHours { get; set; } = string.Empty;
    public string GuestPolicy { get; set; } = string.Empty;
    public string CookingRules { get; set; } = string.Empty;
    public string ParkingRules { get; set; } = string.Empty;
}

public class RoomDetailViewModel : ObservableObject
{
    public const double IctuLatitude = 21.5850;
    public const double IctuLongitude = 105.8066;

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IRoomDetailService _roomDetailService;
    private readonly IAppNavigationService? _navigationService;

    private bool _isAvailable = true;
    private bool _isAuthenticated;
    private bool _isBusy;
    private bool _isFromLiveApi;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _roomId = "room-2026-001";
    private string _campusId = "ictu-main";
    private string _title = string.Empty;
    private string _description = string.Empty;
    private decimal _rent;
    private decimal _deposit;
    private double _area;
    private string _roomType = string.Empty;
    private string _address = string.Empty;
    private int _distanceMeters = 400;
    private bool _isVerified = true;
    private double _securityScore = 4.5;
    private string _landlordName = string.Empty;
    private string _rawLandlordPhone = "0987654321";
    private string _maskedOrApiLandlordPhone = string.Empty;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;
    private string _unavailableMessage = MockRoomDetailService.DefaultUnavailableMessage;
    private string _heroImageUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=900&q=80";
    private HouseRulesDetailModel _houseRules = new();

    public string HeroImageUrl
    {
        get => _heroImageUrl;
        set => SetProperty(ref _heroImageUrl, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(IsUnavailable));
                OnPropertyChanged(nameof(AvailabilityBadgeText));
                OnPropertyChanged(nameof(AvailabilityToggleButtonText));
            }
        }
    }

    public bool IsUnavailable => !IsAvailable;

    public string AvailabilityBadgeText => IsAvailable ? "● Đang còn trống" : "● Đã cho thuê";

    public string AvailabilityToggleButtonText => IsAvailable ? "Thử chế độ: Hết phòng" : "Thử chế độ: Còn trống";

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(LandlordContactButtonText));
                OnPropertyChanged(nameof(DisplayedLandlordPhone));
                OnPropertyChanged(nameof(AuthStatusBadgeText));
            }
        }
    }

    public string AuthStatusBadgeText => IsAuthenticated ? "Đã xác thực sinh viên ICTU" : "Chưa đăng nhập • Đang ẩn SĐT";

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

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

    public string DataSourceBadgeText => IsFromLiveApi ? "🟢 API T02.1 trực tuyến" : "⚡ Dữ liệu mẫu Offline";

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string RoomId
    {
        get => _roomId;
        set => SetProperty(ref _roomId, value);
    }

    public string CampusId
    {
        get => _campusId;
        set => SetProperty(ref _campusId, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public decimal Rent
    {
        get => _rent;
        set
        {
            if (SetProperty(ref _rent, value))
            {
                OnPropertyChanged(nameof(FormattedRent));
            }
        }
    }

    public decimal Deposit
    {
        get => _deposit;
        set
        {
            if (SetProperty(ref _deposit, value))
            {
                OnPropertyChanged(nameof(FormattedDeposit));
            }
        }
    }

    public double Area
    {
        get => _area;
        set
        {
            if (SetProperty(ref _area, value))
            {
                OnPropertyChanged(nameof(FormattedArea));
            }
        }
    }

    public string RoomType
    {
        get => _roomType;
        set => SetProperty(ref _roomType, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public int DistanceMeters
    {
        get => _distanceMeters;
        set
        {
            if (SetProperty(ref _distanceMeters, value))
            {
                OnPropertyChanged(nameof(FormattedDistance));
                OnPropertyChanged(nameof(CommuteEstimateText));
            }
        }
    }

    public bool IsVerified
    {
        get => _isVerified;
        set => SetProperty(ref _isVerified, value);
    }

    public double SecurityScore
    {
        get => _securityScore;
        set
        {
            if (SetProperty(ref _securityScore, value))
            {
                OnPropertyChanged(nameof(FormattedSecurityScore));
            }
        }
    }

    public string FormattedSecurityScore => $"★ {SecurityScore:0.0} An ninh";

    public string LandlordName
    {
        get => _landlordName;
        set
        {
            if (SetProperty(ref _landlordName, value))
            {
                OnPropertyChanged(nameof(LandlordContactButtonText));
                OnPropertyChanged(nameof(LandlordInitials));
            }
        }
    }

    public string LandlordInitials => string.IsNullOrWhiteSpace(LandlordName)
        ? "CT"
        : string.Concat(LandlordName.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2).Select(w => char.ToUpperInvariant(w[0])));

    public string RawLandlordPhone
    {
        get => _rawLandlordPhone;
        set
        {
            if (SetProperty(ref _rawLandlordPhone, value))
            {
                OnPropertyChanged(nameof(LandlordContactButtonText));
                OnPropertyChanged(nameof(DisplayedLandlordPhone));
            }
        }
    }

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (SetProperty(ref _updatedAt, value))
            {
                OnPropertyChanged(nameof(FormattedUpdatedAt));
            }
        }
    }

    public string UnavailableMessage
    {
        get => _unavailableMessage;
        set => SetProperty(ref _unavailableMessage, value);
    }

    public HouseRulesDetailModel HouseRules
    {
        get => _houseRules;
        set => SetProperty(ref _houseRules, value);
    }

    public ObservableCollection<string> Amenities { get; } = [];

    public ObservableCollection<AmenityChipItem> AmenityChips { get; } = [];

    public ObservableCollection<RoomPhotoItem> RoomPhotos { get; } = [];

    public ObservableCollection<SimilarRoomItemDto> SimilarRooms { get; } = [];

    public string FormattedRent => string.Format(ViCulture, "{0:N0} đ", Rent);

    public string FormattedDeposit => string.Format(ViCulture, "{0:N0} đ", Deposit);

    public string FormattedArea => $"{Area:0.#} m²";

    public string FormattedDistance => DistanceMeters < 1000
        ? $"{DistanceMeters} m"
        : $"{DistanceMeters / 1000.0:0.1} km";

    public string CommuteEstimateText => DistanceMeters <= 500
        ? "Đi bộ ~5 phút tới ICTU"
        : "Đi xe ~3 phút tới ICTU";

    public string FormattedUpdatedAt => $"Cập nhật: {UpdatedAt:dd/MM/yyyy HH:mm}";

    public string DisplayedLandlordPhone => IsAuthenticated
        ? $"📞 {RawLandlordPhone}"
        : $"🔒 {(!string.IsNullOrWhiteSpace(_maskedOrApiLandlordPhone) ? _maskedOrApiLandlordPhone : MockRoomDetailService.MaskPhone(RawLandlordPhone))}";

    public string LandlordContactButtonText => IsAuthenticated
        ? $"Gọi ngay • {RawLandlordPhone}"
        : "Đăng nhập để xem SĐT chủ trọ";

    public IAsyncRelayCommand ContactOrSignInCommand { get; }

    public IAsyncRelayCommand ToggleAvailabilityPreviewCommand { get; }

    public IAsyncRelayCommand<SimilarRoomItemDto> SelectSimilarRoomCommand { get; }

    public IAsyncRelayCommand RetryLoadCommand { get; }

    public IRelayCommand<RoomPhotoItem> SelectPhotoCommand { get; }

    public RoomDetailViewModel(
        IRoomDetailService? roomDetailService = null,
        IAppNavigationService? navigationService = null)
    {
        _roomDetailService = roomDetailService ?? new MockRoomDetailService();
        _navigationService = navigationService;

        ContactOrSignInCommand = new AsyncRelayCommand(ContactOrSignInAsync);
        ToggleAvailabilityPreviewCommand = new AsyncRelayCommand(ToggleAvailabilityPreviewAsync);
        SelectSimilarRoomCommand = new AsyncRelayCommand<SimilarRoomItemDto>(SelectSimilarRoomAsync);
        RetryLoadCommand = new AsyncRelayCommand(() => LoadRoomDetailAsync(RoomId));
        SelectPhotoCommand = new RelayCommand<RoomPhotoItem>(SelectPhoto);

        _ = LoadRoomDetailAsync("room-2026-001");
    }

    public async Task ApplyNavigationParametersAsync(IDictionary<string, object> query)
    {
        var targetRoomId = query.TryGetValue("id", out var idObj) && idObj is string idStr && !string.IsNullOrWhiteSpace(idStr)
            ? idStr
            : RoomId;

        if (query.TryGetValue("isAuthenticated", out var authObj))
        {
            if (authObj is bool b)
            {
                IsAuthenticated = b;
            }
            else if (bool.TryParse(authObj?.ToString(), out var parsedAuth))
            {
                IsAuthenticated = parsedAuth;
            }
        }

        bool? forceAvailable = null;
        if (query.TryGetValue("isAvailable", out var availObj))
        {
            if (availObj is bool b)
            {
                forceAvailable = b;
            }
            else if (bool.TryParse(availObj?.ToString(), out var parsedBool))
            {
                forceAvailable = parsedBool;
            }
        }

        await LoadRoomDetailAsync(targetRoomId, forceAvailable);
    }

    public async Task LoadRoomDetailAsync(
        string roomId,
        bool? forceAvailable = null,
        CancellationToken cancellationToken = default)
    {
        var targetRoomId = string.IsNullOrWhiteSpace(roomId) ? "room-2026-001" : roomId.Trim();

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var fetchResult = await _roomDetailService.GetRoomDetailAsync(
                targetRoomId,
                IsAuthenticated,
                cancellationToken);

            IsFromLiveApi = fetchResult.IsFromLiveApi;

            if (fetchResult.Detail is null)
            {
                HasError = true;
                ErrorMessage = $"Không tìm thấy thông tin chi tiết cho phòng '{targetRoomId}'.";
                SimilarRooms.Clear();
                return;
            }

            ApplyDetailContract(fetchResult.Detail, forceAvailable);
        }
        catch (OperationCanceledException)
        {
            // Ignore valid cancellation
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Không thể tải chi tiết phòng trọ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyDetailContract(ListingDetailContract dto, bool? forceAvailable)
    {
        RoomId = dto.Id;
        CampusId = dto.CampusId;
        Title = dto.Title;
        Description = dto.Description;
        Rent = dto.Rent;
        Deposit = dto.Deposit;
        Area = dto.Area;
        RoomType = dto.RoomType;
        Address = dto.Address;
        IsVerified = dto.IsVerified;
        SecurityScore = dto.SecurityScore > 0 ? dto.SecurityScore : 4.5;
        UpdatedAt = dto.UpdatedAt;

        if (dto.Location?.Coordinates is { Length: >= 2 } coords)
        {
            var computed = CalculateDistanceMeters(coords[1], coords[0], IctuLatitude, IctuLongitude);
            DistanceMeters = computed < 5 && dto.Title.Contains("400m", StringComparison.OrdinalIgnoreCase)
                ? 400
                : computed;
        }
        else
        {
            DistanceMeters = 400;
        }

        var effectiveAvailable = forceAvailable ?? dto.IsAvailable;
        IsAvailable = effectiveAvailable;

        if (!string.IsNullOrWhiteSpace(dto.UnavailableMessage))
        {
            UnavailableMessage = dto.UnavailableMessage;
        }
        else if (!effectiveAvailable)
        {
            UnavailableMessage = MockRoomDetailService.DefaultUnavailableMessage;
        }

        if (dto.HouseRules is not null)
        {
            HouseRules = new HouseRulesDetailModel
            {
                QuietHours = dto.HouseRules.QuietHours,
                GuestPolicy = dto.HouseRules.GuestPolicy,
                CookingRules = dto.HouseRules.CookingRules,
                ParkingRules = dto.HouseRules.ParkingRules
            };
        }

        if (dto.Landlord is not null)
        {
            LandlordName = dto.Landlord.Name;
            if (!string.IsNullOrWhiteSpace(dto.Landlord.Phone) && !dto.Landlord.Phone.Contains('*'))
            {
                RawLandlordPhone = dto.Landlord.Phone;
                _maskedOrApiLandlordPhone = MockRoomDetailService.MaskPhone(dto.Landlord.Phone);
            }
            else
            {
                _maskedOrApiLandlordPhone = dto.Landlord.Phone;
                OnPropertyChanged(nameof(DisplayedLandlordPhone));
                OnPropertyChanged(nameof(LandlordContactButtonText));
            }
        }

        Amenities.Clear();
        AmenityChips.Clear();
        foreach (var rawAmenity in dto.Amenities)
        {
            var (icon, label) = MapAmenityDisplay(rawAmenity);
            Amenities.Add(label);
            AmenityChips.Add(new AmenityChipItem { Icon = icon, Name = label });
        }

        PopulatePhotosForRoom(dto.Id, dto.Photos);

        SimilarRooms.Clear();
        if (!effectiveAvailable && dto.SimilarRooms.Count > 0)
        {
            var colors = new[] { "#4F46E5", "#0D9488", "#7C3AED" };
            for (var i = 0; i < dto.SimilarRooms.Count; i++)
            {
                var sim = dto.SimilarRooms[i];
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = sim.Id,
                    Title = sim.Title,
                    Rent = sim.Rent,
                    Area = sim.Area,
                    RoomType = sim.RoomType,
                    Address = sim.Address,
                    DistanceMeters = (int)Math.Round(sim.DistanceMeters),
                    IsVerified = sim.IsVerified,
                    AccentColorHex = colors[i % colors.Length],
                    ImageUrl = ResolveRoomPhotoUrl(sim.Id, sim.ThumbnailUrl),
                    PhotoBadgeText = "📷 6 ảnh • 360°"
                });
            }
        }
    }

    public static int CalculateDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double EarthRadiusMeters = 6_371_000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (int)Math.Round(EarthRadiusMeters * c);
    }

    private void PopulatePhotosForRoom(string roomId, IReadOnlyList<PhotoContract>? apiPhotos)
    {
        var primaryUrl = ResolveRoomPhotoUrl(roomId, apiPhotos?.FirstOrDefault()?.Url);
        HeroImageUrl = primaryUrl;

        RoomPhotos.Clear();
        RoomPhotos.Add(new RoomPhotoItem
        {
            ImageUrl = primaryUrl,
            Caption = "Không gian chính & Ban công",
            IsSelected = true
        });
        RoomPhotos.Add(new RoomPhotoItem
        {
            ImageUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=900&q=80",
            Caption = "Góc bàn học & Cửa sổ lớn",
            IsSelected = false
        });
        RoomPhotos.Add(new RoomPhotoItem
        {
            ImageUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80",
            Caption = "Khu vực kệ bếp & Tủ gỗ",
            IsSelected = false
        });
        RoomPhotos.Add(new RoomPhotoItem
        {
            ImageUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=900&q=80",
            Caption = "Toàn cảnh 360° VR",
            IsSelected = false
        });
    }

    private static string ResolveRoomPhotoUrl(string roomId, string? rawUrl)
    {
        if (!string.IsNullOrWhiteSpace(rawUrl) && !rawUrl.Contains("cdn.example", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        return roomId.ToLowerInvariant() switch
        {
            "room-2026-002" or "room-ictu-002" or "room-map-002" => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=900&q=80",
            "room-2026-003" or "room-ictu-003" or "room-map-003" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=900&q=80",
            "room-2026-004" or "room-ictu-004" or "room-map-004" or "room-2026-009" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=900&q=80",
            "room-2026-005" or "room-ictu-005" or "room-map-005" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80",
            "room-2026-006" or "room-map-006" => "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=900&q=80",
            "room-2026-007" or "room-map-007" => "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=900&q=80",
            "room-2026-008" or "room-map-008" => "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=900&q=80",
            _ => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=900&q=80"
        };
    }

    private static (string Icon, string Label) MapAmenityDisplay(string code)
    {
        return code.Trim() switch
        {
            "Wifi" => ("📶", "Wifi tốc độ cao"),
            "WaterHeater" => ("🚿", "Bình nóng lạnh"),
            "AirConditioner" => ("❄️", "Điều hòa Inverter"),
            "PrivateWC" => ("🚽", "WC khép kín"),
            "FingerprintLock" => ("🔐", "Khóa vân tay 24/7"),
            "Balcony" => ("🌤️", "Ban công thoáng"),
            "Parking" => ("🛵", "Chỗ để xe máy"),
            _ => ("✨", code)
        };
    }

    private void SelectPhoto(RoomPhotoItem? photo)
    {
        if (photo is null)
            return;

        HeroImageUrl = photo.ImageUrl;
        foreach (var item in RoomPhotos)
        {
            item.IsSelected = ReferenceEquals(item, photo);
        }
    }

    private async Task ContactOrSignInAsync()
    {
        if (!IsAuthenticated)
        {
            IsAuthenticated = true;

            var fetchResult = await _roomDetailService.GetRoomDetailAsync(RoomId, isAuthenticated: true);
            if (fetchResult.Detail?.Landlord is { } landlord
                && !string.IsNullOrWhiteSpace(landlord.Phone)
                && !landlord.Phone.Contains('*'))
            {
                RawLandlordPhone = landlord.Phone;
            }

            if (_navigationService is not null)
            {
                await _navigationService.ShowAlertAsync(
                    "Xác thực tài khoản thành công",
                    $"Đã mở khóa thông tin liên hệ!\nChủ trọ: {LandlordName}\nSố điện thoại: {RawLandlordPhone}",
                    "Tuyệt vời");
            }
            return;
        }

        if (_navigationService is not null)
        {
            await _navigationService.ShowAlertAsync(
                "Kết nối Chủ trọ",
                $"Đang gọi cho {LandlordName} qua số {RawLandlordPhone}...",
                "Đóng");
        }
    }

    private async Task ToggleAvailabilityPreviewAsync()
    {
        var nextAvailable = !IsAvailable;
        var targetId = nextAvailable ? "room-2026-001" : "room-2026-004";
        await LoadRoomDetailAsync(targetId);
    }

    private async Task SelectSimilarRoomAsync(SimilarRoomItemDto? selectedRoom)
    {
        if (selectedRoom is null)
            return;

        await LoadRoomDetailAsync(selectedRoom.Id);
    }
}

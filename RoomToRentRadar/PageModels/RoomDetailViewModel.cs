using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.PageModels;

public class AmenityChipItem
{
    public string Icon { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayText => $"{Icon}  {Name}";
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

public class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private bool _isAvailable = true;
    private bool _isAuthenticated;
    private bool _isBusy;
    private string _roomId = "room-2026-001";
    private string _campusId = "ictu-main";
    private string _title = string.Empty;
    private string _description = string.Empty;
    private decimal _rent;
    private decimal _deposit;
    private double _area;
    private string _roomType = string.Empty;
    private string _address = string.Empty;
    private int _distanceMeters;
    private bool _isVerified = true;
    private double _securityScore = 4.8;
    private string _landlordName = string.Empty;
    private string _rawLandlordPhone = string.Empty;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;
    private string _unavailableMessage = "Phòng này hiện đã có sinh viên đặt cọc thuê. Hệ thống gợi ý 3 phòng tương tự đang còn trống ngay gần cổng ICTU:";
    private HouseRulesDetailModel _houseRules = new();

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
        : $"🔒 {MaskPhoneNumber(RawLandlordPhone)}";

    public string LandlordContactButtonText => IsAuthenticated
        ? $"Gọi ngay • {RawLandlordPhone}"
        : "Đăng nhập để xem SĐT chủ trọ";

    public IAsyncRelayCommand ContactOrSignInCommand { get; }

    public IRelayCommand ToggleAvailabilityPreviewCommand { get; }

    public IRelayCommand<SimilarRoomItemDto> SelectSimilarRoomCommand { get; }

    public RoomDetailViewModel()
    {
        ContactOrSignInCommand = new AsyncRelayCommand(ContactOrSignInAsync);
        ToggleAvailabilityPreviewCommand = new RelayCommand(ToggleAvailabilityPreview);
        SelectSimilarRoomCommand = new RelayCommand<SimilarRoomItemDto>(SelectSimilarRoom);

        LoadSampleRoomDetail("room-2026-001", isAvailable: true, isAuthenticated: false);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var targetRoomId = query.TryGetValue("id", out var idObj) && idObj is string idStr && !string.IsNullOrWhiteSpace(idStr)
            ? idStr
            : RoomId;

        var availableFlag = IsAvailable;
        if (query.TryGetValue("isAvailable", out var availObj))
        {
            if (availObj is bool b)
                availableFlag = b;
            else if (bool.TryParse(availObj?.ToString(), out var parsedBool))
                availableFlag = parsedBool;
        }

        var authFlag = IsAuthenticated;
        if (query.TryGetValue("isAuthenticated", out var authObj))
        {
            if (authObj is bool b)
                authFlag = b;
            else if (bool.TryParse(authObj?.ToString(), out var parsedAuth))
                authFlag = parsedAuth;
        }

        LoadSampleRoomDetail(targetRoomId, availableFlag, authFlag);
    }

    public void LoadSampleRoomDetail(string roomId, bool isAvailable = true, bool isAuthenticated = false)
    {
        IsBusy = true;
        try
        {
            RoomId = roomId;
            CampusId = "ictu-main";
            Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m";
            Description = "Phòng Studio mới xây thiết kế hiện đại, cửa sổ lớn và ban công phơi đồ đón nắng tự nhiên. Trang bị sẵn điều hòa Inverter tiết kiệm điện, bình nóng lạnh Ariston, giường tủ gỗ và hệ thống khóa vân tay 2 lớp an ninh 24/7. Khu dân cư văn minh, yên tĩnh, cực kỳ thuận tiện cho sinh viên ĐH CNTT & Truyền thông (ICTU).";
            Rent = 1_800_000m;
            Deposit = 1_800_000m;
            Area = 22.5;
            RoomType = "Studio Khép Kín";
            Address = "Ngõ 18 Đường Z115, Phường Tân Thịnh, TP. Thái Nguyên";
            DistanceMeters = 400;
            IsVerified = true;
            SecurityScore = 4.8;
            LandlordName = "Cô Nguyễn Thị Lan";
            RawLandlordPhone = "0987654321";
            UpdatedAt = new DateTimeOffset(2026, 11, 9, 8, 30, 0, TimeSpan.FromHours(7));
            IsAuthenticated = isAuthenticated;
            IsAvailable = isAvailable;

            HouseRules = new HouseRulesDetailModel
            {
                QuietHours = "Giữ trật tự từ 23:00 đêm đến 05:30 sáng hôm sau",
                GuestPolicy = "Bạn bè đến chơi thoải mái trước 22:30, ở qua đêm báo trước chủ nhà",
                CookingRules = "Được nấu ăn bằng bếp từ/hồng ngoại, không dùng bếp gas mini",
                ParkingRules = "Để xe đúng vị trí vạch kẻ tầng 1, tự giác khóa cổ xe"
            };

            Amenities.Clear();
            Amenities.Add("Wifi tốc độ cao");
            Amenities.Add("Điều hòa Inverter");
            Amenities.Add("Bình nóng lạnh");
            Amenities.Add("WC khép kín");
            Amenities.Add("Khóa vân tay 24/7");
            Amenities.Add("Ban công phơi đồ");

            AmenityChips.Clear();
            AmenityChips.Add(new AmenityChipItem { Icon = "📶", Name = "Wifi cáp quang" });
            AmenityChips.Add(new AmenityChipItem { Icon = "❄️", Name = "Điều hòa Inverter" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🚿", Name = "Bình nóng lạnh" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🚽", Name = "WC khép kín" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🔐", Name = "Khóa vân tay 24/7" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🌤️", Name = "Ban công thoáng" });
            AmenityChips.Add(new AmenityChipItem { Icon = "📹", Name = "Camera an ninh" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🛵", Name = "Chỗ để xe tầng 1" });

            SimilarRooms.Clear();
            if (!IsAvailable)
            {
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-002",
                    Title = "Studio Full Nội Thất Cổng Phụ ICTU",
                    Rent = 1_900_000m,
                    Area = 24.0,
                    RoomType = "Studio",
                    Address = "Ngõ 42 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                    DistanceMeters = 350,
                    IsVerified = true,
                    AccentColorHex = "#4F46E5"
                });
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-003",
                    Title = "Phòng Khép Kín Gác Xép Gỗ Thoáng Mát",
                    Rent = 1_650_000m,
                    Area = 20.0,
                    RoomType = "Gác xép",
                    Address = "Đường Quang Trung, Tân Thịnh, TP. Thái Nguyên",
                    DistanceMeters = 550,
                    IsVerified = true,
                    AccentColorHex = "#0D9488"
                });
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-004",
                    Title = "Phòng Mới Xây Có Ban Công Gần Hồ Điều Hòa",
                    Rent = 1_750_000m,
                    Area = 21.5,
                    RoomType = "Studio",
                    Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                    DistanceMeters = 620,
                    IsVerified = true,
                    AccentColorHex = "#7C3AED"
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContactOrSignInAsync()
    {
        if (!IsAuthenticated)
        {
            IsAuthenticated = true;
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Xác thực tài khoản thành công",
                    $"Đã mở khóa thông tin liên hệ!\nChủ trọ: {LandlordName}\nSố điện thoại: {RawLandlordPhone}",
                    "Tuyệt vời");
            }
            return;
        }

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Kết nối Chủ trọ",
                $"Đang gọi cho {LandlordName} qua số {RawLandlordPhone}...",
                "Đóng");
        }
    }

    private void ToggleAvailabilityPreview()
    {
        LoadSampleRoomDetail(RoomId, isAvailable: !IsAvailable, isAuthenticated: IsAuthenticated);
    }

    private void SelectSimilarRoom(SimilarRoomItemDto? selectedRoom)
    {
        if (selectedRoom is null)
            return;

        RoomId = selectedRoom.Id;
        Title = selectedRoom.Title;
        Rent = selectedRoom.Rent;
        Deposit = selectedRoom.Rent;
        Area = selectedRoom.Area;
        RoomType = selectedRoom.RoomType;
        Address = selectedRoom.Address;
        DistanceMeters = selectedRoom.DistanceMeters;
        IsVerified = selectedRoom.IsVerified;
        UpdatedAt = DateTimeOffset.Now;
        IsAvailable = true;
        SimilarRooms.Clear();
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4)
            return "098*** (Đăng nhập để xem)";

        return $"{phone[..3]}*** (Đăng nhập để xem)";
    }
}

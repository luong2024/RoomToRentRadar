using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.PageModels;

public sealed record ApiGeoJsonPointDto(string Type, double[] Coordinates);

public sealed record ApiPhotoDto(string Url, bool Is360, int Order);

public sealed record ApiHouseRulesDto(
    int Version,
    DateTimeOffset UpdatedAt,
    string QuietHours,
    string GuestPolicy,
    string CookingRules,
    string ParkingRules);

public sealed record ApiLandlordDto(string Id, string Name, string Phone);

public sealed class ApiListingCardDto
{
    public string Id { get; set; } = string.Empty;
    public string CampusId { get; set; } = "ictu-main";
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public double DistanceMeters { get; set; }
    public string RoomType { get; set; } = "Studio";
    public string Address { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public bool IsVerified { get; set; }
}

public sealed record ApiListingDetailDto(
    string Id,
    string CampusId,
    string Title,
    string Description,
    string Status,
    bool IsVerified,
    bool IsAvailable,
    string? UnavailableMessage,
    decimal Rent,
    decimal Deposit,
    double Area,
    string RoomType,
    string Address,
    ApiGeoJsonPointDto? Location,
    IReadOnlyList<string>? Amenities,
    double SecurityScore,
    IReadOnlyList<ApiPhotoDto>? Photos,
    ApiHouseRulesDto? HouseRules,
    ApiLandlordDto? Landlord,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ApiListingCardDto>? SimilarRooms);

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

public class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2.5)
    };

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
    private double _securityScore = 4.5;
    private string _landlordName = string.Empty;
    private string _rawLandlordPhone = "0987654321";
    private string _maskedOrApiLandlordPhone = string.Empty;
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;
    private string _unavailableMessage = "Phòng này hiện không còn trống. Dưới đây là một số phòng tương tự đang Available.";
    private string _heroImageUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=900&q=80";
    private HouseRulesDetailModel _houseRules = new();

    private static string BaseApiUrl =>
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000"
            : "http://localhost:5000";

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
        : $"🔒 {(!string.IsNullOrWhiteSpace(_maskedOrApiLandlordPhone) ? _maskedOrApiLandlordPhone : MaskPhoneNumber(RawLandlordPhone))}";

    public string LandlordContactButtonText => IsAuthenticated
        ? $"Gọi ngay • {RawLandlordPhone}"
        : "Đăng nhập để xem SĐT chủ trọ";

    public IAsyncRelayCommand ContactOrSignInCommand { get; }

    public IRelayCommand ToggleAvailabilityPreviewCommand { get; }

    public IRelayCommand<SimilarRoomItemDto> SelectSimilarRoomCommand { get; }

    public IRelayCommand<RoomPhotoItem> SelectPhotoCommand { get; }

    public RoomDetailViewModel()
    {
        ContactOrSignInCommand = new AsyncRelayCommand(ContactOrSignInAsync);
        ToggleAvailabilityPreviewCommand = new RelayCommand(ToggleAvailabilityPreview);
        SelectSimilarRoomCommand = new RelayCommand<SimilarRoomItemDto>(SelectSimilarRoom);
        SelectPhotoCommand = new RelayCommand<RoomPhotoItem>(SelectPhoto);

        LoadSampleRoomDetail("room-2026-001", isAvailable: true, isAuthenticated: false);
        _ = FetchRoomDetailFromApiAsync("room-2026-001");
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
        else if (string.Equals(targetRoomId, "room-2026-004", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(targetRoomId, "room-ictu-004", StringComparison.OrdinalIgnoreCase))
        {
            availableFlag = false;
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
        _ = FetchRoomDetailFromApiAsync(targetRoomId);
    }

    /// <summary>
    /// Calls Backend API T02.1 (GET /api/v1/listings/{id}) and maps ListingDetailDto onto the ViewModel.
    /// Automatically falls back to local seed data if the local API server is not running.
    /// </summary>
    public async Task FetchRoomDetailFromApiAsync(string roomId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseApiUrl}/api/v1/listings/{Uri.EscapeDataString(roomId)}";
            var dto = await SharedHttpClient.GetFromJsonAsync<ApiListingDetailDto>(url, JsonOptions, cancellationToken);
            if (dto is null)
                return;

            ApplyApiDetailDto(dto);
        }
        catch (Exception)
        {
            // Keep local seed fallback when Backend API server (http://localhost:5000) is offline
        }
    }

    private void ApplyApiDetailDto(ApiListingDetailDto dto)
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
        IsAvailable = dto.IsAvailable;
        UpdatedAt = dto.UpdatedAt;

        if (!string.IsNullOrWhiteSpace(dto.UnavailableMessage))
        {
            UnavailableMessage = dto.UnavailableMessage;
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
            if (!dto.Landlord.Phone.Contains('*'))
            {
                RawLandlordPhone = dto.Landlord.Phone;
            }
            else
            {
                _maskedOrApiLandlordPhone = dto.Landlord.Phone;
                OnPropertyChanged(nameof(DisplayedLandlordPhone));
            }
        }

        if (dto.Amenities is { Count: > 0 })
        {
            Amenities.Clear();
            AmenityChips.Clear();
            foreach (var rawAmenity in dto.Amenities)
            {
                var (icon, label) = MapAmenityDisplay(rawAmenity);
                Amenities.Add(label);
                AmenityChips.Add(new AmenityChipItem { Icon = icon, Name = label });
            }
        }

        PopulatePhotosForRoom(dto.Id, dto.Photos);

        SimilarRooms.Clear();
        if (!dto.IsAvailable && dto.SimilarRooms is { Count: > 0 })
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

    public void LoadSampleRoomDetail(string roomId, bool isAvailable = true, bool isAuthenticated = false)
    {
        IsBusy = true;
        try
        {
            RoomId = roomId;
            CampusId = "ictu-main";
            Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m";
            Description = "Phòng mới xây, an ninh vân tay, giờ giấc tự do, gần cổng Z115. Trang bị sẵn điều hòa Inverter tiết kiệm điện, bình nóng lạnh, giường tủ gỗ và hệ thống khóa vân tay 2 lớp an ninh 24/7.";
            Rent = 1_800_000m;
            Deposit = 1_800_000m;
            Area = 22.5;
            RoomType = "Studio";
            Address = "Ngõ 112 Đường Z115, Quyết Thắng, TP. Thái Nguyên";
            DistanceMeters = 400;
            IsVerified = true;
            SecurityScore = 4.5;
            LandlordName = "Cô Nguyễn Thị Lan";
            RawLandlordPhone = "0987654321";
            _maskedOrApiLandlordPhone = "098*** (Sign in to view)";
            UpdatedAt = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
            IsAuthenticated = isAuthenticated;
            IsAvailable = isAvailable;
            UnavailableMessage = "Phòng này hiện không còn trống. Dưới đây là một số phòng tương tự đang Available.";

            HouseRules = new HouseRulesDetailModel
            {
                QuietHours = "23:00 - 05:30",
                GuestPolicy = "Khách về trước 22:30, qua đêm báo trước chủ nhà",
                CookingRules = "Được nấu ăn bằng bếp từ, không dùng bếp gas mini",
                ParkingRules = "Để xe đúng vạch tầng 1, khóa cổ xe"
            };

            Amenities.Clear();
            Amenities.Add("Wifi tốc độ cao");
            Amenities.Add("Bình nóng lạnh");
            Amenities.Add("Điều hòa Inverter");
            Amenities.Add("WC khép kín");
            Amenities.Add("Khóa vân tay 24/7");
            Amenities.Add("Ban công thoáng");

            AmenityChips.Clear();
            AmenityChips.Add(new AmenityChipItem { Icon = "📶", Name = "Wifi tốc độ cao" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🚿", Name = "Bình nóng lạnh" });
            AmenityChips.Add(new AmenityChipItem { Icon = "❄️", Name = "Điều hòa Inverter" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🚽", Name = "WC khép kín" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🔐", Name = "Khóa vân tay 24/7" });
            AmenityChips.Add(new AmenityChipItem { Icon = "🌤️", Name = "Ban công thoáng" });

            PopulatePhotosForRoom(roomId, null);

            SimilarRooms.Clear();
            if (!IsAvailable)
            {
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-001",
                    Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m",
                    Rent = 1_800_000m,
                    Area = 22.5,
                    RoomType = "Studio",
                    Address = "Ngõ 112 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
                    DistanceMeters = 400,
                    IsVerified = true,
                    AccentColorHex = "#4F46E5",
                    ImageUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80",
                    PhotoBadgeText = "📷 6 ảnh • 360°"
                });
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-002",
                    Title = "Studio Full Nội Thất Cổng Phụ ICTU",
                    Rent = 1_900_000m,
                    Area = 20.0,
                    RoomType = "Studio",
                    Address = "Ngõ 68 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                    DistanceMeters = 520,
                    IsVerified = true,
                    AccentColorHex = "#0D9488",
                    ImageUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80",
                    PhotoBadgeText = "📷 8 ảnh • 360°"
                });
                SimilarRooms.Add(new SimilarRoomItemDto
                {
                    Id = "room-2026-005",
                    Title = "Studio cao cấp gần cổng chính ICTU",
                    Rent = 2_500_000m,
                    Area = 30.0,
                    RoomType = "Studio",
                    Address = "Cổng chính ICTU, Quyết Thắng, TP. Thái Nguyên",
                    DistanceMeters = 180,
                    IsVerified = true,
                    AccentColorHex = "#7C3AED",
                    ImageUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
                    PhotoBadgeText = "📷 10 ảnh • 360°"
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulatePhotosForRoom(string roomId, IReadOnlyList<ApiPhotoDto>? apiPhotos)
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
            "room-2026-002" or "room-ictu-002" => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=900&q=80",
            "room-2026-003" or "room-ictu-003" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=900&q=80",
            "room-2026-004" or "room-ictu-004" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=900&q=80",
            "room-2026-005" or "room-ictu-005" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=900&q=80",
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
        var nextAvailable = !IsAvailable;
        var targetId = nextAvailable ? "room-2026-001" : "room-2026-004";
        LoadSampleRoomDetail(targetId, isAvailable: nextAvailable, isAuthenticated: IsAuthenticated);
        _ = FetchRoomDetailFromApiAsync(targetId);
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
        if (!string.IsNullOrWhiteSpace(selectedRoom.ImageUrl))
        {
            HeroImageUrl = selectedRoom.ImageUrl;
        }
        UpdatedAt = DateTimeOffset.Now;
        IsAvailable = true;
        SimilarRooms.Clear();

        _ = FetchRoomDetailFromApiAsync(selectedRoom.Id);
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 3)
            return "*** (Sign in to view)";

        return $"{phone[..3]}*** (Sign in to view)";
    }
}

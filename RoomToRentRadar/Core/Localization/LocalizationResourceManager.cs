using System.ComponentModel;
using System.Globalization;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Core.Localization;

/// <summary>
/// Manages reactive application localization across MAUI pages in real time.
/// </summary>
public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    private const string LanguagePreferenceKey = "AppLanguage";
    private static readonly Lazy<LocalizationResourceManager> _instance =
        new(() => new LocalizationResourceManager());

    public static LocalizationResourceManager Instance => _instance.Value;

    private CultureInfo _currentCulture;

    private LocalizationResourceManager()
    {
        var savedCode = GetStoredLanguage();
        _currentCulture = string.Equals(savedCode, "en", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("en")
            : new CultureInfo("vi");

        ApplyCultureToThreads(_currentCulture);
    }

    public CultureInfo CurrentCulture => _currentCulture;

    public bool IsVietnamese =>
        string.Equals(_currentCulture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);

    public bool IsEnglish =>
        string.Equals(_currentCulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    public string CurrentLanguageCode => IsVietnamese ? "vi" : "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            try
            {
                var localized = AppResources.ResourceManager.GetString(key, _currentCulture);
                if (!string.IsNullOrEmpty(localized))
                {
                    return localized;
                }
            }
            catch
            {
                // Fallback to internal lookup below
            }

            return GetFallbackString(key, _currentCulture);
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        _currentCulture = culture;
        ApplyCultureToThreads(culture);
        SaveStoredLanguage(culture.TwoLetterISOLanguageName);

        // Notify bindings that all translated keys have changed
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVietnamese)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCode)));
    }

    public void ToggleLanguage()
    {
        SetCulture(IsVietnamese ? new CultureInfo("en") : new CultureInfo("vi"));
    }

    private static void ApplyCultureToThreads(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private static string GetStoredLanguage()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        try
        {
            return Microsoft.Maui.Storage.Preferences.Default.Get(LanguagePreferenceKey, "vi");
        }
        catch
        {
            return "vi";
        }
#else
        return "vi";
#endif
    }

    private static void SaveStoredLanguage(string code)
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        try
        {
            Microsoft.Maui.Storage.Preferences.Default.Set(LanguagePreferenceKey, code);
        }
        catch
        {
        }
#endif
    }

    private static string GetFallbackString(string key, CultureInfo culture)
    {
        var isEn = string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);
        var map = isEn ? FallbackEnglish : FallbackVietnamese;

        if (map.TryGetValue(key, out var val))
        {
            return val;
        }

        return key;
    }

    private static readonly Dictionary<string, string> FallbackVietnamese = new(StringComparer.Ordinal)
    {
        ["App_Name"] = "Smart Rent",
        ["Campus_Location"] = "ĐH CNTT&TT (ICTU) • Thái Nguyên",
        ["Menu_Open"] = "Mở menu chính",
        ["Notification_Title"] = "Thông báo",
        ["Profile_Title"] = "Hồ sơ cá nhân",

        ["Nav_SectionTitle"] = "ĐIỀU HƯỚNG CHÍNH",
        ["Nav_Home"] = "Trang chủ",
        ["Nav_Home_Hint"] = "Đi tới trang chủ",
        ["Nav_Rooms"] = "Danh sách phòng",
        ["Nav_Rooms_Hint"] = "Đi tới danh sách phòng trọ quanh ICTU",
        ["Badge_Verified"] = "100% Xác thực",
        ["Badge_Radius"] = "Bán kính < 2km",
        ["Language_Label"] = "Ngôn ngữ / Language",
        ["Language_Vietnamese"] = "Tiếng Việt",
        ["Language_English"] = "English",
        ["Footer_Version"] = "Smart Rent • ICTU v1.0",

        ["Title_ExploreRooms"] = "Khám phá phòng trọ",
        ["Result_Summary_Format"] = "Tìm thấy {0} phòng quanh ICTU",
        ["Result_Summary_Updating"] = "Đang cập nhật danh sách phòng quanh ICTU",
        ["Search_Placeholder"] = "Tìm tên phòng, đường Z115, tiện ích...",
        ["Filter_SortTooltip"] = "Bộ lọc và sắp xếp theo giá",
        ["Filter_Active_Format"] = "Đang lọc: {0}",
        ["Sort_Newest"] = "Mới nhất",
        ["Sort_PriceAsc"] = "Giá: Thấp đến Cao",
        ["Sort_PriceDesc"] = "Giá: Cao đến Thấp",
        ["Sort_DistanceAsc"] = "Gần ICTU nhất",
        ["Filter_AirConditioner"] = "Điều hòa",
        ["Filter_Mezzanine"] = "Gác xép",
        ["Filter_FullFurniture"] = "Full nội thất",

        ["State_LoadingMessage"] = "Đang tìm phòng trọ...",
        ["State_LoadingMore"] = "Đang tải thêm phòng...",
        ["State_EmptyTitle"] = "Chưa tìm thấy phòng trọ!",
        ["State_EmptyDesc"] = "Không có phòng nào khớp với từ khóa tìm kiếm hoặc tiêu chí lọc hiện tại của bạn quanh khu vực ICTU.",
        ["State_SuggestionsTitle"] = "Gợi ý tìm kiếm:",
        ["State_Suggestion1"] = "Mở rộng khoảng giá tìm kiếm",
        ["State_Suggestion2"] = "Tăng bán kính tìm kiếm quanh trường",
        ["State_Suggestion3"] = "Thử thay đổi hoặc bỏ bớt các bộ lọc tiện ích",
        ["Action_ResetFilters"] = "Xóa bộ lọc tìm kiếm",
        ["State_ErrorTitle"] = "Không thể tải dữ liệu",
        ["State_ErrorNetwork"] = "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.",
        ["State_ErrorServer"] = "Không thể tải danh sách phòng trọ. Vui lòng thử lại sau.",
        ["Action_Retry"] = "Thử lại",

        ["Home_BannerPill"] = "CỔNG THÔNG TIN PHÒNG TRỌ SINH VIÊN ICTU",
        ["Home_Subtitle"] = "Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)",
        ["Home_IntroDesc"] = "Tra cứu nhanh phòng trọ quanh trường ĐH CNTT&TT Thái Nguyên với giá thuê minh bạch, khoảng cách thực tế tới cổng trường và bộ lọc tiện nghi trực quan.",
        ["Home_SearchPlaceholder"] = "Nhập tên đường (Z115, Quyết Thắng) hoặc tiện ích (Điều hòa, Gác xép)...",
        ["Home_CtaButton"] = "Khám phá phòng trọ ngay",
        ["Home_PopularTags"] = "Tìm nhanh phổ biến:",

        ["RoomDetails_Title"] = "Chi tiết phòng trọ",
        ["RoomDetails_Back"] = "Quay lại danh sách",
        ["RoomDetails_NotFound"] = "Không tìm thấy thông tin phòng trọ.",
        ["RoomDetails_AmenitiesTitle"] = "Tiện nghi nổi bật",

        ["Home_Tag_AirConditioner"] = "❄️ Có điều hòa",
        ["Home_Tag_PrivateBathroom"] = "🚿 Khép kín",
        ["Home_Tag_Mezzanine"] = "🪜 Có gác xép",
        ["Home_Tag_StreetZ115"] = "📍 Đường Z115",
        ["Home_Tag_WaterHeater"] = "🔥 Bình nóng lạnh",
        ["Home_Stat_VerifiedTitle"] = "100+ Phòng xác thực",
        ["Home_Stat_VerifiedDesc"] = "Hình ảnh & thông tin kiểm duyệt",
        ["Home_Stat_NearCampusTitle"] = "Gần trường < 2 km",
        ["Home_Stat_NearCampusDesc"] = "Đi bộ hoặc xe đạp tới giảng đường",
        ["Home_Stat_PriceTitle"] = "Giá sinh viên từ 1,1tr",
        ["Home_Stat_PriceDesc"] = "Phù hợp ngân sách ở ghép & ở một mình",
        ["Home_Guide_Title"] = "Tìm phòng dễ dàng chỉ với 3 bước",
        ["Home_Guide_Subtitle"] = "Được thiết kế tối ưu cho tân sinh viên và sinh viên ĐH CNTT&TT (ICTU)",
        ["Home_Guide_ViewAll"] = "Xem tất cả phòng",
        ["Home_Step1_Title"] = "1. Chọn tiêu chí & mức giá",
        ["Home_Step1_Desc"] = "Sắp xếp theo phòng mới đăng, khoảng cách gần trường hoặc giá từ thấp đến cao.",
        ["Home_Step2_Title"] = "2. Kiểm tra tiện ích sẵn có",
        ["Home_Step2_Desc"] = "Xem ngay diện tích, điều hòa, nóng lạnh, gác xép và nhãn xác thực trên từng thẻ phòng.",
        ["Home_Step3_Title"] = "3. Xem chi tiết & liên hệ",
        ["Home_Step3_Desc"] = "Bấm vào phòng bất kỳ để xem thông tin chi tiết và chọn phòng ưng ý nhanh nhất.",
        ["Listings_HeaderTitle"] = "Phòng trọ quanh ICTU",
        ["Location_ThaiNguyen"] = "Thái Nguyên",
        ["Action_Search"] = "Tìm",
        ["Sort_Prefix"] = "Sắp xếp:",
        ["Sort_Price"] = "Giá",
        ["Result_Summary_WithSearch"] = "Tìm thấy {0} phòng cho \"{1}\"",
        ["Empty_NoRooms"] = "Chưa có phòng trọ nào phù hợp.",
        ["Action_ReloadList"] = "Tải lại danh sách",
        ["Card_Verified"] = "Đã xác thực",
        ["Card_Available"] = "Còn trống",
        ["Card_ViewDetails"] = "Xem chi tiết",

        ["Filter_SheetTitle"] = "Lọc phòng trọ",
        ["Filter_SheetSubtitle"] = "Tìm chỗ ở sinh viên phù hợp nhất",
        ["Filter_PriceRange"] = "Khoảng giá",
        ["Filter_PriceUnder1M"] = "Dưới 1M",
        ["Filter_Price1MTo2M"] = "1M – 2M",
        ["Filter_Price2MTo3M"] = "2M – 3M",
        ["Filter_MinPrice"] = "Giá tối thiểu",
        ["Filter_MaxPrice"] = "Giá tối đa",
        ["Filter_RoomType"] = "Loại phòng",
        ["Filter_PrivateRoom"] = "Phòng riêng",
        ["Filter_SharedRoom"] = "Ở ghép",
        ["Filter_Amenities"] = "Tiện nghi",
        ["Filter_Amenity_AirCon"] = "Điều hòa",
        ["Filter_Amenity_Wifi"] = "Wi-Fi",
        ["Filter_Amenity_Parking"] = "Chỗ để xe",
        ["Filter_Amenity_Bathroom"] = "Khép kín",
        ["Filter_Amenity_Washing"] = "Máy giặt",
        ["Filter_Amenity_Kitchen"] = "Bếp nấu",
        ["Filter_ResetAll"] = "Đặt lại",
        ["Filter_ShowResults_Format"] = "Xem kết quả ({0})"
    };

    private static readonly Dictionary<string, string> FallbackEnglish = new(StringComparer.Ordinal)
    {
        ["App_Name"] = "Smart Rent",
        ["Campus_Location"] = "ICTU Campus • Thai Nguyen",
        ["Menu_Open"] = "Open main menu",
        ["Notification_Title"] = "Notifications",
        ["Profile_Title"] = "Profile",

        ["Nav_SectionTitle"] = "MAIN NAVIGATION",
        ["Nav_Home"] = "Home",
        ["Nav_Home_Hint"] = "Go to home page",
        ["Nav_Rooms"] = "Room Listings",
        ["Nav_Rooms_Hint"] = "Go to room listings around ICTU",
        ["Badge_Verified"] = "100% Verified",
        ["Badge_Radius"] = "Radius < 2km",
        ["Language_Label"] = "Language",
        ["Language_Vietnamese"] = "Tiếng Việt",
        ["Language_English"] = "English",
        ["Footer_Version"] = "Smart Rent • ICTU v1.0",

        ["Title_ExploreRooms"] = "Explore Rooms",
        ["Result_Summary_Format"] = "Found {0} rooms around ICTU",
        ["Result_Summary_Updating"] = "Updating room listings around ICTU",
        ["Search_Placeholder"] = "Search by room name, street, amenities...",
        ["Filter_SortTooltip"] = "Filter and sort by price",
        ["Filter_Active_Format"] = "Filtering: {0}",
        ["Sort_Newest"] = "Newest",
        ["Sort_PriceAsc"] = "Price: Low to High",
        ["Sort_PriceDesc"] = "Price: High to Low",
        ["Sort_DistanceAsc"] = "Closest to ICTU",
        ["Filter_AirConditioner"] = "Air conditioner",
        ["Filter_Mezzanine"] = "Mezzanine",
        ["Filter_FullFurniture"] = "Full furniture",

        ["State_LoadingMessage"] = "Searching for rooms...",
        ["State_LoadingMore"] = "Loading more rooms...",
        ["State_EmptyTitle"] = "No rooms found!",
        ["State_EmptyDesc"] = "No properties match your current search keywords or filters around the ICTU campus.",
        ["State_SuggestionsTitle"] = "Search suggestions:",
        ["State_Suggestion1"] = "Expand rental price range",
        ["State_Suggestion2"] = "Increase search radius around campus",
        ["State_Suggestion3"] = "Try adjusting or clearing amenity filters",
        ["Action_ResetFilters"] = "Reset search filters",
        ["State_ErrorTitle"] = "Unable to load data",
        ["State_ErrorNetwork"] = "Cannot connect to server. Please check your network connection.",
        ["State_ErrorServer"] = "Cannot load room listings. Please try again later.",
        ["Action_Retry"] = "Retry",

        ["Home_BannerPill"] = "ICTU STUDENT ROOM RENTAL PORTAL",
        ["Home_Subtitle"] = "Find your ideal room around ICTU campus",
        ["Home_IntroDesc"] = "Quickly look up rooms around ICTU Thai Nguyen with transparent prices, actual distance to campus, and intuitive filters.",
        ["Home_SearchPlaceholder"] = "Enter street name (Z115, Quyet Thang) or amenities (Air-con, Loft)...",
        ["Home_CtaButton"] = "Explore rooms now",
        ["Home_PopularTags"] = "Popular searches:",

        ["RoomDetails_Title"] = "Room Details",
        ["RoomDetails_Back"] = "Back to listings",
        ["RoomDetails_NotFound"] = "Room information not found.",
        ["RoomDetails_AmenitiesTitle"] = "Highlights & Amenities",

        ["Home_Tag_AirConditioner"] = "❄️ Air conditioner",
        ["Home_Tag_PrivateBathroom"] = "🚿 Private bath",
        ["Home_Tag_Mezzanine"] = "🪜 Mezzanine",
        ["Home_Tag_StreetZ115"] = "📍 Z115 Street",
        ["Home_Tag_WaterHeater"] = "🔥 Water heater",
        ["Home_Stat_VerifiedTitle"] = "100+ Verified rooms",
        ["Home_Stat_VerifiedDesc"] = "Curated photos & verified info",
        ["Home_Stat_NearCampusTitle"] = "Near campus < 2 km",
        ["Home_Stat_NearCampusDesc"] = "Walk or bike to lecture halls",
        ["Home_Stat_PriceTitle"] = "Student prices from 1.1M",
        ["Home_Stat_PriceDesc"] = "Fits solo or shared room budgets",
        ["Home_Guide_Title"] = "Find a room easily in 3 steps",
        ["Home_Guide_Subtitle"] = "Optimized for ICTU students and freshmen",
        ["Home_Guide_ViewAll"] = "View all rooms",
        ["Home_Step1_Title"] = "1. Select criteria & price",
        ["Home_Step1_Desc"] = "Sort by latest listings, closest to campus, or lowest price.",
        ["Home_Step2_Title"] = "2. Check available amenities",
        ["Home_Step2_Desc"] = "Check room area, AC, water heater, mezzanine, and verified badge on each card.",
        ["Home_Step3_Title"] = "3. View details & contact",
        ["Home_Step3_Desc"] = "Tap any room to see complete details and reach landlords directly.",
        ["Listings_HeaderTitle"] = "Rooms around ICTU",
        ["Location_ThaiNguyen"] = "Thai Nguyen",
        ["Action_Search"] = "Search",
        ["Sort_Prefix"] = "Sort:",
        ["Sort_Price"] = "Price",
        ["Result_Summary_WithSearch"] = "Found {0} rooms for \"{1}\"",
        ["Empty_NoRooms"] = "No matching rooms found.",
        ["Action_ReloadList"] = "Reload listings",
        ["Card_Verified"] = "Verified",
        ["Card_Available"] = "Available",
        ["Card_ViewDetails"] = "View details",

        ["Filter_SheetTitle"] = "Filter Rooms",
        ["Filter_SheetSubtitle"] = "Find your ideal student accommodation",
        ["Filter_PriceRange"] = "Price Range",
        ["Filter_PriceUnder1M"] = "Under 1M",
        ["Filter_Price1MTo2M"] = "1M – 2M",
        ["Filter_Price2MTo3M"] = "2M – 3M",
        ["Filter_MinPrice"] = "Min Price",
        ["Filter_MaxPrice"] = "Max Price",
        ["Filter_RoomType"] = "Room Type",
        ["Filter_PrivateRoom"] = "Private Room",
        ["Filter_SharedRoom"] = "Shared Room",
        ["Filter_Amenities"] = "Amenities",
        ["Filter_Amenity_AirCon"] = "Air Conditioner",
        ["Filter_Amenity_Wifi"] = "Wi-Fi",
        ["Filter_Amenity_Parking"] = "Parking",
        ["Filter_Amenity_Bathroom"] = "Private Bathroom",
        ["Filter_Amenity_Washing"] = "Washing Machine",
        ["Filter_Amenity_Kitchen"] = "Kitchen",
        ["Filter_ResetAll"] = "Reset All",
        ["Filter_ShowResults_Format"] = "Show Results ({0})"
    };
}

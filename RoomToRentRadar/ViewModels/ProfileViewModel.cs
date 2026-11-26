using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Profile;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.ViewModels;

public enum ProfileState
{
    Loading,
    Content,
    SignedOut,
    Error
}

public partial class ProfileViewModel : ObservableObject
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IProfileClient _client;
    private readonly AuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IAlertPrompt _alertPrompt;
    private readonly IErrorHandler _errorHandler;
    private readonly IProfileDetailsStore _detailsStore;

    public ProfileViewModel(
        IProfileClient client,
        AuthService authService,
        INavigationService navigationService,
        IAlertPrompt alertPrompt,
        IErrorHandler errorHandler,
        IProfileDetailsStore detailsStore)
    {
        _client = client;
        _authService = authService;
        _navigationService = navigationService;
        _alertPrompt = alertPrompt;
        _errorHandler = errorHandler;
        _detailsStore = detailsStore;
        State = ProfileState.Loading;
        DisplayName = string.Empty;
        Email = string.Empty;
        RoleText = string.Empty;
        MemberSinceText = string.Empty;
        Initials = string.Empty;
        AvatarPath = string.Empty;
        IntroductionText = string.Empty;
        MenuSectionTitle = string.Empty;
        AverageRatingText = string.Empty;
        TotalReviewsText = string.Empty;
        Message = string.Empty;
        PersonalInfoRows = [];
        RentalPreferenceRows = [];
        BusinessInfoRows = [];
        ReputationRows = [];
        VerificationItems = [];
        MenuItems = [];
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsContentState))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutState))]
    [NotifyPropertyChangedFor(nameof(IsErrorState))]
    public partial ProfileState State { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    public partial string RoleText { get; set; }

    [ObservableProperty]
    public partial string MemberSinceText { get; set; }

    [ObservableProperty]
    public partial string Initials { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    public partial string AvatarPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditProfile))]
    public partial bool IsStudent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditProfile))]
    public partial bool IsLandlord { get; set; }

    [ObservableProperty]
    public partial bool IsStaff { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIntroduction))]
    public partial string IntroductionText { get; set; }

    [ObservableProperty]
    public partial string MenuSectionTitle { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfoRow> PersonalInfoRows { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfoRow> RentalPreferenceRows { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfoRow> BusinessInfoRows { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfoRow> ReputationRows { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileVerificationItem> VerificationItems { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileMenuItem> MenuItems { get; set; }

    [ObservableProperty]
    public partial int TotalRooms { get; set; }

    [ObservableProperty]
    public partial int AvailableRooms { get; set; }

    [ObservableProperty]
    public partial int OccupiedRooms { get; set; }

    [ObservableProperty]
    public partial int PendingRooms { get; set; }

    [ObservableProperty]
    public partial string AverageRatingText { get; set; }

    [ObservableProperty]
    public partial string TotalReviewsText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; }

    public bool IsLoadingState => State == ProfileState.Loading;

    public bool IsContentState => State == ProfileState.Content;

    public bool IsSignedOutState => State == ProfileState.SignedOut;

    public bool IsErrorState => State == ProfileState.Error;

    public bool HasAvatar => !string.IsNullOrEmpty(AvatarPath);

    public bool HasIntroduction => !string.IsNullOrWhiteSpace(IntroductionText);

    public bool CanEditProfile => IsStudent || IsLandlord;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand]
    private Task SignInAsync() => _navigationService.GoToLoginAsync();

    [RelayCommand]
    private Task EditProfileAsync() => _navigationService.GoToEditProfileAsync();

    [RelayCommand]
    private Task OpenSettingsAsync() => _navigationService.GoToAccountSettingsAsync();

    [RelayCommand]
    private Task ShowNotificationsAsync() => ShowComingSoonAsync("Trung tâm thông báo");

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        State = ProfileState.Loading;
        Message = string.Empty;
        try
        {
            if (!await _authService.IsSignedInAsync(cancellationToken))
            {
                ClearProfile();
                State = ProfileState.SignedOut;
                return;
            }

            Apply(await _client.GetProfileAsync(cancellationToken));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearProfile();
            State = ProfileState.SignedOut;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = ProfileState.Error;
            Message = "Không thể tải hồ sơ. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        var confirmed = await _alertPrompt.ConfirmAsync(
            "Đăng xuất",
            "Bạn có chắc muốn đăng xuất khỏi tài khoản này?",
            "Đăng xuất",
            "Hủy",
            cancellationToken);
        if (!confirmed)
        {
            return;
        }

        await _authService.SignOutAsync(cancellationToken);
        ClearProfile();
        State = ProfileState.SignedOut;
        await _navigationService.GoToRoleRootAsync(null);
    }

    private void Apply(UserProfileDto profile)
    {
        DisplayName = profile.DisplayName;
        Email = profile.Email;
        var role = AppUserRoleWire.TryParse(profile.Role) ?? AppUserRole.Student;
        RoleText = FormatRole(role);
        MemberSinceText = string.Create(
            CultureInfo.InvariantCulture,
            $"Thành viên từ {profile.CreatedAt.ToLocalTime():dd/MM/yyyy}");
        Initials = BuildInitials(profile.DisplayName);
        IsStudent = role == AppUserRole.Student;
        IsLandlord = role == AppUserRole.Landlord;
        IsStaff = role is AppUserRole.Moderator or AppUserRole.Admin;
        AvatarPath = string.Empty;
        IntroductionText = string.Empty;
        PersonalInfoRows = [];
        RentalPreferenceRows = [];
        BusinessInfoRows = [];
        ReputationRows = [];
        VerificationItems = [];

        if (IsStudent)
        {
            ApplyStudent(_detailsStore.GetStudentDetails(profile.UserId, profile.DisplayName));
        }
        else if (IsLandlord)
        {
            ApplyLandlord(_detailsStore.GetLandlordDetails(profile.UserId, profile.DisplayName));
        }

        MenuSectionTitle = IsLandlord ? "Quản lý cho thuê" : "Tiện ích của bạn";
        MenuItems = BuildMenuItems();
        State = ProfileState.Content;
        _authService.UpdateCurrentDisplayName(profile.DisplayName);
    }

    private void ApplyStudent(StudentProfileDetails details)
    {
        AvatarPath = details.AvatarPath;
        IntroductionText = details.Introduction;
        PersonalInfoRows =
        [
            new("Họ và tên", ValueOrFallback(details.FullName)),
            new("Ngày sinh", FormatDate(details.DateOfBirth)),
            new("Giới tính", ValueOrFallback(details.Gender)),
            new("Số điện thoại", ValueOrFallback(details.Phone)),
            new("Email", Email),
            new("Trường đại học", ValueOrFallback(details.University)),
            new("Chuyên ngành", ValueOrFallback(details.Major)),
            new("Năm học", ValueOrFallback(details.AcademicYear))
        ];
        RentalPreferenceRows =
        [
            new("Ngân sách", ValueOrFallback(details.Budget)),
            new("Khu vực mong muốn", ValueOrFallback(details.PreferredLocation)),
            new("Loại phòng", ValueOrFallback(details.RoomType)),
            new("Ngày dọn vào", FormatDate(details.MoveInDate)),
            new("Thời hạn thuê", ValueOrFallback(details.RentalDuration)),
            new("Số người ở", ValueOrFallback(details.Occupants)),
            new("Yêu cầu giới tính", ValueOrFallback(details.GenderPreference)),
            new("Thú cưng", details.HasPets ? "Có" : "Không"),
            new("Hút thuốc", details.IsSmoker ? "Có" : "Không")
        ];
    }

    private void ApplyLandlord(LandlordProfileDetails details)
    {
        AvatarPath = details.AvatarPath;
        IntroductionText = details.Introduction;
        BusinessInfoRows =
        [
            new("Họ và tên", ValueOrFallback(details.FullName)),
            new("Số điện thoại", ValueOrFallback(details.Phone)),
            new("Email", Email),
            new("Loại tài khoản", ValueOrFallback(details.AccountType)),
            new("Tên cơ sở", ValueOrFallback(details.BusinessName)),
            new("Địa chỉ liên hệ", ValueOrFallback(details.ContactAddress))
        ];
        VerificationItems =
        [
            new("Số điện thoại", details.PhoneVerification),
            new("Email", details.EmailVerification),
            new("Danh tính (CCCD)", details.IdentityVerification),
            new("Quyền sở hữu phòng", details.PropertyOwnershipVerification)
        ];
        TotalRooms = details.RoomStats.Total;
        AvailableRooms = details.RoomStats.Available;
        OccupiedRooms = details.RoomStats.Occupied;
        PendingRooms = details.RoomStats.Pending;
        AverageRatingText = details.Reputation.AverageRating.ToString("0.0", VietnameseCulture);
        TotalReviewsText = $"{details.Reputation.TotalReviews} đánh giá";
        ReputationRows =
        [
            new("Thành viên từ", MemberSinceText.Replace("Thành viên từ ", string.Empty)),
            new("Tỷ lệ phản hồi", $"{details.Reputation.ResponseRatePercent}%")
        ];
    }

    private IReadOnlyList<ProfileMenuItem> BuildMenuItems()
    {
        if (IsLandlord)
        {
            return
            [
                CreateMenuItem(
                    "Tin đăng của tôi",
                    "Quản lý tin cho thuê đã đăng",
                    "listings_tab.png",
                    "ProfileMenuMyListings",
                    () => _navigationService.GoToMyListingsAsync()),
                CreateComingSoonItem(
                    "Quản lý phòng",
                    "Tình trạng các phòng đang cho thuê",
                    "home_icon.png",
                    "ProfileMenuManageRooms"),
                CreateComingSoonItem(
                    "Yêu cầu thuê",
                    "Yêu cầu thuê từ sinh viên",
                    "icon_requests.png",
                    "ProfileMenuLandlordRequests"),
                CreateComingSoonItem(
                    "Lịch xem phòng",
                    "Lịch hẹn xem phòng sắp tới",
                    "icon_calendar.png",
                    "ProfileMenuLandlordAppointments"),
                CreateComingSoonItem(
                    "Đánh giá & xếp hạng",
                    "Nhận xét từ người thuê",
                    "icon_star.png",
                    "ProfileMenuLandlordReviews")
            ];
        }

        if (IsStudent)
        {
            return
            [
                CreateMenuItem(
                    "Phòng yêu thích",
                    "Danh sách phòng bạn đã lưu",
                    "favourites_tab.png",
                    "ProfileMenuFavourites",
                    () => _navigationService.GoToFavouritesAsync()),
                CreateComingSoonItem(
                    "Lịch sử tìm kiếm",
                    "Các tìm kiếm gần đây của bạn",
                    "icon_history.png",
                    "ProfileMenuSearchHistory"),
                CreateComingSoonItem(
                    "Yêu cầu thuê",
                    "Trạng thái các yêu cầu thuê phòng",
                    "icon_requests.png",
                    "ProfileMenuRentalRequests"),
                CreateComingSoonItem(
                    "Lịch xem phòng",
                    "Các buổi hẹn xem phòng sắp tới",
                    "icon_calendar.png",
                    "ProfileMenuViewingAppointments"),
                CreateComingSoonItem(
                    "Tin nhắn",
                    "Trao đổi với chủ phòng",
                    "icon_chat.png",
                    "ProfileMenuMessages"),
                CreateComingSoonItem(
                    "Đánh giá của tôi",
                    "Đánh giá bạn đã gửi",
                    "icon_star.png",
                    "ProfileMenuMyReviews")
            ];
        }

        return [];
    }

    private ProfileMenuItem CreateMenuItem(
        string title,
        string description,
        string icon,
        string automationId,
        Func<Task> action)
        => new(title, description, icon, automationId, new AsyncRelayCommand(action));

    private ProfileMenuItem CreateComingSoonItem(
        string title,
        string description,
        string icon,
        string automationId)
        => new(
            title,
            description,
            icon,
            automationId,
            new AsyncRelayCommand(() => ShowComingSoonAsync(title)),
            isComingSoon: true);

    private Task ShowComingSoonAsync(string feature)
        => _alertPrompt.ShowAsync(
            "Sắp ra mắt",
            $"{feature} sẽ có trong bản cập nhật tới.",
            "Đã hiểu");

    private void ClearProfile()
    {
        DisplayName = string.Empty;
        Email = string.Empty;
        RoleText = string.Empty;
        MemberSinceText = string.Empty;
        Initials = string.Empty;
        AvatarPath = string.Empty;
        IntroductionText = string.Empty;
        IsStudent = false;
        IsLandlord = false;
        IsStaff = false;
        MenuSectionTitle = string.Empty;
        MenuItems = [];
        PersonalInfoRows = [];
        RentalPreferenceRows = [];
        BusinessInfoRows = [];
        ReputationRows = [];
        VerificationItems = [];
        TotalRooms = 0;
        AvailableRooms = 0;
        OccupiedRooms = 0;
        PendingRooms = 0;
        AverageRatingText = string.Empty;
        TotalReviewsText = string.Empty;
    }

    private static string FormatRole(AppUserRole role) => role switch
    {
        AppUserRole.Landlord => "Chủ phòng",
        AppUserRole.Moderator => "Kiểm duyệt viên",
        AppUserRole.Admin => "Quản trị viên",
        _ => "Sinh viên"
    };

    private static string ValueOrFallback(string value)
        => string.IsNullOrWhiteSpace(value) ? "Chưa cập nhật" : value;

    private static string FormatDate(DateTime? date)
        => date is { } value
            ? value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "Chưa cập nhật";

    private static string BuildInitials(string displayName)
    {
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => words[0][..1].ToUpperInvariant(),
            _ => string.Concat(words[0][..1], words[^1][..1]).ToUpperInvariant()
        };
    }
}

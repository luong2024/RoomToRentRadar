using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Media;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Profile;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Validations;

namespace RoomToRentRadar.ViewModels;

public partial class EditProfileViewModel : ObservableObject
{
    private readonly IProfileClient _client;
    private readonly AuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IAlertPrompt _alertPrompt;
    private readonly IErrorHandler _errorHandler;
    private readonly IProfileDetailsStore _detailsStore;
    private readonly IAvatarPicker _avatarPicker;

    private Guid _userId;
    private string _originalDisplayName = string.Empty;
    private StudentProfileDetails? _studentSource;
    private LandlordProfileDetails? _landlordSource;

    public EditProfileViewModel(
        IProfileClient client,
        AuthService authService,
        INavigationService navigationService,
        IAlertPrompt alertPrompt,
        IErrorHandler errorHandler,
        IProfileDetailsStore detailsStore,
        IAvatarPicker avatarPicker)
    {
        _client = client;
        _authService = authService;
        _navigationService = navigationService;
        _alertPrompt = alertPrompt;
        _errorHandler = errorHandler;
        _detailsStore = detailsStore;
        _avatarPicker = avatarPicker;
        State = ProfileState.Loading;
        Email = string.Empty;
        FullName = string.Empty;
        Phone = string.Empty;
        Gender = string.Empty;
        University = string.Empty;
        Major = string.Empty;
        AcademicYear = string.Empty;
        Budget = string.Empty;
        PreferredLocation = string.Empty;
        RoomType = string.Empty;
        RentalDuration = string.Empty;
        Occupants = string.Empty;
        GenderPreference = string.Empty;
        Introduction = string.Empty;
        BusinessName = string.Empty;
        ContactAddress = string.Empty;
        AvatarPath = string.Empty;
        FullNameError = string.Empty;
        PhoneError = string.Empty;
        DateOfBirthError = string.Empty;
        Message = string.Empty;
        DateOfBirth = new DateTime(2000, 1, 1);
        MoveInDate = DateTime.Today.AddDays(30);
    }

    public IReadOnlyList<string> GenderOptions { get; } = ["Nam", "Nữ", "Khác"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsContentState))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutState))]
    [NotifyPropertyChangedFor(nameof(IsErrorState))]
    public partial ProfileState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditProfile))]
    public partial bool IsStudent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditProfile))]
    public partial bool IsLandlord { get; set; }

    [ObservableProperty]
    public partial bool IsStaff { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    public partial string AvatarPath { get; set; }

    [ObservableProperty]
    public partial string FullName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFullNameError))]
    public partial string FullNameError { get; set; }

    [ObservableProperty]
    public partial string Phone { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoneError))]
    public partial string PhoneError { get; set; }

    [ObservableProperty]
    public partial DateTime DateOfBirth { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDateOfBirthError))]
    public partial string DateOfBirthError { get; set; }

    [ObservableProperty]
    public partial string Gender { get; set; }

    [ObservableProperty]
    public partial string University { get; set; }

    [ObservableProperty]
    public partial string Major { get; set; }

    [ObservableProperty]
    public partial string AcademicYear { get; set; }

    [ObservableProperty]
    public partial string Budget { get; set; }

    [ObservableProperty]
    public partial string PreferredLocation { get; set; }

    [ObservableProperty]
    public partial string RoomType { get; set; }

    [ObservableProperty]
    public partial DateTime MoveInDate { get; set; }

    [ObservableProperty]
    public partial string RentalDuration { get; set; }

    [ObservableProperty]
    public partial string Occupants { get; set; }

    [ObservableProperty]
    public partial string GenderPreference { get; set; }

    [ObservableProperty]
    public partial bool HasPets { get; set; }

    [ObservableProperty]
    public partial bool IsSmoker { get; set; }

    [ObservableProperty]
    public partial string Introduction { get; set; }

    [ObservableProperty]
    public partial string BusinessName { get; set; }

    [ObservableProperty]
    public partial string ContactAddress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; }

    public bool IsLoadingState => State == ProfileState.Loading;

    public bool IsContentState => State == ProfileState.Content;

    public bool IsSignedOutState => State == ProfileState.SignedOut;

    public bool IsErrorState => State == ProfileState.Error;

    public bool CanEditProfile => IsStudent || IsLandlord;

    public bool HasAvatar => !string.IsNullOrEmpty(AvatarPath);

    public bool HasFullNameError => !string.IsNullOrEmpty(FullNameError);

    public bool HasPhoneError => !string.IsNullOrEmpty(PhoneError);

    public bool HasDateOfBirthError => !string.IsNullOrEmpty(DateOfBirthError);

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        State = ProfileState.Loading;
        Message = string.Empty;
        try
        {
            if (!await _authService.IsSignedInAsync(cancellationToken))
            {
                State = ProfileState.SignedOut;
                return;
            }

            var profile = await _client.GetProfileAsync(cancellationToken);
            _userId = profile.UserId;
            _originalDisplayName = profile.DisplayName;
            Email = profile.Email;
            var role = AppUserRoleWire.TryParse(profile.Role) ?? AppUserRole.Student;
            IsStudent = role == AppUserRole.Student;
            IsLandlord = role == AppUserRole.Landlord;
            IsStaff = role is AppUserRole.Moderator or AppUserRole.Admin;

            if (IsStudent)
            {
                PopulateStudent(_detailsStore.GetStudentDetails(profile.UserId, profile.DisplayName));
            }
            else if (IsLandlord)
            {
                PopulateLandlord(_detailsStore.GetLandlordDetails(profile.UserId, profile.DisplayName));
            }

            State = ProfileState.Content;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
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
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (State != ProfileState.Content || !CanEditProfile || IsSaving || !Validate())
        {
            return;
        }

        IsSaving = true;
        Message = string.Empty;
        try
        {
            var fullName = FullName.Trim();
            if (!string.Equals(fullName, _originalDisplayName, StringComparison.Ordinal))
            {
                await _client.UpdateDisplayNameAsync(fullName, cancellationToken);
                _originalDisplayName = fullName;
            }

            _authService.UpdateCurrentDisplayName(fullName);
            if (IsStudent)
            {
                SaveStudent(fullName);
            }
            else if (IsLandlord)
            {
                SaveLandlord(fullName);
            }

            await _alertPrompt.ShowAsync(
                "Đã lưu thay đổi",
                "Hồ sơ của bạn đã được cập nhật.",
                "OK",
                cancellationToken);
            await _navigationService.GoBackAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest
            && ex.Code == "invalid_display_name")
        {
            FullNameError = $"Họ tên không hợp lệ (tối đa {AccountRules.DisplayNameMaxLength} ký tự).";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = "Chưa lưu được hồ sơ. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task PickAvatarAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pickedPath = await _avatarPicker.PickAvatarAsync(_userId, cancellationToken);
            if (pickedPath is not null)
            {
                AvatarPath = pickedPath;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = "Không thể chọn ảnh đại diện. Thử lại sau.";
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigationService.GoBackAsync();

    [RelayCommand]
    private Task SignInAsync() => _navigationService.GoToLoginAsync();

    partial void OnFullNameChanged(string value) => FullNameError = string.Empty;

    partial void OnPhoneChanged(string value) => PhoneError = string.Empty;

    partial void OnDateOfBirthChanged(DateTime value) => DateOfBirthError = string.Empty;

    private bool Validate()
    {
        FullNameError = string.Empty;
        PhoneError = string.Empty;
        DateOfBirthError = string.Empty;
        var isValid = true;

        if (!AccountRules.IsDisplayNameValid(FullName.Trim()))
        {
            FullNameError = $"Nhập họ tên (tối đa {AccountRules.DisplayNameMaxLength} ký tự).";
            isValid = false;
        }

        if (!ProfileFormValidator.IsValidPhone(Phone))
        {
            PhoneError = "Số điện thoại không hợp lệ (VD: 0901234567).";
            isValid = false;
        }

        if (IsStudent && !ProfileFormValidator.IsValidBirthDate(DateOfBirth))
        {
            DateOfBirthError = "Ngày sinh phải là một ngày trong quá khứ.";
            isValid = false;
        }

        return isValid;
    }

    private void PopulateStudent(StudentProfileDetails details)
    {
        _studentSource = details;
        AvatarPath = details.AvatarPath;
        FullName = details.FullName;
        Phone = details.Phone;
        DateOfBirth = details.DateOfBirth ?? DateTime.Today.AddYears(-20);
        Gender = details.Gender;
        University = details.University;
        Major = details.Major;
        AcademicYear = details.AcademicYear;
        Budget = details.Budget;
        PreferredLocation = details.PreferredLocation;
        RoomType = details.RoomType;
        MoveInDate = details.MoveInDate ?? DateTime.Today.AddDays(30);
        RentalDuration = details.RentalDuration;
        Occupants = details.Occupants;
        GenderPreference = details.GenderPreference;
        HasPets = details.HasPets;
        IsSmoker = details.IsSmoker;
        Introduction = details.Introduction;
    }

    private void PopulateLandlord(LandlordProfileDetails details)
    {
        _landlordSource = details;
        AvatarPath = details.AvatarPath;
        FullName = details.FullName;
        Phone = details.Phone;
        BusinessName = details.BusinessName;
        ContactAddress = details.ContactAddress;
        Introduction = details.Introduction;
    }

    private void SaveStudent(string fullName)
    {
        _detailsStore.SaveStudentDetails(_userId, new StudentProfileDetails
        {
            FullName = fullName,
            AvatarPath = AvatarPath,
            DateOfBirth = DateOfBirth.Date,
            Gender = Gender,
            Phone = ProfileFormValidator.NormalizePhone(Phone),
            University = University.Trim(),
            Major = Major.Trim(),
            AcademicYear = AcademicYear.Trim(),
            Budget = Budget.Trim(),
            PreferredLocation = PreferredLocation.Trim(),
            RoomType = RoomType.Trim(),
            MoveInDate = MoveInDate.Date,
            RentalDuration = RentalDuration.Trim(),
            Occupants = Occupants.Trim(),
            GenderPreference = GenderPreference.Trim(),
            HasPets = HasPets,
            IsSmoker = IsSmoker,
            Introduction = Introduction.Trim()
        });
    }

    private void SaveLandlord(string fullName)
    {
        var source = _landlordSource;
        _detailsStore.SaveLandlordDetails(_userId, new LandlordProfileDetails
        {
            FullName = fullName,
            AvatarPath = AvatarPath,
            Phone = ProfileFormValidator.NormalizePhone(Phone),
            AccountType = source?.AccountType ?? "Cá nhân",
            BusinessName = BusinessName.Trim(),
            ContactAddress = ContactAddress.Trim(),
            Introduction = Introduction.Trim(),
            PhoneVerification = source?.PhoneVerification ?? ProfileVerificationStatus.Pending,
            EmailVerification = source?.EmailVerification ?? ProfileVerificationStatus.Verified,
            IdentityVerification = source?.IdentityVerification ?? ProfileVerificationStatus.Pending,
            PropertyOwnershipVerification = source?.PropertyOwnershipVerification ?? ProfileVerificationStatus.Unverified,
            RoomStats = source?.RoomStats.Clone() ?? new ProfileRoomStats(),
            Reputation = source?.Reputation.Clone() ?? new ProfileReputation()
        });
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.Profile.Models;
using RoomToRentRadar.Features.Profile.Services;

namespace RoomToRentRadar.Features.Profile.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private const int MaxAvatarBytes = 1024 * 1024;
    private readonly IProfileService _profileService;
    private readonly IErrorHandler _errorHandler;
    private string? _pendingAvatarData;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string? _phoneNumber;
    [ObservableProperty] private string? _bio;
    [ObservableProperty] private string? _avatarUrl;
    [ObservableProperty] private ImageSource? _avatarPreview;
    [ObservableProperty] private string? _studentId;
    [ObservableProperty] private string? _university;
    [ObservableProperty] private string? _faculty;
    [ObservableProperty] private string? _maxBudgetText;
    [ObservableProperty] private string? _businessName;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _verificationStatus;
    [ObservableProperty] private string? _staffCode;
    [ObservableProperty] private string? _department;
    [ObservableProperty] private string? _jobTitle;

    public bool IsStudent => string.Equals(Role, "STUDENT", StringComparison.OrdinalIgnoreCase);
    public bool IsLandlord => string.Equals(Role, "LANDLORD", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => string.Equals(Role, "ADMIN", StringComparison.OrdinalIgnoreCase);
    public string RoleDisplay => IsStudent ? "Student" : IsLandlord ? "Landlord" : IsAdmin ? "Administrator" : Role;

    public ProfileViewModel(IProfileService profileService, IErrorHandler errorHandler)
    {
        _profileService = profileService;
        _errorHandler = errorHandler;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var profile = await _profileService.GetMyProfileAsync();
            if (profile is null) throw new InvalidOperationException("Your profile could not be loaded.");
            Apply(profile);
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load your profile. Please try again.";
            _errorHandler.HandleError(ex);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task ChooseAvatarAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a profile photo",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["image/jpeg", "image/png"],
                    [DevicePlatform.iOS] = ["public.jpeg", "public.png"],
                    [DevicePlatform.WinUI] = [".jpg", ".jpeg", ".png"]
                })
            });
            if (result is null) return;

            var contentType = result.ContentType?.ToLowerInvariant();
            if (contentType is not ("image/jpeg" or "image/png"))
            {
                StatusMessage = "Please choose a JPEG or PNG image.";
                return;
            }

            await using var stream = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            if (memory.Length > MaxAvatarBytes)
            {
                StatusMessage = "Avatar image must be 1 MB or smaller.";
                return;
            }

            var bytes = memory.ToArray();
            _pendingAvatarData = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
            AvatarPreview = ImageSource.FromStream(() => new MemoryStream(bytes));
            StatusMessage = "Avatar selected. Save changes to upload it.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to select the avatar image.";
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (!decimal.TryParse(MaxBudgetText, out var maxBudget) && !string.IsNullOrWhiteSpace(MaxBudgetText))
        {
            StatusMessage = "Maximum budget must be a valid number.";
            return;
        }

        try
        {
            IsBusy = true;
            var saved = await _profileService.UpdateAsync(BuildProfile(string.IsNullOrWhiteSpace(MaxBudgetText) ? null : maxBudget), _pendingAvatarData);
            if (saved is null) throw new InvalidOperationException("The profile update returned no data.");
            _pendingAvatarData = null;
            Apply(saved);
            StatusMessage = "Profile saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to save your profile. Please check your details and try again.";
            _errorHandler.HandleError(ex);
        }
        finally { IsBusy = false; }
    }

    partial void OnRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsStudent)); OnPropertyChanged(nameof(IsLandlord));
        OnPropertyChanged(nameof(IsAdmin)); OnPropertyChanged(nameof(RoleDisplay));
    }

    private void Apply(ProfileApiDto profile)
    {
        Role = profile.Role; FullName = profile.FullName; PhoneNumber = profile.PhoneNumber; Bio = profile.Bio; AvatarUrl = profile.AvatarUrl;
        StudentId = profile.Tenant?.StudentId; University = profile.Tenant?.University; Faculty = profile.Tenant?.Faculty;
        MaxBudgetText = profile.Tenant?.MaxBudget?.ToString(); BusinessName = profile.Landlord?.BusinessName;
        Address = profile.Landlord?.Address; VerificationStatus = profile.Landlord?.VerificationStatus;
        StaffCode = profile.Admin?.StaffCode; Department = profile.Admin?.Department; JobTitle = profile.Admin?.Title;
        AvatarPreview = ToImageSource(profile.AvatarUrl);
    }

    private ProfileApiDto BuildProfile(decimal? maxBudget) => new()
    {
        Role = Role, FullName = FullName, PhoneNumber = PhoneNumber, Bio = Bio, AvatarUrl = AvatarUrl,
        Tenant = new() { StudentId = StudentId, University = University, Faculty = Faculty, MaxBudget = maxBudget },
        Landlord = new() { BusinessName = BusinessName, Address = Address },
        Admin = new() { StaffCode = StaffCode, Department = Department, Title = JobTitle }
    };

    private static ImageSource? ToImageSource(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        ? ImageSource.FromUri(uri) : null;
}

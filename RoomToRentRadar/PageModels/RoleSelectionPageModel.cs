using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.PageModels;

public partial class RoleSelectionPageModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public RoleSelectionPageModel(INavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [ObservableProperty]
    public partial string SelectedRole { get; set; } = "Student";

    [ObservableProperty]
    public partial bool IsStudentSelected { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLandlordSelected { get; set; } = false;

    [RelayCommand]
    private void SelectStudent()
    {
        SelectedRole = "Student";
        IsStudentSelected = true;
        IsLandlordSelected = false;
    }

    [RelayCommand]
    private void SelectLandlord()
    {
        SelectedRole = "Landlord";
        IsStudentSelected = false;
        IsLandlordSelected = true;
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await _navigation.GoToAsync(AppRoutes.Register, new Dictionary<string, object> { ["role"] = SelectedRole });
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await _navigation.GoToAsync("//" + AppRoutes.Login);
    }
}


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.PageModels;

public partial class RoleSelectionPageModel : ObservableObject
{
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
        await Shell.Current.GoToAsync($"register?role={SelectedRole}");
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }
}


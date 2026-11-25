using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class EditProfilePage : ContentPage
{
    private readonly EditProfileViewModel _model;

    public EditProfilePage(EditProfileViewModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_model.LoadCommand.CanExecute(null))
        {
            _model.LoadCommand.Execute(null);
        }
    }
}

using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _model;

    public ProfilePage(ProfileViewModel model)
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

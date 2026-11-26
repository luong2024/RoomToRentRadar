using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class AccountSettingsPage : ContentPage
{
    private readonly AccountSettingsViewModel _model;

    public AccountSettingsPage(AccountSettingsViewModel model)
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

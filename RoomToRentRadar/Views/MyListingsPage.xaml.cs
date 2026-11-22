using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class MyListingsPage : ContentPage
{
    private readonly MyListingsViewModel _model;
    public MyListingsPage(MyListingsViewModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_model.LoadCommand.CanExecute(null)) _model.LoadCommand.Execute(null);
    }
}

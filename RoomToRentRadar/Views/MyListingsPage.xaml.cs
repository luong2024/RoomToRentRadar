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
        _model.AttachAuthSession();
        if (_model.LoadCommand.CanExecute(null) && !_model.IsBusy)
        {
            _model.LoadCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        _model.DetachAuthSession();
        base.OnDisappearing();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            await _model.LoadCommand.ExecuteAsync(null);
        }
        finally
        {
            if (sender is RefreshView refreshView)
                refreshView.IsRefreshing = false;
        }
    }
}

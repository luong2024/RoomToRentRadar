using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class ModerationQueuePage : ContentPage
{
    private readonly ModerationQueueViewModel _model;

    public ModerationQueuePage(ModerationQueueViewModel model)
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

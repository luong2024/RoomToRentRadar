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
}

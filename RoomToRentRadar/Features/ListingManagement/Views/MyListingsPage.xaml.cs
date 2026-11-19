using RoomToRentRadar.Features.ListingManagement.ViewModels;

namespace RoomToRentRadar.Features.ListingManagement.Views;

public partial class MyListingsPage : ContentPage
{
    private readonly MyListingsViewModel _viewModel;

    public MyListingsPage(MyListingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Reload on every appearance so edits made in the editor are visible.
        if (!_viewModel.IsBusy && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (GetItem(sender) is not { } item)
            return;

        await _viewModel.EditCommand.ExecuteAsync(item);
    }

    private async void OnUnpublishClicked(object? sender, EventArgs e)
    {
        if (GetItem(sender) is not { } item || !item.CanUnpublish)
            return;

        // US08 AC3: hiding a listing must be confirmed, so the dialog stays in
        // the page while the view model only performs the request.
        var confirmed = await DisplayAlertAsync(
            "Ẩn tin",
            "Tin sẽ không còn hiển thị với sinh viên. Tiếp tục?",
            "Ẩn tin",
            "Hủy");

        if (!confirmed)
            return;

        await _viewModel.UnpublishCommand.ExecuteAsync(item);
    }

    private static MyListingsItem? GetItem(object? sender)
        => (sender as BindableObject)?.BindingContext as MyListingsItem;
}

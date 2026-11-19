using RoomToRentRadar.Features.ListingManagement.Models;
using RoomToRentRadar.Features.ListingManagement.ViewModels;

namespace RoomToRentRadar.Features.ListingManagement.Views;

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

    private async void OnUnpublishClicked(object? sender, EventArgs e)
    {
        if (sender is not VisualElement { BindingContext: OwnedListingItem item } || !item.CanUnpublish || _model.IsBusy)
            return;

        var confirmed = await DisplayAlertAsync(
            "Gỡ đăng tin?",
            $"Tin \"{item.Title}\" sẽ không còn hiển thị công khai. Bạn vẫn có thể chỉnh sửa và đăng lại tin.",
            "Gỡ đăng",
            "Hủy");
        if (confirmed) await _model.UnpublishCommand.ExecuteAsync(item);
    }
}

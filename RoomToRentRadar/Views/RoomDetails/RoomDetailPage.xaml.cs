using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadar.Views.RoomDetails;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is RoomDetailViewModel vm)
        {
            if (vm.IsEditHouseRulesVisible)
            {
                vm.CloseEditHouseRulesCommand.Execute(null);
                return true;
            }

            if (vm.IsHouseRulesHistoryVisible)
            {
                vm.CloseHouseRulesHistoryCommand.Execute(null);
                return true;
            }
        }

        return base.OnBackButtonPressed();
    }
}

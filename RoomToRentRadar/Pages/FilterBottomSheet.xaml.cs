using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class FilterBottomSheet : ContentPage
{
    public FilterBottomSheet(SearchFilterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public FilterBottomSheet() : this(new SearchFilterViewModel())
    {
    }
}

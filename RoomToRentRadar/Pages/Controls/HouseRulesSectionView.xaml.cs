using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages.Controls;

public partial class HouseRulesSectionView : ContentView
{
    public HouseRulesSectionView()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current?.Services.GetService<HouseRulesSectionViewModel>()
            ?? new HouseRulesSectionViewModel();
    }
}

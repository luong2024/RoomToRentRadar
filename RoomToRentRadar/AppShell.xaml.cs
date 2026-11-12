using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Features.Map.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        ExploreTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<RoomListingPage>());
        MapTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MapSearchPage>());
    }
}

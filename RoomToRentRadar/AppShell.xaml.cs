using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.Login.Views;
using RoomToRentRadar.Features.Map.Views;
using RoomToRentRadar.Features.ListingManagement.Views;
using RoomToRentRadar.Features.RoomDetail.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        ExploreTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        MapTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MapSearchPage>());
        MyListingsTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MyListingsPage>());
        Routing.RegisterRoute("listing-editor", typeof(ListingEditorPage));
        Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(RoomDetailPage));
        Routing.RegisterRoute(AppRoutes.Login, typeof(LoginPage));
    }
}

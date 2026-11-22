using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Constants;
using RoomToRentRadar.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        ExploreTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        FavouritesTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<FavouritesPage>());
        MapTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MapSearchPage>());
        MyListingsTab.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MyListingsPage>());
        Routing.RegisterRoute(AppRoutes.ListingEditor, typeof(ListingEditorPage));
        Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(RoomDetailPage));
        Routing.RegisterRoute(AppRoutes.Login, typeof(LoginPage));
        Routing.RegisterRoute(AppRoutes.Directions, typeof(DirectionsPage));
        Routing.RegisterRoute(AppRoutes.Comparison, typeof(ComparisonPage));
    }
}

using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.Login.Views;
using RoomToRentRadar.Features.RoomDetail.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    public AppShell(HomePage homePage)
    {
        InitializeComponent();
        Items.Add(new ShellContent
        {
            Title = "Khám phá phòng",
            Route = AppRoutes.Home,
            Content = homePage
        });
        Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(RoomDetailPage));
        Routing.RegisterRoute(AppRoutes.Login, typeof(LoginPage));
    }
}

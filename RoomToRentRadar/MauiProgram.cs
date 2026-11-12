using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Features.Map.ViewModels;
using RoomToRentRadar.Features.Map.Views;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace RoomToRentRadar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureSyncfusionToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services
            .AddMobileInfrastructure()
            .AddMobileFeatures()
            .AddSingleton<IRoomListingService>(_ => new RoomListingService(DemoRoomListings.Create()))
            .AddTransient<RoomListingPageModel>()
            .AddTransient<RoomListingPage>()
            .AddTransient<MapSearchPageModel>()
            .AddTransient<MapSearchPage>();

        return builder.Build();
    }
}

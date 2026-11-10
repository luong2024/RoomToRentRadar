using Microsoft.Extensions.Logging;
namespace RoomToRentRadar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddSingleton<IRoomListingService>(_ => new RoomListingService(DemoRoomListings.Create()));
        builder.Services.AddTransient<RoomListingPageModel>();
        builder.Services.AddTransient<RoomListingPage>();
        return builder.Build();
    }
}

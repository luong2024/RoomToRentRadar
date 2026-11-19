using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.DeviceAdapters;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Navigation.Core;
using Syncfusion.Maui.Toolkit.Hosting;

namespace RoomToRentRadar
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Register Client Gateways (ADR-003 Decision 5 & NFR-05)
            builder.Services.AddClientAdapters(ApiOptionsFactory.Create());

            // Core UI & Device Services
            builder.Services.AddSingleton<ISessionStore, MauiSecureSessionStore>();
            builder.Services.AddSingleton<IAppSession, AppSession>();
            builder.Services.AddSingleton<INavigationService, NavigationService>();

            builder.Services.AddAppPages();

            return builder.Build();
        }
    }
}

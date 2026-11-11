using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.DeviceAdapters;
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

            // Register Client Gateways (ADR-003)
            var baseApiUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? new Uri("http://10.0.2.2:5000")
                : new Uri("http://localhost:5000");

            builder.Services.AddClientAuthGateway(baseApiUrl);

            // Core UI & Device Services
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<ISessionStore, MauiSecureSessionStore>();

            // UI Design System Catalog
            builder.Services.AddSingleton<PageModels.UiCatalogPageModel>();
            builder.Services.AddTransient<Pages.UiCatalogPage>();

            // Auth Flow Services & Pages
            builder.Services.AddTransient<PageModels.LoginPageModel>();
            builder.Services.AddTransient<Pages.LoginPage>();
            builder.Services.AddTransient<PageModels.RegisterPageModel>();
            builder.Services.AddTransient<Pages.RegisterPage>();
            builder.Services.AddTransient<PageModels.RoleSelectionPageModel>();
            builder.Services.AddTransient<Pages.RoleSelectionPage>();

            return builder.Build();
        }
    }
}

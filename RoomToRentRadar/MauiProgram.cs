using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Application;
using RoomToRentRadar.Datastore;
using RoomToRentRadar.Infrastructure;
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

            // Register Hexagonal Architecture Layers
            builder.Services.AddApplicationServices();
            builder.Services.AddDatastore();

            var clientJwtSettings = new RoomToRentRadar.Infrastructure.Adapters.Security.JwtSettings
            {
                SigningKey = "RoomToRentRadar_ClientDevSecretKey_AtLeast32BytesLong!"
            };
            builder.Services.AddInfrastructure(clientJwtSettings, useMockSsoVerifier: true);

            // Core UI Services
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<RoomToRentRadar.Ports.Outbound.Security.ISecureTokenStoragePort, RoomToRentRadar.DeviceAdapters.MauiSecureTokenStorageAdapter>();

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

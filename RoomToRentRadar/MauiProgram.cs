using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Extensions;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace RoomToRentRadar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        // Input surfaces and focus rings belong to the shared MAUI containers.
        // Suppress Android's second underline inside those surfaces.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("RoomRadarInput", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("RoomRadarInput", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureSyncfusionToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Shell, RoomToRentRadar.Platforms.Android.LiquidGlassShellRenderer>();
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("BeVietnamPro-Regular.ttf", "FontRegular");
                fonts.AddFont("BeVietnamPro-Medium.ttf", "FontMedium");
                fonts.AddFont("BeVietnamPro-SemiBold.ttf", "FontSemiBold");
                fonts.AddFont("BeVietnamPro-Bold.ttf", "FontBold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentIcons");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services
            .AddMobileInfrastructure()
            .AddMobileFeatures();

        return builder.Build();
    }
}

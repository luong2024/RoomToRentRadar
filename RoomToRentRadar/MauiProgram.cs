#if WINDOWS
using System.Runtime.CompilerServices;
#endif
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Infrastructure.DependencyInjection;
using Syncfusion.Maui.Toolkit.Hosting;

namespace RoomToRentRadar
{
    public static class MauiProgram
    {
#if WINDOWS
        private static readonly ConditionalWeakTable<Microsoft.UI.Xaml.Controls.NavigationView, WeakReference<AppShell>> AttachedDesktopShells = new();
#endif

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

                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
                    {
                        if (view is Entry entry && entry.StyleId == "BorderlessEntry")
                        {
                            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4);
                            handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                        }
                    });

                    Microsoft.Maui.Controls.Handlers.ShellHandler.Mapper.AppendToMapping(nameof(Shell.FlyoutBehavior), (handler, view) =>
                    {
                        if (handler.PlatformView is Microsoft.UI.Xaml.Controls.NavigationView navView &&
                            view is AppShell appShell)
                        {
                            ApplyDesktopRailState(navView, appShell);

                            if (!AttachedDesktopShells.TryGetValue(navView, out _))
                            {
                                AttachedDesktopShells.Add(navView, new WeakReference<AppShell>(appShell));

                                navView.PaneOpening += (s, _) =>
                                {
                                    if (AttachedDesktopShells.TryGetValue(s, out var shellRef) &&
                                        shellRef.TryGetTarget(out var target) &&
                                        target.FlyoutBehavior != FlyoutBehavior.Locked)
                                    {
                                        target.FlyoutBehavior = FlyoutBehavior.Locked;
                                        ApplyDesktopRailState(s, target);
                                    }
                                };

                                navView.PaneClosing += (s, _) =>
                                {
                                    if (AttachedDesktopShells.TryGetValue(s, out var shellRef) &&
                                        shellRef.TryGetTarget(out var target) &&
                                        target.FlyoutBehavior == FlyoutBehavior.Locked)
                                    {
                                        target.FlyoutBehavior = FlyoutBehavior.Flyout;
                                        ApplyDesktopRailState(s, target);
                                    }
                                };

                                navView.SizeChanged += (s, _) =>
                                {
                                    if (s is Microsoft.UI.Xaml.Controls.NavigationView nv &&
                                        AttachedDesktopShells.TryGetValue(nv, out var shellRef) &&
                                        shellRef.TryGetTarget(out var target))
                                    {
                                        ApplyDesktopRailState(nv, target);
                                    }
                                };

                                if (Application.Current is not null)
                                {
                                    var weakNav = new WeakReference<Microsoft.UI.Xaml.Controls.NavigationView>(navView);
                                    Application.Current.RequestedThemeChanged += (_, _) =>
                                    {
                                        if (weakNav.TryGetTarget(out var targetNav))
                                        {
                                            ApplyDesktopBackdrop(targetNav);
                                        }
                                    };
                                }
                            }
                        }
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

            builder.Services
                .AddMobileInfrastructure()
                .AddMobileFeatures();

            return builder.Build();
        }

#if WINDOWS
        private static void ApplyDesktopRailState(Microsoft.UI.Xaml.Controls.NavigationView navView, AppShell appShell)
        {
            bool isExpanded = appShell.FlyoutBehavior == FlyoutBehavior.Locked;

            navView.CompactPaneLength = 48;
            navView.OpenPaneLength = 296;
            navView.ExpandedModeThresholdWidth = 0;
            navView.IsTitleBarAutoPaddingEnabled = false;
            navView.IsPaneToggleButtonVisible = true;
            navView.PaneDisplayMode = isExpanded
                ? Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Left
                : Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.LeftCompact;
            navView.IsPaneOpen = isExpanded;

            appShell.UpdateDesktopPaneCompactState(!isExpanded);
            ApplyDesktopBackdrop(navView);
        }

        private static void ApplyDesktopBackdrop(Microsoft.UI.Xaml.Controls.NavigationView navView)
        {
            var app = Application.Current;
            var activeTheme = app is not null && app.UserAppTheme != AppTheme.Unspecified
                ? app.UserAppTheme
                : app?.RequestedTheme ?? AppTheme.Light;

            bool isDark = activeTheme == AppTheme.Dark;

            var paneBg = isDark
                ? Microsoft.UI.ColorHelper.FromArgb(255, 15, 23, 42)    // #0F172A
                : Microsoft.UI.ColorHelper.FromArgb(255, 241, 245, 249); // #F1F5F9

            var indicatorColor = isDark
                ? Microsoft.UI.ColorHelper.FromArgb(255, 96, 165, 250)  // #60A5FA (PrimaryBrandDark)
                : Microsoft.UI.ColorHelper.FromArgb(255, 37, 99, 235);  // #2563EB (PrimaryBrand)

            var selectedBg = isDark
                ? Microsoft.UI.ColorHelper.FromArgb(255, 30, 41, 59)    // #1E293B
                : Microsoft.UI.ColorHelper.FromArgb(255, 255, 255, 255); // #FFFFFF

            var selectedHoverBg = isDark
                ? Microsoft.UI.ColorHelper.FromArgb(255, 51, 65, 85)    // #334155
                : Microsoft.UI.ColorHelper.FromArgb(255, 239, 246, 255); // #EFF6FF

            var hoverBg = isDark
                ? Microsoft.UI.ColorHelper.FromArgb(255, 30, 41, 59)    // #1E293B
                : Microsoft.UI.ColorHelper.FromArgb(255, 226, 232, 240); // #E2E8F0

            navView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(paneBg);
            navView.Resources["NavigationViewSelectionIndicatorForeground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(indicatorColor);
            navView.Resources["NavigationViewItemBackgroundSelected"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectedBg);
            navView.Resources["NavigationViewItemBackgroundSelectedPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectedHoverBg);
            navView.Resources["NavigationViewItemBackgroundPointerOver"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(hoverBg);
        }
#endif
    }
}

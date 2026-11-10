using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Features.Home.Views;

namespace RoomToRentRadar;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        HomeContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());

        var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        ThemeSegmentedControl.SelectedIndex = currentTheme == AppTheme.Light ? 0 : 1;
    }

    private void SfSegmentedControl_SelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
    {
        if (Application.Current is { } application)
            application.UserAppTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
    }
}

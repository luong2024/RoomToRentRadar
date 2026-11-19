using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.Pages.Controls.Navigation;

public partial class BottomNavBarControl : ContentView
{
    private static readonly Color DefaultActiveLightColor = Color.FromArgb("#163A5F");
    private static readonly Color DefaultActiveDarkColor = Color.FromArgb("#5CE1D6");
    private static readonly Color DefaultInactiveLightColor = Color.FromArgb("#94A3B8");
    private static readonly Color DefaultInactiveDarkColor = Color.FromArgb("#94A3B8");

    private readonly IAppSession? _appSession;
    private readonly INavigationService? _navigation;
    private IReadOnlyList<TabDescriptor> _currentDescriptors = Array.Empty<TabDescriptor>();

    public static readonly BindableProperty ActiveRouteProperty =
        BindableProperty.Create(
            nameof(ActiveRoute),
            typeof(string),
            typeof(BottomNavBarControl),
            AppRoutes.Explore,
            propertyChanged: OnActiveRouteChanged);

    public static readonly BindableProperty UnreadMessagesCountProperty =
        BindableProperty.Create(
            nameof(UnreadMessagesCount),
            typeof(int),
            typeof(BottomNavBarControl),
            0,
            propertyChanged: OnUnreadMessagesCountChanged);

    public string ActiveRoute
    {
        get => (string)GetValue(ActiveRouteProperty);
        set => SetValue(ActiveRouteProperty, value);
    }

    public int UnreadMessagesCount
    {
        get => (int)GetValue(UnreadMessagesCountProperty);
        set => SetValue(UnreadMessagesCountProperty, value);
    }

    public ObservableCollection<BottomNavItem> Tabs { get; } = new();

    public ICommand SelectTabCommand { get; }

    public BottomNavBarControl()
    {
        SelectTabCommand = new Command<string>(async route => await NavigateToTabAsync(route));
        InitializeComponent();

        // The control lives in XAML without ctor injection; resolve services from platform DI (T00.15).
        var services = IPlatformApplication.Current?.Services;
        _appSession = services?.GetService<IAppSession>();
        _navigation = services?.GetService<INavigationService>();

        RebuildTabs();

        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsPropertyChanged;

        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        if (_appSession != null)
        {
            _appSession.RoleChanged += OnRoleChanged;
        }

        Unloaded += OnControlUnloaded;
    }

    private void OnControlUnloaded(object? sender, EventArgs e)
    {
        Unloaded -= OnControlUnloaded;
        LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsPropertyChanged;
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }
        if (_appSession != null)
        {
            _appSession.RoleChanged -= OnRoleChanged;
        }
    }

    private void OnLocalizedStringsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshTabStates();
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        RefreshTabStates();
    }

    private void OnRoleChanged(object? sender, EventArgs e)
    {
        // RoleChanged can fire from the thread pool; rebuild on the UI thread (T00.15 AC-2).
        MainThread.BeginInvokeOnMainThread(RebuildTabs);
    }

    private static void OnActiveRouteChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BottomNavBarControl control)
        {
            control.RefreshTabStates();
        }
    }

    private static void OnUnreadMessagesCountChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BottomNavBarControl control)
        {
            control.RefreshTabStates();
        }
    }

    private void RebuildTabs()
    {
        var descriptors = RoleTabsProvider.GetTabs(_appSession?.CurrentRole);

        // Rebuild only when the tab set actually changes, to avoid flicker on unrelated role events.
        if (descriptors.Select(d => d.Route).SequenceEqual(_currentDescriptors.Select(d => d.Route)))
        {
            return;
        }

        _currentDescriptors = descriptors;
        Tabs.Clear();
        foreach (var descriptor in descriptors)
        {
            Tabs.Add(new BottomNavItem(descriptor, SelectTabCommand));
        }

        RefreshTabStates();
    }

    public void RefreshTabStates()
    {
        foreach (var item in Tabs)
        {
            item.IsSelected = item.Route == ActiveRoute;
            item.IconColor = item.IsSelected ? GetActiveColor() : GetInactiveColor();
            item.LabelFontAttributes = item.IsSelected ? FontAttributes.Bold : FontAttributes.None;
            item.Title = AppResources.GetString(item.Descriptor.TitleKey);
            item.Hint = AppResources.GetString(item.Descriptor.HintKey);
            item.HasBadge = item.Route == AppRoutes.Messages && UnreadMessagesCount > 0;
            item.BadgeCount = UnreadMessagesCount;
        }
    }

    private async Task NavigateToTabAsync(string? route)
    {
        if (string.IsNullOrEmpty(route) || route == ActiveRoute || _navigation == null)
        {
            return;
        }

        await _navigation.GoToTabAsync(route);
    }

    private static Color GetActiveColor()
    {
        bool isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        string key = isDark ? "Teal400" : "ActionPrimaryLight";

        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var res) == true && res is Color c)
        {
            return c;
        }

        return isDark ? DefaultActiveDarkColor : DefaultActiveLightColor;
    }

    private static Color GetInactiveColor()
    {
        bool isDark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        string key = isDark ? "Slate400" : "TextMutedLight";

        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var res) == true && res is Color c)
        {
            return c;
        }

        return isDark ? DefaultInactiveDarkColor : DefaultInactiveLightColor;
    }
}

/// <summary>One rendered bottom tab; the control pushes selection/theme/language state into it.</summary>
public partial class BottomNavItem : ObservableObject
{
    public BottomNavItem(TabDescriptor descriptor, ICommand selectCommand)
    {
        Descriptor = descriptor;
        SelectCommand = selectCommand;
        Glyph = GetGlyph(descriptor.Icon);
    }

    public TabDescriptor Descriptor { get; }
    public string Route => Descriptor.Route;
    public string Glyph { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Hint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color IconColor { get; set; } = Colors.Transparent;

    [ObservableProperty]
    public partial FontAttributes LabelFontAttributes { get; set; }

    [ObservableProperty]
    public partial bool HasBadge { get; set; }

    [ObservableProperty]
    public partial int BadgeCount { get; set; }

    private static string GetGlyph(TabIcon icon) => icon switch
    {
        TabIcon.Map => Fonts.FluentUI.map_24_regular,
        TabIcon.Saved => Fonts.FluentUI.heart_24_regular,
        TabIcon.Listings => Fonts.FluentUI.building_24_regular,
        TabIcon.Messages => Fonts.FluentUI.chat_24_regular,
        TabIcon.Moderation => Fonts.FluentUI.shield_24_regular,
        TabIcon.Account => Fonts.FluentUI.person_24_regular,
        _ => Fonts.FluentUI.compass_northwest_24_regular
    };
}
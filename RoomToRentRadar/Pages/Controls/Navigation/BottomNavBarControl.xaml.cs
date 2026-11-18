using System.Windows.Input;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Navigation;

public partial class BottomNavBarControl : ContentView
{
    private static readonly Color DefaultActiveLightColor = Color.FromArgb("#163A5F");
    private static readonly Color DefaultActiveDarkColor = Color.FromArgb("#5CE1D6");
    private static readonly Color DefaultInactiveLightColor = Color.FromArgb("#94A3B8");
    private static readonly Color DefaultInactiveDarkColor = Color.FromArgb("#94A3B8");

    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(
            nameof(ActiveTab),
            typeof(BottomNavTab),
            typeof(BottomNavBarControl),
            BottomNavTab.Explore,
            propertyChanged: OnActiveTabChanged);

    public static readonly BindableProperty UnreadMessagesCountProperty =
        BindableProperty.Create(
            nameof(UnreadMessagesCount),
            typeof(int),
            typeof(BottomNavBarControl),
            0,
            propertyChanged: OnUnreadMessagesCountChanged);

    public BottomNavTab ActiveTab
    {
        get => (BottomNavTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public int UnreadMessagesCount
    {
        get => (int)GetValue(UnreadMessagesCountProperty);
        set => SetValue(UnreadMessagesCountProperty, value);
    }

    public bool IsExploreSelected => ActiveTab == BottomNavTab.Explore;
    public bool IsMapSelected => ActiveTab == BottomNavTab.Map;
    public bool IsSavedSelected => ActiveTab == BottomNavTab.Saved;
    public bool IsMessagesSelected => ActiveTab == BottomNavTab.Messages;
    public bool IsAccountSelected => ActiveTab == BottomNavTab.Account;

    public bool HasUnreadMessages => UnreadMessagesCount > 0;

    public Color ExploreColor => IsExploreSelected ? GetActiveColor() : GetInactiveColor();
    public Color MapColor => IsMapSelected ? GetActiveColor() : GetInactiveColor();
    public Color SavedColor => IsSavedSelected ? GetActiveColor() : GetInactiveColor();
    public Color MessagesColor => IsMessagesSelected ? GetActiveColor() : GetInactiveColor();
    public Color AccountColor => IsAccountSelected ? GetActiveColor() : GetInactiveColor();

    public FontAttributes ExploreFontAttributes => IsExploreSelected ? FontAttributes.Bold : FontAttributes.None;
    public FontAttributes MapFontAttributes => IsMapSelected ? FontAttributes.Bold : FontAttributes.None;
    public FontAttributes SavedFontAttributes => IsSavedSelected ? FontAttributes.Bold : FontAttributes.None;
    public FontAttributes MessagesFontAttributes => IsMessagesSelected ? FontAttributes.Bold : FontAttributes.None;
    public FontAttributes AccountFontAttributes => IsAccountSelected ? FontAttributes.Bold : FontAttributes.None;

    public string NavExploreText => AppResources.GetString("Nav_Explore");
    public string NavMapText => AppResources.GetString("Nav_Map");
    public string NavSavedText => AppResources.GetString("Nav_Saved");
    public string NavMessagesText => AppResources.GetString("Nav_Messages");
    public string NavAccountText => AppResources.GetString("Nav_Account");

    public string NavHintExploreText => AppResources.GetString("Nav_Hint_Explore");
    public string NavHintMapText => AppResources.GetString("Nav_Hint_Map");
    public string NavHintSavedText => AppResources.GetString("Nav_Hint_Saved");
    public string NavHintMessagesText => AppResources.GetString("Nav_Hint_Messages");
    public string NavHintAccountText => AppResources.GetString("Nav_Hint_Account");

    public ICommand SelectTabCommand { get; }

    public BottomNavBarControl()
    {
        SelectTabCommand = new Command<BottomNavTab>(async tab => await NavigateToTabAsync(tab));
        InitializeComponent();

        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsPropertyChanged;

        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            Microsoft.Maui.Controls.Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
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
    }

    private void OnLocalizedStringsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(NavExploreText));
        OnPropertyChanged(nameof(NavMapText));
        OnPropertyChanged(nameof(NavSavedText));
        OnPropertyChanged(nameof(NavMessagesText));
        OnPropertyChanged(nameof(NavAccountText));
        OnPropertyChanged(nameof(NavHintExploreText));
        OnPropertyChanged(nameof(NavHintMapText));
        OnPropertyChanged(nameof(NavHintSavedText));
        OnPropertyChanged(nameof(NavHintMessagesText));
        OnPropertyChanged(nameof(NavHintAccountText));
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        RefreshTabStates();
    }

    private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
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
            control.OnPropertyChanged(nameof(HasUnreadMessages));
            control.OnPropertyChanged(nameof(UnreadMessagesCount));
        }
    }

    public void RefreshTabStates()
    {
        OnPropertyChanged(nameof(IsExploreSelected));
        OnPropertyChanged(nameof(IsMapSelected));
        OnPropertyChanged(nameof(IsSavedSelected));
        OnPropertyChanged(nameof(IsMessagesSelected));
        OnPropertyChanged(nameof(IsAccountSelected));

        OnPropertyChanged(nameof(ExploreColor));
        OnPropertyChanged(nameof(MapColor));
        OnPropertyChanged(nameof(SavedColor));
        OnPropertyChanged(nameof(MessagesColor));
        OnPropertyChanged(nameof(AccountColor));

        OnPropertyChanged(nameof(ExploreFontAttributes));
        OnPropertyChanged(nameof(MapFontAttributes));
        OnPropertyChanged(nameof(SavedFontAttributes));
        OnPropertyChanged(nameof(MessagesFontAttributes));
        OnPropertyChanged(nameof(AccountFontAttributes));
    }

    private async Task NavigateToTabAsync(BottomNavTab tab)
    {
        if (tab == ActiveTab)
        {
            return;
        }

        string route = tab switch
        {
            BottomNavTab.Explore => "//main",
            BottomNavTab.Map => "//map",
            BottomNavTab.Saved => "//saved",
            BottomNavTab.Messages => "//messages",
            BottomNavTab.Account => "//account",
            _ => "//main"
        };

        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(route);
        }
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

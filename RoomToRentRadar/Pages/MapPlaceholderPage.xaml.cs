using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages;

public partial class MapPlaceholderPage : ContentPage
{
    public string PageTitle => AppResources.GetString("Map_Title");
    public string PageSubtitle => AppResources.GetString("Map_Subtitle");
    public string EmptyTitle => AppResources.GetString("Map_EmptyTitle");
    public string EmptyMessage => AppResources.GetString("Map_EmptyMessage");
    public string EmptyAction => AppResources.GetString("Map_EmptyAction");

    public MapPlaceholderPage()
    {
        InitializeComponent();
        BindingContext = this;

        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsPropertyChanged;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object? sender, EventArgs e)
    {
        Unloaded -= OnPageUnloaded;
        LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsPropertyChanged;
    }

    private void OnLocalizedStringsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(EmptyAction));
    }
}

using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages;

public partial class SavedPlaceholderPage : ContentPage
{
    public string PageTitle => AppResources.GetString("Saved_Title");
    public string PageSubtitle => AppResources.GetString("Saved_Subtitle");
    public string EmptyTitle => AppResources.GetString("Saved_EmptyTitle");
    public string EmptyMessage => AppResources.GetString("Saved_EmptyMessage");

    public SavedPlaceholderPage()
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
    }
}

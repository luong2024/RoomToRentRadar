using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages;

public partial class MessagesPlaceholderPage : ContentPage
{
    public string PageTitle => AppResources.GetString("Messages_Title");
    public string PageSubtitle => AppResources.GetString("Messages_Subtitle");
    public string UnreadBannerTitle => AppResources.GetString("Messages_UnreadBannerTitle");
    public string UnreadBannerSubtitle => AppResources.GetString("Messages_UnreadBannerSubtitle");
    public string EmptyTitle => AppResources.GetString("Messages_EmptyTitle");
    public string EmptyMessage => AppResources.GetString("Messages_EmptyMessage");

    public MessagesPlaceholderPage()
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
        OnPropertyChanged(nameof(UnreadBannerTitle));
        OnPropertyChanged(nameof(UnreadBannerSubtitle));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}

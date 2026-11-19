using Microsoft.Maui.Accessibility;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class OfflineBannerView : ContentView
{
    public OfflineBannerView()
    {
        InitializeComponent();
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // MAUI has no live-region attached property; announcing on show gives the same status-update behaviour.
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsVisibleProperty.PropertyName && IsVisible)
            SemanticScreenReader.Announce(BannerLabel.Text);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        ApplyLocalizedText();
        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsChanged;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsChanged;
    }

    private void OnLocalizedStringsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        var text = LocalizedStrings.Instance["State_Offline_Banner"];
        BannerLabel.Text = text;
        SemanticProperties.SetDescription(this, text);
    }
}
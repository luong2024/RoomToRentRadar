using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls.Common;

public partial class AmenityChip : ContentView
{
    public const string FontFamilyIcon = "FluentUI";

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(AmenityChip), string.Empty);

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(AmenityChip), string.Empty,
            propertyChanged: (b, o, n) => ((AmenityChip)b).UpdateHasIcon());

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(AmenityChip), false,
            propertyChanged: (b, o, n) => ((AmenityChip)b).UpdateSelectionState());

    public static readonly BindableProperty ToggleCommandProperty =
        BindableProperty.Create(nameof(ToggleCommand), typeof(ICommand), typeof(AmenityChip), null);

    public static readonly BindableProperty HasIconProperty =
        BindableProperty.Create(nameof(HasIcon), typeof(bool), typeof(AmenityChip), false);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? ToggleCommand
    {
        get => (ICommand?)GetValue(ToggleCommandProperty);
        set => SetValue(ToggleCommandProperty, value);
    }

    public bool HasIcon
    {
        get => (bool)GetValue(HasIconProperty);
        private set => SetValue(HasIconProperty, value);
    }

    public AmenityChip()
    {
        InitializeComponent();
        UpdateHasIcon();
        UpdateSelectionState();
    }

    private void UpdateHasIcon()
    {
        HasIcon = !string.IsNullOrWhiteSpace(IconGlyph);
    }

    private void UpdateSelectionState()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources == null) return;
        var resources = Microsoft.Maui.Controls.Application.Current.Resources;

        if (IsSelected)
        {
            ChipBorder.BackgroundColor = resources.TryGetValue("ActionSecondaryLight", out var aBg) ? (Color)aBg : Color.FromArgb("#256D85");
            ChipBorder.Stroke = Colors.Transparent;
            TitleLabel.TextColor = Colors.White;
            IconLabel.TextColor = Colors.White;
        }
        else
        {
            ChipBorder.BackgroundColor = resources.TryGetValue("SurfaceSubtleLight", out var sBg) ? (Color)sBg : Color.FromArgb("#E8EEF5");
            ChipBorder.Stroke = resources.TryGetValue("BorderSubtleLight", out var bStroke) ? (Color)bStroke : Color.FromArgb("#E2E8F0");
            TitleLabel.TextColor = resources.TryGetValue("TextSecondaryLight", out var sText) ? (Color)sText : Color.FromArgb("#475569");
            IconLabel.TextColor = resources.TryGetValue("TextBrandLight", out var bText) ? (Color)bText : Color.FromArgb("#163A5F");
        }
    }
}

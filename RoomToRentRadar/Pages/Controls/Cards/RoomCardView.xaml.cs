using System.Windows.Input;
using RoomToRentRadar.Pages.Controls.Cards.Models;

namespace RoomToRentRadar.Pages.Controls.Cards;

/// <summary>
/// Room card of SCR-HOME-01. The heart (US06) and Compare (US10) are drawn here; their behaviour is
/// supplied by the owning screen through <see cref="BookmarkCommand"/> and <see cref="CompareCommand"/>.
/// All commands receive the room id.
/// </summary>
public partial class RoomCardView : ContentView
{
    public static readonly BindableProperty RoomProperty =
        BindableProperty.Create(nameof(Room), typeof(RoomCardDisplayModel), typeof(RoomCardView), null,
            propertyChanged: (b, o, n) => ((RoomCardView)b).UpdateBookmarkState());

    public static readonly BindableProperty BookmarkCommandProperty =
        BindableProperty.Create(nameof(BookmarkCommand), typeof(ICommand), typeof(RoomCardView), null);

    public static readonly BindableProperty DetailsCommandProperty =
        BindableProperty.Create(nameof(DetailsCommand), typeof(ICommand), typeof(RoomCardView), null);

    public static readonly BindableProperty CompareCommandProperty =
        BindableProperty.Create(nameof(CompareCommand), typeof(ICommand), typeof(RoomCardView), null);

    public RoomCardDisplayModel? Room
    {
        get => (RoomCardDisplayModel?)GetValue(RoomProperty);
        set => SetValue(RoomProperty, value);
    }

    public ICommand? BookmarkCommand
    {
        get => (ICommand?)GetValue(BookmarkCommandProperty);
        set => SetValue(BookmarkCommandProperty, value);
    }

    public ICommand? DetailsCommand
    {
        get => (ICommand?)GetValue(DetailsCommandProperty);
        set => SetValue(DetailsCommandProperty, value);
    }

    public ICommand? CompareCommand
    {
        get => (ICommand?)GetValue(CompareCommandProperty);
        set => SetValue(CompareCommandProperty, value);
    }

    public RoomCardView()
    {
        InitializeComponent();
        // Bind the inner content to this control; the control's own BindingContext must stay free so that
        // Room="{Binding}" in a host DataTemplate resolves to the room (otherwise the card stays empty).
        Content.BindingContext = this;
        UpdateBookmarkState();
    }

    private void UpdateBookmarkState()
    {
        var saved = Room?.IsBookmarked == true;
        var light = saved ? "StatusDangerLight" : "TextPrimaryLight";
        var dark = saved ? "StatusDangerDark" : "TextPrimaryDark";

        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources is not null
            && resources.TryGetValue(light, out var lightColor)
            && resources.TryGetValue(dark, out var darkColor))
        {
            HeartGlyph.SetAppThemeColor(Label.TextColorProperty, (Color)lightColor, (Color)darkColor);
        }
    }
}

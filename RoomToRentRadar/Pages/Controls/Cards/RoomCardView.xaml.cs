using System.Windows.Input;
using RoomToRentRadar.Pages.Controls.Cards.Models;

namespace RoomToRentRadar.Pages.Controls.Cards;

public partial class RoomCardView : ContentView
{
    public static readonly BindableProperty RoomProperty =
        BindableProperty.Create(nameof(Room), typeof(RoomCardDisplayModel), typeof(RoomCardView), null,
            propertyChanged: (b, o, n) => ((RoomCardView)b).UpdateBookmarkState());

    public static readonly BindableProperty BookmarkCommandProperty =
        BindableProperty.Create(nameof(BookmarkCommand), typeof(ICommand), typeof(RoomCardView), null);

    public static readonly BindableProperty DetailsCommandProperty =
        BindableProperty.Create(nameof(DetailsCommand), typeof(ICommand), typeof(RoomCardView), null);

    public static readonly BindableProperty BookmarkIconGlyphProperty =
        BindableProperty.Create(nameof(BookmarkIconGlyph), typeof(string), typeof(RoomCardView), "♡");

    public static readonly BindableProperty BookmarkIconColorProperty =
        BindableProperty.Create(nameof(BookmarkIconColor), typeof(Color), typeof(RoomCardView), Color.FromArgb("#1E293B"));

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

    public string BookmarkIconGlyph
    {
        get => (string)GetValue(BookmarkIconGlyphProperty);
        private set => SetValue(BookmarkIconGlyphProperty, value);
    }

    public Color BookmarkIconColor
    {
        get => (Color)GetValue(BookmarkIconColorProperty);
        private set => SetValue(BookmarkIconColorProperty, value);
    }

    public RoomCardView()
    {
        InitializeComponent();
        BindingContext = this;
        UpdateBookmarkState();
    }

    private void UpdateBookmarkState()
    {
        if (Room?.IsBookmarked == true)
        {
            BookmarkIconGlyph = "♥";
            BookmarkIconColor = Color.FromArgb("#DC2626"); // Red
        }
        else
        {
            BookmarkIconGlyph = "♡";
            BookmarkIconColor = Color.FromArgb("#1E293B"); // Slate
        }
    }
}


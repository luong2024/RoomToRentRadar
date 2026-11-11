using System.Windows.Input;
using RoomToRentRadar.Pages.Controls.Cards.Models;

namespace RoomToRentRadar.Pages.Controls.Cards;

public partial class ComparisonCardView : ContentView
{
    public static readonly BindableProperty RoomProperty =
        BindableProperty.Create(nameof(Room), typeof(RoomCardDisplayModel), typeof(ComparisonCardView), null);

    public static readonly BindableProperty RemoveCommandProperty =
        BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(ComparisonCardView), null);

    public RoomCardDisplayModel? Room
    {
        get => (RoomCardDisplayModel?)GetValue(RoomProperty);
        set => SetValue(RoomProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ComparisonCardView()
    {
        InitializeComponent();
        BindingContext = this;
    }
}


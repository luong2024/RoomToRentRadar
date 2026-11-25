using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace RoomToRentRadar.Pages.Controls;

public partial class RoomCardView : Border
{
    public static readonly BindableProperty ToggleFavouriteCommandProperty =
        BindableProperty.Create(
            nameof(ToggleFavouriteCommand),
            typeof(ICommand),
            typeof(RoomCardView));

    public ICommand? ToggleFavouriteCommand
    {
        get => (ICommand?)GetValue(ToggleFavouriteCommandProperty);
        set => SetValue(ToggleFavouriteCommandProperty, value);
    }

    public RoomCardView()
    {
        InitializeComponent();
    }
}
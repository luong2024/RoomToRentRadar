using System.Windows.Input;

namespace RoomToRentRadar.Features.Home.Views;

public partial class HomePropertyCard : ContentView
{
    public static readonly BindableProperty CompareCommandProperty = BindableProperty.Create(
        nameof(CompareCommand),
        typeof(ICommand),
        typeof(HomePropertyCard));

    public HomePropertyCard() => InitializeComponent();

    public ICommand? CompareCommand
    {
        get => (ICommand?)GetValue(CompareCommandProperty);
        set => SetValue(CompareCommandProperty, value);
    }
}

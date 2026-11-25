using System.Windows.Input;

namespace RoomToRentRadar.Controls;

public partial class HomePropertyCard : ContentView
{
    public static readonly BindableProperty CompareCommandProperty = BindableProperty.Create(
        nameof(CompareCommand),
        typeof(ICommand),
        typeof(HomePropertyCard));

    public static readonly BindableProperty OpenCommandProperty = BindableProperty.Create(
        nameof(OpenCommand),
        typeof(ICommand),
        typeof(HomePropertyCard));

    public HomePropertyCard() => InitializeComponent();

    public ICommand? CompareCommand
    {
        get => (ICommand?)GetValue(CompareCommandProperty);
        set => SetValue(CompareCommandProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }
}

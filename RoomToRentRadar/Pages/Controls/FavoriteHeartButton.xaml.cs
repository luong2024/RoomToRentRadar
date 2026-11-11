using System.Windows.Input;

namespace RoomToRentRadar.Pages.Controls
{
    public partial class FavoriteHeartButton : ContentView
    {
        public static readonly BindableProperty IsFavoriteProperty =
            BindableProperty.Create(nameof(IsFavorite), typeof(bool), typeof(FavoriteHeartButton), false,
                propertyChanged: OnIsFavoriteChanged);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FavoriteHeartButton), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(FavoriteHeartButton), null);

        public static readonly BindableProperty ButtonSizeProperty =
            BindableProperty.Create(nameof(ButtonSize), typeof(double), typeof(FavoriteHeartButton), 40.0);

        public static readonly BindableProperty IconSizeProperty =
            BindableProperty.Create(nameof(IconSize), typeof(double), typeof(FavoriteHeartButton), 22.0);

        public static readonly BindableProperty HeartColorProperty =
            BindableProperty.Create(nameof(HeartColor), typeof(Color), typeof(FavoriteHeartButton), Color.FromArgb("#E53935"));

        public static readonly BindableProperty ButtonBackgroundProperty =
            BindableProperty.Create(nameof(ButtonBackground), typeof(Color), typeof(FavoriteHeartButton), Color.FromArgb("#F0FFFFFF"));

        public bool IsFavorite
        {
            get => (bool)GetValue(IsFavoriteProperty);
            set => SetValue(IsFavoriteProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public double ButtonSize
        {
            get => (double)GetValue(ButtonSizeProperty);
            set => SetValue(ButtonSizeProperty, value);
        }

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public Color HeartColor
        {
            get => (Color)GetValue(HeartColorProperty);
            set => SetValue(HeartColorProperty, value);
        }

        public Color ButtonBackground
        {
            get => (Color)GetValue(ButtonBackgroundProperty);
            set => SetValue(ButtonBackgroundProperty, value);
        }

        public FavoriteHeartButton()
        {
            InitializeComponent();
            UpdateVisualState();
        }

        private static void OnIsFavoriteChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is FavoriteHeartButton button)
            {
                button.UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            if (IsFavorite)
            {
                HeartColor = Color.FromArgb("#E53935");
                ButtonBackground = Color.FromArgb("#FFFFFFFF");
            }
            else
            {
                HeartColor = Color.FromArgb("#757575");
                ButtonBackground = Color.FromArgb("#F5F5F5");
            }
        }

        private async void OnTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                await HeartBorder.ScaleToAsync(1.25, 90, Easing.CubicOut);
                await HeartBorder.ScaleToAsync(1.0, 90, Easing.CubicIn);
            }
            catch
            {
            }

            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
    }
}


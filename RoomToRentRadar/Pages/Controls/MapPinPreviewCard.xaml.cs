using System.Windows.Input;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Pages.Controls
{
    public partial class MapPinPreviewCard : ContentView
    {
        public static readonly BindableProperty RoomProperty =
            BindableProperty.Create(nameof(Room), typeof(RoomListing), typeof(MapPinPreviewCard), null);

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(MapPinPreviewCard), null);

        public static readonly BindableProperty ViewDetailCommandProperty =
            BindableProperty.Create(nameof(ViewDetailCommand), typeof(ICommand), typeof(MapPinPreviewCard), null);

        public RoomListing? Room
        {
            get => (RoomListing?)GetValue(RoomProperty);
            set => SetValue(RoomProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public ICommand? ViewDetailCommand
        {
            get => (ICommand?)GetValue(ViewDetailCommandProperty);
            set => SetValue(ViewDetailCommandProperty, value);
        }

        public MapPinPreviewCard()
        {
            InitializeComponent();
        }
    }
}

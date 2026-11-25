using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class OtpVerificationPage : ContentPage
{
    public OtpVerificationPage(OtpVerificationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

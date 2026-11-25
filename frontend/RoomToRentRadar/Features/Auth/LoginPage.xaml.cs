namespace RoomToRentRadar.Features.Auth
{
    /// <summary>US01 / T01.2: sign-in screen. Code-behind only sets the BindingContext (MVVM).</summary>
    public partial class LoginPage : ContentPage
    {
        public LoginPage(AuthPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

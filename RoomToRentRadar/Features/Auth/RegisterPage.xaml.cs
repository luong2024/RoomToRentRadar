namespace RoomToRentRadar.Features.Auth
{
    /// <summary>US01 / T01.2: registration screen. Code-behind only sets the BindingContext (MVVM).</summary>
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage(AuthPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

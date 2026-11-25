namespace RoomToRentRadar.Features.Landlord
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US09 Landlord Publishing (T09.1 &amp; T09.2).
    /// </summary>
    public static class LandlordFeatureModule
    {
        public static IServiceCollection AddLandlordFeature(this IServiceCollection services)
        {
            // T09.1 (Quang): services.AddTransientWithShellRoute<PublishListingPage, PublishListingPageModel>(Constants.Routes.PublishListing);
            return services;
        }
    }
}

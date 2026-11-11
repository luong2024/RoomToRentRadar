namespace RoomToRentRadar.Features.Reports
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US13 Listing Reports &amp; Moderation (T13.1 &amp; T13.2).
    /// </summary>
    public static class ReportsFeatureModule
    {
        public static IServiceCollection AddReportsFeature(this IServiceCollection services)
        {
            // T13.1 (Hieu): services.AddSingleton<IListingReportRepository, ListingReportRepository>();
            // T13.2 (Thuy): services.AddTransientWithShellRoute<ReportListingModalPage, ReportListingPageModel>(Constants.Routes.ReportListing);
            return services;
        }
    }
}

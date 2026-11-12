namespace RoomToRentRadar.Features.Alerts
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US08 Saved Searches &amp; Alerts (T08.1 &amp; T08.2).
    /// </summary>
    public static class AlertsFeatureModule
    {
        public static IServiceCollection AddAlertsFeature(this IServiceCollection services)
        {
            // T08.1 (Thuy): services.AddSingleton<ISavedSearchAlertRepository, SavedSearchAlertRepository>();
            // T08.2 (Nam):  services.AddTransientWithShellRoute<SearchAlertsPage, SearchAlertsPageModel>(Constants.Routes.SearchAlerts);
            return services;
        }
    }
}

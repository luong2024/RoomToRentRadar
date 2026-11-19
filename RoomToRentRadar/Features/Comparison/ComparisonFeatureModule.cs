using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.Comparison
{
    /// <summary>
    /// Self-contained DI &amp; Shell Route registration for US11 Room Comparison (T11.1 &amp; T11.2).
    /// </summary>
    public static class ComparisonFeatureModule
    {
        public static IServiceCollection AddComparisonFeature(this IServiceCollection services)
        {
            // T11.2 (Thuy): Register ComparisonBasketService and ComparisonShareService
            services.TryAddSingleton<ComparisonBasketService>();
            services.TryAddSingleton<IComparisonShareService, ComparisonShareService>();

            // T11.1 (Hieu): services.AddTransientWithShellRoute<RoomComparePage, RoomComparePageModel>(Constants.Routes.RoomCompare);
            return services;
        }
    }
}

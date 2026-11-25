using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Pages;

namespace RoomToRentRadar.Services.Navigation.Core;

// MAUI-side partial: kept out of Core/ so the pure-C# unit-test link-compile never sees Shell types.
public static partial class AppRoutes
{
    // Routes whose real page replaced the placeholder (registered in AppShell.xaml.cs).
    // MAUI 10 throws ArgumentException "Duplicated Route" when the same route is registered
    // twice with different factories, so the placeholder must not be registered at all.
    private static readonly HashSet<string> ReplacedRoutes = new(StringComparer.Ordinal)
    {
        RoomDetail,    // US02 / T02.2
        SearchFilters  // US04 / T04.2
    };

    /// <summary>
    /// Registers the 12 detail placeholder routes (T00.15). Each factory carries its catalog entry so
    /// the shared <see cref="PlaceholderPage"/> knows which screen it is. The my-listings and moderation
    /// tabs are ShellContent in AppShell.xaml and are deliberately not registered here.
    /// </summary>
    public static void RegisterAll()
    {
        foreach (var entry in PlaceholderCatalog.All)
        {
            if (entry.Route is MyListings or Moderation || ReplacedRoutes.Contains(entry.Route))
            {
                continue;
            }

            Routing.RegisterRoute(entry.Route, new PlaceholderRouteFactory(entry));
        }
    }

    private sealed class PlaceholderRouteFactory : RouteFactory
    {
        private readonly PlaceholderEntry _entry;

        public PlaceholderRouteFactory(PlaceholderEntry entry) => _entry = entry;

        public override Page GetOrCreate() => GetOrCreate(IPlatformApplication.Current!.Services);

        public override Page GetOrCreate(IServiceProvider services)
        {
            var page = services.GetRequiredService<PlaceholderPage>();
            page.Configure(_entry);
            return page;
        }
    }
}
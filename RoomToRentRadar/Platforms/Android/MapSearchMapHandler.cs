using Microsoft.Maui.Maps.Handlers;
using IMap = Microsoft.Maui.Maps.IMap;

namespace RoomToRentRadar.Views.MapSearch;

internal sealed class MapSearchMapHandler() : MapHandler(Mapper, null)
{
    private static readonly new IPropertyMapper<IMap, IMapHandler> Mapper =
        new PropertyMapper<IMap, IMapHandler>(MapHandler.Mapper)
        {
            [nameof(IMap.Pins)] = (handler, map) =>
            {
                // MAUI sends an update for each change to Pins and rebuilds all Google Maps markers.
                if (map is not MapSearchMap { IsReplacingPins: true })
                    MapPins(handler, map);
            }
        };
}

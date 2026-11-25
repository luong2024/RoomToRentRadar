using Microsoft.Maui.Controls.Maps;
using IMap = Microsoft.Maui.Maps.IMap;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace RoomToRentRadar.Views.MapSearch;

public sealed class MapSearchMap : Map
{
    internal bool IsReplacingPins { get; private set; }

    internal void ReplacePins(IEnumerable<Pin> pins)
    {
        IsReplacingPins = true;
        try
        {
            Pins.Clear();
            foreach (var pin in pins)
                Pins.Add(pin);
        }
        finally
        {
            IsReplacingPins = false;
#if ANDROID
            Handler?.UpdateValue(nameof(IMap.Pins));
#endif
        }
    }
}

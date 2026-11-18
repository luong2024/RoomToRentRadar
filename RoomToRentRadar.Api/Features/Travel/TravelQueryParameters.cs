using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed class TravelQueryParameters
{
    [FromQuery(Name = "to_lat")]
    [Description("Destination latitude in degrees, between -90 and 90. Requires to_lng.")]
    public double? ToLat { get; init; }

    [FromQuery(Name = "to_lng")]
    [Description("Destination longitude in degrees, between -180 and 180. Requires to_lat.")]
    public double? ToLng { get; init; }
}

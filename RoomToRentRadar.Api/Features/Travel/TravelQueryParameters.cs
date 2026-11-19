using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed class TravelQueryParameters
{
    [FromQuery(Name = "campus_id")]
    [Description("Campus identifier from the server-side catalog, for example bach-khoa or khtn.")]
    public string? CampusId { get; init; }
}

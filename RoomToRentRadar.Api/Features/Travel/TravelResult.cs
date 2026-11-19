using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed record TravelResult<T>(T? Value, ProblemDetails? Problem)
    where T : class
{
    public bool IsSuccess => Problem is null;

    public static TravelResult<T> Success(T value) => new(value, null);

    public static TravelResult<T> Failure(ProblemDetails problem) => new(null, problem);
}

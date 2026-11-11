using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Photos;

public sealed record PhotoResult<T>(T? Value, ProblemDetails? Problem)
    where T : class
{
    public bool IsSuccess => Problem is null;

    public static PhotoResult<T> Success(T value) => new(value, null);

    public static PhotoResult<T> Failure(ProblemDetails problem) => new(null, problem);
}

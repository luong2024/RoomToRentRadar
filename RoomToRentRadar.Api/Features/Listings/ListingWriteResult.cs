using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingWriteResult<T>(T? Value, ProblemDetails? Problem)
{
    public bool IsSuccess => Problem is null;

    public static ListingWriteResult<T> Success(T value) => new(value, null);

    public static ListingWriteResult<T> Failure(ProblemDetails problem) => new(default, problem);
}

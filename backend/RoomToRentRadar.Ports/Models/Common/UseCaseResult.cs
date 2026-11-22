namespace RoomToRentRadar.Ports.Models.Common;

public sealed record UseCaseResult<T>(T? Value, ApplicationError? Error)
    where T : class
{
    public bool IsSuccess => Error is null;

    public static UseCaseResult<T> Success(T value) => new(value, null);

    public static UseCaseResult<T> Failure(ApplicationError error) => new(null, error);
}

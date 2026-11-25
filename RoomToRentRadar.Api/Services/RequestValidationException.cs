namespace RoomToRentRadar.Api.Services;

/// <summary>Lỗi validate theo từng field -> controller trả HTTP 400 ValidationProblemDetails.</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

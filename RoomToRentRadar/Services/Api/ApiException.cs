using System.Net;

namespace RoomToRentRadar.Services.Api;

public sealed class ApiException(HttpStatusCode statusCode, string message, string? code = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Code { get; } = code;
}

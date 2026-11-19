using System.Net;

namespace RoomToRentRadar.Infrastructure.Api;

public class ApiException(HttpStatusCode statusCode, string message, string? code = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Code { get; } = code;
}

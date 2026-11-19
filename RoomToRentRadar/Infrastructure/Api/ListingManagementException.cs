using System.Net;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class ListingManagementException(
    HttpStatusCode statusCode,
    string message,
    string? code,
    IReadOnlyDictionary<string, string[]> errors) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Code { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

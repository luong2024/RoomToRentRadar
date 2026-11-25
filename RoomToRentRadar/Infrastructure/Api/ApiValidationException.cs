using System.Net;

namespace RoomToRentRadar.Infrastructure.Api;

internal sealed class ApiValidationException(IReadOnlyCollection<string> fields) :
    HttpRequestException("The API rejected the request.", null, HttpStatusCode.BadRequest)
{
    internal IReadOnlyCollection<string> Fields { get; } = fields;
}

using System.Net;

namespace RoomToRentRadar.Infrastructure.Api;

/// <summary>
/// Raised when a landlord listing write is rejected. The RFC 7807 <c>errors</c>
/// extension is preserved so the editor can mark the offending fields.
/// </summary>
public sealed class ListingSaveException(
    HttpStatusCode statusCode,
    string message,
    string? code = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null)
    : ApiException(statusCode, message, code)
{
    /// <summary>Field name (wire name) to validation messages.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; } =
        fieldErrors ?? new Dictionary<string, IReadOnlyList<string>>();

    public bool HasFieldErrors => FieldErrors.Count > 0;
}

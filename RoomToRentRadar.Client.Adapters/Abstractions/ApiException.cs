using System.Net;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ErrorCode { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    /// <summary>True when the request never got an HTTP response (StatusCode is 0).</summary>
    public bool IsNetworkError => ErrorCode == ApiErrorCodes.NetworkError;

    public ApiException(
        string message,
        HttpStatusCode statusCode,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors;
    }
}


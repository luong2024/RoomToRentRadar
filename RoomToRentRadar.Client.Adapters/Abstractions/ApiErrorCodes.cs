namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Client-only error codes; never sent by the server, so they stay out of ProblemCodes/openapi.</summary>
public static class ApiErrorCodes
{
    public const string NetworkError = "NETWORK_ERROR";
}

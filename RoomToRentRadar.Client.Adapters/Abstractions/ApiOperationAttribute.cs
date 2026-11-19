namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>
/// Maps a gateway method to its <c>operationId</c> in <c>docs/api/openapi.yaml</c> (ADR-003, contract-first).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiOperationAttribute(string operationId) : Attribute
{
    /// <summary>The <c>operationId</c> of the REST operation this method calls.</summary>
    public string OperationId { get; } = operationId;
}
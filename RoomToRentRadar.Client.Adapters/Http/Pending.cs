namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Task-id placeholders for gateway operations whose feature task has not started yet (PLAN §5.4).
/// The <see cref="NotImplementedException"/> surfaces on await, never as a synchronous throw.
/// </summary>
internal static class Pending
{
    public static Task For(string taskId, string operationId) =>
        Task.FromException(new NotImplementedException($"{taskId}: {operationId} is not implemented yet"));

    public static Task<T> For<T>(string taskId, string operationId) =>
        Task.FromException<T>(new NotImplementedException($"{taskId}: {operationId} is not implemented yet"));
}
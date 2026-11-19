using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Client.Adapters.Session;

public sealed class SessionEvents : ISessionEvents
{
    private readonly ILogger<SessionEvents>? _logger;

    public SessionEvents(ILogger<SessionEvents>? logger = null)
    {
        _logger = logger;
    }

    public event EventHandler? SessionExpired;

    public void RaiseSessionExpired()
    {
        // Snapshot so a listener that unsubscribes (or throws) cannot affect the remaining listeners.
        var handlers = SessionExpired?.GetInvocationList();
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in handlers)
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "A SessionExpired listener threw an exception.");
            }
        }
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Session;

namespace RoomToRentRadar.DeviceAdapters;

public sealed class MauiSecureStorageProvider : ISecureStorageProvider
{
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);
    public bool Remove(string key) => SecureStorage.Default.Remove(key);
    public void RemoveAll() => SecureStorage.Default.RemoveAll();
}

/// <summary>
/// Session store implementation adhering to ADR-005 for .NET MAUI:
/// Access token is maintained in-memory only; refresh token and user profile are persisted to SecureStorage.
/// </summary>
public sealed class MauiSecureSessionStore : SecureStorageSessionStore
{
    public MauiSecureSessionStore(ILogger<MauiSecureSessionStore>? logger = null)
        : base(new MauiSecureStorageProvider(), logger != null ? new LoggerWrapper(logger) : null)
    {
    }

    private sealed class LoggerWrapper : ILogger<SecureStorageSessionStore>
    {
        private readonly ILogger _inner;
        public LoggerWrapper(ILogger inner) => _inner = inner;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

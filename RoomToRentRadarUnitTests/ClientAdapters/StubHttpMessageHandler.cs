using System.Net;
using Microsoft.Extensions.Logging;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>
/// Test double for the primary HTTP handler: answers from a function (sync or async, so tests can
/// gate responses with TaskCompletionSource), records every request with its body, and counts per path.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly List<RecordedRequest> _requests = [];
    private readonly object _lock = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((request, _) => Task.FromResult(responder(request)))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return _requests.ToList();
            }
        }
    }

    public int CountForPath(string path) => Requests.Count(r => r.Path == path);

    public IReadOnlyList<RecordedRequest> RequestsForPath(string path) => Requests.Where(r => r.Path == path).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var contentHeaders = request.Content is null
            ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            : request.Content.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            body,
            request.Content?.Headers.ContentType?.ToString(),
            request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            contentHeaders,
            request.Options.ToDictionary(o => o.Key, o => o.Value));

        lock (_lock)
        {
            _requests.Add(recorded);
        }

        return await _responder(request, cancellationToken);
    }
}

public sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Authorization,
    byte[]? Body,
    string? ContentType,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, string[]> ContentHeaders,
    IReadOnlyDictionary<string, object?> Options)
{
    public string Path => Uri.IsAbsoluteUri ? Uri.AbsolutePath : Uri.OriginalString;

    public string? BodyAsString => Body is null ? null : System.Text.Encoding.UTF8.GetString(Body);
}

public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    private readonly object _lock = new();

    public StubHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        lock (_lock)
        {
            RequestedNames.Add(name);
        }

        return _client;
    }
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly object _lock = new();

    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (_lock)
        {
            Messages.Add(message);
        }
    }
}
using System.Net.Sockets;
using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Outermost handler: maps network failures and its own timeout to ApiException(NETWORK_ERROR).
/// A DelegatingHandler cannot tell HttpClient.Timeout from caller cancellation (both surface as
/// OperationCanceledException), so the timeout is applied here with a linked CTS; caller cancellation
/// propagates untouched.
/// </summary>
public sealed class NetworkErrorHandler : DelegatingHandler
{
    private readonly ApiOptions _options;

    public NetworkErrorHandler(ApiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            return await base.SendAsync(request, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ApiResponseReader.FromNetworkException(
                new TimeoutException($"The request timed out after {_options.Timeout.TotalSeconds:0.###}s."));
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException)
        {
            throw ApiResponseReader.FromNetworkException(ex);
        }
    }
}
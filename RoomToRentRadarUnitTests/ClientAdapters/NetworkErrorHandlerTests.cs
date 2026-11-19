using System.Net;
using System.Net.Sockets;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class NetworkErrorHandlerTests
{
    private const string TestUrl = "http://localhost:5000/api/rooms";

    [Test]
    public void Send_InnerThrowsHttpRequestException_ThrowsApiExceptionNetworkError()
    {
        var inner = new HttpRequestException("connection refused");
        var invoker = CreateInvoker(new StubHttpMessageHandler(_ => throw inner));

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUrl), CancellationToken.None));

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.Message, Is.EqualTo("Unable to connect to the server. Please check your internet connection."));
    }

    [Test]
    public void Send_InnerThrowsSocketExceptionWrapped_ThrowsNetworkError()
    {
        var inner = new HttpRequestException("socket failure", new SocketException(10061));
        var invoker = CreateInvoker(new StubHttpMessageHandler(_ => throw inner));

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUrl), CancellationToken.None));

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void Send_InnerNeverCompletesTimeoutElapsed_ThrowsNetworkError()
    {
        var stub = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var invoker = CreateInvoker(stub, TimeSpan.FromMilliseconds(50));

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUrl), CancellationToken.None));

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(ex.InnerException, Is.InstanceOf<TimeoutException>());
    }

    [Test]
    public void Send_CallerCancels_ThrowsOperationCanceledNotApiException()
    {
        var stub = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var invoker = CreateInvoker(stub);
        using var callerCts = new CancellationTokenSource();

        var sendTask = invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUrl), callerCts.Token);
        callerCts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await sendTask);
    }

    [TestCase(200)]
    [TestCase(401)]
    [TestCase(500)]
    public async Task Send_SuccessAndHttpErrors_PassThrough(int statusCode)
    {
        var invoker = CreateInvoker(new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode)statusCode)));

        var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUrl), CancellationToken.None);

        Assert.That((int)response.StatusCode, Is.EqualTo(statusCode));
    }

    private static HttpMessageInvoker CreateInvoker(StubHttpMessageHandler stub, TimeSpan? timeout = null)
    {
        var options = new ApiOptions();
        if (timeout.HasValue)
        {
            options.Timeout = timeout.Value;
        }

        var handler = new NetworkErrorHandler(options) { InnerHandler = stub };
        return new HttpMessageInvoker(handler);
    }
}
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels.Base;

[TestFixture]
public class BasePageModelTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Vi = new("vi-VN");

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedStrings.Instance.SetCulture(En);
    }

    private static ApiException ApiError(HttpStatusCode statusCode, string? code) =>
        new("Server rejected the request", statusCode, code);

    private static ApiException NetworkError() =>
        new("Network unreachable", (HttpStatusCode)0, ApiErrorCodes.NetworkError);

    [Test]
    public async Task RunAsync_LoadTrue_StateContent()
    {
        var model = new BasePageModel();

        await model.RunAsync(() => Task.FromResult(true));

        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.HasContent, Is.True);
        Assert.That(model.ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task RunAsync_LoadFalse_StateEmpty()
    {
        var model = new BasePageModel();

        await model.RunAsync(() => Task.FromResult(false));

        Assert.That(model.State, Is.EqualTo(ViewState.Empty));
        Assert.That(model.HasContent, Is.False);
    }

    [Test]
    public async Task RunAsync_ApiException_StateErrorWithMessage()
    {
        var model = new BasePageModel();

        await model.RunAsync(() => Task.FromException<bool>(ApiError(HttpStatusCode.NotFound, "ROOM_NOT_FOUND")));

        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.HasContent, Is.False);
        Assert.That(model.ErrorMessage, Is.EqualTo("Listing does not exist."));
    }

    [Test]
    public async Task RunAsync_NetworkErrorNoContent_StateErrorNoInternet()
    {
        var model = new BasePageModel();

        await model.RunAsync(() => Task.FromException<bool>(NetworkError()));

        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.HasContent, Is.False);
        Assert.That(model.ErrorMessage, Is.EqualTo("No internet connection."));
    }

    [Test]
    public async Task RunAsync_NetworkErrorAfterContent_StateOfflineKeepsHasContent()
    {
        var model = new BasePageModel();
        await model.RunAsync(() => Task.FromResult(true));

        await model.RunAsync(() => Task.FromException<bool>(NetworkError()));

        Assert.That(model.State, Is.EqualTo(ViewState.Offline));
        Assert.That(model.HasContent, Is.True);
        Assert.That(model.ErrorMessage, Is.EqualTo("No internet connection."));
    }

    [Test]
    public async Task RunAsync_ApiErrorAfterContent_StateErrorHasContentTrue()
    {
        var model = new BasePageModel();
        await model.RunAsync(() => Task.FromResult(true));

        await model.RunAsync(() => Task.FromException<bool>(ApiError(HttpStatusCode.InternalServerError, "INTERNAL_ERROR")));

        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.HasContent, Is.True);
        Assert.That(model.ErrorMessage, Is.EqualTo("An unexpected error occurred, please try again."));
    }

    [Test]
    public async Task RunAsync_SecondCallWhileRunning_QueuesLatestLoad()
    {
        var model = new BasePageModel();
        var pending = new TaskCompletionSource<bool>();
        var calls = 0;

        var first = model.RunAsync(() =>
        {
            calls++;
            return pending.Task;
        });
        var second = model.RunAsync(() =>
        {
            calls++;
            return Task.FromResult(false);
        });

        Assert.That(second.IsCompleted, Is.True, "A queued call returns immediately.");
        Assert.That(calls, Is.EqualTo(1));

        pending.SetResult(true);
        await first;
        await second;

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(model.State, Is.EqualTo(ViewState.Empty));
    }

    [Test]
    public async Task RunAsync_MultipleCallsWhileRunning_OnlyLatestPendingLoadRuns()
    {
        var model = new BasePageModel();
        var pending = new TaskCompletionSource<bool>();
        var calls = 0;

        var first = model.RunAsync(() =>
        {
            calls++;
            return pending.Task;
        });
        _ = model.RunAsync(() =>
        {
            calls++;
            return Task.FromResult(false);
        });
        _ = model.RunAsync(() =>
        {
            calls++;
            return Task.FromResult(true);
        });

        pending.SetResult(true);
        await first;

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(model.State, Is.EqualTo(ViewState.Content));
    }

    [Test]
    public async Task RunAsync_Refresh_SetsIsRefreshingNotLoading()
    {
        var model = new BasePageModel();
        await model.RunAsync(() => Task.FromResult(true));

        var pending = new TaskCompletionSource<bool>();
        var refreshingDuringLoad = false;
        var loadingDuringLoad = true;
        var stateDuringLoad = ViewState.Empty;

        var refresh = model.RunAsync(() =>
        {
            refreshingDuringLoad = model.IsRefreshing;
            loadingDuringLoad = model.IsLoading;
            stateDuringLoad = model.State;
            return pending.Task;
        }, isRefresh: true);

        pending.SetResult(true);
        await refresh;

        Assert.That(refreshingDuringLoad, Is.True);
        Assert.That(loadingDuringLoad, Is.False);
        Assert.That(stateDuringLoad, Is.EqualTo(ViewState.Content));
        Assert.That(model.IsRefreshing, Is.False);
        Assert.That(model.IsLoading, Is.False);
        Assert.That(model.State, Is.EqualTo(ViewState.Content));
    }

    [Test]
    public async Task RunAsync_StateSequence_LoadingThenContent()
    {
        var model = new BasePageModel();
        var observed = new List<ViewState>();

        var run = model.RunAsync(() =>
        {
            observed.Add(model.State);
            return Task.FromResult(true);
        });
        await run;
        observed.Add(model.State);

        Assert.That(observed, Is.EqualTo(new[] { ViewState.Loading, ViewState.Content }));
    }

    [Test]
    public async Task RunAsync_Cancelled_RestoresPreviousState()
    {
        var model = new BasePageModel();
        await model.RunAsync(() => Task.FromResult(true));

        await model.RunAsync(() => Task.FromException<bool>(new OperationCanceledException()));

        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task RunAsync_SuccessAfterError_ClearsErrorMessage()
    {
        var model = new BasePageModel();
        var calls = 0;

        await model.RunAsync(() =>
        {
            calls++;
            return Task.FromException<bool>(ApiError(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"));
        });
        Assert.That(model.ErrorMessage, Is.Not.Empty);

        await model.RunAsync(() =>
        {
            calls++;
            return Task.FromResult(true);
        });

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task RetryCommand_AfterError_RerunsLastLoad()
    {
        var model = new BasePageModel();
        var calls = 0;

        await model.RunAsync(() =>
        {
            calls++;
            return calls == 1
                ? Task.FromException<bool>(ApiError(HttpStatusCode.InternalServerError, "INTERNAL_ERROR"))
                : Task.FromResult(true);
        });
        Assert.That(model.State, Is.EqualTo(ViewState.Error));

        await model.RetryCommand.ExecuteAsync(null);

        Assert.That(calls, Is.EqualTo(2));
        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task RetryCommand_NoPriorLoad_DoesNothing()
    {
        var model = new BasePageModel();
        var stateBefore = model.State;

        await model.RetryCommand.ExecuteAsync(null);

        Assert.That(model.State, Is.EqualTo(stateBefore));
        Assert.That(model.IsLoading, Is.False);
        Assert.That(model.IsRefreshing, Is.False);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDroppedModel() => new(new BasePageModel());

    [Test]
    public void LocalizationSubscription_DroppedModel_IsCollectableAndLanguageChangeDoesNotThrow()
    {
        var weak = CreateDroppedModel();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(weak.IsAlive, Is.False);
        Assert.DoesNotThrow(() => LocalizedStrings.Instance.SetCulture(Vi));
    }

    [Test]
    public async Task CultureChangeToVi_WhileError_RemapsMessage()
    {
        var model = new BasePageModel();
        await model.RunAsync(() => Task.FromException<bool>(ApiError(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS")));
        Assert.That(model.ErrorMessage, Is.EqualTo("Invalid email or password."));

        LocalizedStrings.Instance.SetCulture(Vi);

        Assert.That(model.ErrorMessage, Is.EqualTo("Email hoặc mật khẩu không đúng."));
    }
}

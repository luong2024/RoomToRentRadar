using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RoomReportViewModelTests
{
    private static readonly Guid ListingId = Guid.Parse("0c7a1f42-3b5e-4a8d-9c61-7d2f90a1b2c3");

    [Test]
    public void Constructor_ExposesAllReportReasons()
    {
        var viewModel = CreateViewModel(new FakeReportsClient(), out _, out _);

        Assert.That(
            viewModel.Reasons.Select(option => option.Reason),
            Is.EqualTo(new[]
            {
                ListingReportReason.Spam,
                ListingReportReason.WrongInfo,
                ListingReportReason.Scam,
                ListingReportReason.Offensive,
                ListingReportReason.AlreadyRented
            }));
    }

    [Test]
    public void Open_ShowsSheetWithListingAndResetsPreviousSelection()
    {
        var client = new FakeReportsClient
        {
            Result = new ListingReportSubmitResult(ListingReportSubmitOutcome.DuplicateOpenReport)
        };
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng ban đầu");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[2]);
        viewModel.Note = "Ghi chú cũ";
        viewModel.SubmitCommand.Execute(null);

        viewModel.Open(ListingId, "Phòng mới");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsOpen, Is.True);
            Assert.That(viewModel.ListingTitle, Is.EqualTo("Phòng mới"));
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
            Assert.That(viewModel.SelectedReason, Is.Null);
            Assert.That(viewModel.Reasons.Any(option => option.IsSelected), Is.False);
            Assert.That(viewModel.Note, Is.Empty);
            Assert.That(viewModel.StatusMessage, Is.Empty);
            Assert.That(viewModel.HasStatusMessage, Is.False);
            Assert.That(viewModel.IsFormVisible, Is.True);
        });
    }

    [Test]
    public void SelectReason_SelectsSingleOptionAndEnablesSubmit()
    {
        var viewModel = CreateViewModel(new FakeReportsClient(), out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");

        Assert.That(viewModel.CanSubmit, Is.False);

        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[1]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedReason, Is.EqualTo(ListingReportReason.WrongInfo));
            Assert.That(viewModel.Reasons[1].IsSelected, Is.True);
            Assert.That(viewModel.Reasons.Where(option => option.IsSelected), Has.Exactly(1).Items);
            Assert.That(viewModel.CanSubmit, Is.True);
        });
    }

    [Test]
    public async Task SubmitAsync_WhenCreated_ClosesSheetAndShowsSuccessAlert()
    {
        var reportId = Guid.NewGuid();
        var client = new FakeReportsClient
        {
            Result = new ListingReportSubmitResult(ListingReportSubmitOutcome.Created, reportId)
        };
        var alert = new FakeAlertPrompt();
        var viewModel = CreateViewModel(client, out _, out _, alert);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[2]);
        viewModel.Note = "  Chủ phòng đòi cọc trước  ";

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Has.Count.EqualTo(1));
            Assert.That(client.Calls[0].ListingId, Is.EqualTo(ListingId));
            Assert.That(client.Calls[0].Reason, Is.EqualTo(ListingReportReason.Scam));
            Assert.That(client.Calls[0].Details, Is.EqualTo("  Chủ phòng đòi cọc trước  "));
            Assert.That(viewModel.IsOpen, Is.False);
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
            Assert.That(alert.Alerts, Has.Count.EqualTo(1));
            Assert.That(alert.Alerts[0].Title, Is.EqualTo("Đã gửi báo cáo"));
            Assert.That(viewModel.IsSubmitting, Is.False);
        });
    }

    [Test]
    public async Task SubmitAsync_WithoutReason_DoesNotCallClient()
    {
        var client = new FakeReportsClient();
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty);
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
        });
    }

    [Test]
    public async Task SubmitAsync_WhenUnauthorized_ShowsSignInState()
    {
        var client = new FakeReportsClient
        {
            Result = new ListingReportSubmitResult(ListingReportSubmitOutcome.Unauthorized)
        };
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[0]);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.SignInRequired));
            Assert.That(viewModel.IsSignInRequiredVisible, Is.True);
            Assert.That(viewModel.StatusMessage, Does.Contain("đăng nhập"));
        });
    }

    [TestCase(ListingReportSubmitOutcome.ListingNotFound, "không còn khả dụng")]
    [TestCase(ListingReportSubmitOutcome.OwnListing, "chính mình")]
    [TestCase(ListingReportSubmitOutcome.DuplicateOpenReport, "đã báo cáo")]
    public async Task SubmitAsync_WhenRejected_KeepsFormAndExplains(
        ListingReportSubmitOutcome outcome,
        string expectedFragment)
    {
        var client = new FakeReportsClient { Result = new ListingReportSubmitResult(outcome) };
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[3]);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
            Assert.That(viewModel.HasStatusMessage, Is.True);
            Assert.That(viewModel.StatusMessage, Does.Contain(expectedFragment));
            Assert.That(viewModel.CanSubmit, Is.True);
        });
    }

    [Test]
    public async Task SubmitAsync_WhenRateLimited_RoundsRetryWindowUpToMinutes()
    {
        var client = new FakeReportsClient
        {
            Result = new ListingReportSubmitResult(
                ListingReportSubmitOutcome.RateLimited,
                RetryAfter: TimeSpan.FromSeconds(90))
        };
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[0]);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.That(viewModel.StatusMessage, Does.Contain("2 phút"));
    }

    [Test]
    public async Task SubmitAsync_WhileAwaitingResponse_DisablesSubmitAndShowsProgressText()
    {
        var pending = new TaskCompletionSource<ListingReportSubmitResult>();
        var client = new FakeReportsClient { Pending = pending };
        var alert = new FakeAlertPrompt();
        var viewModel = CreateViewModel(client, out _, out _, alert);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[0]);

        var submission = viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSubmitting, Is.True);
            Assert.That(viewModel.SubmitButtonText, Is.EqualTo("Đang gửi…"));
            Assert.That(viewModel.CanSubmit, Is.False);
        });

        pending.SetResult(new ListingReportSubmitResult(ListingReportSubmitOutcome.Created, Guid.NewGuid()));
        await submission;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsOpen, Is.False);
            Assert.That(alert.Alerts, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task SubmitAsync_WhenClientThrows_ReportsErrorAndKeepsForm()
    {
        var client = new FakeReportsClient { Exception = new HttpRequestException("network down") };
        var viewModel = CreateViewModel(client, out _, out var errorHandler);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[0]);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(errorHandler.HandledExceptions, Has.Count.EqualTo(1));
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
            Assert.That(viewModel.HasStatusMessage, Is.True);
            Assert.That(viewModel.IsSubmitting, Is.False);
        });
    }

    [Test]
    public void Note_WhenLongerThanMaximum_IsClamped()
    {
        var viewModel = CreateViewModel(new FakeReportsClient(), out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");

        viewModel.Note = new string('a', RoomReportViewModel.MaximumNoteLength + 25);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Note, Has.Length.EqualTo(RoomReportViewModel.MaximumNoteLength));
            Assert.That(viewModel.NoteLengthText, Is.EqualTo("1000/1000"));
        });
    }

    [Test]
    public async Task Close_HidesSheetAndReturnsToForm()
    {
        var client = new FakeReportsClient
        {
            Result = new ListingReportSubmitResult(ListingReportSubmitOutcome.DuplicateOpenReport)
        };
        var viewModel = CreateViewModel(client, out _, out _);
        viewModel.Open(ListingId, "Phòng trọ");
        viewModel.SelectReasonCommand.Execute(viewModel.Reasons[0]);
        await viewModel.SubmitCommand.ExecuteAsync(null);

        viewModel.CloseCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsOpen, Is.False);
            Assert.That(viewModel.State, Is.EqualTo(RoomReportState.Form));
            Assert.That(viewModel.StatusMessage, Is.Empty);
        });
    }

    [Test]
    public async Task SignInAsync_ClosesSheetAndNavigatesToLogin()
    {
        var viewModel = CreateViewModel(new FakeReportsClient(), out var navigation, out _);
        viewModel.Open(ListingId, "Phòng trọ");

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsOpen, Is.False);
            Assert.That(navigation.GoToLoginCount, Is.EqualTo(1));
        });
    }

    private static RoomReportViewModel CreateViewModel(
        FakeReportsClient client,
        out FakeNavigationService navigation,
        out FakeErrorHandler errorHandler,
        FakeAlertPrompt? alert = null)
    {
        navigation = new FakeNavigationService();
        errorHandler = new FakeErrorHandler();
        return new RoomReportViewModel(client, navigation, errorHandler, alert ?? new FakeAlertPrompt());
    }

    private sealed class FakeReportsClient : IListingReportsClient
    {
        public List<(Guid ListingId, ListingReportReason Reason, string? Details)> Calls { get; } = new();

        public ListingReportSubmitResult Result { get; set; } =
            new(ListingReportSubmitOutcome.Created, Guid.NewGuid());

        public Exception? Exception { get; set; }

        public TaskCompletionSource<ListingReportSubmitResult>? Pending { get; set; }

        public Task<ListingReportSubmitResult> CreateReportAsync(
            Guid listingId,
            ListingReportReason reason,
            string? details,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((listingId, reason, details));

            if (Exception is not null)
                throw Exception;

            return Pending is not null ? Pending.Task : Task.FromResult(Result);
        }
    }

    private sealed class FakeAlertPrompt : IAlertPrompt
    {
        public List<(string Title, string Message)> Alerts { get; } = new();

        public Task ShowAsync(
            string title,
            string message,
            string dismissText = "OK",
            CancellationToken cancellationToken = default)
        {
            Alerts.Add((title, message));
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string acceptText,
            string cancelText,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task GoToListingEditorAsync(Guid? listingId = null) => Task.CompletedTask;

        public int GoToLoginCount { get; private set; }

        public Task GoToLoginAsync()
        {
            GoToLoginCount++;
            return Task.CompletedTask;
        }

        public Task GoToRegisterAsync() => Task.CompletedTask;

        public Task GoToRoleRootAsync(RoomToRentRadar.Models.AppUserRole? role) => Task.CompletedTask;

        public Task GoToRoomDetailAsync(Guid listingId)
            => throw new NotSupportedException("The report sheet does not open listings.");

        public Task GoToDirectionsAsync(
            double destinationLat,
            double destinationLng,
            double? originLat,
            double? originLng)
            => throw new NotSupportedException("The report sheet does not open directions.");

        public Task GoToComparisonAsync()
            => throw new NotSupportedException("The report sheet does not open comparison.");

        public Task GoBackAsync()
            => throw new NotSupportedException("The report sheet does not navigate back.");
    }

    private sealed class FakeErrorHandler : IErrorHandler
    {
        public List<Exception> HandledExceptions { get; } = new();

        public void HandleError(Exception ex) => HandledExceptions.Add(ex);
    }
}

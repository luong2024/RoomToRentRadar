using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Features.RoomReport.ViewModels;

public enum RoomReportState
{
    Form,
    SignInRequired
}

public partial class ReportReasonOption(
    ListingReportReason reason,
    string label,
    string description) : ObservableObject
{
    public ListingReportReason Reason { get; } = reason;

    public string Label { get; } = label;

    public string Description { get; } = description;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public partial class RoomReportViewModel : ObservableObject
{
    public const int MaximumNoteLength = 1000;

    private readonly IListingReportsClient _reportsClient;
    private readonly INavigationService _navigationService;
    private readonly IErrorHandler _errorHandler;
    private readonly IAlertPrompt _alertPrompt;

    private Guid? _listingId;

    public RoomReportViewModel(
        IListingReportsClient reportsClient,
        INavigationService navigationService,
        IErrorHandler errorHandler,
        IAlertPrompt alertPrompt)
    {
        _reportsClient = reportsClient;
        _navigationService = navigationService;
        _errorHandler = errorHandler;
        _alertPrompt = alertPrompt;

        Reasons =
        [
            new(ListingReportReason.Spam, "Spam / Tin rác", "Tin đăng lặp lại hoặc quảng cáo không liên quan"),
            new(ListingReportReason.WrongInfo, "Thông tin sai", "Giá, diện tích hoặc địa chỉ không đúng thực tế"),
            new(ListingReportReason.Scam, "Lừa đảo", "Đòi cọc trước hoặc có dấu hiệu gian lận"),
            new(ListingReportReason.Offensive, "Nội dung phản cảm", "Hình ảnh hoặc nội dung không phù hợp"),
            new(ListingReportReason.AlreadyRented, "Đã cho thuê", "Phòng đã có người thuê nhưng vẫn còn đăng")
        ];

        ListingTitle = string.Empty;
        Note = string.Empty;
        StatusMessage = string.Empty;
    }

    public IReadOnlyList<ReportReasonOption> Reasons { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string ListingTitle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial ListingReportReason? SelectedReason { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteLengthText))]
    public partial string Note { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    public partial bool IsSubmitting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    [NotifyPropertyChangedFor(nameof(IsSignInRequiredVisible))]
    public partial RoomReportState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string StatusMessage { get; set; }

    public string NoteLengthText => $"{Note.Length}/{MaximumNoteLength}";

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public bool IsFormVisible => State == RoomReportState.Form;

    public bool IsSignInRequiredVisible => State == RoomReportState.SignInRequired;

    public bool CanSubmit => SelectedReason is not null && !IsSubmitting && State == RoomReportState.Form;

    public string SubmitButtonText => IsSubmitting ? "Đang gửi…" : "Gửi báo cáo";

    public void Open(Guid listingId, string listingTitle)
    {
        _listingId = listingId;
        ListingTitle = listingTitle;
        Note = string.Empty;
        SelectedReason = null;
        StatusMessage = string.Empty;
        State = RoomReportState.Form;
        IsSubmitting = false;
        foreach (var option in Reasons)
            option.IsSelected = false;
        IsOpen = true;
    }

    [RelayCommand]
    private void SelectReason(ReportReasonOption? option)
    {
        if (option is null)
            return;

        foreach (var candidate in Reasons)
            candidate.IsSelected = candidate == option;

        SelectedReason = option.Reason;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (!CanSubmit || _listingId is not { } listingId)
            return;

        IsSubmitting = true;
        StatusMessage = string.Empty;
        try
        {
            var result = await _reportsClient.CreateReportAsync(
                listingId,
                SelectedReason!.Value,
                Note,
                cancellationToken);

            switch (result.Outcome)
            {
                case ListingReportSubmitOutcome.Created:
                    Close();
                    await _alertPrompt.ShowAsync(
                        "Đã gửi báo cáo",
                        "Cảm ơn bạn đã giúp cộng đồng an toàn hơn. Đội ngũ kiểm duyệt sẽ xem xét tin đăng này.",
                        cancellationToken: cancellationToken);
                    break;

                case ListingReportSubmitOutcome.Unauthorized:
                    State = RoomReportState.SignInRequired;
                    StatusMessage = "Bạn cần đăng nhập để gửi báo cáo.";
                    break;

                case ListingReportSubmitOutcome.ListingNotFound:
                    StatusMessage = "Tin đăng không còn khả dụng để báo cáo.";
                    break;

                case ListingReportSubmitOutcome.OwnListing:
                    StatusMessage = "Bạn không thể báo cáo tin đăng của chính mình.";
                    break;

                case ListingReportSubmitOutcome.DuplicateOpenReport:
                    StatusMessage = "Bạn đã báo cáo tin này rồi. Báo cáo trước đang chờ kiểm duyệt.";
                    break;

                case ListingReportSubmitOutcome.RateLimited:
                    var minutes = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalMinutes));
                    StatusMessage = $"Bạn đã gửi quá nhiều báo cáo. Vui lòng thử lại sau {minutes} phút.";
                    break;
            }
        }
        catch (Exception exception)
        {
            _errorHandler.HandleError(exception);
            StatusMessage = "Chưa gửi được báo cáo. Kiểm tra kết nối mạng và thử lại.";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        State = RoomReportState.Form;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        IsOpen = false;
        try
        {
            await _navigationService.GoToLoginAsync();
        }
        catch (Exception exception)
        {
            _errorHandler.HandleError(exception);
        }
    }

    partial void OnNoteChanged(string value)
    {
        if (value.Length > MaximumNoteLength)
            Note = value[..MaximumNoteLength];
    }
}

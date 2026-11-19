using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Abstractions.Reports;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.Reports
{
    /// <summary>
    /// TASK OWNER: T13.2 (US13 — Build report dialog UI and user confirmation toast).
    /// Presentation-Backend / PageModel API supporting Report Listing dialog, validation,
    /// anti-spam prevention, and user confirmation toast state.
    /// Acceptance Criteria:
    ///   AC1: A Report action is available on every listing detail screen.
    ///   AC2: The user chooses a reason and adds an optional note.
    ///   AC3: The report is sent to the moderation queue and a confirmation toast is shown.
    /// </summary>
    [QueryProperty(nameof(RoomId), "roomId")]
    [QueryProperty(nameof(RoomTitle), "roomTitle")]
    public partial class ReportListingPageModel : ObservableObject, IQueryAttributable
    {
        private readonly IListingReportService _reportService;
        private readonly IRoomListingRepository? _roomRepository;

        public const int MaxNoteLength = ListingReportService.MaxNoteLength;

        [ObservableProperty]
        public partial int RoomId { get; set; }

        [ObservableProperty]
        public partial string RoomTitle { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int ReporterUserId { get; set; } = 1;

        [ObservableProperty]
        public partial ReportReasonOption? SelectedReasonOption { get; set; }

        [ObservableProperty]
        public partial string Note { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSubmitting { get; set; }

        [ObservableProperty]
        public partial bool HasError { get; set; }

        [ObservableProperty]
        public partial string? ErrorMessage { get; set; }

        [ObservableProperty]
        public partial bool ShowConfirmationToast { get; set; }

        [ObservableProperty]
        public partial string ConfirmationToastMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSubmissionSuccess { get; set; }

        [ObservableProperty]
        public partial int SubmittedReportId { get; set; }

        public ObservableCollection<ReportReasonOption> PredefinedReasons { get; } = new();

        public int NoteRemainingChars => Math.Max(0, MaxNoteLength - (Note?.Length ?? 0));

        public ReportListingPageModel(
            IListingReportService reportService,
            IRoomListingRepository? roomRepository = null)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _roomRepository = roomRepository;

            LoadPredefinedReasons();
        }

        public void LoadPredefinedReasons()
        {
            PredefinedReasons.Clear();
            var reasons = _reportService.GetPredefinedReasons();
            foreach (var reason in reasons)
            {
                PredefinedReasons.Add(reason);
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query == null) return;

            if (query.TryGetValue("roomId", out var idObj) || query.TryGetValue("RoomId", out idObj))
            {
                if (idObj is int idInt)
                {
                    RoomId = idInt;
                }
                else if (int.TryParse(idObj?.ToString(), out var parsedId))
                {
                    RoomId = parsedId;
                }
            }

            if (query.TryGetValue("roomTitle", out var titleObj) || query.TryGetValue("RoomTitle", out titleObj))
            {
                RoomTitle = titleObj?.ToString() ?? string.Empty;
            }

            if (query.TryGetValue("reporterUserId", out var userObj) || query.TryGetValue("ReporterUserId", out userObj))
            {
                if (userObj is int userIdInt)
                {
                    ReporterUserId = userIdInt;
                }
                else if (int.TryParse(userObj?.ToString(), out var parsedUserId))
                {
                    ReporterUserId = parsedUserId;
                }
            }

            // If RoomTitle is still empty and RoomId is valid, attempt background resolution
            if (string.IsNullOrWhiteSpace(RoomTitle) && RoomId > 0 && _roomRepository != null)
            {
                _ = TryPopulateRoomTitleAsync(RoomId);
            }
        }

        private async Task TryPopulateRoomTitleAsync(int roomId)
        {
            try
            {
                if (_roomRepository != null)
                {
                    var room = await _roomRepository.GetByIdAsync(roomId);
                    if (room != null && !string.IsNullOrWhiteSpace(room.Title))
                    {
                        RoomTitle = room.Title;
                    }
                }
            }
            catch
            {
                // Non-critical fallback
            }
        }

        partial void OnNoteChanged(string value)
        {
            OnPropertyChanged(nameof(NoteRemainingChars));
        }

        [RelayCommand]
        public void SelectReason(ReportReasonOption? reason)
        {
            SelectedReasonOption = reason;
            HasError = false;
            ErrorMessage = null;
        }

        [RelayCommand]
        public async Task<SubmitReportResult> SubmitReportAsync()
        {
            if (IsSubmitting)
            {
                return SubmitReportResult.Failed("Đang xử lý báo cáo, vui lòng đợi...");
            }

            HasError = false;
            ErrorMessage = null;
            ShowConfirmationToast = false;

            var request = new SubmitReportRequest
            {
                RoomId = RoomId,
                RoomTitle = RoomTitle,
                ReporterUserId = ReporterUserId,
                Reason = SelectedReasonOption?.DisplayText ?? string.Empty,
                Note = Note
            };

            var validation = _reportService.ValidateReport(request);
            if (!validation.IsValid)
            {
                HasError = true;
                ErrorMessage = validation.ErrorMessage;
                return SubmitReportResult.Failed(validation.ErrorMessage ?? "Dữ liệu không hợp lệ.");
            }

            try
            {
                IsSubmitting = true;

                var result = await _reportService.SubmitReportAsync(request);
                if (result.Success)
                {
                    IsSubmissionSuccess = true;
                    SubmittedReportId = result.ReportId;
                    ConfirmationToastMessage = result.ConfirmationToastMessage;
                    ShowConfirmationToast = true;
                    return result;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = result.ErrorMessage;
                    return result;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Đã xảy ra lỗi khi gửi báo cáo: {ex.Message}";
                return SubmitReportResult.Failed(ErrorMessage);
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        [RelayCommand]
        public void DismissToast()
        {
            ShowConfirmationToast = false;
        }

        [RelayCommand]
        public void Reset()
        {
            SelectedReasonOption = null;
            Note = string.Empty;
            HasError = false;
            ErrorMessage = null;
            ShowConfirmationToast = false;
            IsSubmissionSuccess = false;
            SubmittedReportId = 0;
            ConfirmationToastMessage = string.Empty;
        }
    }
}

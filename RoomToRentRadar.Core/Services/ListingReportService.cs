using RoomToRentRadar.Abstractions.Reports;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Severity level of a reported violation for moderation queue prioritization (US13).
    /// </summary>
    public enum ReportSeverity
    {
        Normal = 0,
        Warning = 1,
        Urgent = 2
    }

    /// <summary>
    /// Represents a selectable predefined report reason option in the report dialog (AC2).
    /// </summary>
    public class ReportReasonOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportSeverity Severity { get; set; } = ReportSeverity.Normal;

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Data transfer object for submitting a room listing report (AC1 &amp; AC2).
    /// </summary>
    public class SubmitReportRequest
    {
        public int RoomId { get; set; }
        public int ReporterUserId { get; set; } = 1; // Default to current authenticated student
        public string Reason { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string RoomTitle { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result payload after submitting a listing report to the moderation queue (AC3).
    /// Contains confirmation toast message or error details.
    /// </summary>
    public class SubmitReportResult
    {
        public bool Success { get; set; }
        public int ReportId { get; set; }
        public string ConfirmationToastMessage { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public static SubmitReportResult Succeeded(int reportId, string confirmationMessage) =>
            new()
            {
                Success = true,
                ReportId = reportId,
                ConfirmationToastMessage = confirmationMessage,
                SubmittedAt = DateTime.UtcNow
            };

        public static SubmitReportResult Failed(string errorMessage) =>
            new()
            {
                Success = false,
                ErrorMessage = errorMessage,
                SubmittedAt = DateTime.UtcNow
            };
    }

    /// <summary>
    /// Validation outcome for a listing report request.
    /// </summary>
    public class ReportValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public static ReportValidationResult Success() => new() { IsValid = true };
        public static ReportValidationResult Failed(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// Contract for US13 Listing Report Service &amp; Moderation Workflow (T13.2).
    /// Acceptance Criteria:
    ///   AC1: A Report action is available on every listing detail screen.
    ///   AC2: The user chooses a reason and adds an optional note.
    ///   AC3: The report is sent to the moderation queue and a confirmation is shown.
    /// </summary>
    public interface IListingReportService
    {
        IReadOnlyList<ReportReasonOption> GetPredefinedReasons();

        ReportReasonOption? GetReasonByKey(string key);

        ReportValidationResult ValidateReport(SubmitReportRequest? request);

        Task<SubmitReportResult> SubmitReportAsync(
            SubmitReportRequest request,
            CancellationToken cancellationToken = default);

        Task<bool> HasPendingReportAsync(
            int roomId,
            int reporterUserId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// TASK OWNER: T13.2 (US13 — Build report dialog UI and user confirmation toast).
    /// Implementation of <see cref="IListingReportService"/> coordinating validation, anti-spam, and submission.
    /// </summary>
    public class ListingReportService : IListingReportService
    {
        private readonly IListingReportRepository _reportRepository;

        public const int MaxNoteLength = 500;
        public const string DefaultConfirmationToast = "Cảm ơn bạn đã phản ánh! Báo cáo vi phạm đã được gửi tới Ban quản trị ICTU để kiểm duyệt trong vòng 24h.";

        public ListingReportService(IListingReportRepository reportRepository)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        }

        /// <summary>
        /// AC2: Returns standardized, pre-vetted report reasons tailored for ICTU students.
        /// </summary>
        public virtual IReadOnlyList<ReportReasonOption> GetPredefinedReasons() => PredefinedReasonsList;

        private static readonly IReadOnlyList<ReportReasonOption> PredefinedReasonsList = new List<ReportReasonOption>
        {
            new()
            {
                Key = "AlreadyRented",
                DisplayText = "Phòng đã cho thuê / hết phòng",
                Description = "Chủ trọ thông báo phòng đã có người thuê nhưng bài đăng chưa hạ xuống.",
                Severity = ReportSeverity.Normal
            },
            new()
            {
                Key = "InaccuratePriceDeposit",
                DisplayText = "Giá thuê hoặc tiền cọc sai lệch thực tế",
                Description = "Giá thực tế khi đến xem phòng cao hơn mức giá niêm yết trong ứng dụng.",
                Severity = ReportSeverity.Warning
            },
            new()
            {
                Key = "InaccuratePhotosAddress",
                DisplayText = "Địa chỉ hoặc hình ảnh phòng không đúng",
                Description = "Hình ảnh phòng hoặc địa chỉ sai lệch, lấy ảnh mạng hoặc không đúng thực tế.",
                Severity = ReportSeverity.Warning
            },
            new()
            {
                Key = "SuspiciousScam",
                DisplayText = "Chủ trọ có dấu hiệu lừa đảo / yêu cầu cọc bất thường",
                Description = "Yêu cầu chuyển tiền đặt cọc giữ phòng trước khi xem phòng trực tiếp.",
                Severity = ReportSeverity.Urgent
            },
            new()
            {
                Key = "UnreasonableRulesOrFees",
                DisplayText = "Nội quy hoặc chi phí sinh hoạt phát sinh vô lý",
                Description = "Giá điện, nước, internet hoặc giờ giấc giới nghiêm ép buộc không đúng cam kết.",
                Severity = ReportSeverity.Normal
            },
            new()
            {
                Key = "Other",
                DisplayText = "Lý do khác",
                Description = "Các vấn đề khác liên quan đến an ninh, vệ sinh hoặc hành vi vi phạm.",
                Severity = ReportSeverity.Normal
            }
        };

        /// <summary>
        /// Looks up a predefined report reason option by its unique key or display text.
        /// </summary>
        public virtual ReportReasonOption? GetReasonByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var trimmed = key.Trim();
            return PredefinedReasonsList.FirstOrDefault(r =>
                string.Equals(r.Key, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.DisplayText, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates report request fields according to US13 AC2 constraints.
        /// </summary>
        public virtual ReportValidationResult ValidateReport(SubmitReportRequest? request)
        {
            if (request == null)
            {
                return ReportValidationResult.Failed("Yêu cầu báo cáo không được để trống.");
            }

            if (request.RoomId <= 0)
            {
                return ReportValidationResult.Failed("Mã phòng trọ không hợp lệ.");
            }

            if (request.ReporterUserId <= 0)
            {
                return ReportValidationResult.Failed("Tài khoản người dùng báo cáo không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return ReportValidationResult.Failed("Vui lòng chọn lý do báo cáo.");
            }

            if (!string.IsNullOrEmpty(request.Note) && request.Note.Trim().Length > MaxNoteLength)
            {
                return ReportValidationResult.Failed($"Nội dung ghi chú bổ sung không được vượt quá {MaxNoteLength} ký tự.");
            }

            return ReportValidationResult.Success();
        }

        /// <summary>
        /// AC1, AC2, AC3: Validates request, checks anti-spam, and submits report to moderation queue.
        /// </summary>
        public virtual async Task<SubmitReportResult> SubmitReportAsync(
            SubmitReportRequest request,
            CancellationToken cancellationToken = default)
        {
            var validation = ValidateReport(request);
            if (!validation.IsValid)
            {
                return SubmitReportResult.Failed(validation.ErrorMessage ?? "Dữ liệu báo cáo không hợp lệ.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Anti-Spam Check (AC3): Prevent duplicate pending reports from the same student for the same room
            bool isDuplicate = await HasPendingReportAsync(request.RoomId, request.ReporterUserId, cancellationToken);
            if (isDuplicate)
            {
                return SubmitReportResult.Failed("Bạn đã gửi một báo cáo vi phạm cho phòng trọ này và đang chờ Ban quản trị duyệt. Vui lòng không gửi trùng lặp.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Normalize reason if matching a predefined option key
            var reasonOption = GetReasonByKey(request.Reason);
            string finalReason = reasonOption?.DisplayText ?? request.Reason.Trim();

            // Sanitize optional note
            var sanitizedNote = (request.Note ?? string.Empty).Trim();
            if (sanitizedNote.Length > MaxNoteLength)
            {
                sanitizedNote = sanitizedNote[..MaxNoteLength];
            }

            var report = new ListingReport
            {
                RoomId = request.RoomId,
                ReporterUserId = request.ReporterUserId,
                Reason = finalReason,
                Note = sanitizedNote,
                Status = Constants.ReportStatuses.Pending,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")
            };

            int reportId = await _reportRepository.SubmitReportAsync(report);

            string toastMessage = !string.IsNullOrWhiteSpace(request.RoomTitle)
                ? $"Cảm ơn bạn đã phản ánh về '{request.RoomTitle.Trim()}'! Báo cáo vi phạm đã được gửi tới Ban quản trị ICTU để kiểm duyệt trong vòng 24h."
                : DefaultConfirmationToast;

            return SubmitReportResult.Succeeded(reportId, toastMessage);
        }

        /// <summary>
        /// Checks if the user already has an active pending report for this room to prevent spam.
        /// </summary>
        public virtual async Task<bool> HasPendingReportAsync(
            int roomId,
            int reporterUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pendingReports = await _reportRepository.ListPendingReportsAsync();
            return pendingReports.Any(r =>
                r.RoomId == roomId &&
                r.ReporterUserId == reporterUserId &&
                string.Equals(r.Status, Constants.ReportStatuses.Pending, StringComparison.OrdinalIgnoreCase));
        }
    }
}

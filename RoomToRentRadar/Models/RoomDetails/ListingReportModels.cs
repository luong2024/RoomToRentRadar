using CommunityToolkit.Mvvm.ComponentModel;

namespace RoomToRentRadar.Models.RoomDetails;

/// <summary>
/// Categorizes why a user reports a rental listing (matches backend ReportReason enum).
/// </summary>
public enum ReportReasonCode
{
    InaccurateInfo = 1,
    SuspectedScam = 2,
    AlreadyRented = 3,
    DuplicateListing = 4,
    InappropriateContent = 5,
    Other = 99
}

public partial class ReportReasonOption : ObservableObject
{
    public ReportReasonCode Code { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RequiresNote { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public static IReadOnlyList<ReportReasonOption> CreateDefaultOptions() =>
    [
        new ReportReasonOption
        {
            Code = ReportReasonCode.InaccurateInfo,
            Title = "Inaccurate information",
            Description = "Price, area, photos, or room details do not match reality."
        },
        new ReportReasonOption
        {
            Code = ReportReasonCode.SuspectedScam,
            Title = "Suspected scam",
            Description = "Suspicious deposit request, fake listing, or deceptive behavior."
        },
        new ReportReasonOption
        {
            Code = ReportReasonCode.AlreadyRented,
            Title = "Already rented",
            Description = "The landlord confirmed this room is no longer available."
        },
        new ReportReasonOption
        {
            Code = ReportReasonCode.DuplicateListing,
            Title = "Duplicate listing",
            Description = "The same room is posted multiple times."
        },
        new ReportReasonOption
        {
            Code = ReportReasonCode.InappropriateContent,
            Title = "Inappropriate content",
            Description = "Offensive text, spam, or misleading images."
        },
        new ReportReasonOption
        {
            Code = ReportReasonCode.Other,
            Title = "Other reason",
            Description = "Specify details in the note below.",
            RequiresNote = true
        }
    ];
}

public sealed record ReportListingDialogResult(
    ReportReasonCode Reason,
    string? Note);

public sealed record SubmitListingReportResult(
    bool IsSuccess,
    string? ReportId = null,
    string? Status = null,
    DateTimeOffset? CreatedAtUtc = null,
    string? ErrorMessage = null,
    bool IsDuplicate = false,
    bool IsUnauthorized = false,
    bool IsRateLimited = false,
    bool IsValidationError = false)
{
    public static SubmitListingReportResult Success(
        string reportId,
        string status,
        DateTimeOffset createdAtUtc) =>
        new(
            IsSuccess: true,
            ReportId: reportId,
            Status: status,
            CreatedAtUtc: createdAtUtc);

    public static SubmitListingReportResult Failure(
        string errorMessage,
        bool isDuplicate = false,
        bool isUnauthorized = false,
        bool isRateLimited = false,
        bool isValidationError = false) =>
        new(
            IsSuccess: false,
            ErrorMessage: errorMessage,
            IsDuplicate: isDuplicate,
            IsUnauthorized: isUnauthorized,
            IsRateLimited: isRateLimited,
            IsValidationError: isValidationError);
}

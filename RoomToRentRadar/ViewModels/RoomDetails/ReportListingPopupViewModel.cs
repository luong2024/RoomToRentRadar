using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.RoomDetails;

namespace RoomToRentRadar.ViewModels.RoomDetails;

public partial class ReportListingPopupViewModel : ObservableObject
{
    public const int MaxNoteLength = 500;

    public ReportListingPopupViewModel(string roomTitle)
    {
        RoomTitle = string.IsNullOrWhiteSpace(roomTitle) ? "this listing" : roomTitle.Trim();
        Reasons = new ObservableCollection<ReportReasonOption>(ReportReasonOption.CreateDefaultOptions());
    }

    public string RoomTitle { get; }

    public ObservableCollection<ReportReasonOption> Reasons { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoteRequired))]
    [NotifyPropertyChangedFor(nameof(NoteLabelText))]
    public partial ReportReasonOption? SelectedReason { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteCharacterCountText))]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationError { get; set; }

    public bool IsNoteRequired => SelectedReason?.RequiresNote == true;

    public string NoteLabelText => IsNoteRequired
        ? "Additional details (Required)"
        : "Additional details (Optional)";

    public string NoteCharacterCountText => $"{Note?.Length ?? 0} / {MaxNoteLength}";

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    partial void OnNoteChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(ValidationError) &&
            (!IsNoteRequired || !string.IsNullOrWhiteSpace(value)) &&
            (value?.Trim().Length ?? 0) <= MaxNoteLength)
        {
            ValidationError = null;
        }
    }

    [RelayCommand]
    public void SelectReason(ReportReasonOption? option)
    {
        if (option is null)
        {
            return;
        }

        foreach (var item in Reasons)
        {
            item.IsSelected = ReferenceEquals(item, option);
        }

        SelectedReason = option;
        ValidationError = null;
    }

    public ReportListingDialogResult? TryBuildResult()
    {
        if (SelectedReason is null)
        {
            ValidationError = "Please select a reason for reporting this listing.";
            return null;
        }

        var trimmedNote = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        if (SelectedReason.RequiresNote && string.IsNullOrWhiteSpace(trimmedNote))
        {
            ValidationError = "Please provide details when selecting 'Other reason'.";
            return null;
        }

        if (trimmedNote is not null && trimmedNote.Length > MaxNoteLength)
        {
            ValidationError = $"Details cannot exceed {MaxNoteLength} characters.";
            return null;
        }

        ValidationError = null;
        return new ReportListingDialogResult(SelectedReason.Code, trimmedNote);
    }
}

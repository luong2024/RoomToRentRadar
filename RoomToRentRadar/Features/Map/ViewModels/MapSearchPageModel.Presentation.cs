using CommunityToolkit.Mvvm.ComponentModel;

namespace RoomToRentRadar.Features.Map.ViewModels;

public partial class MapSearchPageModel
{
    [ObservableProperty]
    public partial bool HasCompletedSearch { get; set; }

    [ObservableProperty]
    public partial bool SearchFailed { get; set; }

    [ObservableProperty]
    public partial bool IsMapLoading { get; set; }

    [ObservableProperty]
    public partial string MapNotice { get; set; } = "";

    public bool HasMapNotice => !string.IsNullOrWhiteSpace(MapNotice);
    public bool ShowFloatingControls => !IsSavedPanelOpen && !IsNotificationsPanelOpen;
    public bool CanSearchAgain => HasPolygon && !IsDrawing && !IsBusy && !IsLoadingMore;
    public bool ShowEmptyState => HasCompletedSearch && !SearchFailed && !HasResults && !IsBusy && !IsDrawing;
    public bool ShowSearchError => SearchFailed && !IsBusy && !IsDrawing;
    public bool ShowSearchState => (ShowEmptyState || ShowSearchError) && !IsSavedPanelOpen && !IsNotificationsPanelOpen;
    public string SearchStateTitle => ShowSearchError ? "Chưa thể tải kết quả" : "Chưa tìm thấy phòng phù hợp";
    public string SearchStateHint => ShowSearchError ? StatusMessage : "Thử vẽ vùng rộng hơn hoặc tìm lại trong khu vực này.";
    public string SelectedSortLabel => SortOptions[Math.Clamp(SelectedSortIndex, 0, SortOptions.Count - 1)].Label;
    public string MapHeading => IsDrawing ? "Đang chọn khu vực" : HasPolygon ? "Khám phá vùng đã chọn" : "Tìm phòng quanh bạn";
    public string MapSubtitle => IsDrawing ? $"{DraftVertexCount} điểm · chạm bản đồ để thêm" : HasResults ? ResultsText : HasPolygon ? "Tìm phòng trong ranh giới của bạn" : "Vẽ khu vực bạn muốn sống";

    partial void OnHasCompletedSearchChanged(bool value) => NotifyPresentationChanged();
    partial void OnSearchFailedChanged(bool value) => NotifyPresentationChanged();
    partial void OnMapNoticeChanged(string value) => OnPropertyChanged(nameof(HasMapNotice));

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(CanSearchAgain));
        OnPropertyChanged(nameof(ShowFloatingControls));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSearchError));
        OnPropertyChanged(nameof(ShowSearchState));
        OnPropertyChanged(nameof(SearchStateTitle));
        OnPropertyChanged(nameof(SearchStateHint));
        OnPropertyChanged(nameof(SelectedSortLabel));
        OnPropertyChanged(nameof(MapHeading));
        OnPropertyChanged(nameof(MapSubtitle));
    }
}

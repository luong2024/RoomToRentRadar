# .NET MAUI Screen Architecture, AutomationIds & Appium Test Guide

## 1. Screen-to-ViewModel Mapping & Required `AutomationId`s (Sprint 1)

| User Story & Task | XAML View & ViewModel | Required `AutomationId`s for Appium Testing | Key Acceptance Criteria Verified |
| :--- | :--- | :--- | :--- |
| **US01 / T01.2** | `RoomListPage.xaml`<br>`RoomListViewModel.cs` | `ListingCollectionView`, `SortPicker`, `RoomCard_{id}`, `VerifiedBadge_{id}` | Loads <= 3s for 500 items; default sorted by campus distance; only Verified + Available shown. |
| **US02 / T02.2** | `RoomDetailPage.xaml`<br>`RoomDetailViewModel.cs` | `RoomTitleLabel`, `UnavailableBanner`, `SimilarRoomsList`, `LandlordPhoneButton` | Shows similar rooms when unavailable; hides landlord phone until signed in. |
| **US03 / T03.2** | `PhotoGalleryModal.xaml`<br>`Viewer360Page.xaml` | `PhotoCarouselView`, `Open360ViewerButton`, `PlaceholderRoomImage` | Supports >= 8 photos; launches interactive 360° viewer; displays placeholder if empty. |
| **US04 / T04.2** | `FilterBottomSheet.xaml`<br>`SearchFilterViewModel.cs` | `MinRentEntry`, `MaxRentEntry`, `MaxDistanceSlider`, `ResultCountLabel`, `ClearFiltersButton` | Updates within 2s; displays active filter chips & count; Clear restores full list. |
| **US05 / T05.1, T05.3** | `MapSearchPage.xaml`<br>`MapSearchViewModel.cs` | `CampusMap`, `DrawPolygonToggleButton`, `PolygonCanvas`, `SaveAreaButton`, `PinPreviewCard` | Freehand polygon search; pins with preview card; saves drawn area as named search. |
| **US06 / T06.2** | `FavouritesPage.xaml`<br>`FavouritesViewModel.cs` | `FavHeartButton_{id}`, `FavouritesCollectionView`, `UndoRemoveSnackbarButton` | Toggles favourite; syncs across devices; 5-second Snackbar undo on removal. |
| **US07 / T07.2** | `SavedSearchesPage.xaml`<br>`AlertSettingsViewModel.cs` | `AlertToggleSwitch_{id}`, `PauseAlertButton_{id}`, `DeleteAlertButton_{id}` | Enables/pauses/deletes push alerts for saved searches. |
| **US08 / T08.1** | `CreateListingPage.xaml`<br>`CreateListingViewModel.cs` | `ListingTitleEntry`, `RentEntry`, `PickPhotosButton`, `SubmitListingButton`, `PendingReviewBadge` | Enforces required fields + >= 3 JPG/PNG photos (<= 10MB); saves as Pending review. |
| **US09 / T09.2** | `CommuteSectionView.xaml` | `MotorbikeTimeLabel`, `BusTimeLabel`, `WalkTimeLabel`, `FallbackDistanceLabel` | Shows motorbike/bus/walk commute times or straight-line distance fallback. |
| **US10 / T10.1, T10.2** | `CompareRoomsPage.xaml`<br>`CompareRoomsViewModel.cs` | `ComparisonGrid`, `AddRoomToCompareButton`, `ShareComparisonImageButton` | Compares up to 3 rooms side-by-side; exports and shares table as PNG image. |
| **US11 / T11.2** | `HouseRulesSectionView.xaml` | `HouseRulesContainer`, `RulesVersionLabel`, `EditRulesButton` | Placed above landlord contact button; shows versioned quiet hours, guest, cooking, parking rules. |
| **US12 / T12.2** | `ReportListingBottomSheet.xaml` | `ReportListingButton`, `ReportReasonPicker`, `ReportNoteEditor`, `SubmitReportButton` | Submits report reason + note to moderation queue and displays confirmation. |

## 2. Key C# Implementation Snippets

### 5-Second Undo Snackbar (`US06 T06.2`)
```csharp
[RelayCommand]
private async Task RemoveFavouriteAsync(ListingCardDto item)
{
    int index = Favourites.IndexOf(item);
    if (index < 0) return;

    Favourites.RemoveAt(index);
    bool undone = false;

    var snackbar = Snackbar.Make(
        message: $"Đã xóa '{item.Title}' khỏi danh sách yêu thích",
        action: () =>
        {
            undone = true;
            Favourites.Insert(index, item);
        },
        actionButtonText: "Hoàn tác",
        duration: TimeSpan.FromSeconds(5));

    await snackbar.Show();

    if (!undone)
    {
        await _favouritesApiService.RemoveFavouriteAsync(item.Id);
    }
}
```

### Share Comparison Table as Image (`US10 T10.1 & T10.2`)
```csharp
public async Task ShareComparisonTableAsync(VisualElement comparisonGrid)
{
    IScreenshotResult? screenshot = await comparisonGrid.CaptureAsync();
    if (screenshot is null) return;

    string filePath = Path.Combine(FileSystem.CacheDirectory, $"RoomCompare_{DateTime.Now:yyyyMMddHHmmss}.png");
    using (FileStream fileStream = File.OpenWrite(filePath))
    {
        await screenshot.CopyToAsync(fileStream, ScreenshotFormat.Png);
    }

    await Share.Default.RequestAsync(new ShareFileRequest
    {
        Title = "So sánh phòng trọ - RoomToRentRadar",
        File = new ShareFile(filePath)
    });
}
```

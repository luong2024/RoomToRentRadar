# .NET 10 MAUI Project Configuration, Screen Architecture & Appium Test Guide

## 1. Standardized `RoomToRentRadar.Maui.csproj` (.NET 10 LTS + Android API 35 / Min API 24)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-windows10.0.19041.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <RootNamespace>RoomToRentRadar.Maui</RootNamespace>
    <UseMaui>true</UseMaui>
    <UseMauiMaps>true</UseMauiMaps>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- Standardized Android API Levels for Group 3 -->
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">24.0</SupportedOSPlatformVersion>
    <TargetPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">35.0</TargetPlatformVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.*" />
    <PackageReference Include="Microsoft.Maui.Controls.Maps" Version="10.0.*" />
    <PackageReference Include="CommunityToolkit.Maui" Version="9.*" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.*" />
    <PackageReference Include="Refit.HttpClientFactory" Version="8.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RoomToRentRadar.Shared\RoomToRentRadar.Shared.csproj" />
  </ItemGroup>
</Project>
```

## 2. Screen-to-ViewModel Mapping, Frontend Assignee & Required `AutomationId`s (Sprint 1)

| User Story & Task | Frontend Assignee | XAML View & ViewModel | Required `AutomationId`s for Appium Testing | Key Acceptance Criteria Verified |
| :--- | :--- | :--- | :--- | :--- |
| **US01 / T01.2** | Phạm Anh Dũng | `RoomListPage.xaml`<br>`RoomListViewModel.cs` | `ListingCollectionView`, `SortPicker`, `RoomCard_{id}`, `VerifiedBadge_{id}` | Loads <= 3s for 500 items; default sorted by campus distance; only Verified + Available shown. |
| **US02 / T02.2** | Đặng Quang Anh | `RoomDetailPage.xaml`<br>`RoomDetailViewModel.cs` | `RoomTitleLabel`, `UnavailableBanner`, `SimilarRoomsList`, `LandlordPhoneButton` | Shows similar rooms when unavailable; hides landlord phone until signed in. |
| **US03 / T03.2** | Phạm Anh Dũng | `PhotoGalleryModal.xaml`<br>`Viewer360Page.xaml` | `PhotoCarouselView`, `Open360ViewerButton`, `PlaceholderRoomImage` | Supports >= 8 photos; launches interactive 360° viewer; displays placeholder if empty. |
| **US04 / T04.2** | Đặng Quang Anh | `FilterBottomSheet.xaml`<br>`SearchFilterViewModel.cs` | `MinRentEntry`, `MaxRentEntry`, `MaxDistanceSlider`, `ResultCountLabel`, `ClearFiltersButton` | Updates within 2s; displays active filter chips & count; Clear restores full list. |
| **US05 / T05.1, T05.3** | Dũng (`T05.1`) & Quang Anh (`T05.3`) | `MapSearchPage.xaml`<br>`MapSearchViewModel.cs` | `CampusMap`, `DrawPolygonToggleButton`, `PolygonCanvas`, `SaveAreaButton`, `PinPreviewCard` | Freehand polygon search (`T05.1`); pins with preview card & saved search (`T05.3`). |
| **US06 / T06.2** | Phạm Anh Dũng | `FavouritesPage.xaml`<br>`FavouritesViewModel.cs` | `FavHeartButton_{id}`, `FavouritesCollectionView`, `UndoRemoveSnackbarButton` | Toggles favourite; syncs across devices; 5-second Snackbar undo on removal. |
| **US08 / T08.1** | Đặng Quang Anh | `CreateListingPage.xaml`<br>`CreateListingViewModel.cs` | `ListingTitleEntry`, `RentEntry`, `PickPhotosButton`, `SubmitListingButton`, `PendingReviewBadge` | Enforces required fields + >= 3 JPG/PNG photos (<= 10MB); saves as Pending review. |
| **US09 / T09.2** | Phạm Anh Dũng | `CommuteSectionView.xaml` | `MotorbikeTimeLabel`, `BusTimeLabel`, `WalkTimeLabel`, `FallbackDistanceLabel` | Shows motorbike/bus/walk commute times or straight-line distance fallback. |
| **US10 / T10.1, T10.2** | Quang Anh (`T10.1`) & Dũng (`T10.2`) | `CompareRoomsPage.xaml`<br>`CompareRoomsViewModel.cs` | `ComparisonGrid`, `AddRoomToCompareButton`, `ShareComparisonImageButton` | Compares up to 3 rooms side-by-side (`T10.1`); exports and shares table as PNG image (`T10.2`). |
| **US11 / T11.2** | Đặng Quang Anh | `HouseRulesSectionView.xaml` | `HouseRulesContainer`, `RulesVersionLabel`, `EditRulesButton` | Placed above landlord contact button; shows versioned quiet hours, guest, cooking, parking rules. |
| **US12 / T12.2** | Phạm Anh Dũng | `ReportListingBottomSheet.xaml` | `ReportListingButton`, `ReportReasonPicker`, `ReportNoteEditor`, `SubmitReportButton` | Submits report reason + note to moderation queue and displays confirmation. |

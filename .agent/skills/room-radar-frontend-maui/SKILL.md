---
name: room-radar-frontend-maui
description: Build, style, and test the .NET MAUI (.NET 10 LTS, Android API 35 / Min API 24) mobile application (MVVM, XAML, Map Polygon Drawing, 360 Photo Viewer, Offline Caching, and Appium UI Tests) for RoomToRentRadar. Assigned exclusively to Frontend Developers (Pham Anh Dung & Dang Quang Anh). Use when implementing mobile frontend tasks such as listing screen & infinite scroll (T01.2), room detail screen (T02.2), 360 gallery UI (T03.2), filter panel (T04.2), map polygon drawing & pins (T05.1, T05.3), favourites & 5s undo (T06.2), landlord creation form (T08.1), travel time display (T09.2), room comparison & share-as-image (T10.1, T10.2), house rules UI (T11.2), or report UI (T12.2).
allowed-tools: default_api
---
# Room Radar Frontend (.NET 10 LTS .NET MAUI XAML & Appium UI Testing)

Develop, optimize, and test the **.NET MAUI (`net10.0-android`)** mobile application for **RoomToRentRadar** using **XAML + `CommunityToolkit.Mvvm` (Option 1 architecture)**, compiled bindings, interactive map polygons, and **Appium** automated UI tests.

## When to Use

- Implementing any Frontend `.NET MAUI` mobile task assigned to **Phạm Anh Dũng** (`T01.2`, `T03.2`, `T05.1`, `T06.2`, `T09.2`, `T10.2`, `T12.2`) or **Đặng Quang Anh** (`T02.2`, `T04.2`, `T05.3`, `T08.1`, `T10.1`, `T11.2`)
- Creating XAML pages, custom controls, or ViewModels with `CommunityToolkit.Mvvm` on **.NET 10.0 LTS**
- Configuring Android SDK versions (`TargetPlatformVersion = 35.0`, `SupportedOSPlatformVersion = 24.0`, OpenJDK 21)
- Integrating `Microsoft.Maui.Controls.Maps` for freehand polygon drawing (`T05.1`), map pins, and preview cards (`T05.3`)
- Writing Appium UI tests to verify Acceptance Criteria (`AC1`-`AC4`)

## Standardized .NET 10 & Android SDK Baseline (Option 1)

1. **Framework, JDK & Android API Levels**:
   - **IDE & SDK**: Visual Studio 2026 (v18.x) + **.NET 10.0 LTS SDK** + **Microsoft OpenJDK 21**.
   - **TargetFrameworks**: `net10.0-android;net10.0-windows10.0.19041.0` (keep build fast on dev machines).
   - **Target Android SDK (`targetSdkVersion`)**: **Android 15.0 (`API Level 35` -> `35.0`)** (or `36.0` if Android 16 SDK is installed). Recommended Android Emulator: **Pixel 7 - Android 15.0 (API 35) Google APIs x86_64**.
   - **Minimum Android SDK (`minSdkVersion`)**: **Android 7.0 (`API Level 24` -> `24.0`)**.
   - **Core NuGet Packages**: `CommunityToolkit.Mvvm` (`8.3.x+`), `CommunityToolkit.Maui`, `Microsoft.Maui.Controls.Maps`, `Refit.HttpClientFactory`.
2. **MVVM & Compiled Bindings**:
   - Use `[ObservableProperty]` and `[RelayCommand]` in all ViewModels.
   - Always declare **Compiled Bindings** (`x:DataType="vm:RoomListViewModel"`) on every XAML Page and `DataTemplate` so scrolling 500 listings finishes within 3 seconds (`US01 AC3`).
   - Reference `RoomToRentRadar.Shared` for all DTOs and connect to local Backend API via `http://10.0.2.2:5000` on Android Emulator.
3. **AutomationId Mandate for Appium**:
   - Every interactive control (Button, Entry, CollectionView, Map, CheckBox, Chip) MUST declare an explicit `AutomationId` matching `references/maui-screens-and-appium.md`.

## Steps

### Step 1: Implement Priority Batch 1 Core Discovery & Search Screens (`US01`, `US02`, `US04`)

1. **Listing Screen & Infinite Scroll (`US01 T01.2` - Phạm Anh Dũng)**:
   - Use `<CollectionView ItemsSource="{Binding Listings}" RemainingItemsThreshold="5" RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">`.
   - Each room card displays: Title, Monthly Rent (VND), Area ($m^2$), Distance to selected ICTU campus, and the green **Verified Badge** (`US01 AC2`).
2. **Room Detail Screen (`US02 T02.2` - Đặng Quang Anh)**:
   - Render description, rent, deposit, area, room type, amenities chips, address, house rules (`US11`), and last-updated date (`US02 AC1, AC2`).
   - If the API returns `IsAvailable == false`, display the unavailable banner and bind the horizontal `SimilarRooms` list (`US02 AC3`).
   - If the user is not signed in, show a `"Đăng nhập để xem SĐT chủ trọ"` button on the contact bar (`US02 AC4`).
3. **Filter Panel (`US04 T04.2` - Đặng Quang Anh)**:
   - Provide range sliders/inputs for Price, Area, Room Type picker, and Max Distance to campus (`US04 AC1`).
   - Keep active filter chips and total result count (`"Tìm thấy 42 phòng"`) pinned at the top (`US04 AC3`), alongside a `"Xóa bộ lọc" (Clear filters)` button (`US04 AC4`).

### Step 2: Implement Media Gallery, 360° Viewer & Map Polygon Search (`US03`, `US05`)

1. **Photo Gallery & 360° Viewer (`US03 T03.2` - Phạm Anh Dũng)**:
   - Support progressive image loading and disk caching (`UriImageSource` with `CachingEnabled="True"`) and show `placeholder_room.png` when empty (`US03 AC3, AC4`).
   - For photos marked `Is360 == true`, open a full-screen `WebView` loading local `Resources/Raw/viewer360.html` (`Pannellum` equirectangular viewer) for interactive 360° drag/pan (`US03 AC2`).
2. **Map Polygon Drawing & Pins (`US05 T05.1` Phạm Anh Dũng & `T05.3` Đặng Quang Anh)**:
   - Overlay a touch-capture `GraphicsView` or Map `Polygon` element when the user toggles `"Vẽ khu vực" (Draw Area)`.
   - Collect touch coordinates as the user drags, render the live polygon stroke, and pass the closed coordinate array to `POST /api/v1/listings/spatial-search` (`US05 AC1`).
   - Render matching rooms as custom Map Pins; tapping a pin opens a floating bottom preview card and a `"Lưu khu vực tìm kiếm"` button (`US05 AC3, AC4`).

### Step 3: Implement Favourites Undo, Landlord Form, Commute & Comparison (`US06`-`US12`)

1. **Favourites with 5-Second Undo (`US06 T06.2` - Phạm Anh Dũng)**:
   - Display a `CommunityToolkit.Maui` `Snackbar` with an `"Hoàn tác" (Undo)` action for **5 seconds** before committing removal (`US06 AC4`).
2. **Landlord Listing Creation Form (`US08 T08.1` - Đặng Quang Anh)**:
   - Validate required fields and enforce selecting **at least 3 photos** (`JPG/PNG <= 10MB` each) before enabling Submit (`US08 AC1, AC4`). Show `"Đang chờ duyệt (Pending Review)"` badge (`US08 AC2`).
3. **Travel Time & Fallback Distance (`US09 T09.2` - Phạm Anh Dũng)**:
   - Display Motorbike, Bus, and Walking times; if `IsFallbackStraightLine == true`, label it `"Khoảng cách đường chim bay: X km"` (`US09 AC1-AC3`).
4. **Side-by-Side Comparison & Share-as-Image (`US10 T10.1` Đặng Quang Anh & `T10.2` Phạm Anh Dũng)**:
   - Compare up to **3 rooms** side-by-side (`T10.1`) and use `VisualElement.CaptureAsync()` + `Share.Default.RequestAsync` to share as PNG (`T10.2`) (`US10 AC1-AC3`).
5. **House Rules & Listing Reports (`US11 T11.2` Đặng Quang Anh & `US12 T12.2` Phạm Anh Dũng)**:
   - Render versioned House Rules directly above the Contact Landlord button (`US11 AC1, AC2`).
   - Provide a `"Báo cáo tin đăng"` bottom sheet with reason picker + note (`US12 AC1-AC3`).

## Gotchas

- **Map Gesture Conflict on Android (`US05`)**: Set `CampusMap.IsScrollEnabled = false` while `"Draw Area Mode"` is active so finger drag draws the polygon instead of panning the map.
- **Android Cleartext HTTP Traffic**: Add `android:usesCleartextTraffic="true"` in `Platforms/Android/AndroidManifest.xml` during local development so Android API 35 allows calling `http://10.0.2.2:5000`.

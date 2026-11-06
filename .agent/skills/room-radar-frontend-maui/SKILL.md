---
name: room-radar-frontend-maui
description: Build, style, and test the .NET MAUI mobile application (MVVM, XAML, Map Polygon Drawing, 360 Photo Viewer, Offline Caching, Push Notifications, and Appium UI Tests) for RoomToRentRadar. Use when implementing mobile frontend tasks such as listing screen & infinite scroll (T01.2), room detail screen (T02.2), 360 gallery UI (T03.2), filter panel (T04.2), map polygon drawing & pins (T05.1, T05.3), favourites & 5s undo (T06.2), push alerts UI (T07.2), landlord form (T08.1), travel time display (T09.2), room comparison & share-as-image (T10.1, T10.2), house rules UI (T11.2), or report UI (T12.2).
allowed-tools: default_api
---
# Room Radar Frontend (.NET MAUI & Appium UI Testing)

Develop, optimize, and test the **.NET MAUI** mobile application for **RoomToRentRadar** using `CommunityToolkit.Mvvm`, compiled XAML bindings, interactive map polygons, and **Appium** automated UI tests.

## When to Use

- Implementing any .NET MAUI mobile task in Sprint 1 (`T01.2`, `T02.2`, `T03.2`, `T04.2`, `T05.1`, `T05.3`, `T06.2`, `T07.2`, `T08.1`, `T09.2`, `T10.1`, `T10.2`, `T11.2`, `T12.2`) or Sprints 2-4
- Creating XAML pages, custom controls, or ViewModels with `CommunityToolkit.Mvvm`
- Integrating Map SDKs for freehand polygon drawing, map pins, and preview cards (`US05`)
- Building the 360-degree equirectangular photo viewer (`US03`) or exporting the room comparison table as an image (`US10`)
- Writing Appium UI tests to verify Acceptance Criteria (`AC1`-`AC4`)

## Architectural Standards

1. **MVVM & Dependency Injection**:
   - Use `CommunityToolkit.Mvvm` attributes (`[ObservableProperty]`, `[RelayCommand]`) in all ViewModels.
   - Always use **Compiled Bindings** (`x:DataType="vm:RoomListViewModel"`) on every XAML Page and `DataTemplate` so scrolling 500 listings stays smooth (`US01 AC3`).
   - Register all Views, ViewModels, and Refit/HttpClient API services in `MauiProgram.cs`.
2. **AutomationId Mandate for Appium**:
   - Every interactive control (Button, Entry, CollectionView, Map, CheckBox, Chip) MUST declare an explicit `AutomationId` matching the conventions in `references/maui-screens-and-appium.md` so Appium UI tests can locate elements reliably across Android and iOS.

## Steps

### Step 1: Implement Core Discovery & Search Screens (`US01`, `US02`, `US04`)

1. **Listing Screen & Infinite Scroll (`US01 T01.2` - Be Van Duy)**:
   - Use `<CollectionView ItemsSource="{Binding Listings}" RemainingItemsThreshold="5" RemainingItemsThresholdReachedCommand="{Binding LoadMoreCommand}">`.
   - Each room card displays: Title, Monthly Rent (VND), Area ($m^2$), Distance to selected ICTU campus, and the green **Verified Badge** (`US01 AC2`).
2. **Room Detail Screen (`US02 T02.2` - Dang Quang Anh)**:
   - Render description, rent, deposit, area, room type, amenities chips, address, house rules (`US11`), and last-updated date (`US02 AC1, AC2`).
   - If the API returns `IsAvailable == false`, display the unavailable banner and bind the horizontal `SimilarRooms` list (`US02 AC3`).
   - If the user is not signed in, show a `"Đăng nhập để xem SĐT chủ trọ"` button on the contact bar (`US02 AC4`).
3. **Filter Panel (`US04 T04.2` - Dang Quang Anh)**:
   - Provide range sliders/inputs for Price, Area, Room Type picker, and Max Distance to campus (`US04 AC1`).
   - Keep active filter chips and total result count (`"Tìm thấy 42 phòng"`) pinned at the top (`US04 AC3`), alongside a `"Xóa bộ lọc" (Clear filters)` button that resets to the full list (`US04 AC4`).

### Step 2: Implement Media Gallery, 360° Viewer & Map Polygon Search (`US03`, `US05`)

1. **Photo Gallery & 360° Viewer (`US03 T03.2` - Be Van Duy)**:
   - Support progressive image loading and disk caching (`UriImageSource` with `CachingEnabled="True"` or `FFImageLoading.Maui`) and show `placeholder_room.png` when empty (`US03 AC3, AC4`).
   - For photos marked `Is360 == true`, open a full-screen `WebView` or SkiaSharp spherical projection viewer (`Pannellum` bundled locally in `Resources/Raw/viewer360.html`) for smooth touch drag/gyroscope pan (`US03 AC2`).
2. **Map Polygon Drawing & Pins (`US05 T05.1` Be Van Duy & `T05.3` Dang Quang Anh)**:
   - Overlay a transparent touch-capture layer (`GraphicsView` or Map `Polygon` element) when the user toggles `"Vẽ khu vực" (Draw Area)`.
   - Collect touch coordinates as the user drags, render the live polygon stroke, and pass the closed coordinate array to `POST /api/v1/listings/spatial-search` (`US05 AC1`).
   - Render matching rooms as custom Map Pins; tapping a pin opens a floating bottom preview card and a `"Lưu khu vực tìm kiếm"` button (`US05 AC3, AC4`).

### Step 3: Implement Favourites Undo, Landlord Form, Commute & Comparison (`US06`-`US12`)

1. **Favourites with 5-Second Undo (`US06 T06.2` - Be Van Duy)**:
   - When removing a room from Favourites, hold the deletion in memory and display a `CommunityToolkit.Maui` `Snackbar` with an `"Hoàn tác" (Undo)` action for **5 seconds** before committing or rolling back (`US06 AC4`).
2. **Landlord Listing Creation Form (`US08 T08.1` - Nguyen Minh Hieu)**:
   - Validate required fields (Title, Address, Rent, Deposit, Area, Room Type) and enforce selecting **at least 3 photos** (`JPG/PNG <= 10MB` each) before enabling Submit (`US08 AC1, AC4`).
   - Show a clear `"Đang chờ duyệt (Pending Review)"` status badge after submission (`US08 AC2`).
3. **Travel Time & Fallback Distance (`US09 T09.2` - Nguyen Minh Hieu)**:
   - Display Motorbike, Bus, and Walking times from the room to the user's selected ICTU campus; if `IsFallbackStraightLine == true`, label it `"Khoảng cách đường chim bay: X km"` (`US09 AC1-AC3`).
4. **Side-by-Side Comparison & Share-as-Image (`US10 T10.1` Be Van Duy & `T10.2` Pham Anh Dung)**:
   - Allow pinning up to **3 rooms** into a 3-column comparison grid (Rent, Deposit, Area, Distance, Amenities, Security Score) (`US10 AC1, AC2`).
   - Use `.NET MAUI` `IScreenshot` / `VisualElement.CaptureAsync()` on the comparison table container and invoke `Share.Default.RequestAsync(new ShareFileRequest(...))` to share as PNG (`US10 AC3`).
5. **House Rules & Listing Reports (`US11 T11.2` Hieu & `US12 T12.2` Dung)**:
   - Render House Rules (`QuietHours`, `GuestPolicy`, `CookingRules`, `ParkingRules`, `Version`) directly above the Contact Landlord button so students read them before calling (`US11 AC1, AC2`).
   - Provide a `"Báo cáo tin đăng"` bottom sheet with predefined reasons + optional note and display a confirmation toast upon submission (`US12 AC1-AC3`).

### Step 4: Verify with Appium UI Tests

Refer to `references/maui-screens-and-appium.md` to write and execute Appium test cases verifying every User Story's Acceptance Criteria before marking the task `Done`.

## Gotchas

- **Map Gesture Conflict on Android/iOS (`US05`)**: Standard Map controls consume pan gestures. Disable map pan/scroll (`IsScrollEnabled = false`) while `"Draw Area Mode"` is active so the user's finger draws the polygon smoothly instead of panning the map.
- **Memory Leaks with High-Res Photos (`US03`, `US08`)**: Downsample camera/gallery picks to max `1920px` width on the client before uploading or rendering in `CollectionView` to prevent `OutOfMemoryException` on mid-range student Android phones.

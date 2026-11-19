---
name: scrum-task-routing-and-git-branching
description: Enforces Group 5's Scrum Sprint 1 task-to-file mapping (T01.1 to T13.2), Clean 3-Layer SOLID Architecture, Git branch naming conventions (feature/group-5/Txx.x-...), main/group-5 branch protection, and conflict-free FeatureModule DI/Route registration in .NET MAUI.
---

# Scrum Task Routing & Git Branching Skill (Group 5)

## Mục đích
Đảm bảo cả 4 thành viên Developer (Quang, Hiếu, Thùy, Nam) dưới sự điều phối của Scrum Master Đức (`@Duc-Nguyen-Github`) và Tech Lead Quang (`@quangtldc0301`) khi dùng AI Agent để code song song đều:
1. Đứng đúng nhánh Git của Task (`feature/group-5/Txx.x-...`), **TUYỆT ĐỐI KHÔNG commit/push trực tiếp lên `main` hay `group-5`**.
2. Chỉ tạo/sửa đúng các file thuộc Task ID (`T01.1` $\rightarrow$ `T13.2`) theo kiến trúc 3 tầng chuẩn SOLID (`RoomToRentRadar.Core`, `RoomToRentRadar.Infrastructure`, `RoomToRentRadar`).
3. Tự đăng ký DI và Shell Route trong file `Features/<Feature>/<Feature>FeatureModule.cs` của tính năng mình — **không bao giờ phải sửa chung `MauiProgram.cs`**.

---

## Quy Tắc Git Bắt Buộc Cho Mọi AI Agent

1. **CẤM TUYỆT ĐỐI nhánh `main` và cấm commit thẳng lên `group-5`:**
   - Không bao giờ chạy `git push origin main` hoặc commit trực tiếp trên `group-5`.
   - Hệ thống `.githooks/pre-commit` và `.githooks/pre-push` sẽ tự động chặn mọi thao tác vi phạm. Cấm dùng `--no-verify`.
   - Chỉ Scrum Master (`@Duc-Nguyen-Github`) và Tech Lead (`@quangtldc0301`) được quyền duyệt và bấm Merge Pull Request vào nhánh `group-5`.
2. **Quy chuẩn đặt tên nhánh & commit theo Task ID của Group 5:**
   ```powershell
   git checkout group-5
   git pull origin group-5
   git checkout -b feature/group-5/<TaskID>-<short-name>
   # Ví dụ: git checkout -b feature/group-5/T01.1-auth-api
   # Ví dụ: git checkout -b feature/group-5/T02.2-listing-ui
   ```
   Khi commit và push:
   ```powershell
   git commit -m "feat(group-5/<TaskID>): <mo-ta-ngan-gon> (<US>)"
   git push -u origin feature/group-5/<TaskID>-<short-name>
   ```

---

## Bảng Ánh Xạ 26 Tasks Sprint 1 (`T01.1` $\rightarrow$ `T13.2`) & Phạm Vi File (Kiến Trúc 3 Tầng SOLID)

> **Lưu ý về đường dẫn:**
> - Các Task **Backend (Repository / Service)** đặt file trong project `RoomToRentRadar.Infrastructure/` (hoặc `RoomToRentRadar/` nếu là Service phụ thuộc trực tiếp MAUI).
> - Các Task **Frontend (UI Page / PageModel)** đặt file trong `RoomToRentRadar/Features/<Feature>/` (hoặc `RoomToRentRadar/Pages/` & `PageModels/` — cả hai đều được hỗ trợ qua `GlobalUsings.cs`, ưu tiên đặt trong `RoomToRentRadar/Features/<Feature>/` cùng với `<Feature>FeatureModule.cs`).

| Task ID | US | Tên Task | Phụ trách | File được phép tạo / sửa (Safe Zone) | Hợp đồng kết nối (Trong `RoomToRentRadar.Core`) |
| :---: | :---: | :--- | :---: | :--- | :--- |
| **T01.1** | US01 | Build Auth & JWT/Session Service | Quang | `RoomToRentRadar.Infrastructure/Services/AuthService.cs`, `RoomToRentRadar.Infrastructure/Repositories/UserRepository.cs`, `RoomToRentRadar.Api/Endpoints/AuthEndpoints.cs`, `RoomToRentRadar/Features/Auth/AuthFeatureModule.cs` | Implement `IAuthService`, dùng `UserAccount`, `RentalDatabaseService` |
| **T01.2** | US01 | Build Login & Register UI | Nam | `RoomToRentRadar/Features/Auth/LoginPage.xaml(.cs)`, `RegisterPage.xaml(.cs)`, `AuthPageModel.cs`, `AuthFeatureModule.cs` | Inject `IAuthService`; Route: `Constants.Routes.Login`, `Constants.Routes.Register` |
| **T02.1** | US02 | Build Listing API (pagination & sorting) | Thùy | `RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Listing.cs`, `RoomToRentRadar.Api/Endpoints/RoomListingEndpoints.cs`, `RoomToRentRadar/Features/Rooms/RoomsFeatureModule.cs` | Override `ListVerifiedRoomsAsync` trong partial class `RoomListingRepository` |
| **T02.2** | US02 | Build Listing Screen & Room Cards | Hiếu | `RoomToRentRadar/Features/Rooms/RoomListPage.xaml(.cs)`, `RoomListPageModel.cs`, `RoomsFeatureModule.cs`, `RoomToRentRadar/Pages/Controls/RoomCardView.xaml(.cs)` | Inject `IRoomListingRepository`; điều hướng `${Constants.Routes.RoomDetail}?roomId={room.Id}` |
| **T03.1** | US03 | Build Room Detail Data Service | Thùy | `RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Detail.cs`, `RoomToRentRadar.Api/Endpoints/RoomDetailEndpoints.cs` | Override `GetByIdAsync(int id)` & `GetSimilarRoomsAsync` trong file partial riêng |
| **T03.2** | US03 | Build Room Detail Screen & Guest Masking | Nam | `RoomToRentRadar/Features/Rooms/RoomDetailPage.xaml(.cs)`, `RoomDetailPageModel.cs`, `RoomsFeatureModule.cs` | Nhận `[QueryProperty(nameof(RoomId), "roomId")]` (Mặc định `RoomId = 1` nếu test độc lập) |
| **T04.1** | US04 | Build Photo Upload & Gallery Service | Quang | `RoomToRentRadar.Infrastructure/Services/PhotoGalleryService.cs`, `IPhotoGalleryService.cs`, `RoomToRentRadar.Api/Endpoints/PhotoGalleryEndpoints.cs` | Đọc/ghi `PhotosJson` và `Panorama360Url` trong `RoomListing` |
| **T04.2** | US04 | Build Photo Gallery UI & 360° Viewer | Hiếu | `RoomToRentRadar/Features/Rooms/PhotoGallery360Page.xaml(.cs)`, `PhotoGallery360PageModel.cs`, `RoomsFeatureModule.cs` | Route: `Constants.Routes.RoomGallery360` |
| **T05.1** | US05 | Build Multi-Filter Search Service | Thùy | `RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Search.cs`, `RoomToRentRadar.Api/Endpoints/RoomSearchEndpoints.cs` | Override `SearchAsync(RoomFilterCriteria criteria)` trong file partial riêng |
| **T05.2** | US05 | Build Filter Panel & Active Filter Count | Nam | `RoomToRentRadar/Features/Rooms/RoomFilterModalPage.xaml(.cs)`, `RoomFilterPageModel.cs`, `RoomsFeatureModule.cs` | Route: `Constants.Routes.RoomFilter` |
| **T06.1** | US06 | Integrate Map & ICTU Coordinates | Hiếu | `RoomToRentRadar/Features/Map/CampusMapService.cs`, `RoomMapPage.xaml(.cs)`, `MapFeatureModule.cs` | Tâm bản đồ: `Constants.IctuLatitude`, `Constants.IctuLongitude` |
| **T06.2** | US06 | Build Map Pins & Preview Cards | Nam | `RoomToRentRadar/Features/Map/RoomMapPageModel.cs`, `MapFeatureModule.cs`, `RoomToRentRadar/Pages/Controls/MapPinPreviewCard.xaml(.cs)` | Bấm Preview Card gọi `Constants.Routes.RoomDetail` |
| **T07.1** | US07 | Build Local SQLite Favorites & Sync | Quang | `RoomToRentRadar.Infrastructure/Repositories/FavoriteRepository.cs`, `RoomToRentRadar.Api/Endpoints/FavoriteEndpoints.cs`, `RoomToRentRadar/Features/Favorites/FavoritesFeatureModule.cs` | Implement `IFavoriteRepository`, dùng `FavoriteRoom` |
| **T07.2** | US07 | Build Favorite Button, Screen & Undo | Hiếu | `RoomToRentRadar/Features/Favorites/FavoritesPage.xaml(.cs)`, `FavoritesPageModel.cs`, `FavoritesFeatureModule.cs` | Inject `IFavoriteRepository`; Route: `Constants.Routes.Favorites` |
| **T08.1** | US08 | Build Saved-Search & Alert Rules | Thùy | `RoomToRentRadar.Infrastructure/Repositories/SavedSearchAlertRepository.cs`, `RoomToRentRadar.Api/Endpoints/AlertEndpoints.cs`, `RoomToRentRadar/Features/Alerts/AlertsFeatureModule.cs` | Implement `ISavedSearchAlertRepository`, dùng `SavedSearchAlert` |
| **T08.2** | US08 | Build Alert Settings UI | Nam | `RoomToRentRadar/Features/Alerts/SearchAlertsPage.xaml(.cs)`, `SearchAlertsPageModel.cs`, `AlertsFeatureModule.cs` | Inject `ISavedSearchAlertRepository`; Route: `Constants.Routes.SearchAlerts` |
| **T09.1** | US09 | Build Listing Creation Form & Validation | Quang | `RoomToRentRadar/Features/Landlord/PublishListingPage.xaml(.cs)`, `PublishListingPageModel.cs`, `LandlordFeatureModule.cs` | Inject `IRoomListingRepository`; Route: `Constants.Routes.PublishListing` |
| **T09.2** | US09 | Build Listing Create/Edit Service | Hiếu | `RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Publish.cs`, `RoomToRentRadar.Api/Endpoints/RoomPublishEndpoints.cs` | Override `SaveListingAsync` & `UpdateStatusAsync` trong file partial riêng |
| **T10.1** | US10 | Build Routing & Travel-Time Service | Thùy | `RoomToRentRadar.Core/Services/TravelTimeService.cs` | Tính khoảng cách Haversine & thời gian đi bộ/xe buýt/xe máy tới ICTU |
| **T10.2** | US10 | Display Travel Time & Fallback UI | Quang | `RoomToRentRadar/Pages/Controls/TravelTimeCardView.xaml(.cs)` | Hiển thị `WalkMinutes`, `BusMinutes`, `MotorbikeMinutes` |
| **T11.1** | US11 | Build Room Comparison Model & Screen | Hiếu | `RoomToRentRadar/Features/Comparison/RoomComparePage.xaml(.cs)`, `RoomComparePageModel.cs`, `ComparisonFeatureModule.cs` | Inject `ComparisonBasketService`; Route: `Constants.Routes.RoomCompare` |
| **T11.2** | US11 | Test Comparison & Share-as-Image | Thùy | `RoomToRentRadarUnitTests/RoomComparisonTests.cs`, `RoomToRentRadar.Core/Services/ComparisonShareService.cs` | Kiểm thử so sánh 3 phòng |
| **T12.1** | US12 | Build House-Rules Versioning Service | Nam | `RoomToRentRadar.Infrastructure/Repositories/HouseRuleRepository.cs`, `RoomToRentRadar.Api/Endpoints/HouseRuleEndpoints.cs`, `RoomToRentRadar/Features/Rooms/RoomsFeatureModule.cs` | Implement `IHouseRuleRepository`, dùng `HouseRuleVersion` |
| **T12.2** | US12 | Build House-Rules UI with Versioning | Quang | `RoomToRentRadar/Features/Rooms/HouseRulesView.xaml(.cs)` | Hiển thị `QuietHours`, `GuestPolicy`, `CookingRules`, `ParkingRules` |
| **T13.1** | US13 | Build Report API & Moderation Queue | Hiếu | `RoomToRentRadar.Infrastructure/Repositories/ListingReportRepository.cs`, `RoomToRentRadar.Api/Endpoints/ReportEndpoints.cs`, `RoomToRentRadar/Features/Reports/ReportsFeatureModule.cs` | Implement `IListingReportRepository`, dùng `ListingReport` |
| **T13.2** | US13 | Build Report Dialog UI & Confirmation | Thùy | `RoomToRentRadar/Features/Reports/ReportListingModalPage.xaml(.cs)`, `ReportListingPageModel.cs`, `ReportsFeatureModule.cs` | Inject `IListingReportRepository`; Route: `Constants.Routes.ReportListing` |

---

## Cơ Chế Đăng Ký DI, Route & Web API Endpoint Không Đụng Độ (`FeatureModule` + `Endpoints` + `TryAddSingleton`)

1. **KHÔNG BAO GIỜ sửa `MauiProgram.cs`, `AppShell.xaml.cs` hoặc `RoomToRentRadar.Api/Program.cs`:**
   - Trong MAUI App: `MauiProgram.cs` đã gọi sẵn toàn bộ `AddAuthFeature()`, `AddRoomsFeature()`, `AddMapFeature()`, `AddFavoritesFeature()`, `AddAlertsFeature()`, `AddLandlordFeature()`, `AddComparisonFeature()`, `AddReportsFeature()`. Khi làm Task của tính năng nào, chỉ mở đúng file `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs` của tính năng đó để đăng ký:
     ```csharp
     services.AddTransientWithShellRoute<FavoritesPage, FavoritesPageModel>(Constants.Routes.Favorites);
     ```
   - Trong Web API (`RoomToRentRadar.Api`): `Program.cs` đã gọi sẵn toàn bộ `app.Map...Endpoints()`. Khi làm Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`), chỉ mở đúng file `RoomToRentRadar.Api/Endpoints/<Task>Endpoints.cs` của Task mình để thêm route HTTP.
2. **Cách viết `PageModel` của Frontend (Chuẩn DIP — Constructor Injection):**
   - Inject trực tiếp Interface vào constructor của `PageModel` (ví dụ: `public RoomListPageModel(IRoomListingRepository roomRepo)`).
   - **Không cần kiểm tra `null` hay tự viết mock data trong `PageModel`!**
   - Lý do: `InfrastructureModule` đã đăng ký sẵn các class Fake in-memory (`InMemoryRoomListingRepository`, `InMemoryAuthService`...) bằng `TryAddSingleton` với đúng 7 phòng trọ mẫu quanh ICTU.
   - Ngay khi nhánh Backend được Merge và đăng ký `services.AddSingleton<IRoomListingRepository, RoomListingRepository>()` trong `<Feature>FeatureModule.cs`, DI container sẽ tự động dùng bản SQLite thật mà Frontend không phải sửa bất kỳ dòng code nào!

---

## Giải Pháp Kỹ Thuật Bắt Buộc Cho Bản Đồ (`US06`) & Ảnh 360° (`US04`) Đa Nền Tảng

1. **Task `T06.1` & `T06.2` (`US06`):** Sử dụng `WebView` của .NET MAUI nhúng **Leaflet.js + OpenStreetMap HTML/JS** (tâm bản đồ mặc định tại cổng trường ICTU `Constants.IctuLatitude = 21.5850`, `Constants.IctuLongitude = 105.8064`). Khi bấm vào Pin thì gửi sự kiện về C# (`WebView.Navigating` với scheme `rtr://pin?roomId=X`) để bật `MapPinPreviewCard`.
2. **Task `T04.2` (`US04`):** Sử dụng `CarouselView` cho bộ sưu tập ảnh 2D và `WebView` nhúng **Pannellum 360° HTML5 Viewer** (nạp `RoomListing.Panorama360Url`).

---

## Hợp Đồng Chữ Ký Hàm (Method Signature Contracts — Trong `RoomToRentRadar.Core/Abstractions/`)

1. **`IAuthService` (`RoomToRentRadar.Abstractions.Auth`):**
   - `UserAccount? CurrentUser { get; }`
   - `bool IsSignedIn { get; }`
   - `Task<UserAccount?> LoginAsync(string email, string password)`
   - `Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role)`
   - `void Logout()`
2. **`IRoomListingRepository` (`RoomToRentRadar.Abstractions.Rooms`):**
   - `Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")`
   - `Task<RoomListing?> GetByIdAsync(int roomId)`
   - `Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)`
   - `Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria)`
   - `Task<int> SaveListingAsync(RoomListing listing)`
   - `Task<bool> UpdateStatusAsync(int roomId, string status, bool isVerified)`
3. **`IFavoriteRepository` (`RoomToRentRadar.Abstractions.Favorites`):**
   - `Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId)`
   - `Task<bool> ToggleFavoriteAsync(int userId, int roomId)`
   - `Task<bool> AddFavoriteAsync(int userId, int roomId)`
   - `Task<bool> RemoveFavoriteAsync(int userId, int roomId)`
4. **`ISavedSearchAlertRepository` (`RoomToRentRadar.Abstractions.Alerts`):**
   - `Task<List<SavedSearchAlert>> ListByUserAsync(int userId)`
   - `Task<int> SaveAlertAsync(SavedSearchAlert alert)`
   - `Task<bool> ToggleAlertAsync(int alertId, bool isEnabled)`
   - `Task<bool> DeleteAlertAsync(int alertId)`
5. **`IHouseRuleRepository` (`RoomToRentRadar.Abstractions.HouseRules`):**
   - `Task<List<HouseRuleVersion>> GetHistoryByRoomAsync(int roomId)`
   - `Task<int> SaveNewVersionAsync(HouseRuleVersion ruleVersion)`
6. **`IListingReportRepository` (`RoomToRentRadar.Abstractions.Reports`):**
   - `Task<int> SubmitReportAsync(ListingReport report)`
   - `Task<List<ListingReport>> ListPendingReportsAsync()`

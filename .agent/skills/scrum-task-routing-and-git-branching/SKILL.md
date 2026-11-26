---
name: scrum-task-routing-and-git-branching
description: Enforces Group 5's Scrum Sprint 1 task-to-file mapping (T01.1 to T13.2), the 4-project SOLID architecture with an MVC Web API backend in Docker and a MAUI frontend calling it over HTTP, Git branch naming conventions (feature/group-5/Txx.x-...), main/group-5 branch protection, and conflict-free FeatureModule DI/Route registration.
---

# Scrum Task Routing & Git Branching Skill (Group 5)

## Mục đích
Đảm bảo cả 4 thành viên Developer (Quang, Hiếu, Thùy, Nam) dưới sự điều phối của Scrum Master kiêm Tech Lead Quang (`@quangtldc0301`) khi dùng AI Agent để code song song đều:
1. Đứng đúng nhánh Git của Task (`feature/group-5/Txx.x-...`), **TUYỆT ĐỐI KHÔNG commit/push trực tiếp lên `main` hay `group-5`**.
2. Chỉ tạo/sửa đúng các file thuộc Task ID (`T01.1` $\rightarrow$ `T13.2`) theo kiến trúc 4 project SOLID: **Backend** (`RoomToRentRadar.Core`, `RoomToRentRadar.Infrastructure`, `RoomToRentRadar.Api` theo MVC, chạy trong Docker) và **Frontend** (`RoomToRentRadar` MAUI, gọi backend qua HTTP).
3. Tự đăng ký DI và Shell Route trong file `Features/<Feature>/<Feature>FeatureModule.cs` của tính năng mình — **không bao giờ phải sửa chung `MauiProgram.cs` hay `backend/RoomToRentRadar.Api/Program.cs`**.

User Story và Acceptance Criteria của từng Task: xem `docs/backlog.md`.

---

## Quy Tắc Git Bắt Buộc Cho Mọi AI Agent

1. **CẤM TUYỆT ĐỐI nhánh `main` và cấm commit thẳng lên `group-5`:**
   - Không bao giờ chạy `git push origin main` hoặc commit trực tiếp trên `group-5`.
   - Hệ thống `.githooks/pre-commit` và `.githooks/pre-push` sẽ tự động chặn mọi thao tác vi phạm. Cấm dùng `--no-verify`.
   - Chỉ Scrum Master kiêm Tech Lead (`@quangtldc0301`) được quyền duyệt và bấm Merge Pull Request vào nhánh `group-5`.
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
3. **Cập nhật nhánh Task theo `group-5`:** `git checkout group-5; git pull origin group-5; git checkout <nhánh Task>; git merge group-5`. Không merge vào `group-5` trên máy.

---

## Bảng Ánh Xạ 26 Tasks Sprint 1 (`T01.1` $\rightarrow$ `T13.2`) & Phạm Vi File

> **Lưu ý về đường dẫn:**
> - **Task Backend:** logic SQLite trong `backend/RoomToRentRadar.Infrastructure/`; HTTP route trong **Controller MVC** `backend/RoomToRentRadar.Api/Controllers/<Feature>Controller.cs` với DTO trong `backend/RoomToRentRadar.Api/Models/<Feature>Models.cs` (mẫu chuẩn: `AuthController.cs` + `AuthModels.cs`). Các file `backend/RoomToRentRadar.Api/Endpoints/*Endpoints.cs` là Minimal API **cũ**: không thêm route mới; khi làm Task thì chuyển route sang Controller và xóa file Endpoint tương ứng.
> - **Task Frontend:** `frontend/RoomToRentRadar/Features/<Feature>/` (Page, PageModel, `<Feature>FeatureModule.cs`) **và client HTTP** cho interface mà màn hình dùng, đặt trong `frontend/RoomToRentRadar/Services/Api/`. Client cho `IRoomListingRepository` là `partial class ApiRoomListingRepository` chia file theo Task (giống `RoomListingRepository` ở backend) để các bạn không sửa chung 1 file.

| Task ID | US | Tên Task | Phụ trách | File được phép tạo / sửa (Safe Zone) | Hợp đồng kết nối (Trong `RoomToRentRadar.Core`) |
| :---: | :---: | :--- | :---: | :--- | :--- |
| **T01.1** | US01 | Build ASP.NET Core Identity & JWT Token API | Quang | `backend/RoomToRentRadar.Infrastructure/Services/AuthService.cs`, `backend/RoomToRentRadar.Infrastructure/Repositories/UserRepository.cs`, `backend/RoomToRentRadar.Api/Controllers/AuthController.cs`, `backend/RoomToRentRadar.Api/Models/AuthModels.cs`, `backend/RoomToRentRadar.Api/Auth/*`, `frontend/RoomToRentRadar/Features/Auth/AuthFeatureModule.cs`; hạ tầng Docker: `backend/RoomToRentRadar.Api/Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example` | Implement `IAuthService`, dùng `UserAccount`, `RentalDatabaseService`; JWT chứa `sub` (UserId) và `role` |
| **T01.2** | US01 | Build MAUI Login & Register screens with SecureStorage token caching | Nam | `frontend/RoomToRentRadar/Features/Auth/LoginPage.xaml(.cs)`, `RegisterPage.xaml(.cs)`, `AuthPageModel.cs`, `AuthFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiEndpoint.cs`, `ApiAuthService.cs` | `ApiAuthService : IAuthService` gọi `POST /api/auth/login`, `/register`; lưu token bằng `SecureStorage`; Route: `Constants.Routes.Login`, `Constants.Routes.Register` |
| **T02.1** | US02 | Build listing API with pagination and sorting | Thùy | `backend/RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Listing.cs`, `backend/RoomToRentRadar.Api/Controllers/RoomListingController.cs`, `backend/RoomToRentRadar.Api/Models/RoomListingModels.cs`, `frontend/RoomToRentRadar/Features/Rooms/RoomsFeatureModule.cs` | Override `ListVerifiedRoomsAsync` trong partial class `RoomListingRepository`; route `GET /api/rooms` |
| **T02.2** | US02 | Build listing screen, room cards, and infinite scroll | Hiếu | `frontend/RoomToRentRadar/Features/Rooms/RoomListPage.xaml(.cs)`, `RoomListPageModel.cs`, `RoomsFeatureModule.cs`, `frontend/RoomToRentRadar/Pages/Controls/RoomCardView.xaml(.cs)`, `frontend/RoomToRentRadar/Services/Api/ApiRoomListingRepository.cs` | Inject `IRoomListingRepository`; client gọi `GET /api/rooms`; điều hướng `${Constants.Routes.RoomDetail}?roomId={room.Id}` |
| **T03.1** | US03 | Build room detail API (description, amenities, rules) | Thùy | `backend/RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Detail.cs`, `backend/RoomToRentRadar.Api/Controllers/RoomDetailController.cs` | Override `GetByIdAsync(int id)` & `GetSimilarRoomsAsync`; route `GET /api/rooms/{id}`, `GET /api/rooms/{id}/similar` |
| **T03.2** | US03 | Build room detail screen and guest contact masking | Nam | `frontend/RoomToRentRadar/Features/Rooms/RoomDetailPage.xaml(.cs)`, `RoomDetailPageModel.cs`, `RoomsFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiRoomListingRepository.Detail.cs` | Nhận `[QueryProperty(nameof(RoomId), "roomId")]` (Mặc định `RoomId = 1` nếu test độc lập) |
| **T04.1** | US04 | Build photo upload pipeline and gallery API | Quang | `backend/RoomToRentRadar.Infrastructure/Services/PhotoGalleryService.cs`, `IPhotoGalleryService.cs`, `backend/RoomToRentRadar.Api/Controllers/PhotoGalleryController.cs`, `backend/RoomToRentRadar.Api/Models/PhotoGalleryModels.cs` | Đọc/ghi `PhotosJson` và `Panorama360Url` trong `RoomListing`; route `/api/rooms/{roomId}/photos` |
| **T04.2** | US04 | Build photo gallery UI and 360° viewer | Hiếu | `frontend/RoomToRentRadar/Features/Rooms/PhotoGallery360Page.xaml(.cs)`, `PhotoGallery360PageModel.cs`, `RoomsFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiPhotoGalleryClient.cs` | Route: `Constants.Routes.RoomGallery360` |
| **T05.1** | US05 | Build search API with price, area, and distance filters | Thùy | `backend/RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Search.cs`, `backend/RoomToRentRadar.Api/Controllers/RoomSearchController.cs` | Override `SearchAsync(RoomFilterCriteria criteria)`; route `GET /api/rooms/search` |
| **T05.2** | US05 | Build filter bottom sheet, active filters, and result counter | Nam | `frontend/RoomToRentRadar/Features/Rooms/RoomFilterModalPage.xaml(.cs)`, `RoomFilterPageModel.cs`, `RoomsFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiRoomListingRepository.Search.cs` | Route: `Constants.Routes.RoomFilter` |
| **T06.1** | US06 | Integrate Map SDK with ICTU campus center coordinates | Hiếu | `frontend/RoomToRentRadar/Features/Map/CampusMapService.cs`, `RoomMapPage.xaml(.cs)`, `MapFeatureModule.cs` | Tâm bản đồ: `Constants.IctuLatitude`, `Constants.IctuLongitude` |
| **T06.2** | US06 | Build map pins, preview cards, and room pin navigation | Nam | `frontend/RoomToRentRadar/Features/Map/RoomMapPageModel.cs`, `MapFeatureModule.cs`, `frontend/RoomToRentRadar/Pages/Controls/MapPinPreviewCard.xaml(.cs)` | Dùng `IRoomListingRepository` (client của T02.2); bấm Preview Card gọi `Constants.Routes.RoomDetail` |
| **T07.1** | US07 | Build local SQLite favorites table and API sync | Quang | `backend/RoomToRentRadar.Infrastructure/Repositories/FavoriteRepository.cs`, `backend/RoomToRentRadar.Api/Controllers/FavoritesController.cs`, `backend/RoomToRentRadar.Api/Models/FavoriteModels.cs`, `frontend/RoomToRentRadar/Features/Favorites/FavoritesFeatureModule.cs` | Implement `IFavoriteRepository`, dùng `FavoriteRoom`; userId lấy từ JWT, không nhận từ client |
| **T07.2** | US07 | Build favorite heart button, favorites screen, and undo | Hiếu | `frontend/RoomToRentRadar/Features/Favorites/FavoritesPage.xaml(.cs)`, `FavoritesPageModel.cs`, `FavoritesFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiFavoriteRepository.cs` | Inject `IFavoriteRepository`; Route: `Constants.Routes.Favorites` |
| **T08.1** | US08 | Build saved-search model and alert criteria API | Thùy | `backend/RoomToRentRadar.Infrastructure/Repositories/SavedSearchAlertRepository.cs`, `backend/RoomToRentRadar.Api/Controllers/AlertsController.cs`, `backend/RoomToRentRadar.Api/Models/AlertModels.cs`, `frontend/RoomToRentRadar/Features/Alerts/AlertsFeatureModule.cs` | Implement `ISavedSearchAlertRepository`, dùng `SavedSearchAlert` |
| **T08.2** | US08 | Build in-app alert notification settings UI | Nam | `frontend/RoomToRentRadar/Features/Alerts/SearchAlertsPage.xaml(.cs)`, `SearchAlertsPageModel.cs`, `AlertsFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiSavedSearchAlertRepository.cs` | Inject `ISavedSearchAlertRepository`; Route: `Constants.Routes.SearchAlerts` |
| **T09.1** | US09 | Build listing creation form and input validation | Quang | `frontend/RoomToRentRadar/Features/Landlord/PublishListingPage.xaml(.cs)`, `PublishListingPageModel.cs`, `LandlordFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiRoomListingRepository.Publish.cs` | Inject `IRoomListingRepository`; Route: `Constants.Routes.PublishListing` |
| **T09.2** | US09 | Build listing create/edit API with pending review status | Hiếu | `backend/RoomToRentRadar.Infrastructure/Repositories/Rooms/RoomListingRepository.Publish.cs`, `backend/RoomToRentRadar.Api/Controllers/RoomPublishController.cs`, `backend/RoomToRentRadar.Api/Models/RoomPublishModels.cs` | Override `SaveListingAsync` & `UpdateStatusAsync`; chỉ Landlord/Admin (`[Authorize(Roles = ...)]`) |
| **T10.1** | US10 | Build distance & estimated travel time calculation service | Thùy | `backend/RoomToRentRadar.Core/Services/TravelTimeService.cs` | Tính khoảng cách Haversine & thời gian đi bộ/xe buýt/xe máy tới ICTU |
| **T10.2** | US10 | Display travel time cards (motorbike, bus, walking) on detail screen | Quang | `frontend/RoomToRentRadar/Pages/Controls/TravelTimeCardView.xaml(.cs)` | Hiển thị `WalkMinutes`, `BusMinutes`, `MotorbikeMinutes` |
| **T11.1** | US11 | Build room comparison data model and comparison screen | Hiếu | `frontend/RoomToRentRadar/Features/Comparison/RoomComparePage.xaml(.cs)`, `RoomComparePageModel.cs`, `ComparisonFeatureModule.cs` | Inject `ComparisonBasketService`; Route: `Constants.Routes.RoomCompare` (Tối đa 3 phòng) |
| **T11.2** | US11 | Test comparison table and share comparison view | Thùy | `backend/RoomToRentRadarUnitTests/RoomComparisonTests.cs`, `backend/RoomToRentRadar.Core/Services/ComparisonShareService.cs` | Kiểm thử so sánh 3 phòng |
| **T12.1** | US12 | Build house rules model and API | Nam | `backend/RoomToRentRadar.Infrastructure/Repositories/HouseRuleRepository.cs`, `backend/RoomToRentRadar.Api/Controllers/HouseRulesController.cs`, `backend/RoomToRentRadar.Api/Models/HouseRuleModels.cs`, `frontend/RoomToRentRadar/Features/Rooms/RoomsFeatureModule.cs` | Implement `IHouseRuleRepository`, dùng `HouseRuleVersion` |
| **T12.2** | US12 | Build house rules UI section with expandable view | Quang | `frontend/RoomToRentRadar/Features/Rooms/HouseRulesView.xaml(.cs)`, `frontend/RoomToRentRadar/Services/Api/ApiHouseRuleRepository.cs` | Hiển thị `QuietHours`, `GuestPolicy`, `CookingRules`, `ParkingRules` |
| **T13.1** | US13 | Build listing report API and moderation queue entry | Hiếu | `backend/RoomToRentRadar.Infrastructure/Repositories/ListingReportRepository.cs`, `backend/RoomToRentRadar.Api/Controllers/ReportsController.cs`, `backend/RoomToRentRadar.Api/Models/ReportModels.cs`, `frontend/RoomToRentRadar/Features/Reports/ReportsFeatureModule.cs` | Implement `IListingReportRepository`, dùng `ListingReport` |
| **T13.2** | US13 | Build report dialog UI and user confirmation toast | Thùy | `frontend/RoomToRentRadar/Features/Reports/ReportListingModalPage.xaml(.cs)`, `ReportListingPageModel.cs`, `ReportsFeatureModule.cs`, `frontend/RoomToRentRadar/Services/Api/ApiListingReportRepository.cs` | Inject `IListingReportRepository`; Route: `Constants.Routes.ReportListing` |

---

## Cơ Chế Đăng Ký DI, Route & Web API Không Đụng Độ (`FeatureModule` + `Controllers` + `TryAddSingleton`)

1. **KHÔNG BAO GIỜ sửa `MauiProgram.cs`, `AppShell.xaml.cs` hoặc `backend/RoomToRentRadar.Api/Program.cs`:**
   - Trong MAUI App: `MauiProgram.cs` đã gọi sẵn toàn bộ `AddAuthFeature()`, `AddRoomsFeature()`, `AddMapFeature()`, `AddFavoritesFeature()`, `AddAlertsFeature()`, `AddLandlordFeature()`, `AddComparisonFeature()`, `AddReportsFeature()`. Khi làm Task của tính năng nào, chỉ mở đúng file `frontend/RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs` của tính năng đó để đăng ký:
     ```csharp
     services.AddTransientWithShellRoute<FavoritesPage, FavoritesPageModel>(Constants.Routes.Favorites);
     services.AddSingleton<IFavoriteRepository, ApiFavoriteRepository>(); // client HTTP của Task Frontend
     ```
   - Trong Web API (`RoomToRentRadar.Api`): `Program.cs` đã bật sẵn `AddControllers()` / `MapControllers()` và JWT. Khi làm Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`), chỉ tạo/sửa `backend/RoomToRentRadar.Api/Controllers/<Feature>Controller.cs` và `Models/<Feature>Models.cs` của Task mình — Controller mới tự được nhận. Nếu Api cần đăng ký repository/service thật mới, báo `@architect` (`@quangtldc0301`) thêm vào `Program.cs`.
2. **Cách viết `PageModel` của Frontend (Chuẩn DIP — Constructor Injection):**
   - Inject trực tiếp Interface vào constructor của `PageModel` (ví dụ: `public RoomListPageModel(IRoomListingRepository roomRepo)`).
   - **Không cần kiểm tra `null` hay tự viết mock data trong `PageModel`!**
   - Lý do: `InfrastructureModule` đã đăng ký sẵn các class Fake in-memory (`InMemoryRoomListingRepository`, `InMemoryAuthService`...) bằng `TryAddSingleton` với đúng 7 phòng trọ mẫu quanh ICTU.
   - Khi client HTTP (`Api*`) được đăng ký bằng `services.AddSingleton<IRoomListingRepository, ApiRoomListingRepository>()` trong `<Feature>FeatureModule.cs`, DI container tự động dùng dữ liệu từ backend mà `PageModel` không phải sửa dòng nào.
3. **Gọi backend từ MAUI:** Windows `http://localhost:8080`, Android Emulator `http://10.0.2.2:8080`; gửi token `Authorization: Bearer <token>` lấy từ `SecureStorage`. Mất mạng hoặc API lỗi: ghi log và hiện thông báo thân thiện, không crash.

---

## Giải Pháp Kỹ Thuật Bắt Buộc Cho Bản Đồ (`US06`) & Ảnh 360° (`US04`) Đa Nền Tảng

1. **Task `T06.1` & `T06.2` (`US06`):** Sử dụng `WebView` của .NET MAUI nhúng **Leaflet.js + OpenStreetMap HTML/JS** (tâm bản đồ mặc định tại cổng trường ICTU `Constants.IctuLatitude = 21.5850`, `Constants.IctuLongitude = 105.8064`). Khi bấm vào Pin thì gửi sự kiện về C# (`WebView.Navigating` với scheme `rtr://pin?roomId=X`) để bật `MapPinPreviewCard`.
2. **Task `T04.2` (`US04`):** Sử dụng `CarouselView` cho bộ sưu tập ảnh 2D và `WebView` nhúng **Pannellum 360° HTML5 Viewer** (nạp `RoomListing.Panorama360Url`).

---

## Hợp Đồng Chữ Ký Hàm (Method Signature Contracts — Trong `backend/RoomToRentRadar.Core/Abstractions/`)

1. **`IAuthService` (`RoomToRentRadar.Abstractions.Auth`):**
   - `UserAccount? CurrentUser { get; }`
   - `bool IsSignedIn { get; }`
   - `Task<UserAccount?> LoginAsync(string email, string password)`
   - `Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role)`
   - `void Logout()`
   - Ghi chú: trong Web API **không dùng `CurrentUser`** (dùng chung cho mọi request) — lấy người gọi từ JWT bằng `AuthController.TryGetUserId(User, out var id)`.
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

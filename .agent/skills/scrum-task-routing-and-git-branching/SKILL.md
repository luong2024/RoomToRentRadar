---
name: scrum-task-routing-and-git-branching
description: Enforces Group 5's Scrum Sprint 1 task-to-file mapping (T01.1 to T13.2), Git branch naming conventions (group-5/Txx.x-...), main-branch protection, and conflict-free DI/Route registration in .NET MAUI.
---

# Scrum Task Routing & Git Branching Skill (Group 5)

## Mục đích
Đảm bảo cả 4 thành viên Developer (Quang, Hiếu, Thùy, Nam) khi dùng AI Agent để code song song đều:
1. Đứng đúng nhánh Git của Task (`group-5/Txx.x-...`), **TUYỆT ĐỐI KHÔNG push lên nhánh `main`**.
2. Chỉ tạo/sửa đúng các file thuộc Task ID (`T01.1` $\rightarrow$ `T13.2`) được giao trong `06. Project Plan - Group 5`.
3. Sử dụng đúng các Models, SQLite tables và Route Constants đã được khởi tạo sẵn trong Bộ Khung Xương Chung (`Models/` và `Data/`), giúp ghép code (Merge) vào nhánh `group-5` khớp nhau 100% mà không cần chờ nhau hoàn thành trước.

---

## Quy Tắc Git Bắt Buộc Cho Mọi AI Agent

1. **CẤM TUYỆT ĐỐI nhánh `main`:**
   - Không bao giờ chạy `git push origin main` hoặc merge vào `main`.
   - Nhánh tích hợp gốc của nhóm là: **`group-5`**.
2. **Quy chuẩn đặt tên nhánh & commit theo Task ID của Group 5:**
   - Mỗi Task phải làm trên một nhánh riêng tách ra từ `group-5` với tiền tố `feature/group-5/` (để không xung đột ref với nhánh `group-5` và không trùng tên nhánh với các nhóm khác trong lớp):
     ```powershell
     git checkout group-5
     git pull origin group-5
     git checkout -b feature/group-5/<TaskID>-<short-name>
     # Ví dụ: git checkout -b feature/group-5/T01.1-auth-api
     # Ví dụ: git checkout -b feature/group-5/T02.2-listing-ui
     ```
   - Khi commit và push:
     ```powershell
     git commit -m "feat(group-5/<TaskID>): <mo-ta-ngan-gon> (<US>)"
     git push -u origin feature/group-5/<TaskID>-<short-name>
     ```
3. **Kiểm tra nhánh trước khi code:**
   - Agent luôn chạy `git branch --show-current` trước khi sửa file để xác nhận đang đứng ở nhánh `feature/group-5/T...` tương ứng với Task được giao.

---

## Bảng Ánh Xạ 26 Tasks Sprint 1 (`T01.1` $\rightarrow$ `T13.2`) & Phạm Vi File

Khi người dùng yêu cầu thực hiện một mã Task (hoặc khi nhánh Git mang mã Task đó), Agent **BẮT BUỘC** tra bảng dưới đây để biết phạm vi file của mình:

| Task ID | US | Tên Task | Phụ trách | File được phép tạo / sửa (Safe Zone) | Hợp đồng kết nối (Models / Routes dùng chung) |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **T01.1** | US01 | Build Auth & JWT/Session Service | Quang | `Services/AuthService.cs`, `Services/IAuthService.cs`, `Data/UserRepository.cs` | Dùng `Models/UserAccount.cs`, `Data/RentalDatabaseService.cs` |
| **T01.2** | US01 | Build Login & Register UI | Nam | `Pages/Auth/LoginPage.xaml(.cs)`, `Pages/Auth/RegisterPage.xaml(.cs)`, `PageModels/Auth/AuthPageModel.cs` | Route: `Constants.Routes.Login`, `Constants.Routes.Register` |
| **T02.1** | US02 | Build Listing API (pagination & sorting) | Thùy | `Data/RoomListingRepository.cs`, `Services/IRoomListingService.cs` | Dùng `Models/RoomListing.cs` (`Status == "Available"`, `IsVerified == true`) |
| **T02.2** | US02 | Build Listing Screen & Room Cards | Hiếu | `Pages/Rooms/RoomListPage.xaml(.cs)`, `Pages/Controls/RoomCardView.xaml(.cs)`, `PageModels/Rooms/RoomListPageModel.cs` | Điều hướng sang chi tiết qua ` $"{Constants.Routes.RoomDetail}?roomId={room.Id}"` |
| **T03.1** | US03 | Build Room Detail Data Service | Thùy | Mở rộng hàm `GetByIdAsync(int id)` & `GetSimilarRoomsAsync` trong `Data/RoomListingRepository.cs` | Dùng `Models/RoomListing.cs` |
| **T03.2** | US03 | Build Room Detail Screen & Guest Masking | Nam | `Pages/Rooms/RoomDetailPage.xaml(.cs)`, `PageModels/Rooms/RoomDetailPageModel.cs` | Nhận `[QueryProperty(nameof(RoomId), "roomId")]` (Mặc định `RoomId = 1` nếu test độc lập) |
| **T04.1** | US04 | Build Photo Upload & Gallery Service | Quang | `Services/PhotoGalleryService.cs`, `Services/IPhotoGalleryService.cs` | Đọc/ghi `PhotosJson` và `Panorama360Url` trong `RoomListing` |
| **T04.2** | US04 | Build Photo Gallery UI & 360° Viewer | Hiếu | `Pages/Rooms/PhotoGallery360Page.xaml(.cs)`, `PageModels/Rooms/PhotoGallery360PageModel.cs` | Route: `Constants.Routes.RoomGallery360` |
| **T05.1** | US05 | Build Multi-Filter Search Service | Thùy | Mở rộng hàm `SearchAsync(RoomFilterCriteria criteria)` trong `Data/RoomListingRepository.cs` | Dùng `Models/RoomFilterCriteria.cs` |
| **T05.2** | US05 | Build Filter Panel & Active Filter Count | Nam | `Pages/Rooms/RoomFilterModalPage.xaml(.cs)`, `PageModels/Rooms/RoomFilterPageModel.cs` | Route: `Constants.Routes.RoomFilter` |
| **T06.1** | US06 | Integrate Map & ICTU Coordinates | Hiếu | `Services/CampusMapService.cs`, `Pages/Map/RoomMapPage.xaml(.cs)` | Tâm bản đồ: `Constants.IctuLatitude`, `Constants.IctuLongitude` |
| **T06.2** | US06 | Build Map Pins & Preview Cards | Nam | `PageModels/Map/RoomMapPageModel.cs`, `Pages/Controls/MapPinPreviewCard.xaml(.cs)` | Bấm Preview Card gọi `Constants.Routes.RoomDetail` |
| **T07.1** | US07 | Build Local SQLite Favorites & Sync | Quang | `Data/FavoriteRepository.cs` | Dùng `Models/FavoriteRoom.cs` |
| **T07.2** | US07 | Build Favorite Button, Screen & Undo | Hiếu | `Pages/Favorites/FavoritesPage.xaml(.cs)`, `PageModels/Favorites/FavoritesPageModel.cs` | Route: `Constants.Routes.Favorites` |
| **T08.1** | US08 | Build Saved-Search & Alert Rules | Thùy | `Data/SavedSearchAlertRepository.cs` | Dùng `Models/SavedSearchAlert.cs` |
| **T08.2** | US08 | Build Alert Settings UI | Nam | `Pages/Alerts/SearchAlertsPage.xaml(.cs)`, `PageModels/Alerts/SearchAlertsPageModel.cs` | Route: `Constants.Routes.SearchAlerts` |
| **T09.1** | US09 | Build Listing Creation Form & Validation | Quang | `Pages/Landlord/PublishListingPage.xaml(.cs)`, `PageModels/Landlord/PublishListingPageModel.cs` | Route: `Constants.Routes.PublishListing` |
| **T09.2** | US09 | Build Listing Create/Edit Service | Hiếu | Mở rộng `SaveListingAsync` & `UnpublishAsync` trong `Data/RoomListingRepository.cs` | Lưu mới với `Status = "PendingReview"`, `IsVerified = false` |
| **T10.1** | US10 | Build Routing & Travel-Time Service | Thùy | `Services/TravelTimeService.cs` | Tính khoảng cách Haversine & thời gian đi bộ/xe buýt/xe máy tới ICTU |
| **T10.2** | US10 | Display Travel Time & Fallback UI | Quang | `Pages/Controls/TravelTimeCardView.xaml(.cs)` | Hiển thị `WalkMinutes`, `BusMinutes`, `MotorbikeMinutes` |
| **T11.1** | US11 | Build Room Comparison Model & Screen | Hiếu | `Pages/Comparison/RoomComparePage.xaml(.cs)`, `PageModels/Comparison/RoomComparePageModel.cs` | Route: `Constants.Routes.RoomCompare` (Tối đa 3 phòng) |
| **T11.2** | US11 | Test Comparison & Share-as-Image | Thùy | `RoomToRentRadarUnitTests/RoomComparisonTests.cs`, `Services/ComparisonShareService.cs` | Kiểm thử so sánh 3 phòng |
| **T12.1** | US12 | Build House-Rules Versioning Service | Nam | `Data/HouseRuleRepository.cs` | Dùng `Models/HouseRuleVersion.cs` |
| **T12.2** | US12 | Build House-Rules UI with Versioning | Quang | `Pages/Rooms/HouseRulesView.xaml(.cs)` | Hiển thị `QuietHours`, `GuestPolicy`, `CookingRules`, `ParkingRules` |
| **T13.1** | US13 | Build Report API & Moderation Queue | Hiếu | `Data/ListingReportRepository.cs` | Dùng `Models/ListingReport.cs` |
| **T13.2** | US13 | Build Report Dialog UI & Confirmation | Thùy | `Pages/Reports/ReportListingModalPage.xaml(.cs)`, `PageModels/Reports/ReportListingPageModel.cs` | Route: `Constants.Routes.ReportListing` |

---

## Quy Tắc Chống Đụng Độ Khi Đăng Ký Vào `MauiProgram.cs` & `AppShell.xaml`

Khi một Task cần đăng ký thêm Service, Page hoặc Route mới vào `MauiProgram.cs` / `AppShell.xaml` / `AppShell.xaml.cs`:
1. **TUYỆT ĐỐI KHÔNG** xóa, sửa, hoặc sắp xếp lại (reorder/reformat) các dòng code đã có sẵn trong `MauiProgram.cs` và `AppShell.xaml(.cs)`.
2. Chỉ **thêm đúng dòng đăng ký của Task mình** vào khu vực comment phân vùng tương ứng (`ZONE 1: LEAD DEV NGUYEN DANG QUANG`, `ZONE 2: LAM MINH HIEU`, `ZONE 3: PHAM THI THUY`, `ZONE 4: LIEU HAI NAM`).
3. Khi test độc lập một màn hình nhận tham số (như `RoomDetailPage` nhận `roomId`), luôn gán giá trị mặc định `roomId = 1` nếu chưa được truyền tham số từ màn hình trước để đảm bảo màn hình luôn hiển thị được dữ liệu mẫu ngay lập tức.

---

## Cơ Chế "Frontend Tự Chạy Mock Data -> Tự Động Nối Vào Backend Khi Merge"

Để các bạn làm Frontend (`T01.2`, `T02.2`, `T03.2`, `T04.2`, `T05.2`, `T06.2`, `T07.2`, `T08.2`, `T09.1`, `T11.1`, `T13.2`) có thể chạy thử giao diện ngay lập tức khi nhánh Backend chưa hoàn thành, và tự động ăn vào Backend SQLite thật ngay khi nhánh Backend được Merge vào `group-5`:

1. **Tất cả các Interface hợp đồng đã có sẵn tại `Services/IRentalContracts.cs`** (do Lead Dev Quang quản lý):
   - `IAuthService`, `IRoomListingRepository`, `IFavoriteRepository`, `ISavedSearchAlertRepository`, `IHouseRuleRepository`, `IListingReportRepository`, và `ComparisonBasketService`.
2. **Cách viết trong `PageModel` của Frontend:**
   - Inject `IServiceProvider` vào constructor của `PageModel` và lấy service qua `_serviceProvider.GetService<IRoomListingRepository>()`.
   - Nếu service khác `null` (tức là nhánh Backend đã được Merge và đăng ký DI): Gọi thẳng hàm của Backend SQLite!
   - Nếu service bằng `null` (tức là đang làm song song, nhánh Backend chưa Merge): Tự động trả về danh sách **Mock Data mẫu** ngay trong `PageModel` để giao diện luôn hiển thị đầy đủ dữ liệu khi test!
3. **Cách viết trong `Repository` / `Service` của Backend (`T01.1`, `T02.1`, `T03.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`):**
   - Luôn kế thừa (implement) đúng Interface tương ứng trong `Services/IRentalContracts.cs` (ví dụ: `public class RoomListingRepository : IRoomListingRepository`).
   - Đăng ký vào đúng Zone của mình trong `MauiProgram.cs`:
     `builder.Services.AddSingleton<IRoomListingRepository, RoomListingRepository>();`

---

## Giải Pháp Kỹ Thuật Bắt Buộc Cho Bản Đồ (`US06`) & Ảnh 360° (`US04`) Đa Nền Tảng

Để ứng dụng chạy mượt 100% trên cả **Windows Desktop (`net10.0-windows`)** và **Android (`net10.0-android`)** mà không bị crash vì thiếu Google Maps API Key trả phí:
1. **Task `T06.1` & `T06.2` (Bản đồ tương tác, vẽ vùng đa giác Polygon, Ghim vị trí & Preview Card - `US06`):**
   - **BẮT BUỘC** sử dụng `WebView` của .NET MAUI nhúng **Leaflet.js + OpenStreetMap HTML/JS** (với tâm bản đồ mặc định tại cổng trường ICTU `Constants.IctuLatitude = 21.5850`, `Constants.IctuLongitude = 105.8064`).
   - Hỗ trợ vẽ vùng chọn (Polygon/Circle), hiển thị các Pin giá phòng và khi bấm vào Pin thì gửi sự kiện về C# (`WebView.Navigating` với scheme `rtr://pin?roomId=X`) để bật `MapPinPreviewCard` trên giao diện MAUI.
2. **Task `T04.2` (Bộ sưu tập ảnh & Trình xem phòng 360° - `US04`):**
   - **BẮT BUỘC** sử dụng `CarouselView` cho bộ sưu tập ảnh 2D (tối thiểu 8 ảnh/phòng) và `WebView` nhúng **Pannellum 360° HTML5 Viewer** (nạp `RoomListing.Panorama360Url`) để người dùng xoay xem toàn cảnh phòng 360 độ mượt mà trên cả Windows và Android.

---

## Hợp Đồng Chữ Ký Hàm (Method Signature Contracts) Giữa Backend Tasks & UI Tasks

Để khi 2 thành viên làm song song (1 bạn làm Repository/Service, 1 bạn làm giao diện UI) rồi gộp nhánh vào `group-5` tự động khớp nhau 100%, AI Agent khi viết hoặc gọi các class dưới đây **BẮT BUỘC** dùng đúng tên class và chữ ký hàm chuẩn sau:

1. **`AuthService` / `IAuthService` (`T01.1` & `T01.2` & `T03.2`):**
   - `UserAccount? CurrentUser { get; }`
   - `bool IsSignedIn { get; }`
   - `Task<UserAccount?> LoginAsync(string email, string password)`
   - `Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role)`
   - `void Logout()`

2. **`RoomListingRepository` (`T02.1`, `T03.1`, `T05.1`, `T09.2` — dùng bởi `T02.2`, `T03.2`, `T05.2`, `T06.2`, `T09.1`, `T11.1`):**
   - `Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc")`
   - `Task<RoomListing?> GetByIdAsync(int roomId)`
   - `Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3)` *(phục vụ US03 AC3 khi phòng `Unavailable`)*
   - `Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria)`
   - `Task<int> SaveListingAsync(RoomListing listing)` *(nếu tạo mới từ chủ trọ US09: đặt `Status = Constants.ListingStatuses.PendingReview`, `IsVerified = false`)*
   - `Task<bool> UpdateStatusAsync(int roomId, string status, bool isVerified)`

3. **`FavoriteRepository` (`T07.1` & `T07.2`):**
   - `Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId)`
   - `Task<bool> ToggleFavoriteAsync(int userId, int roomId)`
   - `Task<bool> AddFavoriteAsync(int userId, int roomId)`
   - `Task<bool> RemoveFavoriteAsync(int userId, int roomId)`

4. **`SavedSearchAlertRepository` (`T08.1` & `T08.2`):**
   - `Task<List<SavedSearchAlert>> ListByUserAsync(int userId)`
   - `Task<int> SaveAlertAsync(SavedSearchAlert alert)`
   - `Task<bool> ToggleAlertAsync(int alertId, bool isEnabled)`
   - `Task<bool> DeleteAlertAsync(int alertId)`

5. **`HouseRuleRepository` (`T12.1` & `T12.2`):**
   - `Task<List<HouseRuleVersion>> GetHistoryByRoomAsync(int roomId)`
   - `Task<int> SaveNewVersionAsync(HouseRuleVersion ruleVersion)`

6. **`ListingReportRepository` (`T13.1` & `T13.2`):**
   - `Task<int> SubmitReportAsync(ListingReport report)`
   - `Task<List<ListingReport>> ListPendingReportsAsync()`

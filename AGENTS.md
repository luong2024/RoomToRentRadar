# AI Agent Governance & Team Collaboration Rules (RoomToRentRadar — Group 5)

Quy chuẩn tối cao dành cho các AI Agent (Antigravity, Cursor, Claude Code, Copilot, Aider, v.v.) hoạt động trên repository **RoomToRentRadar** của **Group 5 (ICTU)**. Mọi Agent khi thực hiện task PHẢI tuân thủ nghiêm ngặt các nguyên tắc sau để đảm bảo **không đá code**, **tuân thủ SOLID**, **không hỏng Database chung** và **ghép nối hoàn hảo** khi Merge về nhánh `group-5`.

---

## 1. Hệ Thống Phân Vai Role-Based Matrix & Người Gác Cổng (Gatekeepers)

- **Scrum Master / Gatekeeper điều phối & duyệt đầu ra:** **Đức Nguyễn** (`@Duc-Nguyen-Github`)
- **Technical Lead / System Architect:** **Nguyễn Đăng Quang** (`@quangtldc0301`)
- **Chỉ `@Duc-Nguyen-Github` và `@quangtldc0301` được quyền Approve & bấm Merge Pull Request vào nhánh `group-5`**, và chỉ sau khi CI GitHub Actions xanh + đã xác nhận code chạy thực tế.

Mỗi Agent khi bắt đầu làm việc PHẢI hoạt động dưới một **Role cụ thể** được Developer chỉ định. Nếu Developer không chỉ định, Agent mặc định nhận role **`@developer`** và phải tự động kiểm tra nhánh Git hiện tại (`git branch --show-current`) để tra cứu mã Task (`T01.1` $\rightarrow$ `T13.2`) trong skill `scrum-task-routing-and-git-branching`.

```
                           ┌────────────────────────┐
                           │   SCRUM MASTER (Đức)   │
                           │  & TECH LEAD (Quang)   │
                           │   @architect Agent     │
                           └───────────┬────────────┘
                                       │ (Khóa Contracts: RoomToRentRadar.Core, Persistence, MauiProgram)
                   ┌───────────────────┴───────────────────┐
                   ▼                                       ▼
       ┌────────────────────────┐             ┌────────────────────────┐
       │   FEATURE DEVELOPER    │             │       QA / TESTER      │
       │   @developer Agent     │             │    @qa-tester Agent    │
       │ (Code Task T01.1-T13.2)│             │  (Unit Tests, Appium)  │
       └───────────┬────────────┘             └────────────┬───────────┘
                   │                                       │
                   └───────────────────┬───────────────────┘
                                       ▼
                           ┌────────────────────────┐
                           │  AUDITOR / GATEKEEPER  │
                           │  @code-reviewer Agent  │
                           │ (Audit Diff -> group-5)│
                           └────────────────────────┘
```

### Bảng Phân Quyền Vai Trò Chi Tiết:

| Role ID | Tên Vai Trò | Phạm Vi Quyền Ghi (Write Permissions) | Thư Mục Bị Cấm Sửa (Read-Only Zone) | Trách Nhiệm Chính |
| :--- | :--- | :--- | :--- | :--- |
| **`@architect`** | System Architect (Đức / Quang) | `RoomToRentRadar.Core/`, `RoomToRentRadar.Infrastructure/Persistence/`, `RoomToRentRadar.Api/Program.cs`, `MauiProgram.cs`, Solution | Không can thiệp vào chi tiết View XAML hay logic riêng của từng Task | Thiết kế Domain Schema (`RoomListing`, `UserAccount`...), Interfaces (`Abstractions/*`), SQLite schema & seed data (`RentalDatabaseService`), Route Constants, Composition Root (`MauiProgram.cs` & `Api/Program.cs`). |
| **`@developer`** | Feature Developer | Các file thuộc đúng **Mã Task (`T01.1` $\rightarrow$ `T13.2`)** được phân định trong `scrum-task-routing-and-git-branching` (gồm `Infrastructure`, `Api/Endpoints/<Task>Endpoints.cs`, `Features/<Feature>/` và `<Feature>FeatureModule.cs`) | 🔒 `RoomToRentRadar.Core/*` (trừ `T10.1`/`T11.2`), `RoomToRentRadar.Infrastructure/Persistence/*`, `RoomToRentRadar.Api/Program.cs`, `RoomToRentRadar/MauiProgram.cs`, Test files cũ | Hiện thực hóa Task được giao (Repository/Service + Web API Endpoint hoặc UI XAML + PageModel); tự đăng ký DI/Route trong `<Feature>FeatureModule.cs` và ánh xạ HTTP route trong `Endpoints/<Task>Endpoints.cs`. |
| **`@qa-tester`** | Test Engineer | `RoomToRentRadarUnitTests/`, `RoomToRentRadarUIAppiumTests/` | 🔒 Toàn bộ mã nguồn `Core`, `Infrastructure`, `Api`, `RoomToRentRadar` | Viết bài test mới, kiểm thử regression; **cấm sửa code app để làm test pass**. |
| **`@code-reviewer`**| Code Auditor | 🔒 **READ-ONLY TOÀN BỘ REPO** | Không được sửa bất kỳ file nào | Soát Git Diff của nhánh `feature/group-5/Txx.x-...` so với `group-5`, kiểm tra vi phạm ranh giới, báo cáo PASS/REJECT cho Scrum Master. |
| **`@devops`** | DevOps & Build | `.csproj`, `.slnx`, `Directory.Build.props`, `.github/`, `.githooks/` | Code nghiệp vụ logic | Sửa lỗi cấu hình build đa nền tảng (Windows & Android), CI/CD, Git hooks. |

---

## 2. Quy Tắc Git & Cưỡng Chế Nhánh Của Group 5 (Zero-Tolerance)

1. **CẤM TUYỆT ĐỐI Nhánh `main`:**
   - **KHÔNG BAO GIỜ** commit, push hoặc mở PR vào nhánh `main`.
   - Nhánh tích hợp chính của nhóm là **`group-5`**.
2. **CẤM Commit / Push Trực Tiếp Task Lên `group-5`:**
   - Không thành viên hay AI Agent nào được commit/push thẳng code tính năng lên `group-5`.
   - Bộ Git Hooks tự động trong `.githooks/pre-commit` và `.githooks/pre-push` (được MSBuild tự động kích hoạt ngay lần build đầu tiên) sẽ **chặn cứng** mọi lệnh `git commit` / `git push` vào `main` và `group-5`.
   - **CẤM TUYỆT ĐỐI** AI Agent sử dụng cờ `--no-verify` để vượt qua Git hooks.
3. **Mỗi Task (`T01.1` $\rightarrow$ `T13.2`) là một Nhánh Con riêng biệt:**
   - Tạo nhánh con từ `group-5` theo cú pháp:
     `feature/group-5/<TaskID>-<ten-ngan-gon>` (Ví dụ: `feature/group-5/T01.1-auth-api`, `feature/group-5/T02.2-listing-ui`).
   - Đặt tên commit rõ ràng:
     `feat(group-5/<TaskID>): <mo-ta-ngan-gon>`
   - Trước khi push, Git hook sẽ tự động chạy `dotnet test RoomToRentRadarUnitTests`. Nếu test fail, lệnh push sẽ bị chặn.
   - Sau khi push lên `origin feature/group-5/<TaskID>-...`, tạo Pull Request hướng về base branch **`group-5`** để Scrum Master (`@Duc-Nguyen-Github`) hoặc Lead (`@quangtldc0301`) kiểm tra và bấm Merge.

---

## 3. Kiến Trúc 4 Project Chuẩn SOLID & Bản Đồ Trách Nhiệm Thư Mục

Dự án được tách thành **4 tầng độc lập** (+ 2 project kiểm thử) theo nguyên tắc Clean Architecture & SOLID:

| Tầng / Khu vực | Trách nhiệm | Mức độ hạn chế | Role Phụ Trách |
| :--- | :--- | :--- | :--- |
| **`RoomToRentRadar.Core/`** | **Domain & Contracts thuần .NET 10** (không phụ thuộc MAUI hay SQLite):<br/>- `Models/`: `RoomListing`, `UserAccount`, `FavoriteRoom`, `RoomFilterCriteria`...<br/>- `Abstractions/<Feature>/`: Mỗi interface 1 file riêng (ISP) — `IAuthService`, `IRoomListingRepository`, `IFavoriteRepository`, `ISavedSearchAlertRepository`, `IHouseRuleRepository`, `IListingReportRepository`<br/>- `Services/ComparisonBasketService.cs`, `Constants.cs` | 🔒 **RESTRICTED** | `@architect` |
| **`RoomToRentRadar.Infrastructure/`** | **Data & Services thực thi (.NET 10, dùng chung cho cả Web API & MAUI, test được bằng Unit Test):**<br/>- `Persistence/`: `RentalDatabaseService.cs`, `DatabaseSchema.cs`, `Seed/IctuSeedData.cs`, `DatabaseOptions.cs` (🔒 khóa)<br/>- `Fakes/`: Các bản in-memory (`InMemoryRoomListingRepository`...) đăng ký sẵn qua `TryAddSingleton` để UI chạy ngay khi Backend chưa merge<br/>- `Repositories/` & `Services/`: Nơi các Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`) viết code SQLite | 🟢 **TASK-SCOPED** (trừ `Persistence/` là 🔒) | `@developer` |
| **`RoomToRentRadar.Api/` (Web API)** | **ASP.NET Core Web API kết nối SQLite (`Infrastructure`):**<br/>- `Endpoints/*Endpoints.cs`: Mỗi Task Backend sở hữu **1 file Endpoint riêng biệt** (`AuthEndpoints.cs`, `RoomListingEndpoints.cs`, `RoomDetailEndpoints.cs`, `PhotoGalleryEndpoints.cs`, `RoomSearchEndpoints.cs`, `RoomPublishEndpoints.cs`, `FavoriteEndpoints.cs`, `AlertEndpoints.cs`, `HouseRuleEndpoints.cs`, `ReportEndpoints.cs`)<br/>- `Program.cs`: 🔒 Đã gọi sẵn tất cả `app.Map...Endpoints()` — **Developer KHÔNG sửa `Program.cs`** | 🟢 **TASK-SCOPED** (`Program.cs` là 🔒) | `@developer` |
| **`RoomToRentRadar/` (MAUI App)** | **Giao diện Android/Windows & Composition Root:**<br/>- `Features/<Feature>/`: Chứa `Page.xaml(.cs)`, `PageModel.cs` và `<Feature>FeatureModule.cs` của từng tính năng (`Auth`, `Rooms`, `Map`, `Favorites`, `Alerts`, `Landlord`, `Comparison`, `Reports`, `Home`)<br/>- `Pages/Controls/`: Các Custom Controls dùng chung theo Task (`RoomCardView`, `TravelTimeCardView`, `MapPinPreviewCard`)<br/>- `MauiProgram.cs`: 🔒 Đã gọi sẵn tất cả `<Feature>FeatureModule` — **Developer KHÔNG sửa `MauiProgram.cs`** | 🟢 **TASK-SCOPED** (`MauiProgram.cs` là 🔒) | `@developer` |
| **`RoomToRentRadarUnitTests/`** | Kiểm thử đơn vị & hợp đồng API/SQLite cho `Core` + `Infrastructure` + `Api` | 🟢 **MANDATORY** | `@qa-tester`, `@developer` |

---

## 4. Nguyên Tắc SOLID Bắt Buộc Trong Dự Án

1. **Single Responsibility (SRP):**
   - Mỗi class/file chỉ có 1 lý do để thay đổi: Schema (`DatabaseSchema.cs`), Seed (`IctuSeedData.cs`), Connection (`RentalDatabaseService.cs`) tách rời hoàn toàn.
   - `RoomListingRepository` được chia thành 4 file `partial` theo đúng 4 Task (`RoomListingRepository.Listing.cs` cho `T02.1`, `.Detail.cs` cho `T03.1`, `.Search.cs` cho `T05.1`, `.Publish.cs` cho `T09.2`) và tương ứng với 4 file Endpoint riêng biệt trong `RoomToRentRadar.Api/Endpoints/` để 2 người làm song song không bao giờ chạm vào cùng 1 file.
2. **Open/Closed (OCP) — Không Sửa `MauiProgram.cs` và `RoomToRentRadar.Api/Program.cs`:**
   - Khi cần đăng ký DI hoặc Route cho Task của mình, Developer mở đúng file `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs` của tính năng đó.
   - Khi cần thêm HTTP Endpoint cho Task Backend, Developer mở đúng file `RoomToRentRadar.Api/Endpoints/<Task>Endpoints.cs` của Task mình. **Tuyệt đối không sửa `MauiProgram.cs` hay `RoomToRentRadar.Api/Program.cs`**.
3. **Interface Segregation (ISP):**
   - Mỗi interface hợp đồng nằm ở 1 file riêng biệt trong `RoomToRentRadar.Core/Abstractions/<Feature>/`.
4. **Dependency Inversion (DIP) — Cấm Service Locator:**
   - Mọi `PageModel` nhận trực tiếp Interface (`IRoomListingRepository`, `IAuthService`, `IFavoriteRepository`...) qua **Constructor Injection**.
   - Nhờ `InfrastructureModule` đã đăng ký sẵn các bản `InMemory*` bằng `TryAddSingleton`, giao diện luôn có dữ liệu mẫu chuẩn ICTU ngay lập tức mà không cần viết `IServiceProvider.GetService` hay nhét mock data vào trong `PageModel`. Khi nhánh Backend đăng ký `AddSingleton<IRoomListingRepository, RoomListingRepository>()`, bản SQLite thật sẽ tự động thay thế bản Fake.

---

## 5. Quy Trình Kiểm Thử & Tự Đánh Giá (Agent Pre-flight Checklist)

Trước khi thông báo hoàn tất bất kỳ Task nào (`T01.1` $\rightarrow$ `T13.2`), Agent PHẢI chạy:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild
```
Và xác nhận đủ các mục sau:
- [ ] Nhánh hiện tại (`git branch --show-current`) là `feature/group-5/Txx.x-...` (KHÔNG phải `main` hay `group-5`).
- [ ] Lệnh `dotnet build RoomToRentRadar.slnx` thành công với `0 Error(s)`.
- [ ] Lệnh `dotnet test RoomToRentRadarUnitTests` pass 100%.
- [ ] Kiểm tra `git status` & `git diff --stat group-5`: chỉ chứa các file thuộc Mã Task được giao, không chạm vào `RoomToRentRadar.Core/`, `Persistence/` hay `MauiProgram.cs`.

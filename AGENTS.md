# AI Agent Governance & Team Collaboration Rules (RoomToRentRadar — Group 5)

Quy chuẩn tối cao dành cho các AI Agent (Antigravity, Cursor, Claude Code, Copilot, Aider, v.v.) hoạt động trên repository **RoomToRentRadar** của **Group 5 (ICTU)**. Mọi Agent khi thực hiện task PHẢI tuân thủ nghiêm ngặt các nguyên tắc sau để đảm bảo **không đá code**, **tuân thủ SOLID**, **không hỏng Database chung** và **ghép nối hoàn hảo** khi Merge về nhánh `group-5`.

User Story và Acceptance Criteria của Sprint 1: xem `docs/backlog.md` (bản trên máy) hoặc file kế hoạch nhóm *06. Project Plan - Group 5*.

---

## 1. Hệ Thống Phân Vai Role-Based Matrix & Người Gác Cổng (Gatekeepers)

- **Product Owner (nghiệm thu Increment):** Vũ Huy Lượng
- **Scrum Master kiêm Technical Lead / System Architect:** **Nguyễn Đăng Quang** (`@quangtldc0301`)
- **Chỉ `@quangtldc0301` được quyền Approve & bấm Merge Pull Request vào nhánh `group-5`**, và chỉ sau khi CI GitHub Actions xanh + đã xác nhận code chạy thực tế (backend chạy trong Docker, app chạy trên Android/Windows).

Mỗi Agent khi bắt đầu làm việc PHẢI hoạt động dưới một **Role cụ thể** được Developer chỉ định. Nếu Developer không chỉ định, Agent mặc định nhận role **`@developer`** và phải tự động kiểm tra nhánh Git hiện tại (`git branch --show-current`) để tra cứu mã Task (`T01.1` $\rightarrow$ `T13.2`) trong skill `scrum-task-routing-and-git-branching`.

```
                           ┌────────────────────────────┐
                           │ SCRUM MASTER & TECH LEAD   │
                           │      (Quang)               │
                           │   @architect Agent         │
                           └─────────────┬──────────────┘
                                         │ (Khóa Contracts: Core, Persistence, Api/Program.cs, MauiProgram.cs)
                   ┌─────────────────────┴─────────────────────┐
                   ▼                                           ▼
       ┌────────────────────────┐                 ┌────────────────────────┐
       │   FEATURE DEVELOPER    │                 │       QA / TESTER      │
       │   @developer Agent     │                 │    @qa-tester Agent    │
       │ (Code Task T01.1-T13.2)│                 │  (Unit Tests, Appium)  │
       └───────────┬────────────┘                 └────────────┬───────────┘
                   │                                           │
                   └─────────────────────┬─────────────────────┘
                                         ▼
                           ┌────────────────────────────┐
                           │  AUDITOR / GATEKEEPER      │
                           │  @code-reviewer Agent      │
                           │ (Audit Diff -> group-5)    │
                           └────────────────────────────┘
```

### Bảng Phân Quyền Vai Trò Chi Tiết:

| Role ID | Tên Vai Trò | Phạm Vi Quyền Ghi (Write Permissions) | Thư Mục Bị Cấm Sửa (Read-Only Zone) | Trách Nhiệm Chính |
| :--- | :--- | :--- | :--- | :--- |
| **`@architect`** | System Architect (Quang) | `RoomToRentRadar.Core/`, `RoomToRentRadar.Infrastructure/Persistence/`, `RoomToRentRadar.Api/Program.cs`, `MauiProgram.cs`, Solution | Không can thiệp vào chi tiết View XAML hay logic riêng của từng Task | Thiết kế Domain Schema (`RoomListing`, `UserAccount`...), Interfaces (`Abstractions/*`), SQLite schema & seed data (`RentalDatabaseService`), Route Constants, Composition Root (`MauiProgram.cs` & `Api/Program.cs`). |
| **`@developer`** | Feature Developer | Các file thuộc đúng **Mã Task (`T01.1` $\rightarrow$ `T13.2`)** được phân định trong `scrum-task-routing-and-git-branching` (gồm `Infrastructure`, `Api/Controllers/<Feature>Controller.cs`, `Api/Models/<Feature>Models.cs`, `Features/<Feature>/`, `<Feature>FeatureModule.cs` và client HTTP `RoomToRentRadar/Services/Api/`) | 🔒 `RoomToRentRadar.Core/*` (trừ `T10.1`/`T11.2`), `RoomToRentRadar.Infrastructure/Persistence/*`, `RoomToRentRadar.Api/Program.cs`, `RoomToRentRadar/MauiProgram.cs`, Test files cũ | Hiện thực hóa Task được giao (Repository/Service + MVC Controller, hoặc UI XAML + PageModel + client HTTP); tự đăng ký DI/Route trong `<Feature>FeatureModule.cs`. |
| **`@qa-tester`** | Test Engineer | `RoomToRentRadarUnitTests/`, `RoomToRentRadarUIAppiumTests/` | 🔒 Toàn bộ mã nguồn `Core`, `Infrastructure`, `Api`, `RoomToRentRadar` | Viết bài test mới, kiểm thử regression; **cấm sửa code app để làm test pass**. |
| **`@code-reviewer`**| Code Auditor | 🔒 **READ-ONLY TOÀN BỘ REPO** | Không được sửa bất kỳ file nào | Soát Git Diff của nhánh `feature/group-5/Txx.x-...` so với `group-5`, kiểm tra vi phạm ranh giới, báo cáo PASS/REJECT cho Scrum Master. |
| **`@devops`** | DevOps & Build | `.csproj`, `.slnx`, `Directory.Build.props`, `.github/`, `.githooks/`, `RoomToRentRadar.Api/Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.gitignore` | Code nghiệp vụ logic | Sửa lỗi cấu hình build đa nền tảng (Windows & Android), Docker, CI/CD, Git hooks. |

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
   - Sau khi push lên `origin feature/group-5/<TaskID>-...`, tạo Pull Request hướng về base branch **`group-5`** để Scrum Master (`@quangtldc0301`) kiểm tra và bấm Merge.
   - Khi merge `group-5` vào nhánh Task, hook `pre-commit` có thể chặn nhầm commit merge vì thấy file khóa đổi theo `group-5`. Chỉ khi đã kiểm tra các file khóa **giống hệt** `group-5`, mới dùng `$env:ALLOW_ARCHITECT_EDIT = "1"` cho đúng lần commit merge đó (không phải `--no-verify`).

---

## 3. Kiến Trúc 4 Project Chuẩn SOLID & Bản Đồ Trách Nhiệm Thư Mục

Dự án được tách thành **4 tầng độc lập** (+ 2 project kiểm thử) theo nguyên tắc Clean Architecture & SOLID. **Backend** (`Core` + `Infrastructure` + `Api`) chạy trong Docker; **Frontend** (`RoomToRentRadar` MAUI) chỉ gọi backend qua HTTP (xem mục 4).

| Tầng / Khu vực | Trách nhiệm | Mức độ hạn chế | Role Phụ Trách |
| :--- | :--- | :--- | :--- |
| **`RoomToRentRadar.Core/`** | **Domain & Contracts thuần .NET 10** (không phụ thuộc MAUI hay SQLite):<br/>- `Models/`: `RoomListing`, `UserAccount`, `FavoriteRoom`, `RoomFilterCriteria`...<br/>- `Abstractions/<Feature>/`: Mỗi interface 1 file riêng (ISP) — `IAuthService`, `IRoomListingRepository`, `IFavoriteRepository`, `ISavedSearchAlertRepository`, `IHouseRuleRepository`, `IListingReportRepository`<br/>- `Services/ComparisonBasketService.cs`, `Constants.cs` | 🔒 **RESTRICTED** | `@architect` |
| **`RoomToRentRadar.Infrastructure/`** | **Data & Services thực thi (.NET 10, chạy trong backend, test được bằng Unit Test):**<br/>- `Persistence/`: `RentalDatabaseService.cs`, `DatabaseSchema.cs`, `Seed/IctuSeedData.cs`, `DatabaseOptions.cs` (🔒 khóa)<br/>- `Fakes/`: Các bản in-memory (`InMemoryRoomListingRepository`...) đăng ký sẵn qua `TryAddSingleton` để chạy được khi Task chưa merge<br/>- `Repositories/` & `Services/`: Nơi các Task Backend (`T01.1`, `T02.1`, `T03.1`, `T04.1`, `T05.1`, `T07.1`, `T08.1`, `T09.2`, `T12.1`, `T13.1`) viết code SQLite | 🟢 **TASK-SCOPED** (trừ `Persistence/` là 🔒) | `@developer` |
| **`RoomToRentRadar.Api/` (Web API — mô hình MVC)** | **ASP.NET Core Web API kết nối SQLite (`Infrastructure`), chạy trong Docker cổng 8080:**<br/>- `Models/<Feature>Models.cs` (**M**): Request/Response DTO, kiểm tra đầu vào bằng DataAnnotations (gắn trên tham số `record`, không dùng `[property: ...]`)<br/>- `Controllers/<Feature>Controller.cs` (**C**): `[ApiController]`, mỗi Task Backend sở hữu Controller của mình; dữ liệu JSON trả về là phần hiển thị (**V**). Mẫu chuẩn: `Controllers/AuthController.cs`<br/>- `Endpoints/*Endpoints.cs`: Minimal API **cũ**, đang chuyển dần sang Controller — **không thêm route mới vào đây**<br/>- `Auth/`: JWT (`JwtTokenService`, `JwtAuthExtensions`)<br/>- `Program.cs`: 🔒 Đã bật sẵn `AddControllers()` / `MapControllers()` và JWT — **Developer KHÔNG sửa `Program.cs`** | 🟢 **TASK-SCOPED** (`Program.cs` là 🔒) | `@developer` |
| **`RoomToRentRadar/` (MAUI App — Frontend)** | **Giao diện Android/Windows & Composition Root:**<br/>- `Features/<Feature>/`: Chứa `Page.xaml(.cs)`, `PageModel.cs` và `<Feature>FeatureModule.cs` của từng tính năng (`Auth`, `Rooms`, `Map`, `Favorites`, `Alerts`, `Landlord`, `Comparison`, `Reports`, `Home`)<br/>- `Services/Api/`: Client HTTP gọi backend (`Api<Feature>...` cài đặt interface trong `Core`)<br/>- `Pages/Controls/`: Các Custom Controls dùng chung theo Task (`RoomCardView`, `TravelTimeCardView`, `MapPinPreviewCard`)<br/>- `MauiProgram.cs`: 🔒 Đã gọi sẵn tất cả `<Feature>FeatureModule` — **Developer KHÔNG sửa `MauiProgram.cs`** | 🟢 **TASK-SCOPED** (`MauiProgram.cs` là 🔒) | `@developer` |
| **`RoomToRentRadarUnitTests/`** | Kiểm thử đơn vị & hợp đồng API/SQLite cho `Core` + `Infrastructure` + `Api` | 🟢 **MANDATORY** | `@qa-tester`, `@developer` |

---

## 4. Tách Backend (Docker) & Frontend (MAUI gọi HTTP)

1. **Backend chạy bằng Docker** (cấu hình ở gốc repo):
   ```powershell
   Copy-Item .env.example .env      # một lần; điền JWT_SIGNING_KEY (>= 32 ký tự). KHÔNG commit .env
   docker compose up --build -d     # API tại http://localhost:8080 — kiểm tra /api/health
   docker compose down              # dừng (thêm -v để xóa cả database volume)
   ```
   - Không có Docker: `$env:ASPNETCORE_ENVIRONMENT = "Development"; dotnet run --project RoomToRentRadar.Api --urls http://localhost:8080`.
   - Image chỉ chứa `Core` + `Infrastructure` + `Api`; SQLite lưu trong volume `api-data`.
2. **Frontend MAUI chỉ gọi backend qua HTTP/JSON:**
   - Địa chỉ API: Windows `http://localhost:8080`, Android Emulator `http://10.0.2.2:8080`, điện thoại thật `http://<IP máy tính>:8080`.
   - Token JWT lưu bằng `SecureStorage` và gửi kèm header `Authorization: Bearer <token>` (US01 AC3).
   - **Người làm Task Frontend viết client HTTP cho interface mà màn hình của mình dùng** (ví dụ T01.2 viết `ApiAuthService : IAuthService`, T02.2 viết `ApiRoomListingRepository : IRoomListingRepository`), đặt trong `RoomToRentRadar/Services/Api/` và đăng ký trong `<Feature>FeatureModule.cs` thay cho bản SQLite cục bộ.
   - Giai đoạn chuyển tiếp: project MAUI vẫn tham chiếu `Infrastructure` cho tới khi mọi interface đã có client HTTP; sau đó `@architect` bỏ tham chiếu này.
3. **Xác định người gọi trong Web API:** luôn đọc từ JWT (`AuthController.TryGetUserId(User, out var id)` và `[Authorize]`), **không bao giờ dùng `IAuthService.CurrentUser`** trong backend (biến này dùng chung cho mọi request).

---

## 5. Nguyên Tắc SOLID Bắt Buộc Trong Dự Án

1. **Single Responsibility (SRP):**
   - Mỗi class/file chỉ có 1 lý do để thay đổi: Schema (`DatabaseSchema.cs`), Seed (`IctuSeedData.cs`), Connection (`RentalDatabaseService.cs`) tách rời hoàn toàn.
   - `RoomListingRepository` được chia thành 4 file `partial` theo đúng 4 Task (`RoomListingRepository.Listing.cs` cho `T02.1`, `.Detail.cs` cho `T03.1`, `.Search.cs` cho `T05.1`, `.Publish.cs` cho `T09.2`); mỗi Task Backend có Controller riêng để 2 người làm song song không bao giờ chạm vào cùng 1 file.
2. **Open/Closed (OCP) — Không Sửa `MauiProgram.cs` và `RoomToRentRadar.Api/Program.cs`:**
   - Khi cần đăng ký DI hoặc Route cho Task của mình, Developer mở đúng file `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs` của tính năng đó.
   - Khi cần thêm HTTP route cho Task Backend, Developer tạo/sửa đúng `RoomToRentRadar.Api/Controllers/<Feature>Controller.cs` của Task mình (`MapControllers()` tự nhận Controller mới). **Tuyệt đối không sửa `MauiProgram.cs` hay `RoomToRentRadar.Api/Program.cs`**; nếu bắt buộc phải đăng ký service mới cho Api, báo `@architect`.
3. **Interface Segregation (ISP):**
   - Mỗi interface hợp đồng nằm ở 1 file riêng biệt trong `RoomToRentRadar.Core/Abstractions/<Feature>/`.
4. **Dependency Inversion (DIP) — Cấm Service Locator:**
   - Mọi `PageModel` và `Controller` nhận trực tiếp Interface (`IRoomListingRepository`, `IAuthService`, `IFavoriteRepository`...) qua **Constructor Injection**.
   - Nhờ `InfrastructureModule` đã đăng ký sẵn các bản `InMemory*` bằng `TryAddSingleton`, mọi nơi luôn có dữ liệu mẫu chuẩn ICTU ngay lập tức mà không cần viết `IServiceProvider.GetService` hay nhét mock data vào trong `PageModel`. Khi bản thật được đăng ký bằng `AddSingleton<...>()`, nó tự động thay thế bản Fake.

---

## 6. Quy Trình Kiểm Thử & Tự Đánh Giá (Agent Pre-flight Checklist)

Trước khi thông báo hoàn tất bất kỳ Task nào (`T01.1` $\rightarrow$ `T13.2`), Agent PHẢI chạy:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild
```
Và xác nhận đủ các mục sau:
- [ ] Nhánh hiện tại (`git branch --show-current`) là `feature/group-5/Txx.x-...` (KHÔNG phải `main` hay `group-5`).
- [ ] Lệnh `dotnet build RoomToRentRadar.slnx` thành công với `0 Error(s)`.
- [ ] Lệnh `dotnet test RoomToRentRadarUnitTests` pass 100%.
- [ ] **Task Backend:** `docker compose up --build -d` chạy được và các route của Task trả đúng mã HTTP (thử bằng `RoomToRentRadar.Api/RoomToRentRadar.Api.http`).
- [ ] Kiểm tra `git status` & `git diff --stat group-5`: chỉ chứa các file thuộc Mã Task được giao, không chạm vào `RoomToRentRadar.Core/`, `Persistence/`, `Api/Program.cs` hay `MauiProgram.cs`.

# AI Agent Governance & Team Collaboration Rules (RoomToRentRadar — Group 5)

Quy chuẩn tối cao dành cho các AI Agent (Antigravity, Cursor, Claude Code, Copilot, Aider, v.v.) hoạt động trên repository **RoomToRentRadar** của **Group 5 (ICTU)**. Mọi Agent khi thực hiện task PHẢI tuân thủ nghiêm ngặt các nguyên tắc sau để đảm bảo không đá code, không phá vỡ kiến trúc, không hỏng Database chung và ghép nối hoàn hảo khi Merge về nhánh `group-5`.

---

## 1. Hệ Thống Phân Vai Role-Based Matrix (Bắt Buộc Khai Báo Vai Trò)

Mỗi Agent khi bắt đầu làm việc PHẢI hoạt động dưới một **Role cụ thể** được Developer chỉ định. Nếu Developer không chỉ định, Agent mặc định nhận role **`@developer`** và phải tự động kiểm tra nhánh Git hiện tại (`git branch --show-current`) để tra cứu mã Task (`T01.1` $\rightarrow$ `T13.2`) trong skill `scrum-task-routing-and-git-branching`.

```
                           ┌────────────────────────┐
                           │      TECH LEAD /       │
                           │   @architect Agent     │
                           └───────────┬────────────┘
                                       │ (Khóa Contracts: Models, RentalDatabaseService, Constants)
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
| **`@architect`** | System Architect | `Models/`, `Data/`, `Services/`, Solution, Interfaces | Không can thiệp vào chi tiết View XAML | Thiết kế Domain Schema (`RoomListing`, `UserAccount`...), SQLite schema & seed data (`RentalDatabaseService`), Route Constants, DI policies. |
| **`@developer`** | Feature Developer | Các file thuộc đúng **Mã Task (`T01.1` $\rightarrow$ `T13.2`)** được phân định trong `scrum-task-routing-and-git-branching` | 🔒 `Models/*`, `Data/RentalDatabaseService.cs`, `Data/Constants.cs`, Test files cũ | Hiện thực hóa Task được giao (Service/Repository hoặc UI XAML + PageModel); chỉ đăng ký nối thêm (append-only) DI/Route mới vào `MauiProgram.cs` & `AppShell`. |
| **`@qa-tester`** | Test Engineer | `RoomToRentRadarUnitTests/`, `RoomToRentRadarUIAppiumTests/` | 🔒 Toàn bộ mã nguồn ứng dụng `RoomToRentRadar/` | Viết bài test mới, kiểm thử regression; **cấm sửa code app để làm test pass**. |
| **`@code-reviewer`**| Code Auditor | 🔒 **READ-ONLY TOÀN BỘ REPO** | Không được sửa bất kỳ file nào | Soát Git Diff của nhánh `group-5/Txx.x-...` so với `group-5`, kiểm tra vi phạm ranh giới, báo cáo PASS/REJECT cho Scrum Master. |
| **`@devops`** | DevOps & Build | `.csproj`, `.slnx`, `.gitignore`, CI/CD, Workload targets | Code nghiệp vụ logic | Sửa lỗi cấu hình build đa nền tảng (Windows & Android), quản lý NuGet an toàn. |

---

## 2. Quy Tắc Git & Phân Nhánh Của Group 5 (Bắt Buộc Tuân Thủ)

1. **CẤM TUYỆT ĐỐI Nhánh `main`:**
   - **KHÔNG BAO GIỜ** commit, push hoặc merge vào nhánh `main`.
   - Nhánh tích hợp chính của nhóm là **`group-5`**.
2. **Mỗi Task (`T01.1` $\rightarrow$ `T13.2`) là một Nhánh Con riêng biệt:**
   - Khi thực hiện bất kỳ Task nào trong kế hoạch Sprint 1, phải tạo nhánh con từ `group-5` theo cú pháp:
     `feature/group-5/<TaskID>-<ten-ngan-gon>` (Ví dụ: `feature/group-5/T01.1-auth-api`, `feature/group-5/T01.2-login-ui`, `feature/group-5/T02.2-listing-ui`).
   - Khi commit code, đặt tên commit rõ ràng có `group-5` và mã Task:
     `feat(group-5/<TaskID>): <mo-ta-ngan-gon>` (Ví dụ: `feat(group-5/T01.1): build auth service and user repository`).
   - Sau khi hoàn thành và vượt qua bài test, chỉ push lên `origin feature/group-5/<TaskID>-...` và tạo Pull Request hướng về base branch là **`group-5`**.
3. **Đọc Skill Định Tuyến Task Trước Khi Code:**
   - Mọi Agent khi nhận yêu cầu làm Task từ thành viên nhóm PHẢI đọc `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md` để biết chính xác file nào thuộc Task đó và sử dụng đúng các Model / Route Constants đã thống nhất.

---

## 3. Nguyên Tắc Bất Di Bất Dịch (Zero-Tolerance Rules)

1. **Strict Bounded Context (Ranh giới phạm vi nghiêm ngặt):**
   - Chỉ được phép đọc/ghi các file nằm trong phạm vi Mã Task và vai trò được phân định.
   - **Shared Core Database & Models là Read-Only:** Các file trong `RoomToRentRadar/Models/`, `RoomToRentRadar/Data/RentalDatabaseService.cs`, và `RoomToRentRadar/Data/Constants.cs` là **Vùng Cấm** đối với `@developer` trừ khi có chỉ đạo từ `@architect`.
2. **Contract Immutability (Bất biến Interface, Models & Routes):**
   - KHÔNG ĐƯỢC tự ý đổi tên thuộc tính, đổi kiểu dữ liệu, hoặc xóa method công khai trong `Models/`, `Data/`, và các Service.
   - Khi chuyển trang giữa các màn hình, BẮT BUỘC sử dụng các hằng số định sẵn trong `Constants.Routes` (ví dụ: `Constants.Routes.RoomDetail` kèm tham số `?roomId=...`) để các màn hình do các thành viên làm song song tự động khớp nhau 100% khi Merge.
3. **Regression Immunity (Bảo toàn tính năng cũ):**
   - TUYỆT ĐỐI KHÔNG được comment-out, xóa, hoặc sửa đổi kết quả kỳ vọng của các bài test hiện có trong `RoomToRentRadarUnitTests` hoặc xóa các file mẫu gốc đang được test tham chiếu.
   - Nếu code mới làm fail test cũ: **Code mới sai, không phải test cũ sai**. Agent phải tự sửa code của mình.
4. **No Code Splatter & Conflict-Free Registration:**
   - Khi đăng ký thêm DI hoặc Route vào `MauiProgram.cs` / `AppShell.xaml.cs`, chỉ được phép **chèn thêm dòng mới vào đúng phân vùng**, tuyệt đối không format lại toàn bộ file hay sửa dòng của thành viên khác để tránh gây Merge Conflict.
5. **No Unauthorized Dependencies:**
   - Không được tự ý cài thêm NuGet package mới vào `.csproj` mà không được sự chấp thuận của `@architect`.

---

## 4. Bản Đồ Trách Nhiệm Thư Mục (Codebase Boundary Map)

| Khu vực | Trách nhiệm | Mức độ hạn chế | Role Phụ Trách Chính |
| :--- | :--- | :--- | :--- |
| `RoomToRentRadar/Models/` | DTOs, Entities, Domain Models (`RoomListing`, `UserAccount`, `FavoriteRoom`...) | 🔒 **RESTRICTED** | `@architect` |
| `RoomToRentRadar/Data/RentalDatabaseService.cs` | Khởi tạo toàn bộ bảng SQLite & Seed dữ liệu phòng trọ mẫu quanh ICTU | 🔒 **RESTRICTED** | `@architect` |
| `RoomToRentRadar/Data/Constants.cs` | Database path, Tọa độ trường ICTU, Hằng số tên đường dẫn (`Constants.Routes`) | 🔒 **RESTRICTED** | `@architect` |
| `RoomToRentRadar/Data/*Repository.cs` | Các Repository truy vấn dữ liệu từng phần theo Task (`T01.1`, `T02.1`, `T07.1`...) | 🟢 **TASK-SCOPED** | `@developer` (theo bảng phân công Task) |
| `RoomToRentRadar/Services/` | Các Business Services theo Task (`AuthService`, `TravelTimeService`...) | 🟢 **TASK-SCOPED** | `@developer` (theo bảng phân công Task) |
| `RoomToRentRadar/PageModels/` | ViewModels (CommunityToolkit.Mvvm) | 🟢 **TASK-SCOPED** | `@developer` (theo bảng phân công Task) |
| `RoomToRentRadar/Pages/` | Views XAML & Code-behind | 🟢 **TASK-SCOPED** | `@developer` (theo bảng phân công Task) |
| `RoomToRentRadar/MauiProgram.cs` | DI Registration, Handlers, App Config | ⚠️ **APPEND-ONLY** | `@architect`, `@developer` (chỉ nối thêm dòng đăng ký mới) |
| `RoomToRentRadarUnitTests/` | Unit Tests | 🟢 **MANDATORY** | `@qa-tester`, `@developer` |

---

## 5. Tiêu Chuẩn Kỹ Thuật Cho .NET MAUI & C#

1. **Kiến trúc MVVM (CommunityToolkit.Mvvm):**
   - Toàn bộ logic nghiệp vụ và dữ liệu nằm ở `PageModel`, sử dụng source generators: `[ObservableProperty]`, `[RelayCommand]`.
   - Code-behind (`.xaml.cs`) chỉ chứa các tương tác UI thuần túy và gán `BindingContext`, không chứa business logic hay gọi database trực tiếp.
   - Mọi màn hình nhận tham số điều hướng (như `roomId`) phải có giá trị fallback mặc định (ví dụ `RoomId = 1`) khi chạy độc lập để thành viên làm màn hình đó có thể test ngay với dữ liệu mẫu từ `RentalDatabaseService` mà không cần chờ màn hình trước hoàn thành.
2. **Luồng & Threading Safety:**
   - Các tác vụ I/O, Database SQLite phải là `async/await`.
   - Mọi thao tác cập nhật UI hoặc `ObservableCollection` phải được thực hiện an toàn trên Main Thread.
3. **Quản lý tài nguyên & Exception Handling:**
   - Không bao giờ dùng empty catch block `catch (Exception) { }` để giấu lỗi.
   - Phải có logging và thông báo lỗi thân thiện cho người dùng.

---

## 6. Quy Trình Kiểm Thử & Tự Đánh Giá (Agent Pre-flight Checklist)

Trước khi thông báo hoàn tất bất kỳ Task nào (`T01.1` $\rightarrow$ `T13.2`), Agent PHẢI tự chạy checklist sau:
- [ ] Kiểm tra nhánh hiện tại: `git branch --show-current` (Đảm bảo đang ở nhánh `group-5/Txx.x-...`, KHÔNG phải `main` hay `group-5` gốc).
- [ ] Lệnh build thành công: `dotnet build RoomToRentRadar.slnx` không có lỗi biên dịch (`0 Error(s)`).
- [ ] Toàn bộ unit tests pass: `$env:DOTNET_ROLL_FORWARD = "Major"; dotnet test RoomToRentRadarUnitTests`.
- [ ] Git diff kiểm tra (`git status` & `git diff --stat`):
  - Chỉ chứa các file liên quan trực tiếp đến Mã Task được giao.
  - Không có file trong `Models/` hoặc `RentalDatabaseService.cs` bị sửa trái phép.

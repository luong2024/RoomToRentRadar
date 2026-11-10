# Role-Based Operation Guide for RoomToRentRadar AI Agents (Group 5)

Tài liệu hướng dẫn kích hoạt, vận hành và phối hợp giữa các vai trò (Role-Based AI Agents) trong dự án **RoomToRentRadar — Group 5 (ICTU)**. Mọi thành viên trong nhóm khi giao task cho AI Coding Agent chỉ cần copy mẫu Prompt tương ứng bên dưới để Agent tự động kích hoạt hệ thống ranh giới, nhánh Git và quyền hạn.

---

## 1. Danh Mục Các Vai Trò (Agent Personas & Roles)

### 👑 Role 1: `@architect` (System Architect & Tech Lead Agent — Phụ trách: Quang / Đức)
- **Mục tiêu:** Thiết kế nền tảng dữ liệu (`Models/`), khởi tạo bảng SQLite & dữ liệu mẫu (`RentalDatabaseService.cs`), định nghĩa các hằng số kết nối (`Constants.cs`) đầu mỗi Sprint.
- **Quyền hạn ghi:**
  - `RoomToRentRadar/Models/*` (DTOs, Entities, Domain Models)
  - `RoomToRentRadar/Data/RentalDatabaseService.cs`, `RoomToRentRadar/Data/Constants.cs`
  - `RoomToRentRadar/Services/*` (Shared Service Interfaces)
- **Quy tắc vàng:**
  - Chỉ mở khóa vào đầu mỗi Sprint để xây dựng Khung Xương Chung rồi đẩy lên nhánh `group-5`.
  - Đảm bảo tính tương thích ngược (Backward-Compatibility) 100%, không làm hỏng bài test cũ.

> **Prompt mẫu kích hoạt `@architect`:**
> *"Đóng vai `@architect`. Hãy kiểm tra hoặc mở rộng Model và bảng SQLite trong `RentalDatabaseService.cs` cho Sprint hiện tại. Tuân thủ nghiêm ngặt `AGENTS.md` và `contract-first-evolution`."*

---

### 💻 Role 2: `@developer` (Feature Developer Agent — Dành cho Quang, Hiếu, Thùy, Nam khi làm Task `T01.1` $\rightarrow$ `T13.2`)
- **Mục tiêu:** Thực hiện đúng **1 Mã Task** được phân công trong `06. Project Plan - Group 5` trên nhánh Git riêng (`feature/group-5/Txx.x-...`).
- **Quyền hạn ghi:**
  - Đúng các file thuộc Mã Task được quy định trong `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md`.
  - Chỉ nối thêm (append-only) dòng đăng ký DI/Route của Task mình vào `RoomToRentRadar/MauiProgram.cs` và `AppShell.xaml(.cs)`.
- **Vùng cấm (Read-Only):**
  - 🔒 `RoomToRentRadar/Models/*`
  - 🔒 `RoomToRentRadar/Data/RentalDatabaseService.cs` & `RoomToRentRadar/Data/Constants.cs`
  - 🔒 Các file của Task khác và các bài test cũ trong `RoomToRentRadarUnitTests/`
- **Quy tắc vàng:**
  - Trước khi viết code, phải đọc `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md` để xác định đúng file và đúng Route/Model dùng chung.
  - Nếu làm màn hình nhận tham số (như `roomId`), phải có giá trị mặc định `roomId = 1` để test độc lập ngay với dữ liệu mẫu trong `RentalDatabaseService` mà không cần chờ thành viên khác làm xong màn hình trước.

> **📋 PROMPT MẪU CHUẨN CHO 4 BẠN DEV (COPY DÁN VÀO AI AGENT KHI BẮT ĐẦU MỖI TASK):**
> *"Đóng vai `@developer`. Tôi cần thực hiện **Task [Điền mã Task, ví dụ: T02.2]** thuộc Sprint 1 của Group 5.
> Hãy thực hiện theo đúng quy trình bắt buộc sau:
> 1. Đọc `AGENTS.md` và `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md` để xác định phạm vi file được phép tạo/sửa của Task này và các tiêu chí Acceptance Criteria.
> 2. Kiểm tra nhánh Git hiện tại (nếu chưa ở nhánh `feature/group-5/<TaskID>-...` thì tạo nhánh con từ `group-5` theo chuẩn `feature/group-5/<TaskID>-<short-name>`, tuyệt đối không đụng vào nhánh `main`).
> 3. Sử dụng các Models trong `RoomToRentRadar/Models/`, dữ liệu SQLite từ `RentalDatabaseService` và đường dẫn từ `Constants.Routes`. Tuyệt đối KHÔNG sửa đổi các file trong `Models/` hoặc `RentalDatabaseService.cs`.
> 4. Sau khi code xong, hãy chạy `dotnet build RoomToRentRadar.slnx` và `$env:DOTNET_ROLL_FORWARD = 'Major'; dotnet test RoomToRentRadarUnitTests` để đảm bảo 0 lỗi, và đặt tên commit theo chuẩn `feat(group-5/<TaskID>): ...`."*

---

### 🧪 Role 3: `@qa-tester` (QA & Test Automation Agent)
- **Mục tiêu:** Bảo vệ chất lượng code, chống lỗi thoái lui (regression), viết bài kiểm thử đơn vị trong `RoomToRentRadarUnitTests/`.
- **Quyền hạn ghi:**
  - `RoomToRentRadarUnitTests/*`
  - `RoomToRentRadarUIAppiumTests/*`
- **Vùng cấm (Read-Only):**
  - 🔒 Toàn bộ thư mục ứng dụng `RoomToRentRadar/*`.

> **Prompt mẫu kích hoạt `@qa-tester`:**
> *"Đóng vai `@qa-tester`. Hãy viết bộ unit test cho Task hiện tại trong `RoomToRentRadarUnitTests`. Tuân thủ `regression-immunity-testing` và xác nhận lệnh `$env:DOTNET_ROLL_FORWARD = 'Major'; dotnet test RoomToRentRadarUnitTests` pass 100%."*

---

### 🛡️ Role 4: `@code-reviewer` (Auditor & Gatekeeper Agent — Dành cho Scrum Master Đức & Lead Quang khi duyệt Merge vào `group-5`)
- **Mục tiêu:** Rà soát Git Diff của nhánh con `group-5/Txx.x-...` trước khi gộp (Merge) vào nhánh `group-5`.
- **Quyền hạn:** **100% READ-ONLY TOÀN BỘ REPOSITORY**.
- **Nhiệm vụ rà soát:**
  1. *Scope Bleed:* Nhánh này có sửa nhầm file của thành viên khác hoặc sửa trái phép `Models/` / `RentalDatabaseService.cs` không?
  2. *Branch Check:* Có đang trỏ đúng về `group-5` (không phải `main`) không?
  3. *Build & Test:* `dotnet build` và `dotnet test` có pass 100% không?
- **Đầu ra:** Bản đánh giá kèm phán quyết: **`APPROVED (Cho phép Merge vào group-5)`** hoặc **`REJECTED`**.

> **Prompt mẫu kích hoạt `@code-reviewer` (Dành cho Scrum Master Đức):**
> *"Đóng vai `@code-reviewer`. Hãy chạy `git status` và `git diff group-5` để kiểm toán toàn bộ thay đổi của nhánh task hiện tại. Đưa ra báo cáo PASS/REJECT theo tiêu chuẩn `AGENTS.md`."*

---

### ⚙️ Role 5: `@devops` (DevOps & Build Specialist Agent)
- **Mục tiêu:** Quản lý môi trường build, sửa lỗi MSBuild, đa nền tảng (Windows & Android).
- **Quyền hạn ghi:**
  - `RoomToRentRadar.slnx`, `RoomToRentRadar.csproj`, `RoomToRentRadarUnitTests.csproj`, `.gitignore`.

# Role-Based Operation Guide for RoomToRentRadar AI Agents (Group 5)

Tài liệu hướng dẫn kích hoạt, vận hành và phối hợp giữa các vai trò (Role-Based AI Agents) trong dự án **RoomToRentRadar — Group 5 (ICTU)**.
- **Product Owner:** Vũ Huy Lượng
- **Scrum Master kiêm Tech Lead / System Architect / Gatekeeper:** Nguyễn Đăng Quang (`@quangtldc0301`)

Kiến trúc: **Backend** (`Core` + `Infrastructure` + `Api` theo mô hình MVC) chạy trong Docker; **Frontend** (`RoomToRentRadar` MAUI) gọi backend qua HTTP. Chi tiết xem `AGENTS.md` mục 3–4.

---

## 1. Danh Mục Các Vai Trò (Agent Personas & Roles)

### 👑 Role 1: `@architect` (System Architect & Gatekeeper — Phụ trách: Quang)
- **Mục tiêu:** Quản lý Khung Xương Chung 4 tầng (`RoomToRentRadar.Core`, `RoomToRentRadar.Infrastructure/Persistence`, `RoomToRentRadar.Api/Program.cs`, `RoomToRentRadar/MauiProgram.cs`), bảo đảm tính tương thích ngược 100%.
- Duyệt mọi thay đổi vào vùng khóa, cấu hình Docker và việc bỏ tham chiếu `Infrastructure` khỏi app MAUI khi tất cả client HTTP đã sẵn sàng.

---

### 💻 Role 2: `@developer` (Feature Developer Agent — Dành cho Quang, Hiếu, Thùy, Nam khi làm Task `T01.1` $\rightarrow$ `T13.2`)
- **Mục tiêu:** Thực hiện đúng **1 Mã Task** trên nhánh Git riêng (`feature/group-5/Txx.x-...`).
- **Quyền hạn ghi:**
  - Đúng các file thuộc Mã Task được quy định trong `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md`:
    - **Task Backend:** Repository/Service trong `RoomToRentRadar.Infrastructure/`, Controller trong `RoomToRentRadar.Api/Controllers/<Feature>Controller.cs`, DTO trong `RoomToRentRadar.Api/Models/<Feature>Models.cs`.
    - **Task Frontend:** Page/PageModel trong `RoomToRentRadar/Features/<Feature>/`, client HTTP cho interface mình dùng trong `RoomToRentRadar/Services/Api/`.
  - Đăng ký DI & Shell Route trong `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs`.
- **Vùng cấm (Read-Only):**
  - 🔒 `RoomToRentRadar.Core/*` (trừ `T10.1` & `T11.2` được tạo Service thuần trong `Core/Services/`)
  - 🔒 `RoomToRentRadar.Infrastructure/Persistence/*` (`RentalDatabaseService.cs`, `DatabaseSchema.cs`, `IctuSeedData.cs`)
  - 🔒 `RoomToRentRadar.Api/Program.cs` (Controller mới được `MapControllers()` tự nhận; cần đăng ký service cho Api thì báo `@architect`)
  - 🔒 `RoomToRentRadar/MauiProgram.cs` (chỉ sửa trong `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs`)
  - 🔒 `RoomToRentRadar.Api/Endpoints/*` (Minimal API cũ: không thêm route mới, chỉ chuyển sang Controller)
  - 🔒 Các file của Task khác và các bài test cũ trong `RoomToRentRadarUnitTests/`

> **📋 PROMPT MẪU CHUẨN CHO 4 BẠN DEV (COPY DÁN VÀO AI AGENT KHI BẮT ĐẦU MỖI TASK):**
> *"Đóng vai `@developer`. Tôi cần thực hiện **Task [Điền mã Task, ví dụ: T02.1 hoặc T02.2]** thuộc Sprint 1 của Group 5.
> Hãy thực hiện theo đúng quy trình bắt buộc sau:
> 1. Đọc `AGENTS.md`, `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md` và User Story/AC của Task trong `docs/backlog.md` để xác định phạm vi file được phép tạo/sửa.
> 2. Kiểm tra nhánh Git hiện tại (nếu chưa ở nhánh `feature/group-5/<TaskID>-...` thì tạo nhánh con từ `group-5` theo chuẩn `feature/group-5/<TaskID>-<short-name>`, tuyệt đối không commit lên `main` hay `group-5`).
> 3. Nếu là Task Backend: viết logic SQLite trong `RoomToRentRadar.Infrastructure`, viết Controller MVC trong `RoomToRentRadar.Api/Controllers/<Feature>Controller.cs` (mẫu: `AuthController.cs`) với DTO trong `Api/Models/`, lấy người gọi từ JWT (`[Authorize]`, `AuthController.TryGetUserId`), rồi chạy thử bằng `docker compose up --build -d`. Nếu là Task UI: inject trực tiếp Interface từ `RoomToRentRadar.Core` qua Constructor Injection, viết client HTTP cho Interface đó trong `RoomToRentRadar/Services/Api/` (gọi `http://10.0.2.2:8080` trên Android Emulator, `http://localhost:8080` trên Windows) và đăng ký trong `<Feature>FeatureModule.cs`. Tuyệt đối KHÔNG sửa `RoomToRentRadar.Core/Models`, `RentalDatabaseService.cs`, `MauiProgram.cs` hay `RoomToRentRadar.Api/Program.cs`.
> 4. Sau khi code xong, chạy `powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild` để đảm bảo build + unit test pass 100%, và đặt tên commit theo chuẩn `feat(group-5/<TaskID>): ...`."*

---

### 🧪 Role 3: `@qa-tester` (QA & Test Automation Agent)
- **Quyền hạn ghi:** `RoomToRentRadarUnitTests/*`, `RoomToRentRadarUIAppiumTests/*`.
- **Vùng cấm:** Toàn bộ code ứng dụng (`Core`, `Infrastructure`, `Api`, `RoomToRentRadar`).

---

### 🛡️ Role 4: `@code-reviewer` (Auditor & Gatekeeper Agent — Dành cho Scrum Master Quang khi duyệt Merge vào `group-5`)
- **Quyền hạn:** **100% READ-ONLY TOÀN BỘ REPOSITORY**.
- **Nhiệm vụ rà soát trước khi `@quangtldc0301` bấm Merge:**
  1. *Scope Bleed:* Nhánh `feature/group-5/Txx.x-...` có sửa nhầm file của thành viên khác hoặc sửa trái phép `RoomToRentRadar.Core/`, `Persistence/`, `Api/Program.cs`, `MauiProgram.cs` không? Có thêm route mới vào `Api/Endpoints/` thay vì Controller không?
  2. *Branch Check:* Base branch của PR có đúng là `group-5` (không phải `main`) không?
  3. *Build & Test:* CI GitHub Actions và `scripts/preflight.ps1 -FullMauiBuild` có pass 100% không? Task Backend có chạy được bằng `docker compose up --build -d` không?
  4. *AC:* Thay đổi có đáp ứng đúng Acceptance Criteria của User Story trong `docs/backlog.md` không?

# Role-Based Operation Guide for RoomToRentRadar AI Agents (Group 5)

Tài liệu hướng dẫn kích hoạt, vận hành và phối hợp giữa các vai trò (Role-Based AI Agents) trong dự án **RoomToRentRadar — Group 5 (ICTU)**.
- **Scrum Master / Gatekeeper:** Đức Nguyễn (`@Duc-Nguyen-Github`)
- **Tech Lead / System Architect:** Nguyễn Đăng Quang (`@quangtldc0301`)

---

## 1. Danh Mục Các Vai Trò (Agent Personas & Roles)

### 👑 Role 1: `@architect` (System Architect & Gatekeeper — Phụ trách: Đức / Quang)
- **Mục tiêu:** Quản lý Khung Xương Chung 3 tầng (`RoomToRentRadar.Core`, `RoomToRentRadar.Infrastructure/Persistence`, `MauiProgram.cs`), bảo đảm tính tương thích ngược 100%.

---

### 💻 Role 2: `@developer` (Feature Developer Agent — Dành cho Quang, Hiếu, Thùy, Nam khi làm Task `T01.1` $\rightarrow$ `T13.2`)
- **Mục tiêu:** Thực hiện đúng **1 Mã Task** trên nhánh Git riêng (`feature/group-5/Txx.x-...`).
- **Quyền hạn ghi:**
  - Đúng các file thuộc Mã Task được quy định trong `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md`.
  - Đăng ký DI & Shell Route trong `RoomToRentRadar/Features/<Feature>/<Feature>FeatureModule.cs`.
- **Vùng cấm (Read-Only):**
  - 🔒 `RoomToRentRadar.Core/*`
  - 🔒 `RoomToRentRadar.Infrastructure/Persistence/*` (`RentalDatabaseService.cs`, `DatabaseSchema.cs`, `IctuSeedData.cs`)
  - 🔒 `RoomToRentRadar/MauiProgram.cs`
  - 🔒 Các file của Task khác và các bài test cũ trong `RoomToRentRadarUnitTests/`

> **📋 PROMPT MẪU CHUẨN CHO 4 BẠN DEV (COPY DÁN VÀO AI AGENT KHI BẮT ĐẦU MỖI TASK):**
> *"Đóng vai `@developer`. Tôi cần thực hiện **Task [Điền mã Task, ví dụ: T02.2]** thuộc Sprint 1 của Group 5.
> Hãy thực hiện theo đúng quy trình bắt buộc sau:
> 1. Đọc `AGENTS.md` và `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md` để xác định phạm vi file được phép tạo/sửa của Task này.
> 2. Kiểm tra nhánh Git hiện tại (nếu chưa ở nhánh `feature/group-5/<TaskID>-...` thì tạo nhánh con từ `group-5` theo chuẩn `feature/group-5/<TaskID>-<short-name>`, tuyệt đối không commit lên `main` hay `group-5`).
> 3. Inject trực tiếp các Interface trong `RoomToRentRadar.Core` (`IRoomListingRepository`, `IAuthService`...) qua Constructor Injection và đăng ký DI/Route trong `Features/<Feature>/<Feature>FeatureModule.cs`. Tuyệt đối KHÔNG sửa `RoomToRentRadar.Core/*`, `RentalDatabaseService.cs` hay `MauiProgram.cs`.
> 4. Sau khi code xong, chạy `powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild` để đảm bảo build + unit test pass 100%, và đặt tên commit theo chuẩn `feat(group-5/<TaskID>): ...`."*

---

### 🧪 Role 3: `@qa-tester` (QA & Test Automation Agent)
- **Quyền hạn ghi:** `RoomToRentRadarUnitTests/*`, `RoomToRentRadarUIAppiumTests/*`.
- **Vùng cấm:** Toàn bộ code ứng dụng (`Core`, `Infrastructure`, `RoomToRentRadar`).

---

### 🛡️ Role 4: `@code-reviewer` (Auditor & Gatekeeper Agent — Dành cho Scrum Master Đức & Lead Quang khi duyệt Merge vào `group-5`)
- **Quyền hạn:** **100% READ-ONLY TOÀN BỘ REPOSITORY**.
- **Nhiệm vụ rà soát trước khi `@Duc-Nguyen-Github` hoặc `@quangtldc0301` bấm Merge:**
  1. *Scope Bleed:* Nhánh `feature/group-5/Txx.x-...` có sửa nhầm file của thành viên khác hoặc sửa trái phép `RoomToRentRadar.Core/`, `Persistence/`, `MauiProgram.cs` không?
  2. *Branch Check:* Base branch của PR có đúng là `group-5` (không phải `main`) không?
  3. *Build & Test:* CI GitHub Actions và `scripts/preflight.ps1 -FullMauiBuild` có pass 100% không?

---
name: boundary-scope-enforcement
description: Enforces strict file-editing boundaries, bounded contexts, and diff containment to prevent AI agents from colliding, stomping shared code, or corrupting parallel features in team development.
---

# Boundary & Scope Enforcement Skill

## Mục đích
Đảm bảo rằng khi nhiều kỹ sư cùng sử dụng AI Agent để code song song, mỗi Agent chỉ tập trung vào đúng phạm vi (Vertical Slice) của task được giao, không tự ý sửa đổi code chung, không tạo merge conflict và không "đá code" của đồng đội.

## Quy Trình Thực Hiện (Strict Step-by-Step)

### Bước 1: Xác định Bounded Context của Task
Trước khi gõ bất kỳ dòng code nào, Agent phải tự xác định danh sách các file/thư mục thuộc "Safe Zone" và "No-Touch Zone" dựa theo `.agent/skills/scrum-task-routing-and-git-branching/SKILL.md`:

- **Safe Zone (Được phép sửa theo đúng Mã Task):**
  - `RoomToRentRadar/Features/<FeatureName>/...` (gồm Page, PageModel và `<FeatureName>FeatureModule.cs`)
  - `RoomToRentRadar.Infrastructure/Repositories/...` hoặc `Services/...` thuộc đúng Mã Task
  - `RoomToRentRadarUnitTests/<FeatureName>Tests.cs`
- **No-Touch Zone (Chỉ đọc, CẤM sửa nếu không có lệnh của `@architect`):**
  - 🔒 `RoomToRentRadar.Core/*` (`Models/`, `Abstractions/`, `Constants.cs`)
  - 🔒 `RoomToRentRadar.Infrastructure/Persistence/*` (`RentalDatabaseService.cs`, `DatabaseSchema.cs`, `IctuSeedData.cs`)
  - 🔒 `RoomToRentRadar/MauiProgram.cs` (đăng ký DI/Route phải đặt trong `<FeatureName>FeatureModule.cs`)
  - 🔒 Cấu hình CI/CD, `.githooks/`, `.csproj`, NuGet packages.

### Bước 2: Quy Tắc "Hỏi Trước Khi Sửa Vùng Chung"
Nếu trong quá trình giải quyết task, Agent nhận thấy **bắt buộc** phải thay đổi một file trong No-Touch Zone:
1. **DỪNG LẠI NGAY LẬP TỨC.**
2. Báo cáo cho Scrum Master (`@Duc-Nguyen-Github`) hoặc Tech Lead (`@quangtldc0301`) lý do tại sao cần sửa file chung.
3. Chỉ thực hiện sau khi có sự chấp thuận.

### Bước 3: Kiểm Soát Git Diff Trước Khi Kết Thúc
Chạy lệnh kiểm tra diff:
```powershell
git status --short
git diff --stat
```
- Đảm bảo không có file nào nằm ngoài phạm vi task.
- Đảm bảo không có thay đổi trong `RoomToRentRadar.Core/`, `Persistence/` hay `MauiProgram.cs`.

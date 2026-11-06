---
name: boundary-scope-enforcement
description: Enforces strict file-editing boundaries, bounded contexts, and diff containment to prevent AI agents from colliding, stomping shared code, or corrupting parallel features in team development.
---

# Boundary & Scope Enforcement Skill

## Mục đích
Đảm bảo rằng khi nhiều kỹ sư cùng sử dụng AI Agent để code song song, mỗi Agent chỉ tập trung vào đúng phạm vi (Vertical Slice) của task được giao, không tự ý sửa đổi code chung, không tạo merge conflict và không "đá code" của đồng đội.

## Khi nào kích hoạt Skill này
- Trước khi bắt đầu bất kỳ task coding, refactoring hoặc sửa lỗi nào.
- Khi Agent chuẩn bị chạm vào các file nằm ngoài thư mục tính năng trực tiếp của task.
- Trong bước review trước khi commit code.

---

## Quy Trình Thực Hiện (Strict Step-by-Step)

### Bước 1: Xác định Bounded Context của Task
Trước khi gõ bất kỳ dòng code nào, Agent phải tự xác định và công bố danh sách các file/thư mục thuộc "Safe Zone" (Vùng an toàn được phép sửa) và "No-Touch Zone" (Vùng cấm).

- **Safe Zone (Được phép sửa):**
  - `RoomToRentRadar/Pages/<FeatureName>/...`
  - `RoomToRentRadar/PageModels/<FeatureName>PageModel.cs`
  - `RoomToRentRadarUnitTests/<FeatureName>Tests.cs`
- **No-Touch Zone (Chỉ đọc, KHÔNG được sửa nếu chưa hỏi):**
  - `RoomToRentRadar/Models/*`
  - `RoomToRentRadar/Data/*`
  - `RoomToRentRadar/AppShell.xaml` / `AppShell.xaml.cs`
  - `RoomToRentRadar/MauiProgram.cs`
  - Cấu hình CI/CD, .csproj, NuGet packages.

### Bước 2: Quy Tắc "Hỏi Trước Khi Sửa Vùng Chung"
Nếu trong quá trình giải quyết task, Agent nhận thấy **bắt buộc** phải thay đổi một file trong No-Touch Zone (ví dụ: cần thêm 1 method vào `ProjectRepository.cs`):
1. **DỪNG LẠI NGAY LẬP TỨC.**
2. Báo cáo cho người dùng lý do tại sao cần sửa file chung.
3. Chỉ thực hiện sau khi có sự chấp thuận, và chỉ được bổ sung (append/overload), không thay đổi code cũ.

### Bước 3: Kiểm Soát Git Diff Trước Khi Kết Thúc
Chạy lệnh kiểm tra diff:
```powershell
git status --short
git diff --stat
```
Kiểm tra từng file đã bị sửa:
- Có file nào nằm ngoài phạm vi task không?
- Có file format/whitespace toàn bộ dự án do tự động format không? (Nếu có: `git checkout -- <file>` để rollback).
- Có file rác (`bin/`, `obj/`, temporary files) chưa được ignore không?

---

## Checklist Bàn Giao
- [ ] Mọi thay đổi đều nằm trong phạm vi feature được giao.
- [ ] Không có breaking change trên các file dùng chung.
- [ ] Git status sạch, không có file thừa.

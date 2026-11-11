---
name: regression-immunity-testing
description: Standardized testing workflow to verify zero regressions across existing unit and UI tests before completing tasks, guaranteeing that existing working features remain fully operational.
---

# Regression Immunity Testing Skill

## Mục đích
Đảm bảo mọi thay đổi do AI Agent thực hiện không làm hỏng bất kỳ chức năng nào đang hoạt động ổn định trong project. Thiết lập quy chuẩn kiểm thử nghiêm ngặt trước khi một task được coi là hoàn tất.

---

## Các Bước Thực Hiện Bắt Buộc

### Bước 1: Chạy Toàn Bộ Test Suite Hiện Có
Chạy lệnh kiểm thử đơn vị cho tầng `Core` và `Infrastructure`:
```powershell
dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj
```
Quan sát kết quả:
- **Nếu 100% Tests Pass:** Tiến hành sang Bước 2.
- **Nếu có Test bị Fail:**
  > ⚠️ **ĐẶC BIỆT LƯU Ý:**
  > 1. Tuyệt đối KHÔNG ĐƯỢC sửa đổi file test cũ (`UnitTest1.cs`, `ArchitectureAndContractsTests.cs`...) để nó pass theo code mới.
  > 2. Tuyệt đối KHÔNG ĐƯỢC xóa bài test cũ hoặc gắn attribute `[Ignore]`.
  > 3. Phân tích và sửa lại code của Task đang làm để tương thích hoàn toàn với hợp đồng chung.

### Bước 2: Viết Thêm Test Cho Tính Năng Mới
Mỗi task mới có thể bổ sung file test riêng `<TaskName>Tests.cs` vào dự án `RoomToRentRadarUnitTests` (ví dụ `RoomComparisonTests.cs` cho `T11.2`):
1. **Happy Path:** Kiểm tra trường hợp dữ liệu hợp lệ hoạt động đúng kỳ vọng.
2. **Edge Cases:** Kiểm tra dữ liệu rỗng (`null`, `empty string`), danh sách trống, giá trị biên.
3. **SQLite Temp DB / In-Memory Fakes:** Có thể khởi tạo `new RentalDatabaseService(NullLogger<RentalDatabaseService>.Instance, $"Data Source={tempFile}")` để kiểm thử truy vấn SQLite thực tế.

### Bước 3: Build Toàn Giải Pháp (Full Solution Verification)
Chạy script kiểm tra tổng thể trước khi commit/push:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild
```

---

## Tiêu Chuẩn Nghiệm Thu (Definition of Done)
- [ ] Build thành công không có lỗi biên dịch: `0 Error(s)`.
- [ ] Tất cả các test cũ tiếp tục PASS: `Failed: 0`.
- [ ] Có bài test mới được bổ sung cho logic nghiệp vụ mới (nếu thuộc phạm vi task).

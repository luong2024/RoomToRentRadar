## Thông tin Task (Group 5 — Sprint 1)

- **Mã Task:** `Txx.x` (ví dụ: `T02.2`)
- **User Story:** `USxx`
- **Thành viên thực hiện:** 
- **Nhánh nguồn:** `feature/group-5/Txx.x-...` $\rightarrow$ **Nhánh đích bắt buộc:** `group-5` (TUYỆT ĐỐI KHÔNG chọn `main`)

---

## Checklist Bắt Buộc Trước Khi Gửi PR (Developer & AI Agent)

- [ ] **Đứng đúng nhánh:** `git branch --show-current` có dạng `feature/group-5/Txx.x-...`
- [ ] **Đúng phạm vi file (Strict Bounded Context):** `git diff --stat origin/group-5` chỉ chứa các file thuộc Task ID này, KHÔNG sửa `RoomToRentRadar.Core/` hay `RentalDatabaseService.cs`
- [ ] **Không sửa `MauiProgram.cs`:** Đăng ký DI & Route trong `Features/<Feature>/<Feature>FeatureModule.cs`
- [ ] **Build thành công 0 lỗi:** `dotnet build RoomToRentRadar.slnx`
- [ ] **Toàn bộ Unit Tests PASS:** `dotnet test RoomToRentRadarUnitTests`
- [ ] **Đã chạy thử thực tế màn hình/chức năng** trên máy (đính kèm ảnh chụp màn hình bên dưới nếu là Task UI)

## Bằng chứng chạy thử (Screenshot / Log)

*(Dán ảnh màn hình hoặc kết quả chạy thử vào đây trước khi nhờ `@Duc-Nguyen-Github` hoặc `@quangtldc0301` bấm Merge)*

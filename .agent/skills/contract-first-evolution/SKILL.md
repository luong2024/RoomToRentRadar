---
name: contract-first-evolution
description: Guidelines and patterns for evolving C# Domain Models, Repositories, Database entities, and Service Interfaces safely without breaking existing features or concurrent agent tasks.
---

# Contract-First Evolution Skill

## Mục đích
Ngăn chặn hiện tượng Agent tự tiện thay đổi model dữ liệu, chữ ký hàm (method signatures), hay database schema làm crash các màn hình khác hoặc phá vỡ các task đang chạy song song của thành viên khác trong team.

## Khi nào kích hoạt Skill này
- Khi một task cần thêm trường mới vào Model (`RoomToRentRadar/Models`).
- Khi cần mở rộng Repository / Service API (`RoomToRentRadar/Data` hoặc `RoomToRentRadar/Services`).
- Khi phát hiện cần thay đổi kiểu dữ liệu (Type change).

---

## Các Quy Tắc Bảo Vệ Hợp Đồng Dữ Liệu (Contract Rules)

### 1. Quy tắc Mở rộng, không Phá vỡ (Add, Never Break)
- **Được phép:** Thêm thuộc tính mới có giá trị mặc định hoặc cho phép null:
  ```csharp
  // ✅ TỐT: Thêm property mới có default value hoặc nullable
  public string? RentalNotes { get; set; } = string.Empty;
  ```
- **CẤM:** Xóa hoặc đổi tên thuộc tính hiện có:
  ```csharp
  // ❌ NGUY HIỂM: Đổi tên thuộc tính 'Title' thành 'ProjectName' sẽ làm gãy Data Binding ở toàn bộ các XAML Pages khác!
  ```

### 2. Phương thức Repository / Service: Luôn dùng Overload hoặc Optional Parameters
Khi cần truyền thêm tham số vào hàm truy vấn:
- **Cách 1: Overload method cũ:**
  ```csharp
  // Giữ nguyên hàm cũ cho các màn hình hiện tại dùng
  public Task<List<ProjectTask>> GetTasksAsync(int projectId) 
      => GetTasksAsync(projectId, includeCompleted: true);

  // Thêm overload mới cho tính năng mới
  public async Task<List<ProjectTask>> GetTasksAsync(int projectId, bool includeCompleted)
  {
      // Implementation logic
  }
  ```
- **Cách 2: Sử dụng Optional parameter có default value:**
  ```csharp
  public async Task<List<ProjectTask>> GetTasksAsync(int projectId, bool includeCompleted = true)
  ```

### 3. Tương thích Cơ Sở Dữ Liệu (SQLite / Data Storage)
Nếu có sự thay đổi về cấu trúc bảng SQLite:
1. Đảm bảo model mới không làm hỏng dữ liệu đang có của người dùng cũ.
2. Kiểm tra hàm khởi tạo DB (`Data/ProjectRepository.cs`, `Init()`):
   - Đảm bảo SQLite `CreateTableAsync<T>()` tự động thêm cột mới mà không xóa bảng dữ liệu cũ.

---

## Quy Trình Tự Kiểm Tra (Self-Audit Checklist)
- [ ] Không có thuộc tính cũ nào bị xóa hoặc đổi tên trong `Models/`.
- [ ] Tất cả các method gọi tới Repository cũ trong toàn bộ dự án vẫn biên dịch (`dotnet build`) mà không báo lỗi signature mismatch.
- [ ] Data Binding trong các file `.xaml` không bị ảnh hưởng.

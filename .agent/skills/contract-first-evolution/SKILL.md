---
name: contract-first-evolution
description: Guidelines and patterns for evolving C# Domain Models, Repositories, Database entities, and Service Interfaces safely without breaking existing features or concurrent agent tasks.
---

# Contract-First Evolution Skill

## Mục đích
Ngăn chặn hiện tượng Agent tự tiện thay đổi model dữ liệu, chữ ký hàm (method signatures), hay database schema làm crash các màn hình khác hoặc phá vỡ các task đang chạy song song của thành viên khác trong team.

## Khi nào kích hoạt Skill này
- Khi `@architect` cần thêm trường mới vào Model (`RoomToRentRadar.Core/Models/`).
- Khi cần mở rộng Interface hợp đồng (`RoomToRentRadar.Core/Abstractions/`) hoặc SQLite Schema (`RoomToRentRadar.Infrastructure/Persistence/`).
- Lưu ý: `@developer` **CẤM** tự ý sửa các thư mục trên nếu chưa được Scrum Master (`@Duc-Nguyen-Github`) hoặc Tech Lead (`@quangtldc0301`) phê duyệt.

---

## Các Quy Tắc Bảo Vệ Hợp Đồng Dữ Liệu (Contract Rules)

### 1. Quy tắc Mở rộng, không Phá vỡ (Add, Never Break)
- **Được phép:** Thêm thuộc tính mới có giá trị mặc định hoặc cho phép null:
  ```csharp
  // ✅ TỐT: Thêm property mới có default value hoặc nullable
  public string? RentalNotes { get; set; } = string.Empty;
  ```
- **CẤM:** Xóa hoặc đổi tên thuộc tính hiện có trong `RoomListing`, `UserAccount`, `FavoriteRoom`, `SavedSearchAlert`, `HouseRuleVersion`, `ListingReport`, `RoomFilterCriteria`.

### 2. Phương thức Repository / Service: Luôn dùng Overload hoặc Optional Parameters
Khi cần truyền thêm tham số vào hàm truy vấn trong `IRoomListingRepository` hoặc các Interface khác:
- Giữ nguyên chữ ký hàm đã cam kết trong `scrum-task-routing-and-git-branching`, chỉ bổ sung tham số có giá trị mặc định (optional parameter) ở cuối hoặc tạo hàm overload mới.

### 3. Tương thích Cơ Sở Dữ Liệu (`DatabaseSchema.cs` & `RentalDatabaseService.cs`)
- Mọi câu lệnh tạo bảng trong `DatabaseSchema.CreateTablesSql` phải dùng `CREATE TABLE IF NOT EXISTS`.
- Dữ liệu mẫu trong `IctuSeedData.cs` giữ nguyên 7 phòng chuẩn quanh ICTU để các bài Unit Test và màn hình UI luôn đồng nhất.

---

## Quy Trình Tự Kiểm Tra (Self-Audit Checklist)
- [ ] Không có thuộc tính cũ nào bị xóa hoặc đổi tên trong `RoomToRentRadar.Core/Models/`.
- [ ] Tất cả các method trong `RoomToRentRadar.Core/Abstractions/` giữ nguyên chữ ký chuẩn.
- [ ] Lệnh `dotnet test RoomToRentRadarUnitTests` và `dotnet build RoomToRentRadar.slnx` biên dịch `0 Error(s)`.

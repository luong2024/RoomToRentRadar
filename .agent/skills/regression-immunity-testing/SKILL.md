---
name: regression-immunity-testing
description: Standardized NUnit 4 testing workflow to verify zero regressions across existing unit, SQLite, and API contract tests before completing tasks, guaranteeing that existing working features remain fully operational.
---

# Regression Immunity Testing Skill (NUnit 4 + SQLite Contract Suite)

## Mục đích
Đảm bảo mọi thay đổi do AI Agent thực hiện không làm hỏng bất kỳ chức năng nào đang hoạt động ổn định trong project. Thiết lập quy chuẩn kiểm thử tự động bằng **NUnit 4** trước khi một task được coi là hoàn tất.

---

## Các Bước Thực Hiện Bắt Buộc

### Bước 1: Chạy Toàn Bộ Test Suite Hiện Có (Kèm Hang Timeout 30s)
Chạy lệnh kiểm thử đơn vị & hợp đồng API:
```powershell
dotnet test RoomToRentRadarUnitTests/RoomToRentRadarUnitTests.csproj --blame-hang-timeout 30s
```
Quan sát kết quả:
- **Nếu 100% Tests Pass:** Tiến hành sang Bước 2.
- **Nếu có Test bị Fail:**
  > ⚠️ **ĐẶC BIỆT LƯU Ý:**
  > 1. Tuyệt đối KHÔNG ĐƯỢC sửa đổi file test cũ (`UnitTest1.cs`, `ArchitectureAndContractsTests.cs`) để nó pass theo code mới.
  > 2. Tuyệt đối KHÔNG ĐƯỢC xóa bài test cũ hoặc gắn attribute `[Ignore]`.
  > 3. Phân tích và sửa lại code của Task đang làm để tương thích hoàn toàn với hợp đồng chung.

### Bước 2: Quy Chuẩn Viết Bài Test Mới Trong `RoomToRentRadarUnitTests`
> **LƯU Ý QUAN TRỌNG CHO AI AGENT:**
> - Dự án sử dụng **NUnit 4** (`[Test]`, `[SetUp]`, `[TearDown]`, `[CancelAfter(5000)]`, `Assert.That(..., Is.EqualTo(...))`).
> - **KHÔNG sử dụng xUnit (`[Fact]`) và KHÔNG sử dụng `Moq`** (không cài gói `Moq` vào project). Hãy dùng trực tiếp `RentalDatabaseService` với file SQLite tạm hoặc các lớp `InMemory*` trong `RoomToRentRadar.Infrastructure.Fakes`.

Ví dụ mẫu chuẩn NUnit 4 (chạy thật với SQLite hoặc DI Container, có chống treo 5 giây):
```csharp
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;

namespace RoomToRentRadarUnitTests
{
    [CancelAfter(5000)]
    public class RoomListingSqliteTests
    {
        private string _tempDbPath = null!;
        private RentalDatabaseService _db = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rtr_test_{Guid.NewGuid():N}.db3");
            _db = new RentalDatabaseService(NullLogger<RentalDatabaseService>.Instance, $"Data Source={_tempDbPath}");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { }
            }
        }

        [Test]
        public async Task ListVerifiedRoomsAsync_ReturnsOnlyAvailableAndVerifiedRooms()
        {
            var repo = new RoomListingRepository(_db);

            var rooms = await repo.ListVerifiedRoomsAsync(pageIndex: 0, pageSize: 10, sortBy: "DistanceAsc");

            Assert.That(rooms, Is.Not.Empty);
            Assert.That(rooms.All(r => r.IsVerified && r.Status == Constants.ListingStatuses.Available), Is.True);
        }
    }
}
```

### Bước 3: Kiểm Tra Toàn Giải Pháp Trước Khi Kết Thúc Task
Chạy script pre-flight để kiểm tra cả Static Scan, Contract Tests và MAUI Build:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/preflight.ps1 -FullMauiBuild
```

---

## Tiêu Chuẩn Nghiệm Thu (Definition of Done)
- [ ] Build thành công không có lỗi biên dịch: `0 Error(s)`.
- [ ] Tất cả các test cũ và mới đều PASS: `Failed: 0`.
- [ ] Không dùng `.Result` / `.Wait()` hay cài thêm thư viện test ngoài luồng.

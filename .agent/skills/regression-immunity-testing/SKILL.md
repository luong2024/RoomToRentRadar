---
name: regression-immunity-testing
description: Standardized testing workflow to verify zero regressions across existing unit and UI tests before completing tasks, guaranteeing that existing working features remain fully operational.
---

# Regression Immunity Testing Skill

## Mục đích
Đảm bảo mọi thay đổi do AI Agent thực hiện không làm hỏng bất kỳ chức năng nào đang hoạt động ổn định trong project. Thiết lập quy chuẩn kiểm thử nghiêm ngặt trước khi một task được coi là hoàn tất.

## Khi nào kích hoạt Skill này
- Sau khi viết xong code tính năng hoặc sửa bug, trước khi kết thúc task.
- Khi nghi ngờ một thay đổi có thể ảnh hưởng tới các module liên quan.

---

## Các Bước Thực Hiện Bắt Buộc

### Bước 1: Chạy Toàn Bộ Test Suite Hiện Có
Chạy lệnh kiểm thử đơn vị:
```powershell
# Chạy test tiêu chuẩn (kèm RollForward cho môi trường preview SDK nếu cần)
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet test RoomToRentRadarUnitTests
```
Quan sát kết quả:
- **Nếu 100% Tests Pass:** Tiến hành sang Bước 2.
- **Nếu có Test bị Fail:**
  > ⚠️ **ĐẶC BIỆT LƯU Ý:**
  > 1. Tuyệt đối KHÔNG ĐƯỢC sửa đổi file test cũ để nó pass theo code mới.
  > 2. Tuyệt đối KHÔNG ĐƯỢC xóa bài test cũ hoặc gắn attribute `[Ignore]` / `[Fact(Skip = "...")]`.
  > 3. Hãy phân tích lỗi: Code mới đã phá vỡ hành vi kỳ vọng nào của hệ thống? Sau đó sửa lại code mới để tương thích hoàn toàn.

### Bước 2: Viết Thêm Test Cho Tính Năng Mới
Mỗi task mới phải bổ sung test tương ứng vào dự án `RoomToRentRadarUnitTests`:
1. **Happy Path:** Kiểm tra trường hợp dữ liệu hợp lệ hoạt động đúng kỳ vọng.
2. **Edge Cases:** Kiểm tra dữ liệu rỗng (`null`, `empty string`), danh sách trống, giá trị biên.
3. **Async / Cancellation:** Kiểm tra các hàm `async` xử lý đúng khi hoàn tất hoặc khi có ngoại lệ.

Ví dụ mẫu test cho PageModel:
```csharp
[Fact]
public async Task LoadDataAsync_WhenCalled_PopulatesItemsCorrectly()
{
    // Arrange
    var mockRepo = new Mock<IProjectRepository>();
    var pageModel = new MyFeaturePageModel(mockRepo.Object);

    // Act
    await pageModel.LoadDataCommand.ExecuteAsync(null);

    // Assert
    Assert.NotEmpty(pageModel.Items);
    Assert.False(pageModel.IsBusy);
}
```

### Bước 3: Build Toàn Giải Pháp (Full Solution Verification)
Chạy build toàn bộ solution để đảm bảo không có platform nào bị gãy:
```powershell
dotnet build RoomToRentRadar.slnx -c Debug
```

---

## Tiêu Chuẩn Nghiệm Thu (Definition of Done)
- [ ] Build thành công không có lỗi biên dịch: `0 Error(s)`.
- [ ] Tất cả các test cũ tiếp tục PASS: `Failed: 0`.
- [ ] Có bài test mới được bổ sung cho logic nghiệp vụ mới.

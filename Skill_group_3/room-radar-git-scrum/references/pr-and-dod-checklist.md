# Pull Request Template & Definition of Done (DoD) Checklist

## 1. Pull Request Markdown Template (`.github/pull_request_template.md`)

```markdown
## Thông tin Task (Scrum Traceability)
- **User Story**: USxx - [Tên User Story]
- **Task ID**: Txx.y - [Tên Task theo Sprint Backlog]
- **Assignee**: [Tên thành viên]
- **Actual Hours Spent**: [Số giờ thực tế] / [Số giờ Estimate]h

## Tóm tắt thay đổi (Summary of Changes)
- [ ] Backend (ASP.NET Core MVC API / Azure Cosmos DB)
- [ ] Frontend (.NET MAUI XAML / ViewModel / Services)
- [ ] Unit Tests (xUnit) / UI Tests (Appium)

## Kiểm tra Acceptance Criteria (AC Verification)
- [ ] AC1: Đã kiểm tra và pass
- [ ] AC2: Đã kiểm tra và pass
- [ ] AC3: Đã kiểm tra và pass
- [ ] AC4: Đã kiểm tra và pass

## Checklist Chất lượng (Definition of Done)
- [ ] Code build thành công không có Warning nghiêm trọng (`dotnet build --configuration Release`)
- [ ] Đã viết và chạy pass Unit Test / Appium UI Test cho luồng chính và edge cases
- [ ] Không chứa hardcode connection string, API key (Google Maps, Cosmos DB, Payment)
- [ ] Đã cập nhật trạng thái Task và Actual Hours vào bảng Sprint Tracking cho Scrum Master (Nguyễn Trung Thành)
```

## 2. Definition of Done (DoD) theo Mục tiêu Chất lượng Dự án

Một User Story chỉ được chuyển sang trạng thái **Done** và trình bày tại buổi **Sprint Review** với Product Owner (Vũ Huy Lượng) khi thỏa mãn đầy đủ 6 tiêu chí:

1. **Đúng phạm vi & AC**: Thỏa mãn 100% các tiêu chí Acceptance Criteria (`AC1` - `AC4`) đã mô tả trong bảng kế hoạch.
2. **Code Review chéo**: Có ít nhất 1 Developer cùng nhóm (ưu tiên người làm phần ghép nối FE/BE tương ứng) duyệt PR.
3. **Kiểm thử tự động**:
   - Các API Backend có xUnit test kiểm tra mã trạng thái HTTP (`200`, `400`, `401`, `404`) và tốc độ phản hồi.
   - Các màn hình .NET MAUI chính có kịch bản kiểm thử UI tự động bằng **Appium** hoặc kiểm thử chức năng trên máy ảo Android/iOS.
4. **Hiệu năng & Chi phí Cloud**:
   - Truy vấn danh sách phòng <= 3 giây cho 500 phòng (`US01 AC3`).
   - Bộ lọc kết hợp phản hồi <= 2 giây (`US04 AC2`).
   - Sử dụng Azure Cosmos DB Emulator ở môi trường local để không vượt ngân sách 6 triệu VNĐ/12 tuần.
5. **An toàn dữ liệu**: Không lộ số điện thoại chủ trọ khi chưa đăng nhập (`US02 AC4`), không hiển thị phòng chưa được xác thực (`US01 AC4`).
6. **Không có lỗi nghiêm trọng**: 0 lỗi Critical, tối đa <= 3 lỗi High trên toàn bộ bản dựng Increment.

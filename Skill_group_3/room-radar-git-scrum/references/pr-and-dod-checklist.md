# Pull Request Template & Definition of Done (DoD) Checklist (.NET 10 LTS)

## 1. Pull Request Markdown Template (`.github/pull_request_template.md`)

```markdown
## Thông tin Task & Branch (Shared Repo ICTU)
- **Target Branch**: `Group-3` (MANDATORY: Tuyệt đối KHÔNG target vào `main` hoặc nhóm khác)
- **Feature Branch**: `Group-3/feature/USxx-Txx.y-slug`
- **User Story**: USxx - [Tên User Story]
- **Task ID**: Txx.y - [Tên Task theo Sprint Backlog]
- **Assignee**: [Tên thành viên] (Role: Backend / Frontend)
- **Actual Hours Spent**: [Số giờ thực tế] / [Số giờ Estimate]h

## Xác nhận Giới hạn An toàn Git & .NET 10 Baseline
- [ ] KHÔNG commit trực tiếp vào `main` hoặc `Group-3`
- [ ] Đã nhận được sự xác nhận rõ ràng của người phụ trách trước khi commit & push
- [ ] Build thành công trên **.NET 10.0 LTS** (`net10.0` cho API / `net10.0-android` API 35, Min API 24 cho MAUI)

## Kiểm tra Acceptance Criteria (AC Verification)
- [ ] AC1: Đã kiểm tra và pass
- [ ] AC2: Đã kiểm tra và pass
- [ ] AC3: Đã kiểm tra và pass
- [ ] AC4: Đã kiểm tra và pass

## Checklist Chất lượng (Definition of Done)
- [ ] Đã rebase / merge mới nhất từ `origin/Group-3` trước khi tạo PR
- [ ] Đã viết và chạy pass Unit Test (xUnit) / UI Test (Appium) cho luồng chính và edge cases
- [ ] Không chứa hardcode connection string, API key (Google Maps, Cosmos DB, Payment)
- [ ] Đã cập nhật trạng thái Task và Actual Hours vào bảng Sprint Tracking cho Scrum Master (Nguyễn Trung Thành)
```

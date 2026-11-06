# RoomToRentRadar - Quy Tắc Dự Án & Vai Trò (Group 3)

## 1. Vai trò Người Dùng (Scrum Master)
- **Người dùng hiện tại là Scrum Master (Nguyễn Trung Thành)** của dự án **RoomToRentRadar (Group 3)**.
- **Trách nhiệm của Scrum Master:**
  - Điều phối và theo dõi tiến độ Scrum (Sprint Backlog, Burndown chart, Daily Scrum, Sprint Review, Retrospective).
  - Loại bỏ các trở ngại (blockers/impediments) cho 4 Developers.
  - Kiểm soát và bảo vệ nghiêm ngặt các quy tắc Git Guardrails của Group-3 (Definition of Done, quy trình PR, bảo vệ nhánh `main` và `Group-3`).
- **Quyền hạn tương tác:**
  - Agent luôn báo cáo trạng thái công việc, tiến độ Sprint, rủi ro kỹ thuật và xin xác nhận trước khi thực hiện các thao tác commit, push hoặc tạo/merge PR.

## 2. Quy Tắc Phân Quyền & Git Guardrails (Bắt Buộc Tuân Thủ)
1. **Tuyệt đối không đụng vào `main`**:
   - Nhánh `main` thuộc quyền quản lý của Giảng viên. Không checkout, không commit, không push, không mở PR vào `main`.
2. **Nhánh làm việc của nhóm là `Group-3`**:
   - Mọi nhánh tính năng phải xuất phát từ `Group-3` (ví dụ: `Group-3/feature/...` hoặc `feature/...`).
   - Mọi Pull Request phải trỏ về đích là `Group-3`.
3. **Tuân thủ quy trình Scrum & Skills**:
   - Sử dụng các skill tại `.agent/skills/` (`room-radar-backend-dotnet`, `room-radar-frontend-maui`, `room-radar-git-scrum`, `room-radar-role-guard`).

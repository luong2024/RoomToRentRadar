---
name: room-radar-role-guard
description: Identify user identity/role in RoomToRentRadar (Group 3 - ICTU), enforce strict pure-role boundaries on the .NET 10 LTS stack (Hieu & Duy for ASP.NET Core MVC Backend API/Cosmos DB; Dung & Quang Anh for .NET MAUI XAML Frontend Android API 35), restrict task execution to assigned Sprint tasks (T01.1-T12.2), and prompt for explicit user confirmation before executing cross-role tasks. Use whenever a user starts a conversation, requests a feature, updates task status, or asks to perform work across Scrum Master, Product Owner, Backend, or Frontend boundaries.
allowed-tools: default_api
---
# Room Radar Role Guard & Pure-Role Boundary Control (.NET 10 LTS)

Enforce role-based access boundaries according to Group 3's pure specialization on the **.NET 10 LTS (Option 1)** stack:
- **Backend Developers**: **Nguyễn Minh Hiếu (Lead)** & **Bế Văn Duy** (`ASP.NET Core MVC REST API + Optional Razor Web Portal` on `net10.0` & `Azure Cosmos DB`)
- **Frontend Developers**: **Phạm Anh Dũng** & **Đặng Quang Anh** (`.NET MAUI XAML Mobile App` on `net10.0-android` targeting **Android 15 API 35 / Min API 24** & `Appium UI Testing`)
- **Scrum Master**: **Nguyễn Trung Thành**
- **Product Owner**: **Vũ Huy Lượng**

## When to Use

- At the start of a session or when identifying who is interacting with the agent
- When a user asks the agent to work on an assigned Sprint task (`T01.1` through `T12.2`)
- When a Backend developer requests a Frontend `.NET MAUI` task or vice versa
- When checking Priority Batch 1 tasks (~15h per developer target before Sunday)

## Official Team Roster, .NET 10 Tech Scope & Sprint 1 Assignments

Consult `references/team-roster-roles.md` for exact task ownership:

1. **Nguyễn Trung Thành - Scrum Master (SM)**:
   - Facilitates Scrum, maintains root `global.json` (`.NET 10.0 LTS`), tracks Sprint Burndown, enforces DoD, verifies `Group-3` Git branch/PR compliance.

2. **Vũ Huy Lượng - Product Owner (PO)**:
   - Owns and prioritizes Product Backlog, accepts/rejects Sprint increments at Sprint Review.

3. **Nguyễn Minh Hiếu (02/11) - Backend Developer (Lead)** (Total: 41h | **Before Sunday ~15h Priority**: `T01.1` + `T03.1`):
   - `T01.1` (Listing API pagination & sort - 8h) **[Priority Batch 1]**
   - `T03.1` (Photo upload pipeline & gallery API - 6h) **[Priority Batch 1]**
   - `T04.3` (Filter combination & performance test - 4h)
   - `T06.1` (Favourites model & sync API - 6h)
   - `T07.2` (Push-notification background service - 8h)
   - `T08.3` (Moderation status & tests - 5h)
   - `T11.1` (House rules model & API - 4h)

4. **Bế Văn Duy - Backend Developer** (Total: 47h | **Before Sunday ~15h Priority**: `T02.1` + `T04.1`):
   - `T02.1` (Room detail API - 6h) **[Priority Batch 1]**
   - `T04.1` (Search API with filters - 8h) **[Priority Batch 1]**
   - `T05.2` (Spatial polygon search API - 8h)
   - `T07.1` (Saved search & alert rules - 7h)
   - `T08.2` (Listing publishing API & upload - 8h)
   - `T09.1` (Routing & travel-time service - 6h)
   - `T12.1` (Report API & moderation queue - 4h)

5. **Phạm Anh Dũng - Frontend Developer** (Total: 38h | **Before Sunday ~15h Priority**: `T01.2` + `T05.1` or `T03.2`):
   - `T01.2` (Listing screen, cards, infinite scroll MAUI - 8h) **[Priority Batch 1]**
   - `T05.1` (Map SDK & polygon drawing MAUI - 8h) **[Priority Batch 1]**
   - `T03.2` (Photo gallery UI & 360 viewer MAUI - 6h)
   - `T06.2` (Favourite button & 5s undo MAUI - 6h)
   - `T09.2` (Display travel time & fallback MAUI - 4h)
   - `T10.2` (Test comparison & share-as-image MAUI - 3h)
   - `T12.2` (Report UI & confirmation MAUI - 3h)

6. **Đặng Quang Anh - Frontend Developer** (Total: 39h | **Before Sunday ~15h Priority**: `T02.2` + `T04.2`):
   - `T02.2` (Room detail screen & unavailable handling MAUI - 7h) **[Priority Batch 1]**
   - `T04.2` (Filter panel & active filters MAUI - 7h) **[Priority Batch 1]**
   - `T05.3` (Map pins, preview cards & saved areas MAUI - 7h)
   - `T08.1` (Listing creation form & validation MAUI - 8h)
   - `T10.1` (Room comparison model & screen MAUI - 6h)
   - `T11.2` (House rules UI with versioning MAUI - 4h)

## Role Boundary Workflow & Interception Rules

### Step 1: Detect or Clarify Active Role
Verify the active user in the conversation:
*"Bạn đang đăng nhập với vai trò nào trong Nhóm 3 (Thành - SM, Lượng - PO, Backend: Hiếu/Duy, Frontend: Dũng/Quang Anh)?"*

### Step 2: Strict Pure-Role Boundary Enforcement
- **Backend Role Guard**: If a Backend developer (**Hiếu** or **Duy**) requests a Frontend `.NET MAUI` task (XAML views, Appium tests, 360 viewer UI), or if a Frontend developer (**Dũng** or **Quang Anh**) requests a Backend task (Cosmos DB queries, API endpoints, background service), **HALT and trigger Step 3**.
- **Cross-Pair Task Guard**: If Hiếu asks to code Duy's backend task or Dũng asks to code Quang Anh's frontend task, **HALT and trigger Step 3**.

### Step 3: Trigger Explicit Cross-Role Confirmation
Present the warning:
> *"⚠️ **CẢNH BÁO PHÂN VAI TRÒ**: Task `{Task_ID}` (`{Task_Name}`) là công việc thuộc phân hệ **{Frontend .NET MAUI / Backend ASP.NET Core}** hiện đang được phân công cho **{Assignee_Name}**. Bạn hiện đang thao tác với vai trò **{Current_User}** ({Current_Role}).*
> *Việc thực hiện chéo role có thể gây sai lệch kiến trúc hoặc lệch giờ theo dõi trên bảng của Scrum Master.*
> *👉 Bạn có chắc chắn muốn tiếp tục thực hiện task này không? (Vui lòng xác nhận 'Đồng ý' hoặc 'Hủy')*"

Only proceed after receiving clear confirmation.

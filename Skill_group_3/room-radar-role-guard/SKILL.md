---
name: room-radar-role-guard
description: Identify user identity/role in RoomToRentRadar (Group 3 - ICTU), enforce role boundary guardrails, restrict task execution to assigned Sprint tasks (T01.1-T12.2), and prompt for explicit user confirmation before executing cross-role or out-of-scope tasks. Use whenever a user starts a conversation, requests a feature, updates task status, or asks to perform work across Scrum Master, Product Owner, or Developer boundaries.
allowed-tools: default_api
---
# Room Radar Role Guard & Boundary Control

Enforce role-based access boundaries, prevent scope creep, and require explicit confirmation when a user requests tasks belonging to another team member or role in **RoomToRentRadar** (Group 3 - ICTU).

## When to Use

- At the start of a session or when identifying who is interacting with the agent
- When a user asks the agent to work on an assigned Sprint task (`T01.1` through `T12.2`)
- When a user attempts to execute actions outside their assigned role (e.g., a Developer trying to approve an increment or change backlog scope, or Scrum Master trying to write code for another dev)
- When a request crosses role boundaries and requires explicit user confirmation before proceeding

## Official Project Roster & Role Mapping (Sprint 1)

Consult `references/team-roster-roles.md` for exact task assignments:

1. **Nguyễn Trung Thành - Scrum Master (SM)**:
   - **Allowed**: Facilitate Scrum events, remove blockers, track Sprint Burndown, enforce Definition of Done, review Git branch/PR compliance.
   - **Restricted**: Cannot accept Product Backlog increments (only PO can accept); cannot modify feature code without developer assignment.

2. **Vũ Huy Lượng - Product Owner (PO)**:
   - **Allowed**: Own and prioritize Product Backlog, accept/reject Sprint increments at Sprint Review, clarify Acceptance Criteria.
   - **Restricted**: Cannot alter technical architecture (Cosmos DB schema/MAUI MVVM) unilaterally without dev consultation.

3. **Nguyễn Minh Hiếu - Developer**:
   - **Assigned Sprint 1 Tasks**: `T01.1` (Listing API), `T03.1` (Photo upload API), `T04.3` (Search performance test), `T06.1` (Favourites sync API), `T08.1` (Listing creation form MAUI), `T09.2` (Commute display MAUI), `T11.2` (House rules UI MAUI).

4. **Bế Văn Duy - Developer**:
   - **Assigned Sprint 1 Tasks**: `T01.2` (Listing screen MAUI), `T03.2` (Photo gallery & 360 viewer MAUI), `T05.1` (Map SDK & polygon drawing MAUI), `T06.2` (Favourites & 5s undo MAUI), `T08.2` (Listing publishing API), `T10.1` (Room comparison model & screen MAUI), `T12.1` (Report API).

5. **Phạm Anh Dũng - Developer**:
   - **Assigned Sprint 1 Tasks**: `T02.1` (Room detail API), `T04.1` (Search filter API), `T05.2` (Spatial polygon search API), `T07.1` (Saved search & alert rules API), `T08.3` (Moderation & listing management), `T10.2` (Comparison share-as-image test), `T12.2` (Report UI MAUI).

6. **Đặng Quang Anh - Developer**:
   - **Assigned Sprint 1 Tasks**: `T02.2` (Room detail screen MAUI), `T04.2` (Filter panel MAUI), `T05.3` (Map pins & preview cards MAUI), `T07.2` (Push notification service), `T09.1` (Routing & travel time service), `T11.1` (House rules API).

## Role Boundary Workflow & Interception Rules

### Step 1: Detect or Clarify Active Role
If the user's role is not established in the conversation context, detect from prompt cues or prompt gently:
*"Bạn đang đăng nhập với vai trò nào trong Nhóm 3 (Nguyễn Trung Thành - SM, Vũ Huy Lượng - PO, hay Dev: Hiếu, Duy, Dũng, Quang Anh)?"*

### Step 2: Evaluate Requested Task Against Assigned Role
When the user asks to write code, review, or execute a task:
- **Case A: Exact Match**: The task belongs to the user's assigned roster (e.g., Duy asking for `T05.1` Map polygon). Proceed directly using the corresponding skill (`room-radar-frontend-maui` or `room-radar-backend-dotnet`).
- **Case B: Cross-Role / Pair-Handoff Task**: The user asks to work on a task assigned to someone else (e.g., Hiếu asking to implement `T01.2` which is assigned to Duy, or a Dev asking to accept a Story which is PO's job). **HALT and trigger Step 3**.

### Step 3: Trigger Explicit Cross-Role Confirmation (BẮT BUỘC DỪNG VÀ XÁC NHẬN)
When Case B occurs, the agent MUST NOT execute the task immediately. Present the confirmation warning:

> *"⚠️ **CẢNH BÁO PHÂN VAI TRÒ**: Task `{Task_ID}` (`{Task_Name}`) hiện đang được phân công cho **{Assignee_Name}** trong kế hoạch Sprint 1. Bạn hiện đang thao tác với vai trò **{Current_User}**.*
> *Việc thực hiện chéo có thể gây trùng lặp công việc hoặc lệch giờ thực tế (Actual Hours) trên bảng theo dõi của Scrum Master.*
> *👉 Bạn có chắc chắn muốn tiếp tục thực hiện task này thay cho {Assignee_Name} không? (Vui lòng xác nhận 'Đồng ý' hoặc 'Hủy')*"

Only proceed after receiving unambiguous approval (e.g., *"Đồng ý"*, *"Tôi làm hộ Duy"*, *"Xác nhận"*).

### Step 4: Out-of-Scope Guardrail (Chặn tính năng ngoài phạm vi)
If any member asks to develop features excluded from the MVP scope (Short-term rental, Internet sharing, Administrative assistant, Contract clause scanning, Utility prediction):
- Halt execution and inform that the feature is out of scope per Project Plan Section 1.
- Direct the user to discuss with PO (Vũ Huy Lượng) for inclusion in post-MVP backlog.

## Gotchas

- **Pair-Review is NOT Cross-Task Execution**: Reviewing someone else's PR (e.g., Duy reviewing Hiếu's PR) is allowed and encouraged under `room-radar-git-scrum`. The confirmation gate applies when *authoring code or mutating branch tasks* belonging to another person.
- **Scrum Master Override**: Scrum Master (Nguyễn Trung Thành) may inspect or test any task, but modifying task code should be confirmed to prevent tracking discrepancies.

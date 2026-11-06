---
name: room-radar-git-scrum
description: Enforce Git branching strategy (monorepo/shared-repo with Group-3 parent branch), strict Git guardrails (protect main and Group-3, require explicit user confirmation before commit/push), .NET 10 LTS & Android API 35 build checks, commit message conventions, Pull Request rules, Scrum task handoffs, and Quality Gates for the RoomToRentRadar project (Group 3 - ICTU). Use whenever a team member or agent creates branches, stages changes, commits code, opens or reviews PRs, tracks Sprint tasks (US01-US12, T01.1-T12.2), coordinates Backend-Frontend API handoffs, or runs Scrum ceremonies.
allowed-tools: default_api
---
# Room Radar Git and Scrum Workflow (.NET 10 LTS & Shared Repository)

Standardize Git version control, multi-agent task coordination, Backend-Frontend API contracts, Scrum quality gates, and **strict Git guardrails** for **RoomToRentRadar** (Group 3 - ICTU) inside a shared class-wide GitHub repository.

## When to Use

- Creating a Git branch, staging changes, committing code, or resolving merge conflicts in the RoomToRentRadar repository
- Enforcing strict Git boundaries (protecting `main` and `Group-3`, preventing direct commits, requiring explicit user approval before commit/push)
- Opening, reviewing, or merging a Pull Request (PR) across the 4-developer team targeting `Group-3`
- Starting, handing off, or updating a Sprint task (`T01.1` through `T12.2` or Sprint 2-4 tasks)
- Coordinating API contracts (`RoomToRentRadar.Shared`) between ASP.NET Core MVC Backend and .NET MAUI Frontend agents

## Standardized Tech Stack & Environment Baseline (Option 1)

All developers and CI checks must use the exact same baseline to prevent cross-machine build failures:
- **IDE & SDK**: **Visual Studio 2026 (v18.x)** + **.NET 10.0 LTS SDK** (`10.0.100+`, pinned via root `global.json` with `"rollForward": "latestFeature"`) + **C# 14** + **Microsoft OpenJDK 21**.
- **Architecture (Option 1)**:
  - **Mobile Client**: `.NET MAUI` (`net10.0-android`) using **XAML + MVVM (`CommunityToolkit.Mvvm`)**.
  - **Android SDK Targets**: `TargetPlatformVersion = 35.0` (**Android 15.0 - API Level 35**, or `36.0` Android 16) and `SupportedOSPlatformVersion = 24.0` (**Min Android 7.0 - API Level 24**).
  - **Backend & Optional Web Portal**: **ASP.NET Core MVC** (`net10.0`) serving both REST API endpoints (`/api/v1/...`) for the MAUI mobile app and Razor Views (`.cshtml`) for web-based Landlord/Admin management.
  - **Database**: **Azure Cosmos DB** (`Microsoft.Azure.Cosmos` v3 SDK, using Local Emulator during development).

## Strict Git Boundaries & Hard Guardrails (BẮT BUỘC TUÂN THỦ)

1. **RULE 1 - ZERO TOUCH TO `main` / `master` (Tuyệt đối không đụng vào `main`)**:
   - `main` is owned exclusively by the instructor.
   - **NEVER** checkout `main` for editing, **NEVER** commit to `main`, **NEVER** push to `main`, and **NEVER** open a Pull Request targeting `main`.

2. **RULE 2 - ZERO DIRECT COMMITS TO `Group-3` (Tuyệt đối không commit trực tiếp vào `Group-3`)**:
   - `Group-3` is our team's protected parent trunk (acting as `develop`).
   - **NO DIRECT COMMITS**: Developers and agents must NEVER run `git commit` or `git push` directly on branch `Group-3`.
   - **MERGE ONLY VIA PULL REQUEST**: Code enters `Group-3` **ONLY** by merging tested, peer-reviewed Pull Requests originating from task feature branches (`Group-3/feature/...`).

3. **RULE 3 - MANDATORY EXPLICIT USER CONFIRMATION BEFORE COMMIT OR PUSH**:
   - An AI agent must **NEVER** autonomously execute mutating Git commands (`git commit`, `git push`, `git merge`, `git cherry-pick`, `git reset --hard`) without first presenting:
     1. The current active branch name (must verify it is NOT `main` and NOT `Group-3`).
     2. The exact list of modified/staged files (`git status -s`).
     3. The exact commit message formatted per convention.
   - The agent MUST wait for explicit confirmation from the user (e.g., "đồng ý", "commit đi", "confirm") before executing the commit or push command.

4. **RULE 4 - MANDATORY PRE-PUSH HOOK & VALIDATION SCRIPT**:
   - Run `scripts/validate_git_workflow.sh` before staging or pushing.

## Steps

### Step 1: Identify Task ID and Create a Prefixed Feature Branch from `Group-3`

Verify the exact `User Story ID` (`USxx`) and `Task ID` (`Txx.y`) from `references/sprint1-backlog-matrix.md`.

1. Sync local `Group-3` with the remote repository:
   ```bash
   git checkout Group-3
   git pull origin Group-3
   ```
2. Create a branch following the Group-3 prefixed naming convention:
   - Feature task: `Group-3/feature/<US-ID>-<Task-ID>-<short-kebab-slug>`
   - Bug fix: `Group-3/bugfix/<US-ID>-<Task-ID>-<short-kebab-slug>`
   - Hotfix: `Group-3/hotfix/<issue-slug>`
   ```bash
   git checkout -b Group-3/feature/US01-T01.1-listing-api-pagination
   git checkout -b Group-3/feature/US01-T01.2-listing-screen-infinite-scroll
   ```

### Step 2: Follow the Backend-to-Frontend Contract-First Handoff (`RoomToRentRadar.Shared`)

1. **Hour 1 of any User Story (DTO & Swagger Freeze)**:
   - Backend developer (**Hiếu** or **Duy**) defines the Request/Response DTOs inside `RoomToRentRadar.Shared` (`net10.0`) and controller route signatures first.
   - Frontend developer (**Dũng** or **Quang Anh**) references `RoomToRentRadar.Shared` to immediately build Refit services and MAUI XAML ViewModels against exact property names.
2. **Parallel Implementation**:
   - Backend developer (**Hiếu, Duy**) implements Cosmos DB repository queries, spatial indexing, and xUnit tests (refer to `room-radar-backend-dotnet`).
   - Frontend developer (**Dũng, Quang Anh**) implements XAML views, CommunityToolkit.Mvvm bindings, and Appium UI tests (refer to `room-radar-frontend-maui`).

### Step 3: Format Commit Messages, Validate Locally & Seek Confirmation

1. Format commit message:
   ```text
   <type>(<US-ID>/<Task-ID>): <imperative summary under 72 chars>
   ```
   Allowed `<type>` values: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`.
2. Run `bash scripts/validate_git_workflow.sh`.
3. Present branch, staged files, and commit message to the user and **wait for explicit confirmation** before running `git commit` and `git push`.

### Step 4: Submit Pull Request Targeting `Group-3` and Execute Peer Review

Always set the base branch of the PR to `Group-3` (NEVER `main`). Follow the **Backend-Frontend Feature Pair Review Matrix**:
- **US01 & US03 Pair**: **Nguyễn Minh Hiếu** (Backend `T01.1, T03.1`) <-> **Phạm Anh Dũng** (Frontend `T01.2, T03.2`)
- **US02 & US04 Pair**: **Bế Văn Duy** (Backend `T02.1, T04.1`) <-> **Đặng Quang Anh** (Frontend `T02.2, T04.2`)
- **US05 & US08 Pair**: **Bế Văn Duy** (Backend `T05.2, T08.2`) <-> **Phạm Anh Dũng & Đặng Quang Anh** (Frontend `T05.1, T05.3, T08.1`)
- **Scrum Master (Nguyễn Trung Thành)**: Final check that Acceptance Criteria (`AC1`-`AC4`), unit tests, and Sprint Tracking hours are updated before merging into `Group-3`.

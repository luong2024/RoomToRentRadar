---
name: room-radar-git-scrum
description: Enforce Git branching strategy, commit message conventions, Pull Request rules, Scrum task handoffs, and Quality Gates for the RoomToRentRadar project (Group 3 - ICTU). Use whenever a team member or agent creates branches, commits code, opens or reviews PRs, tracks Sprint tasks (US01-US12, T01.1-T12.2), coordinates Backend-Frontend API handoffs, or runs Scrum ceremonies.
allowed-tools: default_api
---
# Room Radar Git and Scrum Workflow

Standardize Git version control, multi-agent task coordination, Backend-Frontend API contracts, and Scrum quality gates for **RoomToRentRadar** (Group 3 - ICTU).

## When to Use

- Creating a Git branch, staging changes, committing code, or resolving merge conflicts in the RoomToRentRadar repository
- Opening, reviewing, or merging a Pull Request (PR) across the 4-developer team
- Starting, handing off, or updating a Sprint task (`T01.1` through `T12.2` or Sprint 2-4 tasks)
- Coordinating API contracts between ASP.NET Core MVC Backend and .NET MAUI Frontend agents
- Checking Definition of Done (DoD) and Quality Objectives before Sprint Review

## Project Context and Team Roster

- **Product**: RoomToRentRadar - Student Room Rental Radar for ICTU students (5,000 potential users; pilot across 3 faculties with 50 students and 100 verified landlords)
- **Tech Stack**: `.NET MAUI` Mobile App + `ASP.NET Core MVC REST API` + `Azure Cosmos DB` + `Appium UI Testing`
- **Timeline**: 12 Weeks, 4 Sprints (200 Story Points total, 50 SP / Sprint velocity, 160 hours NET capacity per Sprint)
- **Team Roles**:
  - **Product Owner (PO)**: Vu Huy Luong (accepts increments, owns Product Backlog)
  - **Scrum Master (SM)**: Nguyen Trung Thanh (facilitates Scrum, tracks Sprint Burndown, removes blockers)
  - **Developers**: Nguyen Minh Hieu, Be Van Duy, Pham Anh Dung, Dang Quang Anh

## Steps

### Step 1: Identify Task ID and Create a Compliant Feature Branch

Before writing any code, verify the exact `User Story ID` (`USxx`) and `Task ID` (`Txx.y`) from `references/sprint1-backlog-matrix.md`.

1. Sync with `develop` before branching:
   ```bash
   git checkout develop
   git pull origin develop
   ```
2. Create a branch following the strict naming convention:
   - Feature task: `feature/<US-ID>-<Task-ID>-<short-kebab-slug>`
   - Bug fix: `bugfix/<US-ID>-<Task-ID>-<short-kebab-slug>`
   - Release candidate: `release/sprint-<N>`
   - Hotfix: `hotfix/<issue-slug>`
   ```bash
   # Examples for Sprint 1:
   git checkout -b feature/US01-T01.1-listing-api-pagination
   git checkout -b feature/US01-T01.2-listing-screen-infinite-scroll
   ```

### Step 2: Follow the Backend-to-Frontend Contract-First Handoff

Because tasks within each User Story are split across two developers (one building the Backend API `.1` and another building the .NET MAUI UI `.2`), agents must prevent integration blockers:

1. **Hour 1 of any User Story (DTO & Swagger Freeze)**:
   - The Backend developer (`Txx.1`) defines the Request/Response DTOs and controller route signatures first, returning mock data if database logic is still in progress.
   - Generate or export the OpenAPI/Swagger JSON contract so the Frontend developer (`Txx.2`) can immediately build Refit/HttpClient services and MVVM ViewModels against the exact property names.
2. **Parallel Implementation**:
   - Backend developer implements Cosmos DB repository queries, spatial indexing, and xUnit tests (refer to `room-radar-backend-dotnet`).
   - Frontend developer implements XAML views, CommunityToolkit.Mvvm bindings, local SQLite/Barrel caching, and Appium UI tests (refer to `room-radar-frontend-maui`).

### Step 3: Format Commit Messages and Validate Locally

Format every commit message using the project convention so automated changelogs and Scrum tracking scripts can parse them:

```text
<type>(<US-ID>/<Task-ID>): <imperative summary under 72 chars>
```

Allowed `<type>` values: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`.

Examples:
- `feat(US01/T01.1): add Cosmos DB continuation token pagination and distance sort`
- `feat(US05/T05.1): integrate map SDK polygon drawing and coordinate capture`
- `test(US04/T04.3): add xUnit filter combination and 2s latency benchmarks`

Run `scripts/validate_git_workflow.sh` before pushing to verify branch naming and commit format.

### Step 4: Submit Pull Request and Execute Peer Review Matrix

Never push directly to `main` or `develop`. Open a PR targeting `develop` using the template in `references/pr-and-dod-checklist.md`.

Follow the **Cross-Pair Peer Review Matrix** so the developer integrating with your code reviews it:

- **Nguyen Minh Hieu** <-> **Be Van Duy** (Review each other's PRs on US01, US03, US06, US08)
- **Pham Anh Dung** <-> **Dang Quang Anh** (Review each other's PRs on US02, US04, US05, US07)
- **Scrum Master (Nguyen Trung Thanh)**: Final check that Acceptance Criteria (`AC1`-`AC4`), unit tests, and Sprint Tracking hours are updated before merge.

### Step 5: Update Sprint Burndown and Enforce Quality Objectives

At the end of each task:

1. Update task status (`Todo` -> `Doing` -> `Done`) and record `Actual (hours)` in the Sprint Tracking sheet.
2. Verify all 3 Project Quality Objectives before Sprint Review:
   - **Backlog Completion**: 100% of committed Sprint User Stories completed.
   - **Defect Threshold**: >= 95% of User Stories pass all Acceptance Criteria on first PO review with **0 Critical** and **<= 3 High** defects.
   - **Performance Budgets**: Listing load <= 3s (for 500 listings), Filter search <= 2s, Alert push <= 15 mins.

## Gotchas

- **Scope Creep Trap (Risk #1)**: The Product Backlog has 20+ ideas. Strictly reject out-of-scope features (short-term rental, internet sharing, administrative procedure assistant, contract scanning, utility consumption prediction) during MVP Sprints 1-4.
- **Blocked Frontend Tasks**: Notice in Sprint 1 that `T05.3` (Quang Anh) waits on `T05.2` (Anh Dung) and `T08.3` (Anh Dung) waits on `T08.2` (Van Duy). Always publish the API DTO contract on Day 1 of the task to unblock the UI developer.
- **Privacy & Verification (Risk #6)**: Never expose landlord phone numbers to unauthenticated users (`US02 AC4`), and never display listings unless `Status == "Available"` and `IsVerified == true` (`US01 AC4`).

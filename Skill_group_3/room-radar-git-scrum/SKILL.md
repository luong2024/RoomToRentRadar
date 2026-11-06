---
name: room-radar-git-scrum
description: Enforce Git branching strategy (monorepo/shared-repo with Group-3 parent branch), strict Git guardrails (protect main and Group-3, require explicit user confirmation before commit/push), commit message conventions, Pull Request rules, Scrum task handoffs, and Quality Gates for the RoomToRentRadar project (Group 3 - ICTU). Use whenever a team member or agent creates branches, stages changes, commits code, opens or reviews PRs, tracks Sprint tasks (US01-US12, T01.1-T12.2), coordinates Backend-Frontend API handoffs, or runs Scrum ceremonies.
allowed-tools: default_api
---
# Room Radar Git and Scrum Workflow (Shared Multi-Group Repository)

Standardize Git version control, multi-agent task coordination, Backend-Frontend API contracts, Scrum quality gates, and **strict Git guardrails** for **RoomToRentRadar** (Group 3 - ICTU) inside a shared class-wide GitHub repository.

## When to Use

- Creating a Git branch, staging changes, committing code, or resolving merge conflicts in the RoomToRentRadar repository
- Enforcing strict Git boundaries (protecting `main` and `Group-3`, preventing direct commits, requiring explicit user approval before commit/push)
- Opening, reviewing, or merging a Pull Request (PR) across the 4-developer team targeting `Group-3`
- Starting, handing off, or updating a Sprint task (`T01.1` through `T12.2` or Sprint 2-4 tasks)
- Coordinating API contracts between ASP.NET Core MVC Backend and .NET MAUI Frontend agents
- Checking Definition of Done (DoD) and Quality Objectives before Sprint Review

## Strict Git Boundaries & Hard Guardrails (BẮT BUỘC TUÂN THỦ)

The entire class shares **one single GitHub repository**. To protect both the class repository and our team's integrity, every developer and autonomous agent MUST enforce the following 4 **non-negotiable hard rules**:

1. **RULE 1 - ZERO TOUCH TO `main` / `master` (Tuyệt đối không đụng vào `main`)**:
   - `main` is owned exclusively by the instructor (teachers).
   - **NEVER** checkout `main` for editing, **NEVER** commit to `main`, **NEVER** push to `main`, and **NEVER** open a Pull Request targeting `main`.
   - Any accidental interaction with `main` risks breaking other groups' projects and incurring severe academic penalties.

2. **RULE 2 - ZERO DIRECT COMMITS TO `Group-3` (Tuyệt đối không commit trực tiếp vào `Group-3`)**:
   - `Group-3` is our team's protected parent trunk. It acts as our team's `develop` branch.
   - **NO DIRECT COMMITS**: Developers and agents must NEVER run `git commit` or `git push` directly on branch `Group-3`.
   - **MERGE ONLY VIA PULL REQUEST**: Code enters `Group-3` **ONLY** by merging tested, peer-reviewed Pull Requests originating from task feature branches (`Group-3/feature/...`).

3. **RULE 3 - MANDATORY EXPLICIT USER CONFIRMATION BEFORE COMMIT OR PUSH**:
   - An AI agent must **NEVER** autonomously execute mutating Git commands (`git commit`, `git push`, `git merge`, `git cherry-pick`, `git reset --hard`) without first presenting:
     1. The current active branch name (must verify it is NOT `main` and NOT `Group-3`).
     2. The exact list of modified/staged files (`git status -s`).
     3. The exact commit message formatted per convention.
   - The agent MUST wait for explicit verbal or written confirmation from the user (e.g., "đồng ý", "commit đi", "confirm") before executing the commit or push command.

4. **RULE 4 - MANDATORY PRE-PUSH HOOK & VALIDATION SCRIPT**:
   - Run `scripts/validate_git_workflow.sh` before staging or pushing. The script halts with exit code 1 if the current branch is `main` or `Group-3` or if the commit message violates project conventions.

## Repository Architecture

```text
[origin/main] (Instructor owned — LOCKED / READ ONLY)
      │
      ▼ (branched at project start)
[origin/Group-3] (Group 3 Team Trunk — PROTECTED: PR Merge Only)
      │
      ├──> [Group-3/feature/US01-T01.1-listing-api] (Dev: Hieu) ──PR──> [Group-3]
      ├──> [Group-3/feature/US01-T01.2-listing-screen] (Dev: Duy)  ──PR──> [Group-3]
      ├──> [Group-3/feature/US05-T05.2-spatial-api]   (Dev: Dung) ──PR──> [Group-3]
      └──> [Group-3/feature/US05-T05.3-map-pins]      (Dev: Quang Anh) ──PR──> [Group-3]
```

## Steps

### Step 1: Identify Task ID and Create a Prefixed Feature Branch from `Group-3`

Before writing any code, verify the exact `User Story ID` (`USxx`) and `Task ID` (`Txx.y`) from `references/sprint1-backlog-matrix.md`.

1. Sync local `Group-3` with the remote repository:
   ```bash
   git checkout Group-3
   git pull origin Group-3
   ```
2. Create a branch following the Group-3 prefixed naming convention:
   - Feature task: `Group-3/feature/<US-ID>-<Task-ID>-<short-kebab-slug>` (or `Group-3-feature/...`)
   - Bug fix: `Group-3/bugfix/<US-ID>-<Task-ID>-<short-kebab-slug>`
   - Hotfix: `Group-3/hotfix/<issue-slug>`
   ```bash
   # Examples for Sprint 1:
   git checkout -b Group-3/feature/US01-T01.1-listing-api-pagination
   git checkout -b Group-3/feature/US01-T01.2-listing-screen-infinite-scroll
   ```

### Step 2: Follow the Backend-to-Frontend Contract-First Handoff

Because tasks within each User Story are split across two developers (one building Backend API `.1` and another building .NET MAUI UI `.2`), agents must prevent integration blockers:

1. **Hour 1 of any User Story (DTO & Swagger Freeze)**:
   - The Backend developer (`Txx.1`) defines Request/Response DTOs and route signatures first, returning mock data if database logic is in progress.
   - Export the OpenAPI/Swagger JSON contract so the Frontend developer (`Txx.2`) can immediately build Refit/HttpClient services and MVVM ViewModels against exact property names.
2. **Parallel Implementation**:
   - Backend developer implements Cosmos DB repository queries, spatial indexing, and xUnit tests (refer to `room-radar-backend-dotnet`).
   - Frontend developer implements XAML views, CommunityToolkit.Mvvm bindings, local SQLite/Barrel caching, and Appium UI tests (refer to `room-radar-frontend-maui`).

### Step 3: Format Commit Messages, Validate Locally & Seek Confirmation

1. Format commit message using the project convention:
   ```text
   <type>(<US-ID>/<Task-ID>): <imperative summary under 72 chars>
   ```
   Allowed `<type>` values: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`.

2. Run validation script:
   ```bash
   bash scripts/validate_git_workflow.sh
   ```

3. **Obtain Explicit Confirmation**:
   - Agent presents branch, file diff summary, and commit message to user.
   - Only execute `git commit` and `git push origin <feature-branch>` AFTER user confirms.

### Step 4: Submit Pull Request Targeting `Group-3` and Execute Peer Review

**Rule**: Always set the target/base branch of the PR to `Group-3` (NEVER `main`). Open a PR using the template in `references/pr-and-dod-checklist.md`.

Follow the **Cross-Pair Peer Review Matrix**:
- **Nguyen Minh Hieu** <-> **Be Van Duy** (Review each other's PRs on US01, US03, US06, US08)
- **Pham Anh Dung** <-> **Dang Quang Anh** (Review each other's PRs on US02, US04, US05, US07)
- **Scrum Master (Nguyen Trung Thành)**: Final check that Acceptance Criteria (`AC1`-`AC4`), unit tests, and Sprint Tracking hours are updated before merging into `Group-3`.

### Step 5: Update Sprint Burndown and Enforce Quality Objectives

At the end of each task:

1. Update task status (`Todo` -> `Doing` -> `Done`) and record `Actual (hours)` in the Sprint Tracking sheet.
2. Verify all 3 Project Quality Objectives before Sprint Review:
   - **Backlog Completion**: 100% of committed Sprint User Stories completed.
   - **Defect Threshold**: >= 95% of User Stories pass all Acceptance Criteria on first PO review with **0 Critical** and **<= 3 High** defects.
   - **Performance Budgets**: Listing load <= 3s (for 500 listings), Filter search <= 2s, Alert push <= 15 mins.

## Gotchas

- **Accidental commit to Group-3**: If you accidentally committed directly on `Group-3` locally, do NOT push. Create a feature branch from the commit, reset local `Group-3` back to `origin/Group-3`, and open a PR.
- **Accidental PR to main**: GitHub defaults base branch to `main`. Always manually switch the base dropdown to `Group-3` when creating the PR.
- **Autonomous commit without approval**: Agents must never run silent commits. Always confirm with the human developer first.

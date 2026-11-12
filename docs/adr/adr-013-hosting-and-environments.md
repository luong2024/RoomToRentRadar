# ADR-013: Environments, hosting and target platforms

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** NFR-09, NFR-11, ADR-002, ADR-009, ADR-010, `.github/workflows/ci.yml`

## Context
Students develop on Windows laptops; demos happen on Android phones/emulators; the budget favours free resources; SQLite, SignalR and background jobs require a single API instance.

## Decision
| Environment | API | Client | Data | External services |
|---|---|---|---|---|
| **Development** | `dotnet run` on `http://localhost:5000` | Android emulator → `http://10.0.2.2:5000`; physical phone → **Visual Studio Dev Tunnels** (free) URL | local `roomtorentradar.db` (git-ignored), seed script | mocks allowed (SSO, push, routing) via `appsettings.Development.json` only |
| **Demo / Pilot** | Single instance: Azure App Service (Azure for Students credit, B1 for WebSockets) **or** a team laptop + Dev Tunnel | Release APK (sideload) | SQLite file on persistent storage, daily backup copy | real FCM, ORS key, Google Maps key from secrets |
| **CI** | build + unit tests (`ubuntu-latest`) | Android build | in-memory SQLite | mocks |

- **Target platforms:** Android (API 24+) primary; iOS best-effort (requires Mac + Apple account for push); Windows and MacCatalyst **not supported** in MVP (MAUI Maps unavailable on Windows) — they may stay in `TargetFrameworks` for build only until the team removes them.
- Secrets (JWT key, ORS key, Firebase service account, Maps key) via user-secrets/environment variables, never committed.

## Consequences
- Positive: zero mandatory cost; realistic demo on phones.
- Negative: no high availability (accepted for pilot).

## Compliance
Startup fail-fast checks (GUARDRAILS 1.5.4); `.gitignore` covers `*.db`, `google-services.json`, `GoogleService-Info.plist`.

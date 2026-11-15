# ADR-013: Environments and demo setup (course scope, no public hosting)

- **Status:** Accepted (revised 2026-11-12: scoped down to a course project — no public server, no domain, no app distribution)
- **Date:** 2026-11-11, revised 2026-11-12
- **Related:** NFR-09, NFR-11, ADR-002 (SQLite), ADR-005 (Google sign-in), ADR-006 (maps), ADR-008 (local media), ADR-009 (SignalR), ADR-010 (FCM), `docs/business-analysis.md` §10 (external services), `.github/workflows/ci.yml`

## Context
This is a university course project. The app is demonstrated by the team to the instructor; it is **not** shared with outside users and **not** published to Google Play / App Store. Everything must cost 0 VND.

Constraints from earlier decisions: SQLite file + local-disk media + in-process SignalR + `BackgroundService` ⇒ **exactly one API process with a local disk** — a developer laptop satisfies this.

## Options considered
| Option | Verdict |
|---|---|
| **API on a team laptop** (`dotnet run`, optionally `docker compose up`), emulator + team phones | **Chosen.** Free, no accounts, same as daily development |
| Public VM + Docker Compose + Caddy + free subdomain (DuckDNS) | Not needed — only useful when outside users must reach the app. Kept as a documented upgrade path (see Consequences) |
| Vercel / Netlify | Rejected even as an upgrade path: serverless only — no persistent disk (SQLite and photos lost), no long-running process for SignalR / `BackgroundService`, no .NET runtime |
| Render free tier | Rejected: sleeps after ~15 min idle, no persistent disk on the free plan |
| Azure App Service | Rejected: `/home` is an SMB share — SQLite WAL/locking is unsafe there |

## Decision
| Environment | API | Data | Client | External services |
|---|---|---|---|---|
| **Development** | `dotnet run --launch-profile http` (`http://localhost:5000`) | local `roomtorentradar.db` + `media/` (git-ignored), dev seed | emulator → `http://10.0.2.2:5000` | mocks allowed only in `appsettings.Development.json` |
| **CI** | build + unit tests | in-memory SQLite | Android build | mocks |
| **Demo** (instructor review, final presentation) | one team laptop, same command, mocks **off** | demo seed DB on that laptop | emulator on the laptop; physical team phones reach the API through a **Microsoft Dev Tunnels** HTTPS URL (free, persistent tunnel) | real Google sign-in, Maps, ORS, FCM keys from user-secrets / environment variables (never in git) |

- **Android only** (NFR-09). The app is installed on team phones directly from Visual Studio / `dotnet build -t:Install` (debug or locally signed build). No APK distribution service.
- Google sign-in and the Maps key are restricted to the app's package name + the **SHA-1 of each developer's debug keystore** (several fingerprints can be registered), or one shared debug keystore kept outside git.
- A Development-only setting lets the app use the Dev Tunnels URL instead of `10.0.2.2` (no rebuild for each tunnel).
- Backups: copy the demo `roomtorentradar.db` + `media/` before each demo.

## Consequences
- Positive: zero cost, zero hosting accounts, no server to patch; demo = the environment the team already uses.
- Negative: the demo depends on one laptop being online; outside users cannot try the app (accepted — not a course requirement).
- Upgrade path, only if the instructor later requires an online deployment: multi-stage `Dockerfile` (non-root, `HEALTHCHECK /api/health`) + `docker-compose.yml` with `api` + `caddy`, a free/student Linux VM, a DuckDNS subdomain, nightly `sqlite3 .backup`. Not planned work.
- Google Play publication remains out of scope (it would require a developer fee, privacy policy, Data safety form and in-app account deletion).

## Compliance
Startup fail-fast checks (GUARDRAILS 1.5.4): real keys required outside Development; the forbidden-files CI job keeps `*.db`, keystores, `.env` and Firebase files out of git.

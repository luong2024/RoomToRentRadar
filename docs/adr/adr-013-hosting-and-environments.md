# ADR-013: Environments, free hosting and app distribution (course scope)

- **Status:** Accepted (revised 2026-11-12, second revision: scoped down to a course project; App Service option rejected in the first revision)
- **Date:** 2026-11-11, revised 2026-11-12
- **Related:** NFR-09, NFR-11, ADR-002 (SQLite), ADR-005 (Google sign-in), ADR-006 (maps), ADR-008 (local media), ADR-009 (SignalR), ADR-010 (FCM), `.github/workflows/ci.yml`

## Context
This is a university course project, not a store product. The app must run on a **real server with HTTPS** so the instructor, the team and invited testers can use it from their own Android phones, at **zero cost**. No Google Play / App Store release is planned.

Constraints from earlier decisions: SQLite file + local-disk media + in-process SignalR + `BackgroundService` ⇒ **exactly one always-on API process with a local persistent disk**. A stable public HTTPS URL is also needed because (a) the release APK has the API URL built in and (b) Google OAuth redirect URIs must be registered in advance (ADR-005).

## Options considered (server)
| Option | Cost | Verdict |
|---|---|---|
| **One Linux VM + Docker Compose + Caddy** (Azure for Students VM; any free/student VPS credit) | Free with student credit (Azure for Students includes 750 h/month of a B1s-class VM for 12 months — check current terms) | **Chosen.** Local disk is safe for SQLite, WebSockets work, Caddy gives automatic HTTPS |
| Vercel / Netlify | Free | **Rejected:** serverless functions only — no persistent file system (SQLite and photos would be lost), no long-running process for SignalR or `BackgroundService`, .NET is not a supported runtime. Fine for a static download page only |
| Render free web service | Free | **Rejected:** sleeps after ~15 min idle (slow first request, SignalR drops), no persistent disk on the free plan ⇒ data loss on every redeploy |
| Azure App Service | Free/Basic tiers | **Rejected:** persistent `/home` is an SMB network share — SQLite WAL/locking is unsafe there; Free tier limits WebSockets |
| Team laptop + tunnel (Microsoft Dev Tunnels / Cloudflare Tunnel) | Free | **Fallback only** (demo day if the VM fails): the laptop must stay on; a quick-tunnel URL changes on every start |

## Options considered (domain)
| Option | Cost | Verdict |
|---|---|---|
| **Free subdomain from DuckDNS** (e.g. `rtrr-ictu.duckdns.org`) pointing to the VM's public IP | Free | **Chosen.** Works with Caddy's automatic Let's Encrypt certificate |
| Free domain from the GitHub Student Developer Pack | Free for 1 year (offers change) | Optional nicer name; do not depend on it |
| Paid `.com`/`.vn` domain | ~250k–800k VND/year | Not needed |

## Decision
| Environment | API | Data | Client | External services |
|---|---|---|---|---|
| **Development** | `dotnet run` (`http://localhost:5000`) or `docker compose up` | local `roomtorentradar.db` + `media/` (git-ignored), dev seed | emulator → `http://10.0.2.2:5000`; physical phone → Dev Tunnels URL | mocks allowed only in `appsettings.Development.json` |
| **CI** | build + unit tests (+ Docker image build without push, late stage) | in-memory SQLite | Android build | mocks |
| **Demo server** (one VM, the only shared environment) | `docker compose up -d --build` from a checkout of `group-2-main`, behind **Caddy** (HTTPS, WebSocket proxy) at `https://<name>.duckdns.org` | named volume `/data` → `/data/roomtorentradar.db`, `/data/media` | release APK pointing to the demo URL | real Google sign-in, FCM, ORS, Maps keys from a `.env` file on the VM (never in git) |

Container rules:
- Multi-stage `Dockerfile` (SDK image builds, `aspnet` runtime image runs) as a **non-root** user; `HEALTHCHECK` on `/api/health`; configuration only through environment variables (`Sqlite__ConnectionString=Data Source=/data/roomtorentradar.db`, `Media__RootPath=/data/media`, `Jwt__SigningKey`, …).
- `docker-compose.yml` services: `api`, `caddy` (ports 80/443, `Caddyfile` with `reverse_proxy api:8080`), `restart: unless-stopped`.
- **Deploy:** manual — SSH to the VM, `git pull && docker compose up -d --build`. GitHub Actions CD (GHCR + SSH) is optional, only if time allows.
- **Backups:** a cron job runs `sqlite3 /data/roomtorentradar.db ".backup /backup/rtrr-$(date +%F).db"` nightly and keeps 7 days; before each demo copy the latest backup + `media/` off the VM. Restore is tried once.
- **Monitoring:** `docker logs`; optional free uptime check on `/api/health`.

## Mobile app distribution (how other people use the app)
- **Android only.** iOS is out of scope (needs a Mac and a paid Apple Developer account to install on other people's iPhones). Windows/MacCatalyst are not supported.
- Build a **signed release APK** with one shared team keystore (stored outside git, e.g. in the team's private drive; its SHA-1 is registered for Google sign-in and the Maps key).
- **Distribute through Firebase App Distribution** (free; the same Firebase project as FCM): testers are invited by e-mail, get a download link and update notifications. Alternative: attach the APK to a GitHub Release and share a QR code/link.
- Testers enable *Install unknown apps* for their browser/App Tester once. Google Play Protect may show a warning for an unknown developer — expected for a course app.
- **Risk to watch:** Google is rolling out *developer verification* for apps installed outside Play (first countries from 2026, global from 2027). Vietnam was not in the first wave; if it applies before the final demo, register a free student/hobbyist Android Developer Console account (limited number of devices) or demo on the team's own phones and the emulator.
- Google Play is **not** planned. If it ever is: one-time developer fee, public privacy policy, Data safety form and in-app account deletion become mandatory (Post-MVP, `docs/prd.md` §5.2).

## Consequences
- Positive: total cost 0 VND; one image from laptop to server; SQLite and media on a local disk; HTTPS by default; anyone with an Android phone and an invite can use the app.
- Negative: single VM = single point of failure (accepted); the team patches the VM; student credit expires after 12 months (enough for the course).
- Follow-up tasks (late stage, before integration testing): `Dockerfile` + `docker-compose.yml` + `Caddyfile`; VM + DuckDNS setup; backup cron; release keystore + Firebase App Distribution; Development-only "API base URL" override for tunnel demos.

## Compliance
Image runs as non-root and passes the health check; startup fail-fast checks (GUARDRAILS 1.5.4); the forbidden-files CI job keeps `*.db`, keystores, `.env` and Firebase files out of git.

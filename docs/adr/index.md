# Architecture Decision Records (ADR)

An ADR records **one** significant decision: context, options considered, the decision and its consequences. ADRs are immutable once `Accepted`; to change a decision write a new ADR that `Supersedes` the old one and mark the old one `Superseded by ADR-xxx`.

> **For AI agents:** treat `Accepted` ADRs as hard constraints. If a task seems to require violating one, stop and ask the user instead of working around it. New technology/package choices require a new ADR (and the NuGet approval step in `.agents/GUARDRAILS.md` §2).

File naming: `adr-<NNN>-<kebab-case-title>.md` (the number is a permanent ID, not a reading order). Template: [adr-template.md](adr-template.md).

| ID | Title | Status | Date |
|---|---|---|---|
| [ADR-001](adr-001-hexagonal-architecture.md) | Hexagonal architecture (Ports & Adapters) with DDD core | Accepted | 2026-11-09 |
| [ADR-002](adr-002-sqlite-single-datastore.md) | SQLite as the single server datastore | Accepted | 2026-11-11 |
| [ADR-003](adr-003-client-server-communication.md) | Client ↔ server: REST/JSON via typed client gateways; client does not host the Application layer | Accepted | 2026-11-11 |
| [ADR-004](adr-004-client-offline-cache.md) | On-device SQLite cache with cache-then-network | Accepted | 2026-11-11 |
| [ADR-005](adr-005-authentication-and-session.md) | JWT + rotating refresh tokens, ICTU SSO via OIDC | Accepted | 2026-11-10 |
| [ADR-006](adr-006-maps-and-spatial-search.md) | .NET MAUI Maps (native Google/Apple) + in-process spatial filtering | Accepted | 2026-11-11 |
| [ADR-007](adr-007-routing-and-travel-time.md) | OpenRouteService for travel time, cached, with straight-line fallback | Accepted | 2026-11-11 |
| [ADR-008](adr-008-media-storage-and-360-viewer.md) | Photo storage behind a port, SkiaSharp renditions, Pannellum 360° viewer | Accepted | 2026-11-11 |
| [ADR-009](adr-009-in-app-chat.md) | In-app chat: HTTP send + SignalR receive + FCM offline | Accepted | 2026-11-11 |
| [ADR-010](adr-010-push-notifications-and-alerts.md) | Firebase Cloud Messaging + in-process alert matcher | Accepted | 2026-11-11 |
| [ADR-011](adr-011-listing-moderation-state-machine.md) | Listing lifecycle state machine and two-level verification | Accepted | 2026-11-11 |
| [ADR-012](adr-012-api-contract-and-error-model.md) | Design-first OpenAPI, ProblemDetails + errorCode, offset paging, ETag concurrency | Accepted | 2026-11-11 |
| [ADR-013](adr-013-hosting-and-environments.md) | Environments, hosting and target platforms | Accepted | 2026-11-11 |

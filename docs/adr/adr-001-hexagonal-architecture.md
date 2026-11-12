# ADR-001: Hexagonal architecture (Ports & Adapters) with DDD core

- **Status:** Accepted (records the decision already implemented on `develop2`, commits `286132d`, `f50a5e8`)
- **Date:** 2026-11-09
- **Deciders:** Group 2 (Scrum Master Nguyễn Văn Tuân)
- **Related:** `.agents/GUARDRAILS.md` §1.3, §1.6; [system-architecture.md](../system-architecture.md)

## Context
Five developers work in parallel on 14 stories, with AI agents generating much of the code. We need (a) a testable business core, (b) the ability to swap infrastructure (SQLite/Cosmos, mock/real SSO, mock/real routing) without touching business code, and (c) hard boundaries that an automated test can check.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| Single MAUI project + single API project (layers as folders) | Simplest, fastest to start | Boundaries not enforceable; business logic leaks into controllers/PageModels |
| N-tier (UI → BLL → DAL) | Familiar | Core depends on DAL; hard to mock; DB-first thinking |
| **Hexagonal + DDD (separate projects)** | Core is pure C#; ports make every external dependency mockable; dependency rule checkable by reflection tests | More projects/ceremony; learning curve for the team |

## Decision
We use Hexagonal architecture with these projects and dependency directions (arrows = "references"):

```
Domain ◀── Ports ◀── Application
   ▲          ▲          ▲
   └── Datastore.Sqlite, Infrastructure (implement Outbound Ports)
Api ──▶ Application, Ports, Infrastructure, Datastore.Sqlite  (composition root)
Client.Adapters ──▶ Ports (Contracts only)       (see ADR-003)
RoomToRentRadar (MAUI) ──▶ Ports, Client.Adapters (composition root)
```

Rules: Core (`Domain`, `Ports`, `Application`) has no reference to MAUI, ASP.NET Core, HTTP, IdentityModel, SQLite or Cosmos; 1 Use Case = 1 Inbound Port with a single `ExecuteAsync`; aggregates protect invariants and expose explicit state transitions.

## Consequences
- Positive: parallel work on contracts; ≥ 110 fast unit tests already; infrastructure swaps are cheap.
- Negative: boilerplate (port + use case + DTO + adapter per feature) — mitigated by Mini-Spec templates in `.agents/`.
- Follow-up: keep `AGENTS.md` project map in sync with the real 10+1 projects (currently drifted).

## Compliance
`RoomToRentRadarUnitTests/Architecture/ArchitectureTests.cs` (forbidden references, concurrency token on aggregates, no public parameterless ctors, mocks off in `appsettings.json`). PR review checklist in GUARDRAILS §3.

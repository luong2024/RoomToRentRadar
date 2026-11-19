# ADR-002: SQLite as the single server datastore

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** Group 2 (decision confirmed by Lại Văn Thịnh on behalf of the team)
- **Related:** [erd.md](../erd.md), ADR-006 (spatial search), Project Plan risk #3 (cloud cost)

## Context
The plan originally named Azure Cosmos DB, and `develop2` contains both `Datastore.CosmosDb` and `Datastore.Sqlite`. Maintaining two persistence implementations doubles the work for every story and every test (LSP requires identical behaviour). Pilot scale is small: ~100 listings, ~50 users, at most a few thousand rows per table. The team decided the project uses **SQLite**.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| Cosmos DB only | Native GeoJSON spatial queries; cloud scale | Cost/quota (risk #3), emulator heavy on student laptops, no joins, team unfamiliar |
| Both (switchable) | Flexibility | 2× repositories and tests per feature; drift risk |
| **SQLite only** | Zero cost, zero setup, file-based, fast for this scale, SQL joins for favourites/compare/chat, same engine already on the device | Single writer (fine at pilot scale); no native spatial index; no horizontal scale-out |

## Decision
- The API uses **SQLite only** (`RoomToRentRadar.Datastore.Sqlite`) via `Microsoft.Data.Sqlite` with hand-written SQL (no EF Core — keeps queries explicit and fast, matches existing code).
- `RoomToRentRadar.Datastore.CosmosDb` is **frozen**: no new features; it may be removed later by team decision. `DatabaseProvider` stays `Sqlite`.
- Pragmas `WAL`, `foreign_keys=ON`, `busy_timeout=5000`. One `SqliteConnection` per operation (pooling on).
- Schema evolves through ordered migration scripts (`Migrations/NNN_<name>.sql`) applied at startup by a runner using `PRAGMA user_version`; replaces the current `CREATE TABLE IF NOT EXISTS` block.
- Spatial queries: bounding-box prefilter on indexed `Latitude/Longitude`, exact Haversine / point-in-polygon in a Domain service (see ADR-006).
- Optimistic concurrency with `Version INTEGER` columns (GUARDRAILS 1.5.2).
- The database file is **never committed**; add `*.db` to `.gitignore` and remove `RoomToRentRadar.Api/roomtorentradar.db` from git.

## Consequences
- Positive: one implementation per port; tests run against real SQLite in-memory; no cloud bill.
- Negative: single API instance only (also required by in-process SignalR/background jobs — acceptable for pilot). Backups = copying the file (daily on the host).
- Follow-up: migration runner; remove the committed `.db`; update `AGENTS.md`/GUARDRAILS that still mention Cosmos as primary.

## Compliance
No new code under `RoomToRentRadar.Datastore.CosmosDb`; repository tests use `Data Source=:memory:`; `.gitignore` contains `*.db`.

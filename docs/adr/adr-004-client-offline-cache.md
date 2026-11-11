# ADR-004: On-device SQLite cache with cache-then-network

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** ADR-003, [erd.md §5](../erd.md#5-client-side-cache-sqlite-on-device), NFR-01, NFR-07, US03 AC3, US06 AC4

## Context
Students browse on unstable 4G. US01 AC3 requires the list in ≤ 3 s; NFR-07 requires graceful offline reads; US06 needs favourite changes to survive brief disconnections. The MAUI project already references `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3`.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| No cache (always network) | Simplest | Blank screens/spinners on slow network; no offline |
| In-memory cache only | Easy | Lost on app restart; no offline after relaunch |
| `Preferences`/JSON files | No DB | No querying/expiry; size limits |
| **SQLite cache (existing packages) + cache-then-network** | Persistent, queryable, expiry, offline queue; no new package | Some code for invalidation |
| Akavache / LiteDB | Ready-made | New dependency; Akavache maintenance uncertain on .NET 10 |

## Decision
- Cache file `roomtorentradar-cache.db3` (schema in ERD §5), accessed only through `ICacheStore` in `Client.Adapters/Caching`.
- **Cache-then-network** for: first page of room queries, room detail (with `ETag`/`If-None-Match` → `304`), favourites, campuses (7 days), conversations/messages. UI shows cached data immediately, then replaces it when the network answer arrives (PageModel exposes `IsRefreshing`, `IsOffline`).
- **Network-only** for: search results beyond page 1, compare, travel time (server caches it), admin queues, all mutations.
- **Offline queue** (`PendingOperations`) only for idempotent mutations: favourite add/remove and chat send (`clientMessageId`). Replayed on connectivity change. Other mutations require network and show "Cần kết nối mạng".
- Images use MAUI `UriImageSource` caching (`CacheValidity` 7 days) — thumbnails are small (≤ 60 KB).
- Tokens are **never** stored in the cache DB (SecureStorage only, ADR-005). Cache is wiped on sign-out.

## Consequences
- Positive: instant repeat views, offline favourites/details, fewer requests.
- Negative: stale data window (mitigated: always revalidate; status shown on detail).
- Follow-up: connectivity monitor in `DeviceAdapters/`.

## Compliance
Unit tests for cache decorators (hit/miss/expired/304); manual airplane-mode test in UAT checklist.

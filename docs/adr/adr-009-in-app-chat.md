# ADR-009: In-app chat — HTTP send, SignalR receive, FCM when offline

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** US14, BR-CHT-01..07, ERD `Conversations`/`Messages`, ADR-010

## Context
Besides phone and Zalo, students must be able to chat with landlords inside the app. Messages must arrive in real time when online and via push when offline; mobile networks drop often, so sending must be retry-safe.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| Firebase Firestore chat | Real-time, offline built in | Second backend/datastore; security rules separate from our RBAC; data outside SQLite |
| Third-party chat SDK (Stream, Sendbird) | Rich features | Paid beyond small tiers; vendor lock-in |
| Polling every N s | Trivial | Battery/data waste, latency |
| **ASP.NET Core SignalR (built-in, free) + SQLite persistence + FCM for offline** | Same server, same auth (JWT), same DB; WebSockets with automatic fallback; MAUI client package from Microsoft | We implement history/unread ourselves (small) |

## Decision
- **Send via HTTP** `POST /api/conversations/{id}/messages { clientMessageId, body }` — goes through normal auth, validation, rate limiting and idempotency (`UQ(SenderId, ClientMessageId)`), easy to retry from the offline queue.
- **Receive via SignalR** hub `/hubs/chat` (JWT via `access_token` query string for WebSockets). Server pushes `MessageReceived(MessageDto)` and `ConversationRead(conversationId, userId, lastReadAtUtc)` to user groups `user:{userId}`. The client connects while the app is in the foreground and reconnects automatically.
- **Offline recipient** (no active connection) → FCM push "New message from <name>" with deep link (ADR-010).
- History: keyset paging `GET …/messages?before=<cursor>&limit=30`. Unread = messages after `ConversationParticipants.LastReadAtUtc`.
- Port split (ISP): `IConversationRepository`, `IMessageRepository`, `IRealtimeNotifierPort` (SignalR adapter in Api), `IPushNotificationPort`.

## Consequences
- Positive: no extra service or cost; consistent security; reliable sending.
- Negative: SignalR in-process ⇒ single API instance (accepted in ADR-013: one API process on the demo laptop; Dev Tunnels forwards WebSockets).
- Security: the JWT travels in the `access_token` query string for WebSockets, so request logging and the audit middleware MUST NOT log query strings for `/hubs/*`.
- Follow-up: NuGet `Microsoft.AspNetCore.SignalR.Client` for the client adapters (approval per GUARDRAILS).

## Compliance
Tests: non-participant cannot read/send (404), idempotent resend returns the same message, rate limit 429, hub authorization.

# ADR-010: Firebase Cloud Messaging + in-process alert matcher

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** US07, US14, BR-ALR-01..06, ERD `SavedSearches`, `SavedSearchAlertLogs`, `DeviceRegistrations`, `Notifications`

## Context
US07 requires push alerts within 15 minutes, max 3 per saved search per day, pause/delete at any time. Chat (ADR-009) and moderation outcomes also need push. Free tools preferred; one API instance.

## Options considered
| Concern | Options | Choice |
|---|---|---|
| Push provider | **FCM (free, Android + iOS via APNs)** · OneSignal (free tier, extra vendor) · Azure Notification Hubs (paid tiers) | FCM |
| Background processing | Hangfire/Quartz (new deps, extra tables) · Azure Functions (extra service) · **`BackgroundService` + `System.Threading.Channels` (built-in)** | Built-in |
| Trigger | Polling only · **event on approval + 5-min sweep** | Both: fast path + safety net |

## Decision
- `IPushNotificationPort` → `FcmPushNotificationAdapter` (server SDK `FirebaseAdmin`, service-account JSON from secret) and `MockPushNotificationAdapter` (Development, logs only; disabled by default).
- Client registers its FCM token with `PUT /api/me/devices/{installationId}` after sign-in and on token refresh; `DELETE` on sign-out. Client plugin: `Plugin.Firebase.CloudMessaging` — **verify .NET 10 compatibility at implementation time**; alternative `Shiny.Push`. Android 13+ asks `POST_NOTIFICATIONS` at the moment the user enables an alert (not on first launch).
- `ApproveListingUseCase` publishes `ListingPublished` to an in-process channel; `AlertMatcherService` (`BackgroundService`) consumes it and also sweeps every 5 min for rooms published in the last 30 min without logs. Matching = Domain `SavedSearchCriteria.Matches(room, campus)`.
- Daily cap via `SavedSearchAlertLogs` count where `LocalDate = today(Asia/Ho_Chi_Minh) AND WasPushed = 1`.
- Every push also creates an inbox `Notification` (works without permission; deep links).
- iOS push requires an Apple Developer account — Android is the demo platform (ADR-013).

## Consequences
- Positive: free; ≤ seconds latency typical; idempotent.
- Negative: Firebase project setup (google-services.json not committed; injected in CI/local).
- Course scope (2026-11-12): FCM is free with no card (Firebase Spark plan). The same Firebase project also hosts App Distribution for the APK (ADR-013). Push is a "nice to have" for the demo: if the client plugin is not compatible with .NET 10 in time, alerts still land in the in-app Notifications inbox (above) and push is listed as a known limitation.

## Compliance
Unit tests for matcher (filters, polygon, distance, cap, idempotency); mock adapter default `false` in `appsettings.json` (architecture test).

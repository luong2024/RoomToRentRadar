# ADR-005: JWT + rotating refresh tokens, ICTU SSO via OIDC

- **Status:** Accepted (records US13 implementation on `develop2`, PR #3; client part added)
- **Date:** 2026-11-10
- **Related:** BR-AUTH-01..07, `.agents/plans/US13_BACKEND_PLAN.md`, ADR-003

## Context
Students and landlords need secure accounts; ICTU students should be able to use their university identity; the mobile app must stay signed in for weeks without storing passwords.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| Cookie sessions | Simple on web | Awkward on mobile; CSRF concerns |
| ASP.NET Core Identity + EF Core | Full-featured | Pulls EF Core; heavy for our hexagonal core |
| External IdP only (Firebase Auth, Auth0) | Offloads security | Landlords without ICTU accounts; vendor lock-in; cost tiers |
| **Own JWT issuance + refresh rotation; ICTU OIDC id_token verification** | Fits hexagon (ports), already built and tested | We own the security code (mitigated by tests and GUARDRAILS) |

## Decision
- Server (implemented): PBKDF2-SHA256 100k iterations; JWT access 30 min (`sub`, `email`, `role`, `is_verified`, `jti`); refresh 7 days stored hashed, rotated each use, reuse detection revokes all sessions; lockout 5 failures / 15 min; policies `StudentPolicy`, `LandlordPolicy`, `VerifiedLandlordPolicy`, `AdminPolicy`. Mock SSO only in `Development`; startup fails fast without a signing key outside Development.
- Client: refresh token in **`SecureStorage`** (Keystore/Keychain), access token in memory; `AuthTokenHandler` refreshes once on `401` with a single-flight lock, then retries the request; on refresh failure → local sign-out. ICTU SSO uses `WebAuthenticator` (system browser, PKCE) to obtain the id_token.
- After landlord approval the client calls `refreshToken` to obtain a token with `is_verified = true`.
- Auth endpoints are rate-limited (fixed window 10/min/IP for login & register).

## Consequences
- Positive: no third-party cost; offline-friendly long sessions.
- Negative: `is_verified` claim can be stale up to 30 min — acceptable; server re-checks landlord status in `CreateListing` use case.
- Follow-up: OQ-3 (real SSO provider).

## Compliance
Existing tests in `RoomToRentRadarUnitTests/Application/Auth` and `Infrastructure/*`; architecture test for mocks disabled in root config.

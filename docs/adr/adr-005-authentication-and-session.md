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
- ICTU SSO provider is **Google** (decided 2026-11-11). The real verifier checks Google's signing keys (JWKS), `aud` = our OAuth client ID, `email_verified`, `hd = ictu.edu.vn`. The client opens Google's authorization endpoint with `hd=ictu.edu.vn` as a UI hint only — the server-side `hd` check is the real enforcement.
- **Setup in course scope (revised 2026-11-12) — no ICTU IT involvement by default:**
  - One Google Cloud project owned by the team (free). OAuth consent screen *External*, scopes `openid email profile` only. Basic scopes need no Google verification, so set the publishing status to **In production**; in *Testing* status only the ≤ 100 listed test users can sign in.
  - Anyone with a Google account can complete Google's sign-in; **recognising ICTU students is our server's job** (BR-AUTH-04: `hd = ictu.edu.vn`, e-mail ends with `@ictu.edu.vn`, `email_verified`). ICTU IT does not need to issue anything.
  - ICTU IT is needed **only if** the ICTU Google Workspace admin restricts "unconfigured third-party apps" — users would then see *Access blocked*. Test once early with a real `DTC…@ictu.edu.vn` account; if blocked, ask IT to mark our client ID as trusted. Fallback for the demo: ICTU students register with e-mail + password (BR-AUTH-01).
  - **Redirect to the app:** Google disables custom-URI-scheme redirects (`<ApplicationId>:/oauth2redirect`; the placeholder `com.companyname.roomtorentradar` must be replaced by a final ID before the client is registered) for new Android OAuth clients by default. Chosen: enable *Custom URI scheme* in the Android client's *Advanced settings* and keep `WebAuthenticator` + PKCE (least code). Risk: Google may remove this option; then switch to the Google Identity Services / Credential Manager SDK, which returns the id_token directly (new ADR). The Android client needs the package name + the team keystore SHA-1 (ADR-013).
  - Client IDs live in configuration, never in git.

## Compliance
Existing tests in `RoomToRentRadarUnitTests/Application/Auth` and `Infrastructure/*`; architecture test for mocks disabled in root config.

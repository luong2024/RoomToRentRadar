# Product Requirements Document (PRD) — RoomToRentRadar

| Field | Value |
|---|---|
| Product | RoomToRentRadar — Student Room Rental Radar |
| Team | Group 2 — Software Development Process Training Course (ICTU) |
| Product Owner | Vũ Huy Lượng (instructor) |
| Scrum Master | Nguyễn Văn Tuân |
| Status | Draft v1.0 — 2026-11-11 |
| Source of truth for scope | `06. Project Plan - Group 2.xlsx` (sheet *Sprint 1* = full MVP backlog US01–US13) |
| Related docs | [business-analysis.md](business-analysis.md) · [use-cases.md](use-cases.md) · [erd.md](erd.md) · [system-architecture.md](system-architecture.md) · [ui-ux-spec.md](ui-ux-spec.md) · [api/openapi.yaml](api/openapi.yaml) · [adr/](adr/index.md) |

> **Reading rule for AI agents:** this file defines *what* and *why*. Exact field names live in [erd.md](erd.md), exact endpoints in [api/openapi.yaml](api/openapi.yaml), business rules (`BR-*`) in [business-analysis.md](business-analysis.md). If two documents disagree, stop and ask — do not guess.

---

## 1. Vision

> *"Rightmove for ICTU students."* A mobile app where students find **verified** rooms near their campus in minutes — browse, filter, draw an area on the map, compare, save, get alerted, and contact the landlord safely — instead of scrolling through unverified Facebook/Zalo groups.

Reference product: **Rightmove** (rightmove.co.uk). We adopt its proven patterns — location-first search, draw-a-search, rich listing pages, favourites, saved searches with alerts, agent (landlord) portal with moderation — and adapt them to the Vietnamese student rental market (VND prices, utilities priced per kWh/m³, motorbike commute, Zalo as the default messenger).

## 2. Problem statement

| Today (as-is) | Consequence |
|---|---|
| Rooms are advertised in Facebook groups, Zalo groups, flyers on walls. | Information is scattered, outdated ("đã cho thuê"), and hard to compare. |
| No verification of landlords or listings. | Scams (fake deposits), misleading photos, hidden fees. |
| Prices exclude utilities (electricity, water, internet, parking). | Students cannot compute the real monthly cost. |
| No notion of "distance to my campus". | Students waste time visiting rooms that are too far. |

## 3. Goals & success metrics

| ID | Goal | Metric | Target (from Project Plan) |
|---|---|---|---|
| G1 | Deliver the full MVP backlog | % of US01–US13 meeting all AC | 100% |
| G2 | Quality before handover | Defects at acceptance | 0 Critical, ≤ 3 High; ≥ 95% stories meet AC |
| G3 | Stakeholder satisfaction | PO + pilot-user satisfaction | ≥ 90% |
| G4 | Pilot adoption | Pilot users / verified listings | 50 pilot users, 100 verified listings, 3 faculties |
| G5 | Trust | Share of listings in the feed that are verified | 100% (unverified listings are never shown) |

## 4. Users (personas — details in [business-analysis.md §4](business-analysis.md#4-personas))

| Persona | Role in system | Primary job-to-be-done |
|---|---|---|
| **Student** (ICTU and other Thái Nguyên University students) | `Student` | Find a safe, affordable room near campus fast. |
| **Landlord** | `Landlord` (unverified → verified by Admin) | Fill rooms quickly with reliable tenants. |
| **Admin / Moderator** (university staff or team) | `Admin` | Keep the platform trustworthy: approve landlords, moderate listings and reports. |
| **Guest** | anonymous | Browse before deciding to sign up. |

## 5. Scope

### 5.1 In scope — MVP (all must ship)

The MVP is exactly the 13 user stories of the project plan plus one addition decided by the team (US14, in-app chat). Story points from the plan.

| Epic | ID | User story (short) | SP | Priority |
|---|---|---|---|---|
| E1 Discovery | **US01** | Browse verified rooms near my campus | 5 | Must |
| E1 Discovery | **US02** | View full room details | 4 | Must |
| E1 Discovery | **US03** | Photo gallery and 360° view | 3 | Must |
| E2 Search | **US04** | Search by price, area, room type, distance | 6 | Must |
| E2 Search | **US05** | Draw an area on the map and search inside it | 6 | Must |
| E3 Decide | **US06** | Save favourite rooms | 4 | Must |
| E2 Search | **US07** | Alerts for new rooms matching a saved search | 5 | Must |
| E4 Landlord | **US08** | Publish a room listing with photos | 6 | Must |
| E3 Decide | **US09** | Travel time from room to campus | 3 | Must |
| E3 Decide | **US10** | Compare up to 3 rooms side by side | 3 | Must |
| E1 Discovery | **US11** | House rules and building regulations | 3 | Must |
| E5 Trust | **US12** | Report a suspicious/inaccurate listing | 2 | Must |
| E5 Trust | **US13** | Register, sign in, role-based access | 5 | Must |
| E6 Contact | **US14** | In-app chat with the landlord *(team addition, 2026-11-11)* | TBD (not in plan sheet) | Must |

> **Supporting capability (not a separate story):** an **Admin console inside the mobile app** (role-gated tab) — required by US08 AC2 (verification), US12 AC3 (moderation queue) and US13 (landlord approval). See [use-cases.md](use-cases.md) UC-40…UC-44.

### 5.2 Out of scope for this release (Post-MVP backlog)

Listed in the Project Plan scope but **not** in the MVP backlog — do **not** implement, do not add data structures "for later":
nearby-amenities map layer, area security data/crime map, online deposit payment, roommate matching, odd-job marketplace, rideshare matching, short-term rental, internet sharing, administrative-procedure assistant, contract-clause scanning, utility consumption prediction, in-app booking of viewings, reviews/ratings of landlords, chat attachments (images/files), user blocking, web front-end, **password reset by e-mail** (pilot workaround: Admin resets via a support script), **in-app account deletion** (becomes mandatory only if published on Google Play — ADR-013), **Vietnamese UI** (strings are in `.resx`, so `vi` can be added later without code changes).

> Note: US10 AC2 requires a **"security score"**. Because area-security data is out of scope, the MVP defines it as a **listing-level Safety Score (0–5)** computed from landlord-declared safety features that the Admin verifies during moderation — see `BR-LST-08`.

## 6. Functional requirements (by story)

Acceptance Criteria (AC) are copied **verbatim** from the project plan. "Clarifications" are binding product decisions made by the team to remove ambiguity; each links to a business rule.

### US01 — Browse verified rooms near my campus
*As a student, I want to browse a list of verified rental rooms near my campus so that I can compare options quickly.*
- AC1: The list is sorted by distance from the selected campus by default.
- AC2: Each card shows title, monthly rent, area, distance to campus, and verification badge.
- AC3: The list loads within 3 seconds for up to 500 listings.
- AC4: Only listings with status Available and Verified are displayed.

Clarifications:
- "Selected campus" = the user's campus (profile for signed-in users; device preference for guests); default **ICTU** (`BR-CMP-01..03`).
- Distance on cards is **straight-line (Haversine)** distance to the selected campus, shown as e.g. "1,2 km" (`BR-SRCH-03`). Real travel time is US09 (detail screen only).
- Infinite scroll, page size 20 (`BR-SRCH-05`). Card also shows the cover thumbnail (Rightmove pattern) and a favourite heart (US06).

### US02 — View room details
*As a student, I want to view the full details of a room so that I can decide whether it suits me.*
- AC1: Details include description, rent, deposit, area, room type, amenities, address, landlord contact, and house rules.
- AC2: The screen shows the listing status and the last-updated date.
- AC3: If the listing is unavailable, the app shows a message and suggests similar rooms.
- AC4: Only signed-in users can see the landlord's phone number.

Clarifications:
- Also shows **monthly cost breakdown** (rent + electricity/water/internet/parking pricing), available-from date, furnishing, max occupants, Safety Score (`BR-LST-03`, `BR-LST-08`).
- "Landlord contact" = landlord display name + **Call**, **Zalo**, **Chat** actions. Phone/Zalo/Chat require sign-in; guests see a "Sign in to contact" prompt (`BR-CNT-01`).
- "Unavailable" = status `Rented` or `Unpublished` (shown with a banner); `Suspended` listings are hidden from the public (`BR-LST-04a`). Similar rooms algorithm: `BR-LST-10`.

### US03 — Photo gallery and 360° view
- AC1: The gallery supports at least 8 photos per listing.
- AC2: 360° photos open in an interactive viewer.
- AC3: Images are cached and load progressively.
- AC4: A placeholder is shown when a listing has no photo.

Clarifications: up to 20 standard photos + up to 3 panoramas per listing (`BR-MED-01`); server generates thumbnail + medium renditions; client shows thumbnail first, then medium/full (`BR-MED-04`). 360° = equirectangular JPG (2:1) rendered with Pannellum ([ADR-008](adr/adr-008-media-storage-and-360-viewer.md)).

### US04 — Search by filters
- AC1: Filters include price range, area range, room type, and maximum distance to campus.
- AC2: Filters can be combined and results update within 2 seconds.
- AC3: The result count and active filters are always visible.
- AC4: The Clear filters action restores the full list.

Clarifications: sort options = distance (default), price ↑, price ↓, newest (`BR-SRCH-04`). Active filters shown as removable chips (Rightmove/Airbnb pattern).

### US05 — Draw an area on the map
- AC1: The app lets the user draw a polygon and search within it.
- AC2: Results are ordered by price or distance as selected.
- AC3: Each room is shown as a pin with a preview card.
- AC4: The drawn area can be saved as a named search.

Clarifications: polygon is drawn by tapping vertices (3–20 points) on the native map (`BR-SRCH-07`); filters from US04 apply on top of the polygon; a saved area becomes a **Saved Search** (same entity as US07).

### US06 — Save favourite rooms
- AC1: A heart icon adds or removes a room from favourites.
- AC2: Favourites sync across devices through the user account.
- AC3: The favourites screen shows the current price and availability of each room.
- AC4: Removing a favourite can be undone within 5 seconds.

Clarifications: requires sign-in (`StudentPolicy`); guest tapping the heart icon is prompted to sign in and the action resumes after login (`BR-FAV-01..04`).

### US07 — New-listing alerts
- AC1: Push notifications can be enabled for any saved search.
- AC2: An alert is sent within 15 minutes of a matching listing being published.
- AC3: Users can pause or delete an alert at any time.
- AC4: No more than 3 alerts are sent per saved search per day.

Clarifications: "published" = the moment a listing first becomes `Available` (Admin approval). Each alert also lands in the in-app **Notifications** inbox; when the daily cap is hit, matches are kept in the inbox only (`BR-ALR-01..06`).

### US08 — Publish a room listing (landlord)
- AC1: Required fields are title, address, rent, deposit, area, room type, and at least 3 photos.
- AC2: A new listing is saved as Pending review until it is verified.
- AC3: The landlord can edit or unpublish the listing at any time.
- AC4: Images must be JPG or PNG and at most 10 MB each.

Clarifications: only **verified landlords** can publish (`BR-AUTH-03`). Location is set by dropping a pin on the map (no paid geocoding). Edits of an Available listing apply immediately; *material* edits (address, pin, photos, room type) flag it for Admin re-review without hiding it (`BR-LST-05`).

### US09 — Travel time to campus
- AC1: The detail screen shows distance and estimated travel time by motorbike, bus, and walking.
- AC2: The estimate uses the campus selected in the user profile.
- AC3: If routing is unavailable, the app shows the straight-line distance instead.

Clarifications: routing via OpenRouteService (free tier) cached per (room, campus, mode); bus time is a labelled estimate (`BR-TRV-01..04`, [ADR-007](adr/adr-007-routing-and-travel-time.md)).

### US10 — Compare up to three rooms
- AC1: Users can add up to 3 rooms to the comparison table.
- AC2: The table compares rent, deposit, area, distance, amenities, and security score.
- AC3: The comparison can be shared as an image.

Clarifications: the compare tray lives on the device (no sign-in needed) and the table highlights the best value per row (`BR-CPR-01..03`).

### US11 — House rules
- AC1: The detail screen lists quiet hours, guest policy, cooking rules, and parking rules.
- AC2: Rules are shown before the user contacts the landlord.
- AC3: The landlord can update the rules, and changes are versioned.

Clarifications: "shown before contact" = the House Rules section is placed above the contact bar content and the first contact action in a session shows a one-time rules summary sheet (`BR-HR-01..03`).

### US12 — Report a listing
- AC1: A Report action is available on every listing.
- AC2: The user chooses a reason and adds an optional note.
- AC3: The report is sent to the moderation queue and a confirmation is shown.

Clarifications: requires sign-in (anti-spam); one open report per user per listing (`BR-RPT-01..05`).

### US13 — Authentication & RBAC *(backend done on develop2)*
- AC1: Users can sign up and sign in using email/password or ICTU student SSO.
- AC2: The system issues JWT access and refresh tokens and enforces role-based permissions (Student, Landlord, Admin).
- AC3: Protected actions (publishing listings, viewing landlord phone numbers, saving favourites) require authentication.
- AC4: Passwords are encrypted and invalid login attempts return clear error messages.

Locked rules: `BR-AUTH-01..07` (originally in `.agents/plans/US13_BACKEND_PLAN.md`, consolidated in BA doc).

### US14 — In-app chat with the landlord *(team addition)*
*As a student, I want to chat with the landlord inside the app so that I can ask questions without sharing my phone number.*
- AC1: A signed-in student can start a conversation from any Available listing; the listing is pinned at the top of the conversation.
- AC2: Messages are delivered in real time while both users are online, and as a push notification when the recipient is offline.
- AC3: Both users see the conversation list with the last message, time, and unread count.
- AC4: Message history is kept and loads progressively (newest first).

Rules: `BR-CHT-01..07`, [ADR-009](adr/adr-009-in-app-chat.md). Text only in MVP.

## 7. Non-functional requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-01 | Performance | Listing first page visible ≤ 3 s on a mid-range Android over 4G, dataset ≤ 500 Available listings (US01 AC3). Cached page visible ≤ 1 s. |
| NFR-02 | Performance | Filter change → updated results ≤ 2 s (US04 AC2). API p95 for search ≤ 300 ms at 1,000 listings. |
| NFR-03 | Performance | Thumbnails ≤ 60 KB; medium images ≤ 300 KB; list scroll at 60 fps (no layout jank). |
| NFR-04 | Timeliness | Alerts delivered ≤ 15 min after publication (US07 AC2); chat delivery ≤ 2 s online. |
| NFR-05 | Security | OWASP ASVS L1 basics: PBKDF2 passwords, JWT 30 min + rotating refresh 7 days, HTTPS outside Development, rate limiting on auth/report/chat endpoints, no secrets in source. GUARDRAILS 1.5.x apply. |
| NFR-06 | Privacy | Store the minimum personal data: no national ID, no student GPS history; landlord phone exposed only to signed-in users; chat visible only to its two participants and Admin on report. |
| NFR-07 | Availability | Read features degrade gracefully offline: last list page, viewed details, favourites and conversations are served from the on-device cache with an "offline" banner. |
| NFR-08 | Usability | English UI (en-US; strings in `.resx`, Vietnamese can be added later), touch targets ≥ 48 dp, WCAG 2.2 AA contrast, every list has loading/empty/error states. |
| NFR-09 | Platform | Android 7.0+ (API 24) is the only tested and distributed target (signed APK, ADR-013). Code stays cross-platform, but iOS is not built or tested (no Mac / Apple Developer account); Windows/MacCatalyst not supported in MVP. |
| NFR-10 | Maintainability | Hexagonal architecture rules in `.agents/GUARDRAILS.md`; ≥ 80% line coverage on Domain/Application; CI green on every PR to `develop2`. |
| NFR-11 | Cost | Only free tiers/free OSS (see [ADR index](adr/index.md)); budget sheet caps paid cloud at 11 M VND. |

## 8. Assumptions & dependencies

- A1: ICTU student SSO is **Google sign-in (OIDC)** restricted to the `ictu.edu.vn` Google Workspace domain; student e-mails look like `DTC235210045@ictu.edu.vn`. The team registers its own Google Cloud OAuth client; the mock verifier is used in Development only.
- A2: Campus coordinates are seeded in [erd.md §4.1](erd.md#41-campuses-reference-seeded) (ICTU baseline `21.5851, 105.8066` + 7 other Thái Nguyên University campuses from OpenStreetMap); the team verifies them before release (see [business-analysis.md §9](business-analysis.md#9-open-questions)).
- A3: A Google Cloud project with Maps SDK for Android enabled (free for mobile dynamic maps, billing account required) is available; otherwise see fallback in [ADR-006](adr/adr-006-maps-and-spatial-search.md).
- A4: Pilot data (100 listings) is entered by real landlords or by the team on their behalf.

## 9. Release & acceptance

- Definition of Done per story: all AC demonstrated on Android, unit tests green, guardrail checklist passed, PR reviewed and merged into `develop2`.
- Release candidate = `develop2` merged into `group-2-main` after integration testing (Project Plan week 11).

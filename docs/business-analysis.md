# Business Analysis — RoomToRentRadar

| Field | Value |
|---|---|
| Status | Draft v1.0 — 2026-11-11 |
| Owner | Group 2 |
| Scope | MVP (US01–US14) as defined in [prd.md](prd.md) |

> **For AI agents:** §7 *Business Rules Catalog* is the authoritative list of domain rules. Every rule has a stable ID (`BR-<AREA>-NN`). Code comments, tests and specs must reference these IDs (e.g. `// BR-LST-04`). Never invent a rule that is not here — add it to this file first (via PR) and reference it.

---

## 1. Business context

ICTU (University of Information and Communication Technology, Thái Nguyên University) has ~5,000 students who mostly rent rooms in the surrounding wards (Quyết Thắng, Tân Thịnh, Thịnh Đán, Phan Đình Phùng). The rental market is informal: individual landlords post on Facebook groups ("Phòng trọ Thái Nguyên", "Tìm trọ ICTU") and Zalo; there is no verification and no structured data.

RoomToRentRadar introduces a **two-sided marketplace with a trust layer**:

```
 Landlords ──publish──▶ [ Moderation (Admin) ] ──verified listings──▶ Students
     ▲                                                                   │
     └──────────── contact (call / Zalo / in-app chat) ◀─────────────────┘
                 reports ─────────▶ [ Moderation (Admin) ]
```

## 2. Reference & competitor analysis

| Product | What we take | What we do differently |
|---|---|---|
| **Rightmove** (UK, primary reference) | Location-first search; radius + filters + sort; **Draw-a-search**; list ⇄ map toggle; rich listing page (gallery, virtual tour, key features, letting details: deposit, available date, furnishing); ♥ save; **saved search + instant/daily alerts**; "Contact agent"; report listing; agent portal (Rightmove Plus) with listing management. | Distance is to the **student's campus**, not a city; prices in VND incl. utilities; motorbike commute; Zalo + in-app chat instead of e-mail enquiry; **side-by-side compare** (Rightmove has none). |
| **UniHomes / StuRents** (UK student lettings) | "Search near my university" as the first-class entry point; bills-inclusive pricing; student-specific badges. | Campus is a profile setting with a quick switcher chip. |
| **Phongtro123, Chợ Tốt Nhà (nhatot.com)** (VN) | Vietnamese vocabulary and room types (phòng trọ, chung cư mini, nhà nguyên căn); price shown as "2,5 triệu/tháng"; Zalo button. | Mandatory verification; no unverified posts in the feed; structured utilities and house rules. |
| **Facebook / Zalo groups** (status quo) | Social proof, speed. | Structure, trust, alerts, comparison. |

## 3. Stakeholders

| Stakeholder | Interest | Involvement |
|---|---|---|
| Product Owner (Vũ Huy Lượng) | Product vision, backlog priority, acceptance | Sprint Review, backlog refinement |
| Group 2 Scrum Team | Deliver MVP | Daily work |
| ICTU Student Affairs / Academic Affairs Office | Student safety, accommodation policy, campus data | Requirements interviews, UAT |
| University IT staff | SSO integration, campus map data | Integration support |
| Students (end users) | Find rooms fast & safely | Interviews, usability tests, UAT |
| Landlords (end users) | Fill rooms quickly | Interviews, UAT |

## 4. Personas

**P1 — Minh, 2nd-year ICTU student (Student).** Budget 1.5–2.5 M VND/month, rides a motorbike, phone-only, mobile data. Wants: ≤ 2 km from campus, private bathroom, air-conditioning, no curfew. Pain: fake photos, hidden electricity prices (some landlords charge 4,000 ₫/kWh vs. state price ~3,000 ₫), rooms already taken.

**P2 — Lan, freshman (Student), new to Thái Nguyên.** Cannot visit many rooms; relies on photos/360° and safety information; parents worry about safety (CCTV, gated entrance, owner on site). Will share comparisons with parents (US10 AC3).

**P3 — Cô Hoa, landlord with 12 rooms (Landlord).** 50+, uses Zalo daily, not tech-savvy. Wants: post quickly from her phone, get calls/Zalo from serious students, mark rooms as rented. Pain: long forms, typing on phone.

**P4 — Anh Tuấn, admin/moderator (Admin).** Student-affairs staff or team member. Wants a short queue: approve landlords, approve listings, resolve reports — each in < 1 minute.

## 5. As-is vs to-be process

### 5.1 Student finds a room

| Step | As-is | To-be (RoomToRentRadar) |
|---|---|---|
| 1 | Join several Facebook/Zalo groups | Open app → feed of verified rooms sorted by distance to campus (US01) |
| 2 | Scroll, read unstructured posts | Filter by price/area/type/distance, or draw an area (US04, US05) |
| 3 | Comment "ib" / call blindly | Read full details, utilities, house rules, 360° photos, travel time (US02, US03, US09, US11) |
| 4 | Screenshot posts to compare | ♥ save and compare up to 3 rooms; share image to parents (US06, US10) |
| 5 | Miss new posts | Saved search alerts within 15 min (US07) |
| 6 | Give phone number publicly | Call / Zalo / in-app chat after signing in (US02 AC4, US14) |
| 7 | Get scammed, no recourse | Report → moderation queue (US12) |

### 5.2 Landlord publishes a room

As-is: write a Facebook post, repost daily, answer the same questions repeatedly.
To-be: register as Landlord (phone required) → Admin approves the landlord → create listing (form + ≥ 3 photos + pin on map) → `PendingReview` → Admin approves → `Available` → alerts fire → landlord receives calls/Zalo/chat → marks `Rented`.

## 6. Domain glossary (EN ↔ VI)

| Term | Vietnamese (UI) | Definition |
|---|---|---|
| Listing / Room | Tin đăng / Phòng | A rentable unit advertised by a landlord. In code: `Room` aggregate. |
| Room type | Loại phòng | `BoardingRoom` (Phòng trọ), `MiniApartment` (Chung cư mini), `WholeHouse` (Nhà nguyên căn), `Dormitory` (Ký túc xá / KTX tư nhân). |
| Monthly rent | Giá thuê / tháng | Integer VND. |
| Deposit | Tiền cọc | Integer VND. |
| Area | Diện tích | m², decimal(1). |
| Utilities | Chi phí dịch vụ | Electricity (₫/kWh), water (₫/m³ or ₫/person), internet, parking, other fees. |
| Verified listing | Tin đã xác minh | Listing approved by Admin at least once and owned by a verified landlord. |
| Verified landlord | Chủ trọ đã xác minh | Landlord account approved by Admin (`IsVerified = true`). |
| Campus | Cơ sở / Trường | Reference point for distance and travel time. |
| Saved search | Tìm kiếm đã lưu | Named set of filters and/or polygon area; may have alerts. |
| Alert | Thông báo tin mới | Push + inbox notification for a new matching listing. |
| Safety Score | Điểm an ninh | 0–5 score from verified safety features of the listing. |
| House rules | Nội quy | Quiet hours, guests, cooking, parking, other rules; versioned. |
| Moderation queue | Hàng chờ kiểm duyệt | Admin work list: landlords, listings, reports. |
| Conversation | Cuộc trò chuyện | Chat thread between one student and one landlord about one listing. |

## 7. Business Rules Catalog

### 7.1 Authentication & accounts (`BR-AUTH`) — *implemented on develop2*
| ID | Rule |
|---|---|
| BR-AUTH-01 | Email is trimmed and lower-cased; it must be unique. Duplicate → `409 Conflict`. |
| BR-AUTH-02 | Public registration allows only `Student` and `Landlord`. Self-registering as `Admin` → `403`. Admin accounts are seeded by configuration. |
| BR-AUTH-03 | `Landlord` registration requires a Vietnamese phone `^(0|\+84)[0-9]{9}$`. New landlords are `IsVerified = false`; they can sign in and edit their profile but cannot upload photos or create listings until approved; publishing requires `VerifiedLandlordPolicy` (Admin, or Landlord with `is_verified = true`). |
| BR-AUTH-04 | ICTU SSO = **Google OIDC**. The server accepts an id_token only if the signature/audience/issuer are valid, `email_verified = true`, the e-mail ends with `@ictu.edu.vn` **and** the `hd` (hosted domain) claim equals `ictu.edu.vn`. It provisions/links a `Student` with `IsVerified = true`; the student code is the e-mail local part upper-cased (e.g. `DTC235210045@ictu.edu.vn` → `DTC235210045`) when not supplied. |
| BR-AUTH-05 | Password 8–128 chars with ≥ 1 upper, ≥ 1 lower, ≥ 1 digit; PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte salt. 5 consecutive failures lock the account for 15 min. Error messages never reveal whether the e-mail exists. |
| BR-AUTH-06 | Access token JWT (30 min) with claims `sub`, `email`, `role`, `is_verified`, `jti`. Refresh token (7 days) stored as SHA-256 hash, rotated on every refresh; reuse of a revoked token revokes all sessions of that user. |
| BR-AUTH-07 | Protected actions require sign-in: viewing landlord phone/Zalo, favourites, saved searches, reports, chat, publishing. Anonymous responses carry `landlordPhoneNumber = null`. |

### 7.2 Campus (`BR-CMP`)
| ID | Rule |
|---|---|
| BR-CMP-01 | Campuses are reference data seeded from [erd.md §4.1](erd.md#41-campuses-reference-seeded) (8 Thái Nguyên University campuses). Default campus is **ICTU** (`21.5851, 105.8066`). Adding a campus = adding one seed row; no code change. |
| BR-CMP-02 | A signed-in user's selected campus is stored in the profile (`Users.SelectedCampusId`) and syncs across devices; a guest's selection is stored on the device only. |
| BR-CMP-03 | Every distance, distance filter, distance sort and travel time is relative to the *selected* campus at request time; it is never stored on the listing. |

### 7.3 Listings (`BR-LST`)
| ID | Rule |
|---|---|
| BR-LST-01 | Required on create: title (10–100 chars), address line (≤ 200), ward, district (default "TP. Thái Nguyên"), pin location (lat/lng inside Thái Nguyên province bounding box), monthly rent (100,000–50,000,000 ₫), deposit (0–3× rent), area (5–200 m²), room type, ≥ 3 standard photos. (US08 AC1) |
| BR-LST-02 | Optional: description (≤ 2,000), max occupants (1–10), furnishing (`Unfurnished`, `Basic`, `Full`), available-from date, amenities (catalog codes), safety features (catalog codes), house rules. |
| BR-LST-03 | Utilities: electricity price ₫/kWh (0–10,000), water price with unit (`PerCubicMeter`, `PerPerson`, `Included`), internet fee ₫/month (0 = included), parking fee ₫/month, other fees note (≤ 200). Displayed as a monthly cost breakdown with an **estimated monthly total** = rent + 100 kWh × electricity price + water (4 m³ × price, or 1 × per-person price, or 0 if included) + internet + parking; labelled "ước tính". |
| BR-LST-04 | **State machine** (direct status assignment is forbidden):<br>`PendingReview → Available` (Admin approve) · `PendingReview → Rejected` (Admin reject, reason required) · `Rejected → PendingReview` (landlord edits & resubmits) · `Available ⇄ Rented` (landlord) · `Available/Rented → Unpublished` (landlord) · `Unpublished → Available` (landlord republish — every Unpublished listing was verified before) · `Available/Rented/Unpublished → Suspended` (Admin, from report) · `Suspended → PendingReview` (landlord edits & resubmits). Diagram: [ADR-011](adr/adr-011-listing-moderation-state-machine.md). |
| BR-LST-04a | Public visibility of the detail page: `Available`, `Rented`, `Unpublished` are viewable by anyone (the last two with an "unavailable" banner, US02 AC3). `PendingReview`, `Rejected`, `Suspended` return 404 to everyone except the owner and Admin (a suspended listing may be a scam — never expose its contact). Favourites keep showing such rooms as "Ngừng hiển thị" without opening them. |
| BR-LST-05 | Edits to an `Available`/`Rented` listing apply immediately. *Material* edits (address, pin, room type, adding photos) set `NeedsReReview = true` → appears in Admin queue; listing stays visible. Edits to `PendingReview`/`Rejected` listings keep/return status `PendingReview`. |
| BR-LST-06 | A listing is **Verified** when `VerifiedAtUtc` is set (first Admin approval). Public feed/search shows only `Status = Available` (which implies verified) **and** landlord `IsVerified = true`. (US01 AC4) |
| BR-LST-07 | Only the owning landlord can edit/unpublish/mark rented (anti-IDOR, GUARDRAILS 1.5.6); Admin can approve/reject/suspend. Every mutation uses optimistic concurrency (`Version`, HTTP `If-Match`). |
| BR-LST-08 | **Safety Score** = number of verified safety criteria met, 0–5: (1) CCTV, (2) secure entrance (gate/smart or fingerprint lock), (3) fire safety (extinguisher **and** emergency exit), (4) owner or guard on site, (5) well-lit access path. Landlord declares; at approval the Admin confirms a subset (`confirmedSafetyFeatures` → `RoomSafetyFeatures.IsConfirmed`) and the room gets `IsSafetyVerified = true`. Verified score = confirmed features; before verification the declared count is shown as "Chưa xác minh". |
| BR-LST-09 | `LastUpdatedAtUtc` changes on any landlord-visible content edit; shown on detail (US02 AC2). |
| BR-LST-10 | **Similar rooms** for an unavailable listing: `Available`, rent within ±20%, within 2 km of the original pin, same room type preferred, ordered by distance to the original, max 5. If fewer than 2 found, relax room type. (US02 AC3) |
| BR-LST-11 | A landlord may have at most 50 non-archived listings (abuse guard). |

### 7.4 Media (`BR-MED`)
| ID | Rule |
|---|---|
| BR-MED-01 | Per listing: 3–20 standard photos, 0–3 panoramas (360°). Gallery supports ≥ 8 (US03 AC1). |
| BR-MED-02 | Accepted: JPEG or PNG, ≤ 10 MB each, validated by **magic bytes** (not file extension). Panorama must have aspect ratio 2:1 (±2%). (US08 AC4) |
| BR-MED-03 | Uploaded photos belong to the uploading landlord; unattached uploads expire after 24 h. A photo can be attached only to the uploader's own listing. |
| BR-MED-04 | Server stores original and generates `thumb` (400 px, ≤ 60 KB) and `medium` (1280 px, ≤ 300 KB) JPEG renditions; EXIF (incl. GPS) is stripped. Client loads `thumb` first then `medium` (progressive, US03 AC3). |
| BR-MED-05 | First photo by `SortOrder` is the cover. Listing with no photo (legacy/edge) shows a placeholder (US03 AC4). |

### 7.5 Search (`BR-SRCH`)
| ID | Rule |
|---|---|
| BR-SRCH-01 | Filters: rent range, area range, room types (multi), max distance to campus (km), and polygon (US05). All combinable with AND. (US04 AC1-2) |
| BR-SRCH-02 | Invalid ranges (min > max, negative) → `400`. Unknown room type/sort → `400` (whitelist, GUARDRAILS 1.5.1). |
| BR-SRCH-03 | Distance = Haversine straight-line km from the selected campus, rounded to 0.1 km. |
| BR-SRCH-04 | Sort: `distance` (default), `price_asc`, `price_desc`, `newest` (by `PublishedAtUtc`). Ties broken by `Id`. In polygon search only `distance` and `price_*` (US05 AC2). |
| BR-SRCH-05 | Pagination: page size default 20, max 50; response always includes `totalCount` (US04 AC3). |
| BR-SRCH-06 | "Clear filters" resets to: selected campus, no filters, sort `distance` (US04 AC4). |
| BR-SRCH-07 | Polygon: 3–20 vertices, simple (non-self-intersecting), area ≤ 100 km². Map search returns at most 200 rooms as `RoomCardDto` (the same card powers the pin preview, so no extra request on pin tap), with `truncated = true` when more matched. |
| BR-SRCH-08 | Saved searches: max 10 per user; name 1–50 chars, unique per user. |

### 7.6 Favourites (`BR-FAV`)
| ID | Rule |
|---|---|
| BR-FAV-01 | Favourites are server-side per user (sync across devices, US06 AC2). Max 200. |
| BR-FAV-02 | Add/remove are idempotent (`PUT`/`DELETE`). |
| BR-FAV-03 | Removing shows an Undo snackbar for 5 s; the API call is sent after the snackbar closes (or immediately if the user leaves the screen) — undo within 5 s means no API call (US06 AC4). |
| BR-FAV-04 | Favourites list shows current rent and availability, including unavailable rooms (greyed, "Đã cho thuê"/"Ngừng đăng") (US06 AC3). |

### 7.7 Alerts & notifications (`BR-ALR`)
| ID | Rule |
|---|---|
| BR-ALR-01 | Alerts can be toggled per saved search (US07 AC1); states: `Off`, `Active`, `Paused` (US07 AC3). Deleting the saved search deletes its alert. |
| BR-ALR-02 | Trigger: a listing transitions to `Available` for the **first time** (approval). Republish/mark-available does not trigger. |
| BR-ALR-03 | Matching runs immediately on the approval event and a safety sweep runs every 5 minutes → delivery ≤ 15 min (US07 AC2). |
| BR-ALR-04 | Max 3 push alerts per saved search per calendar day (Asia/Ho_Chi_Minh) (US07 AC4). Matches beyond the cap go to the inbox only. |
| BR-ALR-05 | A listing is alerted at most once per saved search (idempotency key `(SavedSearchId, RoomId)`). |
| BR-ALR-06 | Push requires the user's device token; if none, inbox only. Invalid tokens reported by FCM are deleted. |

### 7.8 Travel time (`BR-TRV`)
| ID | Rule |
|---|---|
| BR-TRV-01 | Modes: `Motorbike`, `Bus`, `Walking` (US09 AC1); campus = profile campus (US09 AC2). |
| BR-TRV-02 | Motorbike = routing "driving" profile; Walking = "foot" profile; Bus = estimate `driving duration × 1.6 + 10 min` labelled "ước tính". |
| BR-TRV-03 | Results cached per (room, campus, mode) for 30 days; invalidated when the pin moves. |
| BR-TRV-04 | If routing fails/times out (3 s), return straight-line distance with `isFallback = true`; UI shows "Khoảng cách đường chim bay" (US09 AC3). |

### 7.9 Compare (`BR-CPR`)
| ID | Rule |
|---|---|
| BR-CPR-01 | Compare tray holds max 3 rooms, stored on device; adding a 4th asks to replace one (US10 AC1). |
| BR-CPR-02 | Rows: rent, deposit, area, distance, room type, amenities (✓/✗ per amenity), Safety Score, estimated monthly cost (US10 AC2). Best value per numeric row is highlighted. |
| BR-CPR-03 | Share produces a PNG of the table with app name and date via the OS share sheet (US10 AC3). |

### 7.10 House rules (`BR-HR`)
| ID | Rule |
|---|---|
| BR-HR-01 | Fields: quiet hours (start–end), curfew (optional time), guest policy (`Allowed`, `DaytimeOnly`, `NotAllowed` + note), cooking (`Allowed`, `SharedKitchenOnly`, `NotAllowed`), parking (free text ≤ 300 + `ParkingAvailable`), pets/smoking flags, other rules (≤ 1,000). (US11 AC1) |
| BR-HR-02 | Every update creates a new immutable version (`VersionNumber` +1); detail shows the latest, landlord can see history (US11 AC3). |
| BR-HR-03 | Rules are displayed on detail above the contact area, and a rules summary sheet appears on the first contact attempt per listing per device (US11 AC2). |

### 7.11 Reports & moderation (`BR-RPT`)
| ID | Rule |
|---|---|
| BR-RPT-01 | Reasons (whitelist): `Scam`, `IncorrectInformation`, `AlreadyRented`, `Duplicate`, `InappropriateContent`, `Other`. Note optional (≤ 500), **required** for `Other` (US12 AC2). |
| BR-RPT-02 | Signed-in users only; one `Open` report per (user, listing); max 10 reports per user per day. |
| BR-RPT-03 | Report enters the moderation queue with status `Open`; reporter sees confirmation (US12 AC3). |
| BR-RPT-04 | Admin resolution: `Dismissed` or `ActionTaken` (suspend listing, or notify landlord to fix). Resolution note required. |
| BR-RPT-05 | 3 distinct open reports with reason `Scam` auto-flag the listing as high priority in the queue (not auto-suspend). |

### 7.12 Contact & chat (`BR-CNT`, `BR-CHT`)
| ID | Rule |
|---|---|
| BR-CNT-01 | Call (`tel:`), Zalo (`https://zalo.me/<phone>`) and Chat are visible only to signed-in users (US02 AC4). |
| BR-CNT-02 | Landlord can set whether Zalo uses the same phone (default) — no separate Zalo ID in MVP. |
| BR-CHT-01 | Only a `Student` can start a conversation, only on an `Available` listing; one conversation per (listing, student); landlord = listing owner. |
| BR-CHT-02 | Only the two participants can read/write a conversation (anti-IDOR); Admin can read a conversation only when it is attached to a report (post-MVP: chat reporting). |
| BR-CHT-03 | Message: text 1–2,000 chars, trimmed; no attachments in MVP. |
| BR-CHT-04 | Sending is idempotent by `(SenderId, ClientMessageId)` (retry-safe). |
| BR-CHT-05 | Rate limit 30 messages/min per user. |
| BR-CHT-06 | Offline recipient gets a push "Tin nhắn mới từ …"; read state per participant (`LastReadAtUtc`) drives unread counts. |
| BR-CHT-07 | Conversations remain readable after the listing becomes unavailable; new messages remain allowed for 30 days after. |

### 7.13 Audit (`BR-AUD`) — *implemented on develop2*
| ID | Rule |
|---|---|
| BR-AUD-01 | All mutating HTTP requests and security events are audited; audit failure never breaks the business operation; no secrets/passwords in `DetailsJson`. |
| BR-AUD-02 | Only Admin can query audit logs. |

## 8. Risks (from Project Plan, with MVP-specific mitigations)

| Risk | Mitigation in product/design |
|---|---|
| Scope creep (20+ ideas) | §Out-of-scope list in PRD is binding; new ideas → Post-MVP backlog. US14 (chat) is the only addition to the 13 plan stories and is part of the MVP. |
| Academic workload | Contract-first specs (this doc set) enable parallel work; feature PRs ≤ 1 task. |
| Cloud cost | SQLite single datastore ([ADR-002](adr/adr-002-sqlite-single-datastore.md)); free tiers only. |
| Map/geolocation API limits | Native map SDK (free for mobile), no paid geocoding (pin-drop), routing cache ([ADR-006](adr/adr-006-maps-and-spatial-search.md), [ADR-007](adr/adr-007-routing-and-travel-time.md)). |
| Fake listings & privacy | Two-level verification, Safety Score verification, reports, EXIF stripping, phone gating, minimal data. |

## 9. Open questions

| # | Question | Owner | Default if unanswered |
|---|---|---|---|
| OQ-1 | Story points for US14 (chat) — not in the plan sheet. | Scrum Team | Estimate at next backlog refinement. |
| OQ-2 | Verify the 7 non-ICTU campus coordinates (seed in [erd.md §4.1](erd.md#41-campuses-reference-seeded)) before the pilot. | Team | Seed as listed; fix the seed file if a pin is off. |

Resolved decisions (2026-11-11):
- **Campuses:** ICTU plus the other Thái Nguyên University member schools in the city (8 campuses). Coordinates were looked up once from OpenStreetMap (free, no API key) — a one-time seed list, not a runtime dependency.
- **SSO provider:** Google (OIDC), domain `ictu.edu.vn`; student e-mail format `DTC<digits>@ictu.edu.vn` (see BR-AUTH-04).
- **Admin during the pilot:** one team member signs in with a seeded Admin account (`Admin:SeedAccounts` configuration) and performs all approvals/moderation.

# UI/UX Specification — RoomToRentRadar

<!--
CONTEXT NOTE (2026-11-12): the visual target for every SCR-* is the Claude Design canvas linked in AGENTS.md (frames named
"TASK (owner) · screen · US · SCR-id"). The canvas draws screens in English; US15 requires every screen in EN and VI (row 7 shows the rules).
Icons in the canvas are stroke placeholders — the app uses Fluent System Icons, never emoji. Colours come only from §3 tokens.
-->

| Field | Value |
|---|---|
| Status | Draft v1.0 — 2026-11-11 |
| Platform | .NET MAUI 10 · Android first (ADR-013) · bilingual UI: English (en-US) + Vietnamese (vi-VN), switchable in-app (US15) |
| Detailed method skills | `.agents/skills/ui/01…08` (this spec applies them to our screens) |
| Inputs | [use-cases.md](use-cases.md) (UC-*), [business-analysis.md](business-analysis.md) (BR-*) |

> **For AI agents:** every screen has an ID `SCR-<AREA>-NN`, a *primary action*, required *states* and the API operations it calls. A screen is not done until all listed states are implemented. Use only the tokens in §3 (no hard-coded colours/sizes in pages). Strings come from the copy rules in §6.

---

## 1. Design principles (applied)

1. **Visual-first discovery** (marketplace pattern, Rightmove): big cover photo, price in bold, distance to *my campus* on every card.
2. **One primary action per screen** (Polaris): e.g. detail → *Contact*; filter sheet → *Show N rooms*; editor step → *Continue*.
3. **Trust is visible:** verified badge, Safety Score, last-updated date, status banner — never hidden behind taps.
4. **Thumb zone:** primary actions in the bottom third (sticky bars, FAB-style buttons); touch targets ≥ 48 dp.
5. **Recognition over recall:** active filters as chips, saved searches named automatically, campus always shown.
6. **Never a blank screen:** skeletons for loading, helpful empty states, error with *Retry*, offline banner with cached data (ADR-004).
7. **Respect the guest:** browsing, details, map, compare work without an account; sign-in is requested only at the moment of a protected action, then the action resumes.

## 2. Information architecture & navigation

### 2.1 Bottom tabs (Shell `TabBar`, role-aware)

| Tab (route) | Guest | Student | Landlord | Admin |
|---|---|---|---|---|
| Explore (`explore`) — list | ✓ | ✓ | ✓ | ✓ |
| Map (`map`) | ✓ | ✓ | – | ✓ |
| Saved (`saved`) — rooms / searches | ✓ (sign-in prompt) | ✓ | – | ✓ |
| My listings (`my-listings`) | – | – | ✓ | – |
| Messages (`messages`) | ✓ (sign-in prompt) | ✓ | ✓ | ✓ |
| Moderation (`moderation`) | – | – | – | ✓ |
| Account (`account`) | ✓ (sign in / register) | ✓ | ✓ | ✓ |

Max 5 tabs visible: Landlord = Explore, My listings, Messages, Account; Admin = Explore, Map, Moderation, Messages, Account (Admin reaches *Saved* from Account). Tabs are rebuilt after sign-in/out.

### 2.2 Flow map

```mermaid
flowchart LR
  WELCOME[SCR-AUTH-01 Welcome / Sign in] -- browse as guest --> HOME[SCR-HOME-01 Explore]
  HOME -. campus chip .-> CAMPUS[SCR-ACC-02 Campus picker] --> HOME
  HOME --> FILTER[SCR-SRCH-01 Filters] --> HOME
  HOME --> ROOM[SCR-ROOM-01 Room detail]
  MAP[SCR-MAP-01 Map] --> ROOM
  ROOM --> GAL[SCR-ROOM-02 Gallery] --> PANO[SCR-ROOM-03 360°]
  ROOM --> REPORT[SCR-ROOM-04 Report sheet]
  ROOM --> CHAT[SCR-CHAT-02 Conversation]
  ROOM --> CMP[SCR-CMP-01 Compare]
  ROOM -. protected action .-> AUTH[SCR-AUTH-01 Sign in] -. resume .-> ROOM
  SAVED[SCR-FAV-01 / SCR-SAVED-01] --> ROOM
  NOTI[SCR-NOTI-01 Notifications] --> ROOM
  LL[SCR-LL-01 My listings] --> EDIT[SCR-LL-02 Listing editor] --> RULES[SCR-LL-03 House rules]
  ADM[SCR-ADM-01 Moderation] --> ADMROOM[SCR-ADM-02 Review listing]
  ADM --> ADMREP[SCR-ADM-03 Report detail]
```

### 2.3 Campus selection pattern (US01, US09 — decided with references)

References: Airbnb/Rightmove (location chip at the top of results). UniHomes/StuRents ask "which university?" first, but almost all our users study at ICTU, so a mandatory question would be a wasted step (revised 2026-11-12).
- **No onboarding step.** First launch uses **ICTU** as the campus (BR-CMP-01); results appear immediately. The campus only matters to students of the other Thái Nguyên University schools, who change it once.
- **Campus picker** (SCR-ACC-02, full-screen sheet): title "Measure distances from", ICTU listed first, other TNU campuses below with search, primary *Use this campus*. Opened from the campus chip or from *Account*.
- **Campus chip** at the top of *Explore* and *Map*: `[pin] Near ICTU v` → bottom sheet picker (SCR-ACC-02). Changing it reloads results and travel times.
- Distance labels always name the campus: "1.2 km to ICTU".
- Signed-in users: selection saved in profile (syncs); guests: on device (BR-CMP-02).

## 3. Design tokens (`Resources/Styles/Colors.xaml`, `Styles.xaml`)

Use semantic tokens only; method in `.agents/skills/ui/02-design-system`. The palette is the team's **"Navy Blue + Ocean Blue + Teal"** design system implemented in PR #22 (`Resources/Styles/Colors.xaml`, plan `.agents/plans/UI_DESIGN_SYSTEM_PLAN.md`). This document is the single source for *which pairs are allowed*; the XAML files are the single source for the values. If the two disagree, fix the XAML or open a docs PR — do not invent colours.

### 3.1 Colour (light / dark) — values from `Colors.xaml`
| Token (XAML key) | Light | Dark | Use |
|---|---|---|---|
| `ActionPrimary` | `#163A5F` (Navy) | `#3572A5` | primary buttons, active tab |
| `ActionSecondary` | `#256D85` (Ocean) | `#256D85` | secondary buttons, focus ring (`BorderFocus`) |
| `ActionAccent` | `#4CB5AE` (Teal) | `#5CE1D6` | highlights, badges, selected chips, illustrations |
| `TextBrand` | `#163A5F` | `#5CE1D6` | links, brand text |
| `SurfaceCanvas` | `#F4F7FA` | `#0B131E` | page background |
| `SurfaceCard` | `#FFFFFF` | `#162234` | cards |
| `SurfaceElevated` | `#FFFFFF` | `#22334B` | sheets, dialogs |
| `SurfaceSubtle` | `#E8EEF5` | `#1A283C` | chips, input fill |
| `TextPrimary` | `#1E293B` | `#F4F7FA` | body text, **prices** (bold, not coloured) |
| `TextSecondary` | `#475569` | `#CBD5E1` | secondary text |
| `TextMuted` | `#94A3B8` | `#64748B` | **non-essential** hints and disabled only |
| `TextInverse` | `#FFFFFF` | `#0B131E` | text on primary/secondary |
| `BorderSubtle` | `#E2E8F0` | `#273B55` | borders, dividers |
| `StatusSuccess` | `#16A34A` | `#4CB5AE` | verified badge (icon + text) |
| `StatusWarning` | `#D97706` | `#F59E0B` | pending/unavailable banners |
| `StatusDanger` | `#DC2626` | `#EF4444` | errors, destructive |
| `StatusInfo` | `#0284C7` | `#38BDF8` | info |
| `Favourite` *(to add)* | `#DC2626` | `#EF4444` | filled heart (reuse danger red) |

### 3.1.1 Allowed text/background pairs (WCAG 2.2 AA — contrast measured 2026-11-11)
| Pair | Ratio | Rule |
|---|---|---|
| `TextPrimary` on `SurfaceCanvas` / `SurfaceCard` | 13.6 : 1 | ✅ default body text |
| `TextSecondary` on white | 7.6 : 1 | ✅ |
| `TextInverse` on `ActionPrimary` (light) | 11.6 : 1 | ✅ primary button |
| `TextInverse` on `ActionSecondary` | 5.8 : 1 | ✅ secondary button |
| **`ActionPrimary` (Navy) text on `ActionAccent` (Teal)** | 4.7 : 1 | ✅ **accent button/badge text must be Navy** |
| ~~White on `ActionAccent` (light)~~ | 2.5 : 1 | ❌ **forbidden** (PR #22 `AccentButton` currently violates this) |
| ~~`ActionAccent` Teal used as text on white/canvas~~ | 2.3–2.5 : 1 | ❌ forbidden — Teal is a fill colour, not a text colour in light mode |
| `TextMuted` on white (light) | 2.6 : 1 | ⚠️ only for non-essential decoration; never for price, status, form errors or required info |
| `StatusSuccess`/`StatusWarning` as **text** on white | 3.3 / 3.2 : 1 | ⚠️ large text/icons only. For small text use darker variants `#15803D` (5.0 : 1) and `#B45309` (5.0 : 1) — add as `StatusSuccessText`/`StatusWarningText` |
| `StatusDanger` text on white | 4.8 : 1 | ✅ |
| Dark: `TextPrimary` on `SurfaceCanvas` | 17.3 : 1 | ✅ |
| Dark: `TextSecondary` on `SurfaceCard` | 10.8 : 1 | ✅ |
| Dark: `TextInverse` on `ActionAccent` | 11.7 : 1 | ✅ |
| Dark: `TextMuted` on `SurfaceCard` | 3.4 : 1 | ⚠️ non-essential only |
| Dark: `StatusDanger` on `SurfaceCard` | 4.3 : 1 | ⚠️ use for icons/large text, or lighten to ≥ `#F87171` for small text |

Icons and large text (≥ 18 pt or 14 pt bold) need ≥ 3 : 1. No gradients, no neon.

### 3.2 Typography (bundled Open Sans — full Vietnamese diacritics)
| Token | Size / weight | Use |
|---|---|---|
| `Display` | 24 / Semibold | screen titles |
| `Title` | 20 / Semibold | section titles, price on detail |
| `Headline` | 17 / Semibold | card title, price on card |
| `Body` | 15 / Regular | body |
| `Label` | 13 / Semibold | chips, buttons, badges |
| `Caption` | 12 / Regular | meta (distance, updated date) |
Line height 1.4; support OS font scaling up to 130% without truncating prices.

### 3.3 Spacing, shape, elevation
Spacing scale 4-pt: `4, 8, 12, 16, 24, 32`. Page padding 16. Card radius 12, chip radius 16 (pill), sheet radius 16 top. Elevation: cards flat with 1 px `Outline`; sheets/dialogs shadow level 2 only.

### 3.4 Iconography
Fluent System Icons (bundled `FluentSystemIcons-Regular.ttf`). Icons accompany meaning (heart, map, filter, chat, phone); no decorative icons before every line.

## 4. Global components & states

| Component | Spec |
|---|---|
| `RoomCard` | 16:10 cover (thumb), verified badge overlay top-left, [heart] top-right (48 dp hit), title (2 lines max), **price "2.5M/mo"**, `25 m² · Boarding room · 1.2 km to ICTU`, *Compare* text button. Fixed height for scroll perf. |
| `FilterChip` | removable chip `Under 3M [x]`; *Clear filters* text chip at end. |
| `StatusBanner` | full-width under header: Warning colour for `Rented`/`Unpublished` (public) and `PendingReview`/`Rejected`/`Suspended` (owner/admin views), Verified colour for approvals. |
| `StickyContactBar` | bottom of detail: price + *Call* · *Zalo* · primary *Message*. Guest: single primary *Sign in to contact*. |
| `Skeleton` | grey blocks matching final layout; shimmer ≤ 1.2 s cycle. |
| `EmptyState` | illustration-free: icon + title + 1-line hint + one action. |
| `ErrorState` | message from errorCode map (§6) + *Retry*. |
| `OfflineBanner` | "You are offline – showing saved data" (Warning). |
| `Snackbar` | bottom, 5 s, one action (e.g. *Undo*). |
| `SignInPrompt` sheet | "Sign in to …" + *Sign in* / *Create account* / *Not now*. |

Required states for every data screen: **Loading (skeleton) · Content · Empty · Error · Offline**.

## 5. Screen catalogue

> Wireframes write icons as `[icon-name]` placeholders (e.g. `[pin]`, `[heart]`). Implement them with Fluent System Icons glyphs — never emoji (AGENTS.md §6).

Format: **Purpose · Primary action · Content · States/edge cases · API · Story**.

### SCR-ONB-01 (retired)
Retired 2026-11-12: there is no onboarding step; campus selection lives in SCR-ACC-02 (§2.3). The ID is not reused.

### SCR-AUTH-01 Sign in
Primary: *Sign in*. Content: email, password (show/hide), *Sign in with ICTU email* (outlined), *Forgot password?* (post-MVP: hidden), link *Create account*. States: inline field errors; `ACCOUNT_LOCKED` shows unlock time. API: `loginUser`, `loginWithIctuSso`. US13.

### SCR-AUTH-02 Register
Step 1 role cards: *I am a student* / *I am a landlord*. Step 2 form (phone required for landlord, password rules shown live). Landlord success → banner "Your account is awaiting verification (usually within 24 h)". API: `registerUser`. US13.

### SCR-HOME-01 Explore (list)
```
┌──────────────────────────────────────────┐
│ [pin] Near ICTU v              [bell] 2   │  campus chip · notifications
│ [filter] Filters (2)   Sort: Nearest v    │
│ (Under 3M x)(Boarding room x) Clear all   │  active chips
│ 128 rooms · [star] Save search            │  count always visible (US04 AC3)
│ ┌──────────────────────────────────────┐ │
│ │ [cover photo]  [check] Verified [heart]│ │
│ │ Private room near ICTU               │ │
│ │ 2.5M/mo                              │ │
│ │ 25 m² · Boarding room · 1.2 km to ICTU│ │
│ └──────────────────────────────────────┘ │
│ ...infinite scroll (20/page)...           │
│ ┌ Compare (2) ─────────── Compare now ┐  │  floating compare tray
├──────────────────────────────────────────┤
│ Explore   Map   Saved   Messages  Account │
└──────────────────────────────────────────┘
```
Primary: open a room. States: skeleton cards; empty "No rooms match – try a larger distance or *Clear filters*"; offline cached page. Pull-to-refresh. API: `searchRooms`. US01, US04, US06, US10.

### SCR-SRCH-01 Filters (bottom sheet, full height)
Content: rent range slider (0–10M, step 100k) + quick chips (*Under 1.5M*, *Under 2M*, *Under 3M*); area range (5–100 m²); room type multi-select chips; max distance segmented (*1 · 2 · 3 · 5 · 10 km*); sort radio. Sticky footer: *Clear* (text) · primary **Show 42 rooms** (live count, debounced 300 ms). API: `searchRooms` (pageSize=1 for count). US04 AC1–AC4.

### SCR-MAP-01 Map search
```
┌──────────────────────────────────────────┐
│ [pin] Near ICTU v  [filter] (2)  [list]   │
│                                          │
│      (2.5M)    (1.8M)                     │  price-label pins
│   <ICTU>  ·····polygon·····               │  campus marker
│      (3.2M)                               │
│                                          │
│ ┌ preview: photo · 2.5M · 25 m² · 1.2 km ┐│  on pin tap
│ [ [pen] Draw area ]   [ [star] Save ]     │
└──────────────────────────────────────────┘
Draw mode: "Tap to add points (3–20)"  [undo] Undo  [x] Clear  [check] Done
```
Primary: *Draw area* → *Done*. Sort toggle *Price / Distance* in list view. States: truncated hint "Zoom in to see more"; invalid polygon message; no location permission required. API: `searchRoomsInArea`, `createSavedSearch`. US05.

### SCR-ROOM-01 Room detail
Order (inverted pyramid, US11 AC2 rules before contact):
1. Gallery header (swipe, `1/12`, 360° badge) — placeholder if none.
2. `StatusBanner` if not Available; *Updated 3 days ago* (US02 AC2).
3. Title, **price/month**, deposit, verified badge, [heart], share, [more] (Report).
4. Key facts row: area · room type · max occupants · furnishing · available from.
5. **Monthly costs**: rent, electricity ₫/kWh, water, internet, parking, other → "Estimated ~2.9M/mo".
6. Amenities (grid, first 8 + *See all*).
7. **Safety**: Safety Score `4/5 · Verified` + checklist.
8. **House rules** (quiet hours, guests, cooking, parking, other) — always expanded summary (US11).
9. Location: static map + address; **Getting to ICTU**: [motorbike] 8 mins · [bus] ~19 mins (est.) · [walk] 25 mins; fallback text (US09).
10. Landlord block: name, *Verified*, member since.
11. If unavailable: **Similar rooms** carousel (US02 AC3).
12. `StickyContactBar` (disabled when unavailable).
States: header renders from card data instantly; sections skeleton; 404 → "Listing does not exist". First contact tap → house-rules summary sheet (BR-HR-03). API: `getRoomDetail`, `getSimilarRooms`, `getRoomTravelTimes`. US02, US03, US09, US11.

### SCR-ROOM-02 Gallery · SCR-ROOM-03 360° viewer
Full-screen black, swipe, pinch-zoom, counter; panorama: drag + gyro toggle, *Close*. Progressive thumb→medium. US03.

### SCR-ROOM-04 Report sheet
Reason radio list (BR-RPT-01), note field (required for *Other*), primary *Send report*; confirmation state "Thank you…". US12.

### SCR-FAV-01 Saved › Rooms
Segmented control *Rooms | Searches*. Cards show current price & availability (unavailable greyed with label). Swipe or [heart] to remove → snackbar *Removed from saved · Undo* (5 s). Empty: "Tap the heart icon to save rooms you like". API: `listMyFavourites`, `removeFavourite`. US06.

### SCR-SAVED-01 Saved › Searches & alerts
Row: name, summary ("< 3M · Boarding room · ≤ 2 km · Drawn area"), alert switch with states *On / Paused / Off*, [more] (Rename, Delete). Tap → runs search in Explore/Map. API: saved-search ops. US05 AC4, US07.

### SCR-NOTI-01 Notifications
List grouped *Today / Earlier*; unread dot; tap deep-links; *Mark all as read*. API: `listMyNotifications`, `markNotificationsRead`. US07.

### SCR-CMP-01 Compare
Horizontal table, sticky first column (row labels), up to 3 columns with cover + title; rows BR-CPR-02; best value cell highlighted (Verified colour + "Best Value"); *Remove* per column; primary *Share as image*. Empty (<2 rooms): "Add at least 2 rooms to compare". API: `compareRooms`. US10.

### SCR-CHAT-01 Conversations · SCR-CHAT-02 Conversation
List: listing thumb, other party name, last message, time, unread badge. Conversation: pinned listing card (tap → detail), bubbles (mine right / Primary), status *Sending / Sent / Not sent · Retry*, date separators, composer with send button (disabled when empty), load older on scroll top. Landlord sees the student's name only (no phone). API: chat ops + hub. US14.

### SCR-ACC-01 Account · SCR-ACC-02 Campus picker · SCR-ACC-03 Language
Account: profile, role badge, landlord verification status, *Campus*, *Language* (shows current: "English" / "Tiếng Việt"), notification settings (push on/off), *Sign out*. Guest: sign-in/register CTAs plus *Campus* and *Language* rows. API: `getCurrentUser`, `updateMyProfile`. US13.
Language (SCR-ACC-03): bottom sheet titled "Language / Ngôn ngữ" (always bilingual so a user who picked the wrong language can find it), two radio rows each written in its own language — *English*, *Tiếng Việt* — current one checked; selecting applies immediately and closes the sheet. API: `updateMyProfile { preferredLanguage }` when signed in. US15, UC-25.

### SCR-LL-01 My listings (landlord)
Header counts by status (chips filter); card with status badge, views placeholder (post-MVP), actions per state (BR-LST-04): *Edit*, *Mark rented*/*Mark available*, *Unpublish*/*Republish*, *Resubmit*; rejection/suspension reason visible. Unverified landlord: banner + disabled *Post listing*. Primary FAB-style *+ Post listing*. API: `listMyListings` + transitions. US08.

### SCR-LL-02 Listing editor (wizard, 4 steps + review)
Progress `1/4`. **Photos** (grid, per-photo upload progress, drag to reorder, mark 360°, min 3 indicator) → **Details** (numeric keypad for money with live "2,500,000 ₫" formatting) → **Location** (address fields + draggable pin, starts at campus) → **Amenities & safety** (chips, utilities) → **Review** → primary *Submit for review*. Sticky *Continue* in thumb zone; draft autosave on device. Validation inline per BR-LST-01. API: `uploadPhoto`, `createListing`/`updateListing`. US08.

### SCR-LL-03 House rules editor
Time pickers (quiet hours, curfew), policy segmented controls, parking text, flags, other rules; *Save* creates a version; *History* list. API: `updateHouseRules`, `listHouseRulesVersions`. US11 AC3.

### SCR-ADM-01 Moderation hub · SCR-ADM-02 Review listing · SCR-ADM-03 Report detail
Hub segmented *Landlords (n) · Listings (n) · Changes (n) · Reports (n)*, oldest first, high-priority reports pinned. Review listing: all fields, photos, pin, safety checklist toggles to confirm, primary *Approve*, secondary *Reject* (reason required). Report detail: listing summary, all reports, *Dismiss* / *Take action* (+ suspend toggle, note). API: admin ops. US08, US12, US13.

## 6. Content & formatting rules (en-US, vi-VN)

| Item | Rule | English (en-US) | Tiếng Việt (vi-VN) |
|---|---|---|---|
| Price on card | million with 1 decimal | `2.5M/mo`, `950k/mo` (< 1M) | `2,5 tr/tháng`, `950k/tháng` |
| Price on detail/forms | full VND, culture thousands separator | `2,500,000 ₫` | `2.500.000 ₫` |
| Area | `m²` | `25 m²` | `25 m²` |
| Distance | 1 decimal | `1.2 km to ICTU`; < 1 km → `850 m` | `1,2 km tới ICTU`; `850 m` |
| Duration | minutes, `~` for estimates | `8 mins`, `~19 mins (est.)` | `8 phút`, `~19 phút (ước tính)` |
| Dates | relative ≤ 7 days, else absolute | `3 days ago`, `15 Nov 2026` | `3 ngày trước`, `15/11/2026` |

Formatting always uses the **current UI culture** (`CultureInfo.CurrentCulture`), never a hard-coded separator.
| Tone | friendly, concise | "You saved this room" |
| Localisation | every user-facing string exists in `Resources/Strings/AppResources.resx` (en-US, fallback) **and** `AppResources.vi.resx` (vi-VN), including the `errorCode` map and enum labels (`RoomType_BoardingRoom`, `Amenity_Wifi`…). XAML binds through a localization source that raises change notifications (not `x:Static`) so an in-app switch re-renders open pages (US15). Vietnamese text is ~20–30 % longer: labels must wrap, never truncate buttons | `{Binding [SignIn], Source={x:Static loc:LocalizedStrings.Instance}}` |
| Proper nouns | campus names, addresses and people names stay in Vietnamese with diacritics | `Trường Đại học Công nghệ Thông tin và Truyền thông` |

`errorCode` → message map (client `Resources/Strings`): `VALIDATION_FAILED` "Please check the values you entered." · `INVALID_SORT_OPTION` "That sort option is not supported." · `INVALID_PRICE_RANGE` "The price range is not valid." · `INVALID_AREA_RANGE` "The area range is not valid." · `INVALID_DISTANCE_RANGE` "The distance filter is not valid." · `CAMPUS_NOT_FOUND` "That campus could not be found." · `INVALID_STATE_TRANSITION` "This action is not allowed in the current state." · `INVALID_CREDENTIALS` "Invalid email or password." · `ACCOUNT_LOCKED` "Your account is temporarily locked, please try again in {time}." · `EMAIL_TAKEN` "This email address is already in use." · `LANDLORD_NOT_APPROVED` "Landlord account is pending approval." · `ROOM_NOT_FOUND` "Listing does not exist." · `LISTING_NOT_AVAILABLE` "Room is no longer available." · `ALREADY_REPORTED` "You have already reported this listing." · `LIMIT_EXCEEDED` "Limit reached." · `CONCURRENCY_CONFLICT`/`ETAG_MISMATCH` "Data has changed, please refresh." · `RATE_LIMITED` "Too many requests, please try again shortly." · network "No internet connection." · fallback "An unexpected error occurred, please try again.".

Vietnamese (`AppResources.vi.resx`): `VALIDATION_FAILED` "Vui lòng kiểm tra lại các giá trị đã nhập." · `INVALID_SORT_OPTION` "Kiểu sắp xếp này không được hỗ trợ." · `INVALID_PRICE_RANGE` "Khoảng giá không hợp lệ." · `INVALID_AREA_RANGE` "Khoảng diện tích không hợp lệ." · `INVALID_DISTANCE_RANGE` "Bộ lọc khoảng cách không hợp lệ." · `CAMPUS_NOT_FOUND` "Không tìm thấy cơ sở này." · `INVALID_STATE_TRANSITION` "Thao tác này không hợp lệ ở trạng thái hiện tại." · `INVALID_CREDENTIALS` "Email hoặc mật khẩu không đúng." · `ACCOUNT_LOCKED` "Tài khoản tạm thời bị khoá, vui lòng thử lại sau {time}." · `EMAIL_TAKEN` "Email này đã được sử dụng." · `LANDLORD_NOT_APPROVED` "Tài khoản chủ trọ đang chờ duyệt." · `ROOM_NOT_FOUND` "Tin đăng không tồn tại." · `LISTING_NOT_AVAILABLE` "Phòng không còn trống." · `ALREADY_REPORTED` "Bạn đã báo cáo tin đăng này." · `LIMIT_EXCEEDED` "Đã đạt giới hạn." · `CONCURRENCY_CONFLICT`/`ETAG_MISMATCH` "Dữ liệu đã thay đổi, vui lòng tải lại." · `RATE_LIMITED` "Quá nhiều yêu cầu, vui lòng thử lại sau ít phút." · network "Không có kết nối mạng." · fallback "Đã có lỗi xảy ra, vui lòng thử lại.".

## 7. Accessibility & interaction

- `SemanticProperties.Description` on icon-only buttons (heart, share, filter); headings use `SemanticProperties.HeadingLevel`.
- Do not rely on colour alone (status banners include text; best-value cell includes "Best Value").
- Motion: 150–250 ms ease-out; respect reduced motion (no shimmer when animations disabled).
- Gestures always have a visible alternative (swipe-to-remove favourite ↔ heart button).
- Keyboard: numeric keypads for money/area; `ReturnType=Next` through forms.

## 8. Acceptance checklist per screen (for UI PRs)

- [ ] Matches screen spec order and primary action; tokens only.
- [ ] Loading / Empty / Error / Offline states implemented.
- [ ] `x:DataType` on page and every `DataTemplate`.
- [ ] Every string in both `AppResources.resx` and `AppResources.vi.resx`, formatted per §6 for the current culture; errors via `errorCode` map; screen checked in **both** languages (no truncation).
- [ ] Touch targets ≥ 48 dp; contrast AA; semantic descriptions.
- [ ] Protected actions trigger `SignInPrompt` and resume after login.

# Room Radar UI redesign verification

## Scope and audit

This expansion continues the uncommitted Blue & White work already present in the workspace. No commits, dependency additions, backend contract changes, or map SDK replacements were made.

The app has three Shell tabs: discovery, map, and my listings. Related routed pages are listing detail, login, and listing editor. The legacy `RoomListingPage` preview is also styled. There is no separate profile page or Appium project in the current checkout.

The installed package is `Syncfusion.Maui.Toolkit` **1.0.10**. Its local XML documentation was used to verify `SfButton.Content` (`DataTemplate`), `SfBottomSheet`, text input layout, and shimmer APIs. Native MAUI remains responsible for list virtualization, cards, simple layouts, and numeric range fields. No slider package was added.

## Implemented systems

| System | Result |
| --- | --- |
| Buttons | Shared primary, secondary, outline, text, destructive, icon, floating styles. Semibold type, 44–50dp targets, pressed/focus/disabled feedback, and scale feedback. Existing style aliases inherit the shared hierarchy. |
| Discovery | Compact white header, search/filter actions, real result count, removable applied chips, adaptive virtualized feed, reusable image-first property cards, loading/empty/error feedback. |
| Search | Focused campus search overlay, clear/back actions, keyboard Search submission, local accent-insensitive matches against existing campus data. Typing sends no API requests. |
| Filters | Modal Syncfusion sheet, scrollable sections with fixed footer, price presets and validated numeric ranges, area, room type, supported campus/distance options. Reset edits the draft; Apply submits; closing discards unapplied changes on reopening. Counts reflect groups. |
| Map | Compact floating header/actions, separate toolbar above navigation, native sort sheet, location permission/GPS feedback, polygon point emphasis, price pill selection, marker collision avoidance, compact result strip and preview. Loading/offline/error/empty states and saved-area deletion confirmation. |
| Related screens | Persistent detail contact action and editor save action, login busy feedback, consistent management actions and empty/loading/error messaging. Shell tab icons and blue/white launcher/splash assets. |

Only supported filters are exposed. Discovery's list API does not accept keyword search, amenities, district hierarchy, or arbitrary sort; these are not simulated. Map search retains its existing supported sort choices. Search history is not fabricated. The detail DTO has no image gallery; the existing illustration is explicitly labeled instead of claiming verification or showing fake photos.

## Automated verification

- Full rebuilt NUnit suite: **195 passed, 0 failed, 0 skipped**; no warnings reported.
- Thirteen new regression cases cover draft reset/cancel/apply, independent chip removal, grouped counts, campus search preserving filters, suggestions without network requests, and map presentation states.
- Read-only MAUI review found no remaining blocking or major regression. A stale location-progress notice after navigation was fixed.
- Final Android and Windows builds passed with **0 warnings and 0 errors**. The Android APK was installed on the Pixel 7 emulator.
- `git diff --check` is included in the final workspace checks.

## Changed file groups in this expansion

- `RoomToRentRadar/Resources/Styles/{Colors,Styles}.xaml`: action variants, native image-button states and semantic tokens.
- `RoomToRentRadar/Features/Home/ViewModels/{HomeViewModel,HomeListingItem,FilterChip,FilterSummaryChip}.cs`: presentation state, draft workflow, campus matching and removable groups.
- `RoomToRentRadar/Features/Home/Views/HomePage.xaml(.cs)` and new `HomePropertyCard.xaml(.cs)`: discovery, search, filter sheet and reusable cards.
- `RoomToRentRadar/Features/Map/ViewModels/MapSearchPageModel.cs` and new `.Presentation.cs`; `Views/MapSearchPage.xaml(.cs)`, `.Motion.cs`, new `.Controls.cs`: map presentation and interactions.
- `RoomToRentRadar/Features/{Login,RoomDetail,ListingManagement}/Views/*.xaml`: related button/layout consistency, contact/save actions and form presentation.
- `RoomToRentRadar/Pages/RoomListingPage.xaml(.cs)` and `Pages/Controls/RoomCardView.xaml`: existing preview consistency.
- `RoomToRentRadar/{App.xaml.cs,AppShell.xaml,MauiProgram.cs,RoomToRentRadar.csproj}`: native keyboard/input presentation, tab icons and app artwork configuration.
- `RoomToRentRadar/Resources/{Images,AppIcon,Splash}`: task-specific SVG icons and blue/white launcher/splash artwork; existing uncommitted illustrations retained.
- `RoomToRentRadar/Platforms/Android/AndroidManifest.xml`, `Platforms/{iOS,MacCatalyst}/Info.plist`: user-triggered location capability declarations.
- New `RoomToRentRadarUnitTests/{HomeDiscoveryInteractionTests,MapPresentationTests}.cs`; existing `MapSearchPageModelTests.cs` made partial.

The pre-existing untracked API `.csproj.user` and unrelated user work were not edited or staged.

## Brighter palette refinement

The follow-up palette uses azure `#0077D6`, white, ice-blue `#EFF9FF`, canvas `#F7FBFF`, navy text `#152D45`, and restrained cream highlights in the existing SVG artwork. Pressed/secondary ink is `#0061B8`. Icons, polygon/price markers, platform accents, launcher and splash artwork use the same azure family. The duplicate Map action beside the Room Radar logo was removed; the Shell Map tab remains.

Measured light-mode text contrast: primary white 4.56:1; secondary on selected blue 5.21:1; muted text 4.92:1; disabled neutral text 4.77:1; availability 4.79:1; warm semantic text 4.53:1. These are targeted palette checks; they do not assert that every possible image overlay or dark-theme state was audited.

The refinement builds passed on Windows and Android with 0 warnings/errors. A Windows preview with 60 rooms was inspected and left open; capture: `RoomToRentRadar/bin/ui-review/palette-bright-wide.png`. The local API was restarted from its existing HTTP profile for this preview; no API code or configuration changed. No new behavior tests were added for this palette/header-only refinement.

## Selected direction: Plum & Pearl

The user selected the purple direction from the comparison. The current production palette uses plum `#795477`, pressed/chip ink `#623F61`, pearl canvas `#F8F7FA`, white surfaces, dark ink `#302936`, muted ink `#6E6575`, and selected tint `#F0E8F1`.

Prices use dark text in discovery, detail, management, map cards and the legacy preview. Default map price labels are dark on white; selected markers and the polygon use plum. Icons, native platform accents, launcher and splash are synchronized. Existing fallback SVG room illustrations were recolored to restrained warm neutrals with small plum details; authentic cover-photo rendering remains unchanged. The stock photo used to compare directions was not inserted into listings. The duplicate Map action beside the logo remains removed.

Changed groups: global Colors/Styles; SVG brand and illustration assets; app icon configuration and Android native accent; Home/RoomDetail/Map/MyListings/legacy price presentation; map/editor marker colors. No business/API/dependency changes.

Measured contrast: primary white/plum **6.27:1**, small selected-label ink **6.45:1** on the stronger selected tint, body ink/canvas **13.15:1**, muted ink/canvas **5.20:1**, disabled ink/neutral **4.79:1**. SVG parsing and whitespace checks passed. Windows preview capture is stored at `RoomToRentRadar/bin/ui-review/palette-plum-wide.png`.

The selected Plum implementation builds passed on Windows and Android with **0 warnings and 0 errors**. The running Windows preview was visually inspected, including dark pricing and the plum contact action on room detail. The local API remained healthy. No new behavior tests were added for this presentation-only change; Android device visuals for this palette remain unverified in this follow-up.

## Device verification

Target: `emulator-5554`, AVD `pixel_7_-_api_33_0`, 1080×2400, density 420. Captures are local generated artifacts under `RoomToRentRadar/bin/ui-review/` (excluded from Git through the existing `bin/` rule).

| Check | Observed result |
| --- | --- |
| Explore default | Real API returned 60 rooms; image/title/price/area/address and Shell navigation visible. |
| Explore filtered | Applying 3–5 million VND returned 4 rooms; applied chip and a complete `1` badge visible. |
| Filter sheet | Price presets, numeric fields, room choices, scroll body and fixed Apply/Reset footer displayed above navigation. Final `filter-keyboard.png` confirms Apply remains visible above the numeric keyboard after enabling Resize. |
| Search | Focus ring, actual campus choices, keyboard Search action, clear/back, and `zzzz` empty state exercised. Native Android underline removed. |
| Polygon drawing | Four real map taps produced four vertices and highlighted latest point; Finish submitted the polygon. |
| Map results | API returned 8/8 rooms; price markers, selection, result strip, separate toolbar and attribution visible. |
| Map preview/detail | Selecting a result produced compact preview; View details opened the correct listing. |
| Clear polygon | Cleared the overlay/results and returned to the initial map heading; unrelated discovery price filter remained selected. |
| Room detail | Opened from discovery and map; persistent contact area remained above bottom navigation. |
| My listings | Logged-out presentation and sign-in actions inspected. Authenticated management writes were not performed. |
| Login | Opened through contact. The form now uses native Border rather than SfCardView; the final screenshot confirms the complete 50dp sign-in button without clipping. |
| Windows wide/narrow | Captured at 1500×1062 and 480×1000. Final wide capture confirms three columns with gaps. Item padding avoids WinUI's loss of a column with nonzero grid spacing. |

Device iteration fixed filter badge clipping, Android input underline, button-container measurement, and map icon rendering. Simple map image actions use native `ImageButton` with the same design tokens/states; richer actions continue to use `SfButton`. Android is configured to resize content for the soft keyboard, and both discovery/filter containers retain their safe-area handling.

The initially running Pixel 7 emulator exited during preview testing. A software-rendered restart subsequently showed **System UI isn't responding**. The same AVD was restarted with host GPU for further checks. This is a material limit on performance conclusions: no frame-rate, memory-regression, or cross-platform parity claim is made from this session.

Remaining verification: authenticated create/edit/save/unpublish and successful login against a test account; GPS disabled/permission-denied branches on device; reproducible offline/retry cases; all-screen large-font/TalkBack checks; sustained scrolling/tab/frame-time comparison; iOS/Mac Catalyst builds and visuals. Unit tests cover existing business flows but do not substitute for those device checks. Acceptance criteria 13, 15, and 16 are therefore only partially verified, rather than marked complete globally.

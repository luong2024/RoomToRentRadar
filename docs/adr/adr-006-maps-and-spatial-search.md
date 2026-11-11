# ADR-006: .NET MAUI Maps (native Google/Apple) + in-process spatial filtering

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** US05, US08 (pin drop), BR-SRCH-03, BR-SRCH-07, ADR-002

## Context
US05 needs pins, preview cards and polygon drawing; US08 needs the landlord to place a pin; US01/US04 need distance to campus. Budget prefers free tools; the server datastore is SQLite (no native spatial index).

## Options considered — map control
| Option | Pros | Cons |
|---|---|---|
| **`Microsoft.Maui.Controls.Maps`** (Google Maps SDK on Android, Apple MapKit on iOS) | Official, native rendering = smoothest UX users already know (Jakob's law); Pins, Polygons, `MapClicked` built in; Google mobile dynamic maps have no per-load charge, MapKit free | Android needs a Google Maps API key (billing account must be enabled); no Windows support; no freehand drawing |
| Mapsui + OpenStreetMap tiles | Fully free/OSS, cross-platform | Less polished gestures; OSM tile policy forbids heavy app usage → need a tile provider (MapTiler/OpenFreeMap) |
| WebView + Leaflet | Flexible | Web-in-app feel, bridge complexity, slower |

## Options considered — spatial query on SQLite
| Option | Pros | Cons |
|---|---|---|
| SpatiaLite extension | Real spatial SQL | Native extension packaging on Windows/Linux/Android, extra complexity |
| **Bounding box on indexed lat/lng + exact check in C#** | Pure SQL + pure C# Domain service, testable, fast for ≤ thousands of rows | Not suitable for millions of rows (not our scale) |

## Decision
- Client map: **.NET MAUI Maps**. Android key from build-time secret (not committed); iOS uses MapKit. Fallback if a Google billing account is impossible: Mapsui behind the same `MapView` usage in one page (new ADR required).
- **Drawing UX:** "tap to add vertices" (3–20) with live polygon, undo last point, *Xong* / *Xoá* buttons — reliable on all devices, unlike freehand drawing that conflicts with map panning.
- **Pin-drop for landlords** instead of paid geocoding: map opens at campus, landlord drags the pin to the house; address text typed manually.
- **Server spatial search:** `SELECT … WHERE Status='Available' AND Latitude BETWEEN … AND Longitude BETWEEN …` (bbox of polygon or of the distance circle), then Domain services `HaversineDistanceCalculator` and `PolygonContainment` (ray casting) do the exact filtering and distance sort. A short-lived (30 s) in-memory read cache of Available room cards in the API keeps search p95 < 300 ms.
- Client-side clustering is not needed at ≤ 200 pins (BR-SRCH-07); price-label pins.

## Consequences
- Positive: native, familiar map UX; zero spatial infrastructure; fully unit-testable geometry.
- Negative: Google Cloud account setup; maps not available on Windows target (not an MVP platform, ADR-013).
- Follow-up: `AndroidManifest` meta-data for the key + `ACCESS_COARSE/FINE_LOCATION` only if "near me" is added later (not MVP — map centres on campus, no location permission needed).

## Compliance
Unit tests for `PolygonContainment` (convex, concave, edge, self-intersection rejection) and Haversine; no SpatiaLite reference.

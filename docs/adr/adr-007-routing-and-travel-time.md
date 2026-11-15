# ADR-007: OpenRouteService for travel time, cached, with straight-line fallback

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** US09, BR-TRV-01..04, ERD `TravelTimeEstimates`

## Context
US09 needs distance and travel time by motorbike, bus and walking, using the profile campus, with a straight-line fallback. Rooms and campuses do not move, so results are highly cacheable.

## Options considered
| Option | Pros | Cons |
|---|---|---|
| Google Routes API | Best data, has `TWO_WHEELER` mode in some countries | Paid after free monthly cap; billing account; transit data in Thái Nguyên is thin |
| **OpenRouteService (ORS) hosted API** | Free key (daily quota sufficient with caching), OSM data, foot & driving profiles, simple REST | No transit, no motorbike profile; quota limits |
| Public OSRM demo server | Free | Not allowed for production use, no SLA |
| Self-host OSRM/GraphHopper | Unlimited | Server resources and setup beyond course scope |

## Decision
- Port `ITravelTimePort` (Outbound) with adapters `OpenRouteServiceTravelTimeAdapter` and `MockTravelTimeAdapter` (Development/tests; disabled by default per GUARDRAILS 1.5.4).
- Motorbike ≈ ORS `driving-car` duration; Walking = `foot-walking`; Bus = labelled estimate `driving × 1.6 + 10 min` (BR-TRV-02).
- Server caches per (room, campus, mode) for 30 days in `TravelTimeEstimates`; first request computes, later requests are DB reads. Optionally precompute for the default campus when a listing is approved.
- Timeout 3 s; on failure return straight-line Haversine with `isFallback = true` (US09 AC3). API key from configuration/secret; missing key outside Development → startup fails fast unless the mock is explicitly enabled in Development.

## Consequences
- Positive: free; ~1 external call per (room, campus, mode) ever.
- Negative: bus time is an estimate (clearly labelled in UI).
- Follow-up: verify ORS quota on sign-up (free account, no card); document key in `appsettings.Development.json` placeholder only.

## Compliance
Adapter contract tests (success, timeout → fallback, quota error → fallback); no API key in committed files.

# RoomToRentRadar API

ASP.NET Core 10 Web API using MVC controllers with an EF Core 10 SQLite database.
The OpenAPI document is generated with the built-in ASP.NET Core OpenAPI support.

## Project layout

- `Controllers/` — API controllers (`ListingsController`, `HealthController`).
- `Features/Listings/` — listing query validation, cursor codec, sort strategy, query services and the
  polygon helpers used by area search.
- `Data/` — EF Core context, configurations, migrations, SQLite setup and seeding.
- `Domain/` — EF Core entities.

## Run

```bash
dotnet run --project RoomToRentRadar.Api
```

The default URL is `http://localhost:5080` (see `Properties/launchSettings.json`).
In Development the API applies migrations and seeds the sample data on startup.
The SQLite file is created at `RoomToRentRadar.Api/App_Data/roomtorentradar.db`.

Override the database with configuration or an environment variable:

```bash
ConnectionStrings__Default="Data Source=C:/data/roomtorentradar.db" dotnet run --project RoomToRentRadar.Api
```

## Seed

```bash
# Add the 60-row sample set automatically in Development (idempotent).
dotnet run --project RoomToRentRadar.Api

# Add listings until the database contains 100,000 rows (batched, one transaction).
dotnet run --project RoomToRentRadar.Api -- seed --count 100000
```

Use a separate database for benchmarks, for example:

```bash
ConnectionStrings__Default="Data Source=App_Data/benchmark.db" \
  dotnet run --project RoomToRentRadar.Api -- seed --count 100000
```

Database files are ignored by git. Never commit a seeded database.

## Endpoints

- `GET /api/v1/listings`
  - `limit` (default 20, max 100), `cursor`, `sort` (`newest`, `price_asc`, `price_desc`, `area_desc`),
    `min_price`, `max_price`, `bedrooms`
  - Only `published` listings are returned.
  - Errors use RFC 9457 `ProblemDetails` with a stable `code` extension.
- `GET /api/v1/listings/{id}`
  - Returns the full detail of one `published` listing.
  - Response fields: `id`, `title`, `description`, `price`, `areaM2`, `bedrooms`, `address`, `lat`,
    `lng`, `createdAt`, `updatedAt`, `amenities`, `rules`. Photos are not part of the detail payload.
  - `amenities` is an ordered array of wire codes: `wifi`, `air_conditioner`, `parking`,
    `washing_machine`, `refrigerator`, `kitchen`, `private_bathroom`, `balcony`, `elevator`,
    `security`, `gym`, `pool`, `pet_allowed`.
  - `rules` is an ordered array of free-text strings (`sort_order`, max 300 characters each).
  - A missing id and a draft/hidden/removed listing return `404` `application/problem+json` with
    `code` = `listing_not_found`; the response never reveals whether the row exists. A malformed id
    also returns `404` but without the `code` extension.
- `POST /api/v1/listings/search/area`
  - Returns the `published` listings whose coordinates fall inside a drawn GeoJSON polygon, using the
    same filters, sorts, cursor and limits as `GET /api/v1/listings`. See
    [Area search](#area-search) for the full contract.
- `GET /health`
- `GET /openapi/v1.json` (Development only)
- `GET /swagger` (Swagger UI, Development only)

## Area search

`POST /api/v1/listings/search/area` with `Content-Type: application/json`:

```json
{
  "area": {
    "type": "Polygon",
    "coordinates": [
      [[106.68, 10.75], [106.72, 10.75], [106.72, 10.79], [106.68, 10.79], [106.68, 10.75]]
    ]
  },
  "filters": { "minPrice": 2000000, "maxPrice": 15000000, "bedrooms": 2 },
  "sort": "newest",
  "limit": 20,
  "cursor": null
}
```

- `area` is required; `filters`, `sort`, `limit` and `cursor` are optional.
- Positions are WGS84 `[lng, lat]` pairs. Only `type = "Polygon"` (case-insensitive) with exactly one
  closed exterior ring is accepted; holes and extra rings are rejected. There is no circle search.
- `filters.minPrice`, `filters.maxPrice` and `filters.bedrooms` are the camelCase equivalents of
  `min_price`, `max_price` and `bedrooms` and use the same validation and inclusive semantics.
- `sort` keeps the list whitelist (`newest` default, `price_asc`, `price_desc`, `area_desc`). There is
  no distance sort. Cursors are the same opaque tokens produced by `pagination.next_cursor`; a cursor
  issued for another sort is rejected.

Response `200`:

```json
{
  "data": [ /* ListingSummaryDto, same shape as the list endpoint */ ],
  "pagination": { "limit": 20, "next_cursor": null, "has_more": false },
  "totalCount": 37,
  "totalCountCapped": false
}
```

- `totalCount` counts every exact polygon match after the filters and before the cursor, capped at
  1000. `totalCountCapped` is `true` when the real match count exceeds 1000, so every page of the same
  search reports the same total.
- Matching is inclusive of the polygon boundary: points on an edge and exactly on a vertex are
  returned.
- `coverPhotoUrl` follows the same rule as the list endpoint: the cover photo (`is_cover DESC,
  sortOrder, id`) with `thumbUrl ?? url`, or `null` when the listing has no photos.

### Validation

Ring rules, checked in this order:

| Rule | Error |
| --- | --- |
| `area.coordinates` is a non-empty array of rings | `invalid_area_ring` |
| It contains exactly one ring; extra rings are treated as holes | `unsupported_area_holes` |
| Ring has 4-201 positions (3-200 vertices plus the closing position) | `invalid_area_vertex_count` |
| At least 3 distinct vertices | `invalid_area_vertex_count` |
| Every position is exactly two finite numbers, `lng` in [-180, 180], `lat` in [-90, 90] | `invalid_area_coordinate` |
| First and last positions are identical | `invalid_area_ring` |
| Longitude span is at most 180 degrees | `antimeridian_unsupported` |
| The ring encloses a non-zero area | `invalid_area_ring` |
| The polygon is simple (NetTopologySuite `IsValid`) | `self_intersecting_area` |
| Approximate area is at most 100 km² | `area_too_large` |

A geometry that is valid JSON but not an acceptable polygon returns `422` with
`application/problem+json`, `type` = `https://tools.ietf.org/html/rfc9110#section-15.5.21`
(`422 Unprocessable Content`), `title` = `Invalid search area`, and the extensions `code` (one of the
codes above, plus `invalid_area_type` for a non-polygon `type`) and `field` (`area` or
`area.coordinates`).

`400` is returned for malformed JSON or a body that does not match the schema (code
`invalid_parameter_format`, including a missing `area`), and for the shared list query rules:
`invalid_limit`, `invalid_sort`, `invalid_price_range`, `invalid_bedrooms`, `invalid_cursor`,
`cursor_sort_mismatch`. The automatic model-state response shares the `invalid_parameter_format` code
with the list endpoint, so its detail now reads "One or more request parameters have an invalid
format." for both endpoints.

The area is measured with an equirectangular (plate carrée) approximation scaled by the cosine of the
mean latitude. It is not a geodesic area; it is accurate within a fraction of a percent for the
city-scale polygons this endpoint is meant for.

### Query budget

A request issues at most two `SELECT` statements:

1. one candidate query that reads `id`, `lat`, `lng`, `created_at`, `price`, `area_m2` plus the display
   columns of the summary DTO for every `published` listing inside the inclusive bounding box and the
   price/bedroom filters - no photos, no paging before the exact test;
2. one cover-photo query for the ids that ended up on the returned page, skipped entirely when the
   page is empty.

Candidates are then classified with a prepared NetTopologySuite polygon (`PreparedGeometry.Intersects`
against a point), ordered in memory with the same primary key direction and ascending `id` tie-breaker
as the list endpoint, paged with the cursor, and finally `limit + 1` rows are taken to
decide `has_more`.

## SQLite concurrency

SQLite is the only database and the API is intended to run as a single instance.
WAL mode is enabled at startup; `busy_timeout=5000` and `foreign_keys=ON` are applied to every
physical connection by `SqlitePragmaInterceptor`. Write transactions are kept short.
Horizontal scaling would require moving to a server database such as PostgreSQL; that is out of scope.

## Index rationale

Every public listing query filters on `status = 'published'` and orders by one whitelisted sort key
followed by `id` as the tie-breaker. Each sort therefore has a matching composite index:

| Sort | Ordering | Index |
| --- | --- | --- |
| `newest` | `created_at DESC, id ASC` | `ix_listings_status_created_at_id (status, created_at DESC, id)` |
| `price_asc` | `price ASC, id ASC` | `ix_listings_status_price_id (status, price, id)` |
| `price_desc` | `price DESC, id ASC` | `ix_listings_status_price_desc_id (status, price DESC, id)` |
| `area_desc` | `area_m2 DESC, id ASC` | `ix_listings_status_area_m2_desc_id (status, area_m2 DESC, id)` |

Additional indexes:

- `ix_listings_status_bedrooms (status, bedrooms)` for bedroom-only filters.
- `ix_listing_photos_listing_cover_sort_id (listing_id, is_cover DESC, sort_order, id)` keeps the
  cover-photo subquery cheap without a second query (no N+1).
- `ix_listings_status_lat_lng (status, lat, lng)` and `ix_listings_status_lng (status, lng)` back the
  area-search bounding box (migration `AddListingSpatialIndexes`). The `status` equality comes first so
  the index only walks published rows, and both coordinate ranges are applied from the index where
  SQLite can use them. Rows are still filtered on the exact polygon after the bounding box, because
  SQLite does not index the polygon itself.

EF Core collapses indexes over the same properties even when the direction differs, so
`ix_listings_status_price_id` is created explicitly in the initial migration.

`EXPLAIN QUERY PLAN` against 100,000 published rows:

```text
newest       SEARCH listings USING COVERING INDEX ix_listings_status_created_at_id (status=?)
price_asc    SEARCH listings USING COVERING INDEX ix_listings_status_price_id (status=?)
price_desc   SEARCH listings USING COVERING INDEX ix_listings_status_price_desc_id (status=?)
area_desc    SEARCH listings USING COVERING INDEX ix_listings_status_area_m2_desc_id (status=?)
filtered     SEARCH listings USING INDEX ix_listings_status_price_id (status=? AND price>? AND price<?)
```

Warm local measurements for `GET /api/v1/listings` with 100,000 published listings:
median 1.6–2.2 ms, max 2.8 ms (limit 20 and 100, all four sorts, and a combined filter).

### Area-search plans (100,000 published rows)

The benchmark database places every row on one of eight fixed city coordinates, so a bounding box is
deliberately unrealistic here: it either contains everything or one eighth of the table.

```text
city bbox (all rows)      SEARCH listings USING INDEX ix_listings_status_lng (status=? AND lng>? AND lng<?)
                          rows=100000  median 324-341 ms
2 km bbox (12500 rows)    SEARCH listings USING INDEX ix_listings_status_lng (status=? AND lng>? AND lng<?)
                          rows=12500   median 38-63 ms
bbox + price + bedrooms   SEARCH listings USING INDEX ix_listings_status_lng (status=? AND lng>? AND lng<?)
                          rows=1833    median 164 ms
```

SQLite applies one range column from the index and evaluates the remaining predicate as a filter, so
without column statistics it consistently picks `ix_listings_status_lng` (equality on `status` plus the
longitude range). When the latitude range is the selective one, forcing the three-column index is
clearly cheaper:

```text
10.732-10.736 lat band   INDEXED BY ix_listings_status_lng        median 144 ms
10.732-10.736 lat band   INDEXED BY ix_listings_status_lat_lng    median  34 ms
```

Warm end-to-end `POST /api/v1/listings/search/area` measurements on the same database with `limit 20`
(including the bounding-box query, exact polygon classification, in-memory sort, cursor paging and the
cover-photo query):

| Polygon | Candidates | Median | Max |
| --- | --- | --- | --- |
| 10.5 km² covering four seed coordinates | ~50,000 | 200 ms | 314 ms |
| 63 km² covering six seed coordinates | ~75,000 | 301 ms | 317 ms |
| Same 63 km² polygon with `bedrooms: 2` | ~12,500 | 151 ms | 157 ms |

All three are below the 500 ms warm target. The candidate count is derived from the eight fixed seed
coordinates (12,500 rows each) that the polygon contains.

Those bounding-box numbers are the cost of the query and row materialization only; they exclude the
in-memory polygon classification, which runs on the returned candidate set. The design keeps the
candidate query to a fixed two-column range scan; a spatial index (R-tree, PostGIS, SpatiaLite) or a
geohash/quadkey column would be the next step if city-wide boxes ever need to return fewer than the
whole city. Numbers measured with `EXPLAIN QUERY PLAN` and 20 warm runs per query on the local SQLite
file.

## Tests

```bash
dotnet test RoomToRentRadar.Api.Tests
```

`Unit/GeoSearchTests` and `Unit/ListingSortKeyOrderingTests` cover the polygon validation rules and the
in-memory ordering/cursor mirror; `Integration/AreaSearchEndpointTests` covers the endpoint contract,
the cursor page walk, the `totalCount` cap and the two-query budget.

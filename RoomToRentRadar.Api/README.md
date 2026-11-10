# RoomToRentRadar API

ASP.NET Core 10 Web API using MVC controllers with an EF Core 10 SQLite database.
The OpenAPI document is generated with the built-in ASP.NET Core OpenAPI support.

## Project layout

- `Controllers/` — API controllers (`ListingsController`, `HealthController`).
- `Features/Listings/` — listing query validation, cursor codec, sort strategy and query service.
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
- `GET /health`
- `GET /openapi/v1.json` (Development only)
- `GET /swagger` (Swagger UI, Development only)

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

## Tests

```bash
dotnet test RoomToRentRadar.Api.Tests
```

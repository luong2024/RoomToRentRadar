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

## Demo sign-in

The landlord phone number is only available through an authenticated endpoint. In Development a
demo account is seeded on startup when credentials are configured; nothing is seeded otherwise.

```bash
Auth__DemoEmail="demo@example.test" \
Auth__DemoPassword="choose-a-local-password" \
Auth__DemoDisplayName="Demo renter" \
dotnet run --project RoomToRentRadar.Api
```

Locally, user-secrets keep the password out of the shell history:

```bash
dotnet user-secrets set "Auth:DemoPassword" "choose-a-local-password" --project RoomToRentRadar.Api
```

Never commit these values. The API stores only a PBKDF2-SHA256 hash and salt, and stores only a
SHA-256 hash of each issued opaque bearer token. Sessions expire after 7 days.

When a demo account and credentials are configured, the app signs in automatically at startup via
`POST /api/v1/auth/demo-login`. The endpoint returns a session like `/login` and is only available
in Development with demo credentials configured; anywhere else it returns `404`. No credentials are
stored in the app.

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
  - Returns the full listing detail (`Content-Type: application/json`).
  - Response fields: `id`, `title`, `description`, `price`, `deposit`, `areaM2`, `roomType`,
    `bedrooms`, `address`, `lat`, `lng`, `status`, `createdAt`, `updatedAt`, `amenities`, `rules`,
    `landlordName`. Photos are not part of the detail payload, and the landlord phone is never
    included (see the contact endpoint).
  - `roomType` is one of `studio`, `apartment`, `house`, `shared_room`; `status` is one of `draft`,
    `published`, `hidden`, `removed`.
  - `amenities` is an ordered array of wire codes: `wifi`, `air_conditioner`, `parking`,
    `washing_machine`, `refrigerator`, `kitchen`, `private_bathroom`, `balcony`, `elevator`,
    `security`, `gym`, `pool`, `pet_allowed`.
  - `rules` is an ordered array of free-text strings (`sort_order`, max 300 characters each).
  - `200` — the listing exists and is `published`.
  - `404` — no listing has that id; `code` is `listing_not_found`. A malformed id also returns
    `404` but without the `code` extension.
  - `410` — the listing exists but is not `published` (draft, hidden, removed); `code` is
    `listing_unavailable`. The body never contains the listing content.
  - Error responses use `application/problem+json` with a stable `code` extension.
- `GET /api/v1/listings/{id}/similar?limit=4`
  - Returns published listings with the same `bedrooms`, `price` within ±20% and `areaM2` within
    ±30% of the requested listing, closest price first. The requested listing is excluded.
  - `limit` defaults to 4 (max 10). Works for unpublished listings too, so the app can suggest
    alternatives when a listing is unavailable.
  - `404` — no listing has that id (`listing_not_found`); `400` — invalid limit (`invalid_limit`).
- `GET /api/v1/listings/{id}/contact` — requires `Authorization: Bearer <token>`
  - Returns `{ "landlordName": "...", "landlordPhone": "..." }` for a published listing.
  - `401` — missing or invalid token (`unauthorized`); `404` — unknown listing (`listing_not_found`);
    `410` — listing is not published (`listing_unavailable`).
- `POST /api/v1/auth/login` — body `{ "email": "...", "password": "..." }`
  - Returns `{ "accessToken": "...", "expiresAt": "...", "displayName": "..." }`.
  - `401` — invalid email or password (`invalid_credentials`); `400` — missing fields.
- `POST /api/v1/auth/demo-login` — Development only; signs in the configured demo account.
  - Returns the same payload as `/login`; `404` outside Development or when demo credentials are
    not configured.
- `POST /api/v1/auth/logout` — requires a bearer token; returns `204` and invalidates the session.
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

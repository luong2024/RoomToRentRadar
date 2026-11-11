# RoomToRentRadar API

ASP.NET Core 10 Web API using MVC controllers with an EF Core 10 SQLite database.
The OpenAPI document is generated with the built-in ASP.NET Core OpenAPI support.

## Project layout

- `Controllers/` — API controllers (`ListingsController`, `ListingPhotosController`, `HealthController`).
- `Features/Listings/` — listing query validation, cursor codec, sort strategy and query service.
- `Features/Photos/` — photo upload limits, validation, image processing, storage and gallery service.
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
- `GET /api/v1/listings/{listingId}/photos` — gallery for a listing (see [Photos](#photos)).
- `POST /api/v1/listings/{listingId}/photos` — multipart photo upload (see [Photos](#photos)).
- `GET /media/**` — stored photo files served as static files.
- `GET /health`
- `GET /openapi/v1.json` (Development only)
- `GET /swagger` (Swagger UI, Development only)

## Photos

Photo endpoints are currently **unauthenticated**: anyone who can reach the API can upload photos to
any listing. Add authentication/authorization before exposing the API outside a trusted network.

### `POST /api/v1/listings/{listingId}/photos`

`multipart/form-data` with these fields:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `files` | one or more files | yes | 1–10 files per request, 10 MB per file |
| `kind` | string | no | `photo` (default) or `panorama`, case-insensitive |

Accepted inputs are JPEG, PNG and WebP. Every upload is decoded server-side (the declared content
type is checked, then the file signature), auto-oriented using the EXIF orientation tag, and
re-encoded as JPEG at quality 85. A JPEG thumbnail limited to 480 px on the longest edge (quality 80)
is generated for every photo. Animated or multi-frame images, images above 40 megapixels and
undecodable files are rejected.

Successful uploads return `201 Created` with a `Location` header pointing at the gallery and a JSON
array of the created `ListingPhotoDto`. New photos continue the `sortOrder` sequence, and the first
photo of a listing that has no cover yet becomes the cover. Uploads are all-or-nothing: if any file
in the batch fails, or the database write fails, no rows are persisted and every file written during
the request is deleted. A listing can hold at most 20 photos.

Storage layout (client file names are never used):

```text
{Storage:PhotoRootPath}/listings/{listingId}/photo-{photoId:N}.jpg
{Storage:PhotoRootPath}/listings/{listingId}/thumbs/photo-{photoId:N}.jpg
```

### `GET /api/v1/listings/{listingId}/photos`

Returns `200 OK` with the photos of the listing ordered by cover first, then `sortOrder`, then `id`.

### Storage configuration

The storage root defaults to `RoomToRentRadar.Api/App_Data/media` (resolved against the content root)
and is served under `/media`. Override it with configuration or an environment variable:

```bash
Storage__PhotoRootPath="D:/data/rtrr-media" dotnet run --project RoomToRentRadar.Api
```

`App_Data/` is gitignored. Point the root at durable storage (or a mounted volume) in any deployed
environment, and make sure the process can write to it.

### Photo error codes

All photo failures use `application/problem+json` with a stable `code` extension:

| Status | `code` | Cause |
| --- | --- | --- |
| 400 | `empty_upload` | no files were sent |
| 400 | `invalid_photo_kind` | `kind` is not `photo` or `panorama` |
| 400 | `invalid_photo` | empty file, more than 10 files, corrupt/animated image or more than 40 megapixels |
| 404 | `listing_not_found` | the listing does not exist |
| 409 | `photo_limit_exceeded` | the listing already has 20 photos |
| 413 | `photo_too_large` | a file is larger than 10 MB |
| 415 | `unsupported_photo_type` | the declared content type is not `image/jpeg`, `image/png` or `image/webp` |

The request body limit for uploads is 100 MB. A body that is not valid `multipart/form-data` is
rejected with `400` before the endpoint runs.

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

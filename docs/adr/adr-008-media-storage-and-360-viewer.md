# ADR-008: Photo storage behind a port, SkiaSharp renditions, Pannellum 360° viewer

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** US03, US08, BR-MED-01..05, ERD `RoomPhotos`

## Context
Listings need 3–20 photos and up to 3 panoramas (≤ 10 MB JPG/PNG). Students browse on mobile data, so the list must load small images and the gallery must load progressively. A 360° viewer is required; MAUI has no built-in panorama control.

## Options considered
| Concern | Options | Choice |
|---|---|---|
| Storage | Azure Blob · Cloudinary free tier · **local disk on the API host behind `IPhotoStoragePort`** | Local disk now (free, zero setup); Blob later = new adapter only |
| Image processing | ImageSharp (split licence) · Magick.NET · **SkiaSharp (MIT, already in the MAUI ecosystem)** | SkiaSharp |
| Upload flow | Multipart with the whole listing · **upload each photo first, then create listing with `photoIds`** | Per-photo upload: progress per photo, retry one photo, form stays responsive |
| Client downscale | none · **`Microsoft.Maui.Graphics` `IImage.Downsize` to 2560 px (standard photos only)** | Saves upload time on 4G; panoramas uploaded as-is |
| 360° viewer | Native SDKs · **Pannellum (MIT, JS) bundled in `Resources/Raw`, hosted in `HybridWebView`** | Works offline, gyro + drag, no new NuGet |

## Decision
- `POST /api/landlord/photos` (multipart, one file) → validate magic bytes + size + panorama ratio → strip EXIF → write `original`, `medium` (1280 px), `thumb` (400 px) JPEG → return `PhotoUploadDto { id, thumbUrl, mediumUrl, kind }`. Unattached photos deleted after 24 h by a background job.
- Files served by the API at `/media/{path}` with `Cache-Control: public, max-age=31536000, immutable` (paths contain the photo id → never change).
- DTOs return absolute URLs for `thumb`/`medium`/`original`; the client shows `thumb` in lists, `thumb → medium` in the gallery, `original` only for panoramas.
- `Pages/Controls/PanoramaViewer` wraps `HybridWebView` + bundled Pannellum.

## Consequences
- Positive: free; fast lists (≤ 60 KB thumbs); progressive gallery; storage swappable.
- Negative: local disk ties media to one API host (acceptable, ADR-013); backup needed with the DB.

## Compliance
Upload tests with real JPEG/PNG headers and a fake extension (GUARDRAILS 1.5.5); size limit tests; EXIF-stripped assertion.

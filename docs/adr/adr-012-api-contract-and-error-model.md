# ADR-012: Design-first OpenAPI, ProblemDetails + errorCode, offset paging, ETag concurrency

- **Status:** Accepted
- **Date:** 2026-11-11
- **Related:** [api/openapi.yaml](../api/openapi.yaml), ADR-003, GUARDRAILS 1.5.1–1.5.6

## Context
Five developers build client and server in parallel; two PRs already diverged on DTO names. We need one contract that both sides follow, error responses the app can translate into Vietnamese messages, and simple paging/concurrency rules.

## Decision
1. **Design-first:** `docs/api/openapi.yaml` (OpenAPI 3.1) is the contract. A story's API changes are made in the YAML **in the same PR** as the code. The server exposes its generated document via built-in `Microsoft.AspNetCore.OpenApi` and **Scalar** UI (`Scalar.AspNetCore`, MIT) in Development for manual testing.
2. **Naming:** paths kebab-case plural nouns under `/api`; user-scoped resources under `/api/me/...`; landlord under `/api/landlord/...`; admin under `/api/admin/...`; state transitions as `POST …/{id}/<verb>` (e.g. `/unpublish`). JSON camelCase; enums as strings; dates ISO-8601 UTC; money integers in VND. `operationId` = camelCase verb-noun, matching [use-cases.md](../use-cases.md) traceability.
3. **Errors:** RFC 9457 `application/problem+json` with extension members `errorCode` (stable UPPER_SNAKE code, e.g. `LISTING_NOT_AVAILABLE`) and `errors` (field → messages) for validation. The client maps `errorCode` → Vietnamese text; never shows raw `detail`. Status mapping: 400 validation · 401 unauthenticated · 403 forbidden/locked/not approved · 404 not found **or not owned** (anti-IDOR: don't reveal existence) · 409 conflict/invalid state/duplicate · 412 `If-Match` mismatch · 413 file too large · 415 bad media type · 429 rate limited.
4. **Paging:** offset paging with the existing `PagedResult<T>` (`items, totalCount, pageNumber, pageSize`) for lists sorted by computed distance (≤ thousands of rows; total count required by US04 AC3). Keyset (`before` cursor) only for chat messages.
5. **Concurrency:** GET of a mutable resource returns `ETag: "<version>"`; PUT/state transitions on landlord/admin resources require `If-Match` (missing → 428, mismatch → 412). Repositories still throw `ConcurrencyConflictException` (409) for races detected in SQL.
6. **Request DTO whitelisting** (GUARDRAILS 1.5.1): request schemas never contain `status`, `isVerified`, `createdAtUtc`, `landlordId`, `role` (except register's requested public role).

## Consequences
- Positive: client and server teams code against the same YAML; AI agents get exact shapes.
- Negative: YAML must be kept in sync (review checklist item).
- Follow-up: extend `AuthExceptionHandlingMiddleware` into a global `ProblemDetailsMiddleware` that adds `errorCode`; add `EntityNotFoundException`, `InvalidStateTransitionException`.

## Compliance
PR checklist: "openapi.yaml updated"; controller tests assert status codes and `errorCode`.

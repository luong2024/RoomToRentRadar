# US13 BACKEND MINI-SPEC & IMPLEMENTATION PLAN
> **User Story (US13):** As a user (student or landlord), I want to register, sign in, and have role-based access control so that my account is secure and I can access features appropriate to my role.  
> **Story Points:** 5 | **Backend Tasks:** T13.1 (6h), T13.2 (5h)  
> **Author Role:** `@Lead` (01. Lead Orchestrator)

---

## 1. LOCKED BUSINESS RULES & ARCHITECTURAL DECISIONS

1. **BR-AUTH-01 (Unique Normalized Email):**
   - Emails are trimmed and normalized to lowercase (`ToLowerInvariant()`).
   - Registering with an already existing email throws `DuplicateEmailException` (`409 Conflict`).
2. **BR-AUTH-02 (Public Self-Registration Role Restriction):**
   - Public registration (`POST /api/auth/register`) only permits `Student` and `Landlord` roles.
   - Any attempt to self-register as `Admin` throws `ForbiddenRoleException` (`403 Forbidden`).
3. **BR-AUTH-03 (Landlord Phone Requirement & Admin Approval Gate):**
   - Registering with role `Landlord` requires a valid Vietnamese phone number (`^(0|\+84)[0-9]{9}$`) to satisfy US02 AC4.
   - Newly registered `Landlord` accounts are created with `IsVerified = false` (pending Admin approval).
   - **Admin Approval Gate:** A `Landlord` with `IsVerified == false` can sign in and view their profile, but **CANNOT** publish room listings (`POST /api/rooms`) until an `Admin` approves their account (`PATCH /api/auth/admin/landlords/{userId}/approve` $\rightarrow$ `IsVerified = true`). Unapproved landlords attempting to create listings receive `403 Forbidden` (`LandlordNotApprovedException`).
   - `Student` accounts (registered via email/password or ICTU SSO) and `Admin` accounts have `IsVerified = true`.
4. **BR-AUTH-04 (ICTU Student SSO — `@ictu.edu.vn`):**
   - Supports Google/Microsoft OIDC `id_token` restricted strictly to the `@ictu.edu.vn` domain via `IIctuSsoVerifierPort`, with `OidcIctuSsoVerifierAdapter` (Production/Staging) and `MockIctuSsoVerifierAdapter` (Dev/Test).
   - Automatically provisions or links a `Student` account with `IsVerified = true`.
5. **BR-AUTH-05 (Salted One-Way Password Hashing & Lockout — AC4):**
   - Passwords must be 8–128 characters and include at least one uppercase letter, one lowercase letter, and one digit.
   - Passwords are hashed using PBKDF2-HMAC-SHA256 (`100,000` iterations, `16-byte` cryptographic random salt).
   - 5 consecutive failed password attempts lock the account for 15 minutes (`AccountLockedException` $\rightarrow$ `403 Forbidden`).
6. **BR-AUTH-06 (JWT Access Token & Refresh Token Rotation — AC2):**
   - Short-lived JWT access tokens include claims: `sub` (`User.Id`), `email` (`User.Email`), `role` (`Student` | `Landlord` | `Admin`), `is_verified` (`true` | `false`), and `jti`.
   - Refresh tokens are persisted as SHA-256 hashes (`RefreshTokenSession`); refreshing rotates the token and revokes the consumed token. Reusing a revoked token revokes all active sessions for that user.
7. **BR-AUTH-07 (Protected Actions & Phone Masking — AC3 & US02 AC4):**
   - Publishing listings requires `VerifiedLandlordPolicy` (`Admin`, or `Landlord` with `is_verified == "true"`).
   - Saving/syncing favourites requires `StudentPolicy` (`Student` or `Admin`).
   - Viewing landlord phone numbers (`LandlordPhoneNumber` in `RoomDetailDto`) requires an authenticated user; anonymous guests receive `LandlordPhoneNumber = null`.

---

## 2. MINI-SPEC CONTRACT TABLE (LOCKED TYPE SIGNATURES)

### 2.1. Shared DTOs (`RoomToRentRadar.Ports/Contracts/`)
- **Namespace `RoomToRentRadar.Ports.Contracts.Requests.Auth`:**
  - `public record RegisterUserRequest(string Email, string Password, string FullName, string Role, string? PhoneNumber = null, string? StudentCode = null);`
  - `public record LoginRequest(string Email, string Password);`
  - `public record IctuSsoLoginRequest(string IdToken, string? DeviceInfo = null);`
  - `public record RefreshTokenRequest(string RefreshToken);`
  - `public record RevokeTokenRequest(string RefreshToken);`
  - `public record ApproveLandlordRequest(string LandlordUserId);`
- **Namespace `RoomToRentRadar.Ports.Contracts.Responses.Auth`:**
  - `public record UserProfileDto(string Id, string Email, string FullName, string Role, string AuthProvider, string? PhoneNumber, string? StudentCode, bool IsVerified, DateTime CreatedAtUtc);`
  - `public record AuthTokensDto(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc, DateTime RefreshTokenExpiresAtUtc, UserProfileDto User);`
  - `public record IctuSsoUserPayload(string Email, string FullName, string SubjectId, string? StudentCode = null);`
  - `public record GeneratedAccessToken(string Token, string JwtId, DateTime ExpiresAtUtc);`

### 2.2. Domain Layer (`RoomToRentRadar.Domain/`)
- `RoomToRentRadar.Domain.Enums.UserRole`: `Student = 1`, `Landlord = 2`, `Admin = 3`
- `RoomToRentRadar.Domain.Enums.AuthProvider`: `Local = 1`, `IctuSso = 2`
- `RoomToRentRadar.Domain.ValueObjects.EmailAddress`
- `RoomToRentRadar.Domain.Entities.RefreshTokenSession`
- `RoomToRentRadar.Domain.Aggregates.User`
- `RoomToRentRadar.Domain.Exceptions`:
  - `DomainValidationException`
  - `DuplicateEmailException`
  - `InvalidCredentialsException`
  - `AccountLockedException`
  - `ForbiddenRoleException`
  - `InvalidTokenException`
  - `LandlordNotApprovedException`
  - `UserNotFoundException`

### 2.3. Input Ports & Use Cases (`RoomToRentRadar.Ports/Inbound/UseCases/Auth/` & `RoomToRentRadar.Application/UseCases/Auth/`)
- `IRegisterUserUseCase` $\rightarrow$ `RegisterUserUseCase`
- `ILoginUserUseCase` $\rightarrow$ `LoginUserUseCase`
- `ILoginWithIctuSsoUseCase` $\rightarrow$ `LoginWithIctuSsoUseCase`
- `IRefreshTokenUseCase` $\rightarrow$ `RefreshTokenUseCase`
- `IRevokeTokenUseCase` $\rightarrow$ `RevokeTokenUseCase`
- `IGetCurrentUserProfileUseCase` $\rightarrow$ `GetCurrentUserProfileUseCase`
- `IApproveLandlordUseCase` $\rightarrow$ `ApproveLandlordUseCase`
- Mapper: `RoomToRentRadar.Application.Mappers.UserMapper`

### 2.4. Output Ports (`RoomToRentRadar.Ports/Outbound/`)
- `RoomToRentRadar.Ports.Outbound.Repositories.IUserRepository`
- `RoomToRentRadar.Ports.Outbound.ExternalServices.IPasswordHasherPort`
- `RoomToRentRadar.Ports.Outbound.ExternalServices.IJwtTokenServicePort`
- `RoomToRentRadar.Ports.Outbound.ExternalServices.IIctuSsoVerifierPort`
- `RoomToRentRadar.Ports.Outbound.ExternalServices.ISecurityAuditLoggerPort`

### 2.5. Driven Adapters & Driving API (`RoomToRentRadar.Datastore/`, `RoomToRentRadar.Infrastructure/`, `RoomToRentRadar.Api/`)
- **Datastore (`RoomToRentRadar.Datastore.CosmosDb.*`):**
  - `UserDocument`, `RefreshTokenDocument`, `CosmosDbSettings`, `CosmosDbContext`
- **Infrastructure (`RoomToRentRadar.Infrastructure.*`):**
  - `CosmosUserRepository`, `InMemoryUserRepository`
  - `Pbkdf2PasswordHasherAdapter`
  - `JwtTokenServiceAdapter`
  - `OidcIctuSsoVerifierAdapter`, `MockIctuSsoVerifierAdapter`
  - `SecurityAuditLoggerAdapter`
- **API (`RoomToRentRadar.Api.*`):**
  - `Controllers/AuthController.cs`
  - `Middlewares/AuthExceptionHandlingMiddleware.cs`
  - `Authorization/AuthorizationPolicies.cs` (`StudentPolicy`, `LandlordPolicy`, `VerifiedLandlordPolicy`, `AdminPolicy`)

---

## 3. PHASED TASK SEQUENCE

- **Stage 0 (`@Lead`):** Lock Mini-Spec in `.agents/plans/US13_BACKEND_PLAN.md` and register approved NuGet packages (`Microsoft.AspNetCore.Authentication.JwtBearer` in `RoomToRentRadar.Api`, `Microsoft.Azure.Cosmos` in `RoomToRentRadar.Datastore`).
- **Stage 1 (`@Core` — Phase 0 TDD):** Author Domain signatures, Ports, DTOs, and UseCase stubs (`NotImplementedException`). Verify `0` build errors.
- **Stage 2 (`@QA` — Phase 1 TDD RED):** Author NUnit + NSubstitute tests in `RoomToRentRadarUnitTests` covering AC1–AC4 and Landlord approval rules. Verify compilation and expected RED failures.
- **Stage 3 (`@Core` — Phase 2 TDD GREEN Core):** Implement `User` aggregate, `EmailAddress`, `RefreshTokenSession`, `UserMapper`, and all 7 Auth UseCases. Verify all Core unit tests turn GREEN.
- **Stage 4 (`@Infra` + `@QA` — Phase 2 TDD GREEN Infra):** Implement Cosmos DB persistence, PBKDF2 password hashing, JWT service, OIDC/Mock ICTU SSO verifier, and security audit logger + Infrastructure unit tests.
- **Stage 5 (`@Infra` + `@QA` — API, Middleware & RBAC):** Implement `AuthController`, `AuthExceptionHandlingMiddleware`, `AuthorizationPolicies`, configure `Program.cs`, and run full verification suite.


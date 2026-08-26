# Story 06 - Identity Auth Foundation

## Prerequisites

- Story 05 completed: ../customer-management/05-story-agent-customer-management-ui.md.
- Coordinate with owners of customer-management API contracts before replacing header-based auth.

---

## Story Goal

Establish ASP.NET Core Identity as the system authentication source, seed baseline roles (Admin, Agent, Customer), and provide JWT + refresh token authentication APIs for SPA and gateway usage.

User-visible outcomes:

1. Users can register (Agent or Customer), login, refresh, and logout through stable auth endpoints.
2. Tokens contain role and permission claims for downstream authorization.
3. Admin role is seeded but never publicly self-registered.

Out of scope:

- Admin user management UI workflows.
- Retrofitting all existing customer-management policies to permission-based checks.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - read ~lines 1-78. Current auth is wired through `AddAuthentication(...)`, `AddAuthorization(...)`, and `AgentOnly` policy.
2. `src/CustomerManagement.Api/Infrastructure/Auth/HeaderAuthenticationDefaults.cs` - read ~lines 1-6. Current header auth contract defines `X-User-Role`.
3. `src/CustomerManagement.Api/Infrastructure/Auth/HeaderAuthenticationHandler.cs` - read ~lines 1-38. Current runtime identity is synthetic and header-driven.
4. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 1-136. Persistence style and naming conventions (`*AtUtc`, fluent config, table naming) must be followed.
5. `src/CustomerManagement.Api/Domain/Customers/Customer.cs` - read ~lines 1-24. Existing optimistic concurrency pattern uses `RowVersion` with `[Timestamp]`.
6. `tests/CustomerManagement.Api.Tests/Infrastructure/CustomerManagementApiFactory.cs` - read ~lines 1-45. Integration test factory pattern for replacing DB/services.
7. `src/CustomerManagement.Gateway/Program.cs` - read ~lines 1-37. Gateway middleware order and CORS placement.
8. `src/CustomerManagement.Gateway/ocelot.json` - read ~lines 1-137. Existing routing and forwarded headers pattern.
9. Precedent: `../customer-management/04-story-customer-management-gateway-routing.md` - mirror task granularity and route-contract discipline.
10. Intake source: `.squad/stories/security-administration/security-administration/intake.md` - read full file; attachments folder exists but is empty.

---

## Product rules (from story)

- Public registration allows only Agent or Customer role selection.
- Admin accounts are bootstrap/admin-created only.
- Access tokens are short-lived; refresh tokens are longer-lived and revocable.

---

## Backend Tasks

### 1. Add Identity persistence foundation

File: `src/CustomerManagement.Api/Domain/Security/ApplicationUser.cs`

- Create identity user entity inheriting `IdentityUser<Guid>`.
- Add fields:
  - `DisplayName: string` (required, max 200)
  - `IsActive: bool` (required, default true)
  - `CreatedAtUtc: DateTime` (required)
  - `UpdatedAtUtc: DateTime` (required)
  - `DeactivatedAtUtc: DateTime?`
  - `RowVersion: byte[]` with `[Timestamp]`

File: `src/CustomerManagement.Api/Domain/Security/RefreshToken.cs`

- Create entity with fields:
  - `Id: Guid`
  - `UserId: Guid`
  - `TokenHash: string` (required, max 512)
  - `ExpiresAtUtc: DateTime` (required)
  - `CreatedAtUtc: DateTime` (required)
  - `RevokedAtUtc: DateTime?`
  - `ReplacedByTokenHash: string?` (max 512)
  - `CreatedByIp: string?` (max 64)

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Convert DbContext to inherit `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`.
- Keep existing customer-management DbSets and add:
  - `DbSet<RefreshToken> RefreshTokens`.
- Configure `ApplicationUser` and `RefreshToken` in `OnModelCreating` using current fluent style.

File: `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/*`

- Add migration for Identity + refresh tokens.
- Ensure role/user tables use Guid keys and include rowversion on editable entities.

### 2. Add auth contracts and endpoint module

Create file: `src/CustomerManagement.Api/Contracts/Auth/RegisterRequest.cs`

```csharp
public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string DisplayName,
    string AccountType);
```

Create file: `src/CustomerManagement.Api/Contracts/Auth/LoginRequest.cs`

```csharp
public sealed record LoginRequest(string Email, string Password);
```

Create file: `src/CustomerManagement.Api/Contracts/Auth/TokenResponse.cs`

```csharp
public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string UserId,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
```

Create file: `src/CustomerManagement.Api/Endpoints/Auth/AuthEndpoints.cs`

- Add route group `/api/auth`.
- Implement endpoints:
  - `POST /api/auth/register`
    - 201 Created: `TokenResponse`
    - 400 BadRequest: validation problem
    - 409 Conflict: duplicate email
  - `POST /api/auth/login`
    - 200 OK: `TokenResponse`
    - 401 Unauthorized: invalid credentials or inactive user
  - `POST /api/auth/refresh`
    - request: `{ refreshToken: string }`
    - 200 OK: `TokenResponse`
    - 401 Unauthorized: invalid/expired/revoked refresh token
  - `POST /api/auth/logout`
    - request: `{ refreshToken: string }`
    - 204 NoContent

### 3. Configure JWT authentication and baseline role seeding

File: `src/CustomerManagement.Api/Program.cs`

- Replace header auth registration with JWT bearer auth.
- Keep authorization policy registration but map to role/permission claims.
- Add startup role seeding for Admin, Agent, Customer roles.
- Register auth endpoint mapping `app.MapAuthEndpoints()`.

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/JwtOptions.cs`

- Add strongly typed settings:
  - `Issuer`, `Audience`, `SigningKey`, `AccessTokenMinutes`, `RefreshTokenDays`.

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/JwtTokenService.cs`

- Generate JWT with claims:
  - `sub`, `email`, `role` (multiple), `permission` (multiple), `jti`.
- Persist and rotate refresh tokens using hashed values.

---

## Edge Cases & Failure Modes

- Registration account type is `admin` or unknown: reject with 400 and explicit allowed values (`agent`, `customer`) in `AuthEndpoints` validation path.
- Duplicate email registration: return 409 conflict from Identity create path in `AuthEndpoints`.
- Login success for deactivated user: block with 401 after checking `ApplicationUser.IsActive`.
- Refresh token replay/reuse: revoke token chain and reject with 401 in `JwtTokenService`.
- Access token expires but refresh valid: refresh endpoint returns new token pair and revokes prior refresh token.
- Startup role seeding fails on partial migration state: fail fast at startup with clear log and stop app.

---

## Test Plan

1. Integration tests: create `tests/CustomerManagement.Api.Tests/AuthEndpointsTests.cs`.
   - `Register_ReturnsCreated_ForAgent`
   - `Register_RejectsAdminAccountType`
   - `Login_ReturnsUnauthorized_ForInvalidPassword`
   - `Refresh_ReturnsUnauthorized_ForRevokedToken`
2. Integration tests: token claims include role and permissions.
3. Integration tests: deactivated user login denied.
4. Add/update gateway test baseline to include `/api/auth/*` passthrough once routes are added.

---

## Migration / Rollback

- Migration adds Identity core tables and refresh token table; rollback requires reversing migration before applying dependent stories.
- Half-applied risk: app starts with missing role tables; startup seed must fail clearly to avoid partial auth behavior.

---

## Verification Steps

1. **Backend builds:** from repo root run `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** from repo root run `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~AuthEndpointsTests"`.
3. **Regression:** from repo root run `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] ASP.NET Core Identity is the active authentication store with Guid keys.
- [x] Roles Admin, Agent, Customer are seeded and reusable.
- [x] Public registration supports Agent/Customer only.
- [x] JWT + refresh token flow is implemented with revocation.
- [x] Auth endpoints return documented DTOs and status codes.
- [x] Integration tests cover happy path and failure modes.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**

# Story 10 - System Settings API And Policy Enforcement

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Story 08 completed: `08-story-permission-model-and-policy-administration.md`.
- Story 09 completed: `09-story-audit-log-capture-and-query.md` for change auditing.

---

## Story Goal

Add system configuration storage and admin APIs for security-related settings (password policy and token/session expiry) with optimistic concurrency and runtime-safe application behavior.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 19-136 for entity mapping style and index conventions.
2. `src/CustomerManagement.Api/Domain/Customers/Customer.cs` - read ~lines 14-19 for rowversion pattern to mirror on settings entity.
3. `src/CustomerManagement.Api/Program.cs` - read ~lines 11-29 for options registration and policy setup patterns.
4. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` - read ~lines 18-112 for HTTP service conventions and error mapping style.
5. `tests/CustomerManagement.Api.Tests/CustomerProfileEndpointsTests.cs` - use request/response and conflict test style as precedent.

---

## Backend Tasks

### 1. Add settings entity and migration

Create file: `src/CustomerManagement.Api/Domain/Security/SystemSetting.cs`

- Fields:
  - `Id: Guid`
  - `Key: string` required max 100 unique
  - `Value: string` required max 2000
  - `Description: string?` max 500
  - `UpdatedByUserId: Guid?`
  - `UpdatedAtUtc: DateTime`
  - `RowVersion: byte[]` with `[Timestamp]`

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<SystemSetting> SystemSettings`.
- Mapping includes unique index on `Key` and rowversion.

Create migration file in `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations`.

### 2. Add settings contracts and endpoints

Create file: `src/CustomerManagement.Api/Contracts/Admin/Settings/SystemSettingResponse.cs`

```csharp
public sealed record SystemSettingResponse(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
```

Create file: `src/CustomerManagement.Api/Contracts/Admin/Settings/UpdateSystemSettingRequest.cs`

```csharp
public sealed record UpdateSystemSettingRequest(
    string Value,
    byte[] RowVersion);
```

Create file: `src/CustomerManagement.Api/Endpoints/Admin/SystemSettingsEndpoints.cs`

- Route group: `/api/admin/system-settings`
- Required permission: `settings.manage`
- Endpoints:
  - `GET /api/admin/system-settings`
  - `GET /api/admin/system-settings/{key}`
  - `PUT /api/admin/system-settings/{key}`
- Status behavior:
  - 200 success
  - 400 validation
  - 404 missing key
  - 409 stale rowversion

### 3. Apply settings to auth runtime behavior

File: `src/CustomerManagement.Api/Infrastructure/Auth/JwtTokenService.cs`

- Read settings keys:
  - `Auth.AccessTokenMinutes`
  - `Auth.RefreshTokenDays`
- Apply bounded fallback defaults when settings missing.

File: `src/CustomerManagement.Api/Endpoints/Auth/AuthEndpoints.cs`

- Enforce password constraints from `Auth.Password.MinLength` setting (with startup defaults).

### 4. Audit settings changes

File: `src/CustomerManagement.Api/Endpoints/Admin/SystemSettingsEndpoints.cs`

- Emit audit entry for each setting update (who, key, old/new value metadata).

---

## Edge Cases & Failure Modes

- Missing required seed settings rows on first run: initialize defaults in startup seeding routine.
- Invalid numeric setting value (non-int for token minutes): reject update with 400 and preserve prior value.
- Setting update with stale rowversion: return 409 conflict.
- Sensitive values accidentally exposed: keep scope limited to non-secret settings in this story; do not store signing keys in `SystemSettings`.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/SystemSettingsEndpointsTests.cs`.
2. Add tests for list/get/update and rowversion conflict.
3. Add tests for invalid setting value format rejection.
4. Add tests confirming JWT expiry values track settings updates.

---

## Migration / Rollback

- Migration adds new `SystemSettings` table and seeded rows.
- Rollback requires removing dependent code paths that read dynamic settings to avoid null lookups.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~SystemSettings"`.
3. **Regression:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] System settings persistence includes rowversion and unique keys.
- [x] Admin settings API supports safe read/update operations.
- [x] Auth runtime consumes configured token/password settings.
- [x] Settings changes are audit logged.
- [x] Tests validate conflicts, validation, and behavior updates.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 11.**

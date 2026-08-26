# Story 08 - Permission Model And Policy Administration

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Story 07 completed: `07-story-user-lifecycle-and-role-assignment.md`.

---

## Story Goal

Model permissions independently from roles and provide administrator APIs to manage role-permission mapping, with policy-based authorization checking by permission name.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - read ~lines 19-29 for current role policy registration location.
2. `src/CustomerManagement.Api/Infrastructure/Auth/HeaderAuthenticationHandler.cs` - read ~lines 23-31 to understand current claim injection pattern to be replaced by JWT claims.
3. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 19-47 and mapping blocks for table/index conventions.
4. `src/CustomerManagement.Gateway/ocelot.json` - read ~lines 1-137 for claim-forwarding and route concerns used by downstream permission policies.
5. `tests/CustomerManagement.Api.Tests/CustomerProfileEndpointsTests.cs` - reuse authorization negative test pattern for forbidden/unauthorized checks.
6. Precedent story: `../customer-management/02-story-customer-notes-and-attachments.md` for modular endpoint + tests decomposition.

---

## Implementation tasks

### 1. Add permission entities and schema

Create file: `src/CustomerManagement.Api/Domain/Security/Permission.cs`

- Fields:
  - `Id: Guid`
  - `Name: string` required max 200 unique (example `customers.read`)
  - `Description: string?` max 500
  - `CreatedAtUtc: DateTime`

Create file: `src/CustomerManagement.Api/Domain/Security/RolePermission.cs`

- Fields:
  - `RoleId: Guid`
  - `PermissionId: Guid`
  - `CreatedAtUtc: DateTime`
- Composite primary key on `(RoleId, PermissionId)`.

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add DbSets and mappings.
- Add unique index for permission `Name`.

### 2. Add permission-policy infrastructure

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Declare central constants (examples):
  - `UsersManage`, `RolesManage`, `PermissionsManage`, `AuditRead`, `SettingsManage`, `CustomersRead`, `CustomersWrite`.

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/PermissionRequirement.cs`

```csharp
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
```

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/PermissionAuthorizationHandler.cs`

- Validate `permission` claims against required permission.

Create file: `src/CustomerManagement.Api/Infrastructure/Auth/PermissionPolicyProvider.cs`

- Resolve dynamic policies prefixed `Permission:`.
- Policy name example: `Permission:customers.read`.

File: `src/CustomerManagement.Api/Program.cs`

- Register policy provider and handler.
- Keep static policies for role-based admin bootstrap but move feature endpoints toward permission policies.

### 3. Add permission admin endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Admin/PermissionsEndpoints.cs`

- Route group: `/api/admin/permissions`
- Endpoints:
  - `GET /api/admin/permissions`
  - `POST /api/admin/permissions`
  - `PUT /api/admin/permissions/{permissionId:guid}`
  - `GET /api/admin/roles/{roleId:guid}/permissions`
  - `PUT /api/admin/roles/{roleId:guid}/permissions`
- Status behavior:
  - 200/201 success.
  - 400 validation (invalid names, empty assignments).
  - 404 missing role/permission.
  - 409 duplicate permission name.

### 4. Apply permission checks to selected existing routes

File: `src/CustomerManagement.Api/Endpoints/Customers/*.cs`

- Replace `RequireAuthorization("AgentOnly")` with permissions:
  - read endpoints use `Permission:customers.read`.
  - mutating endpoints use `Permission:customers.write`.
- Keep compatibility note in comments for phased migration if needed.

---

## Edge Cases & Failure Modes

- Permission renamed while active in role assignments: reject rename if it breaks protected policy mapping.
- Duplicate permission name with different casing: normalize and enforce case-insensitive uniqueness.
- User has role but missing permission claim due stale token: endpoint returns 403; refresh/login required.
- Permission removed from role while user session active: access remains until token refresh unless token revocation strategy is applied.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/AdminPermissionsEndpointsTests.cs`.
2. Add authorization tests for customer endpoints enforcing permission-based policies.
3. Add policy-provider tests in `tests/CustomerManagement.Api.Tests/Infrastructure/Auth/PermissionPolicyProviderTests.cs`.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~Permissions"`.
3. **Regression:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] Permissions are first-class persistence objects separate from roles.
- [x] Dynamic permission policy resolution is implemented and wired.
- [x] Admin APIs support permission CRUD and role-permission assignment.
- [x] Selected customer-management endpoints enforce permission policies.
- [x] Tests validate policy resolution, assignment behavior, and forbidden outcomes.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09.**

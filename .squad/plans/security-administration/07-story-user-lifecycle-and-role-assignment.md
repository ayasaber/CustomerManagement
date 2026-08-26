# Story 07 - User Lifecycle And Role Assignment

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Admin authorization policy from Story 06 available and tested.

---

## Story Goal

Provide administrator APIs for user lifecycle management (create, view, edit, deactivate) and role assignment updates with optimistic concurrency.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - read ~lines 19-29 and 41-47 for policy and endpoint registration pattern.
2. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 19-47 and ~lines 48-136 for entity mapping patterns, index style, and rowversion usage.
3. `src/CustomerManagement.Api/Domain/Customers/Customer.cs` - read ~lines 14-19 for current `[Timestamp] RowVersion` precedent.
4. `src/CustomerManagement.Ui/src/app/features/customer-management/customer-management-page.component.ts` - read ~lines 64-143 for current form-driven CRUD interaction style.
5. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` - read ~lines 18-112 for current service and error mapping conventions.
6. `tests/CustomerManagement.Api.Tests/Infrastructure/CustomerManagementApiFactory.cs` - read ~lines 17-36 for test host configuration style.
7. `../customer-management/01-story-customer-profile-and-contact-foundation.md` - use API and validation granularity precedent.

---

## Implementation tasks

### 1. Add admin user contracts

Create file: `src/CustomerManagement.Api/Contracts/Admin/Users/AdminUserResponse.cs`

```csharp
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<string> Roles);
```

Create file: `src/CustomerManagement.Api/Contracts/Admin/Users/CreateAdminUserRequest.cs`

```csharp
public sealed record CreateAdminUserRequest(
    string Email,
    string Password,
    string DisplayName,
    IReadOnlyList<string> Roles);
```

Create file: `src/CustomerManagement.Api/Contracts/Admin/Users/UpdateAdminUserRequest.cs`

```csharp
public sealed record UpdateAdminUserRequest(
    string DisplayName,
    bool IsActive,
    byte[] RowVersion);
```

Create file: `src/CustomerManagement.Api/Contracts/Admin/Users/UpdateUserRolesRequest.cs`

```csharp
public sealed record UpdateUserRolesRequest(IReadOnlyList<string> Roles, byte[] RowVersion);
```

### 2. Add admin users endpoint module

Create file: `src/CustomerManagement.Api/Endpoints/Admin/UsersEndpoints.cs`

- Route group: `/api/admin/users`
- Require policy: `AdminOnly`
- Endpoints:
  - `GET /api/admin/users?page=1&pageSize=50&isActive=true|false&role=agent`
    - 200: paged list of `AdminUserResponse`
  - `GET /api/admin/users/{userId:guid}`
    - 200: `AdminUserResponse`
    - 404: user not found
  - `POST /api/admin/users`
    - 201: `AdminUserResponse`
    - 400: validation errors
    - 409: duplicate email
  - `PUT /api/admin/users/{userId:guid}`
    - 200: `AdminUserResponse`
    - 404: not found
    - 409: stale rowversion
  - `PUT /api/admin/users/{userId:guid}/roles`
    - 200: `AdminUserResponse`
    - 400: invalid role names
    - 404: user not found
    - 409: stale rowversion
  - `DELETE /api/admin/users/{userId:guid}` (soft deactivate)
    - 204 no content
    - 404 not found

### 3. Add admin authorization policy

File: `src/CustomerManagement.Api/Program.cs`

- Add `AdminOnly` policy requiring `role=admin` or permission `admin.users.manage`.
- Register `app.MapUsersEndpoints()`.

### 4. Emit role-change audit events hook

File: `src/CustomerManagement.Api/Endpoints/Admin/UsersEndpoints.cs`

- On role changes and deactivation, emit audit write calls through an abstraction (implemented fully in Story 09).
- Until Story 09 completes, write placeholder structured log statements with user id, actor id, and UTC timestamp.

---

## Edge Cases & Failure Modes

- User updated with stale `RowVersion`: return 409 conflict.
- Role assignment includes unknown role: return 400 with per-role validation entry.
- Admin deactivates own account: reject with 400 to avoid lockout.
- Deactivated user appears in active-only queries: ensure `isActive` filter defaults true.
- Concurrent role update and profile update: first writer wins; second receives 409.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/AdminUsersEndpointsTests.cs`.
2. Add tests:
   - create user success and duplicate email conflict.
   - list pagination + role filter.
   - update stale rowversion conflict.
   - role update with invalid role names.
   - soft deactivate and login denial.
3. Ensure existing customer-management tests still pass.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~AdminUsersEndpointsTests"`.
3. **Regression:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] Admin user CRUD + deactivate APIs exist with documented status behavior.
- [x] Role assignment APIs support one-or-more role membership.
- [x] RowVersion conflict handling is enforced on editable user operations.
- [x] Admin-only authorization is enforced.
- [x] Endpoint tests cover success and failure paths.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 08.**

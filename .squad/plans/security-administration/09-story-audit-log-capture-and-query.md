# Story 09 - Audit Log Capture And Query

## Prerequisites

- Story 06 completed: `06-story-identity-auth-foundation.md`.
- Story 07 completed: `07-story-user-lifecycle-and-role-assignment.md`.
- Story 08 completed: `08-story-permission-model-and-policy-administration.md`.

---

## Story Goal

Capture security and high-impact domain actions in a centralized audit log and provide administrator query APIs with filtering and pagination.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - read ~lines 41-47 for mapped endpoint modules where action capture is needed.
2. `src/CustomerManagement.Api/Endpoints/Customers/CustomerProfileEndpoints.cs` - read endpoint write actions to identify deletion/update event hooks.
3. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 19-136 for schema mapping conventions.
4. `tests/CustomerManagement.Api.Tests/CustomerProfileEndpointsTests.cs` - use integration test style for endpoint behavior and auth outcomes.
5. `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` - read ~lines 88-123 for negative propagation patterns relevant to audit-query errors.
6. Precedent: `../customer-management/03-story-customer-interaction-history-read-model.md` for read model filtering and pagination semantics.

---

## Backend Tasks

### 1. Add audit log persistence model

Create file: `src/CustomerManagement.Api/Domain/Security/AuditLogEntry.cs`

- Fields:
  - `Id: Guid`
  - `OccurredAtUtc: DateTime` required
  - `ActorUserId: Guid?`
  - `ActorEmail: string?` max 320
  - `ActionType: string` required max 200
  - `EntityName: string` required max 200
  - `EntityId: string` required max 128
  - `Result: string` required max 50 (Success/Failure)
  - `MetadataJson: string?` max 4000

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<AuditLogEntry> AuditLogEntries`.
- Add indexes:
  - `(OccurredAtUtc DESC)`
  - `(ActorUserId, OccurredAtUtc)`
  - `(ActionType, OccurredAtUtc)`

### 2. Add audit writer abstraction and implementation

Create file: `src/CustomerManagement.Api/Infrastructure/Auditing/IAuditLogWriter.cs`

```csharp
public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogWriteModel entry, CancellationToken cancellationToken);
}
```

Create file: `src/CustomerManagement.Api/Infrastructure/Auditing/AuditLogWriter.cs`

- Persist `AuditLogEntry` records.
- Enforce UTC timestamps and metadata serialization safeguards.

Create file: `src/CustomerManagement.Api/Infrastructure/Auditing/AuditLogWriteModel.cs`

- Request model used by services/endpoints.

### 3. Capture required security events

File: `src/CustomerManagement.Api/Endpoints/Auth/AuthEndpoints.cs`

- Write audit records for:
  - login success
  - login failure
  - refresh success/failure
  - logout

File: `src/CustomerManagement.Api/Endpoints/Admin/UsersEndpoints.cs`

- Write audit records for role assignment and user deactivation.

File: customer deletion endpoints (when added in future stories)

- Include explicit TODO note for deletion capture in modules not yet implemented.

### 4. Add audit query API

Create file: `src/CustomerManagement.Api/Contracts/Admin/Audit/AuditLogResponse.cs`

- Fields include pagination envelope and items.

Create file: `src/CustomerManagement.Api/Endpoints/Admin/AuditLogEndpoints.cs`

- Route group: `/api/admin/audit-logs`
- Endpoint:
  - `GET /api/admin/audit-logs?userId={guid?}&fromUtc={datetime?}&toUtc={datetime?}&actionType={string?}&page=1&pageSize=50`
- Status behavior:
  - 200 for successful query.
  - 400 for invalid date ranges or pagination values.
  - 403 for missing `audit.read` permission.

---

## Edge Cases & Failure Modes

- Audit write fails during business transaction: do not break primary operation; write fallback error log and continue.
- Invalid date filter (`fromUtc > toUtc`): return 400 with validation problem.
- Large result sets: enforce max `pageSize=200`.
- Unknown action type filter: return empty result, not error.
- Metadata over 4k chars: truncate and include truncation marker.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/AuditLogEndpointsTests.cs`.
2. Add tests for login success/failure producing audit rows.
3. Add filter and pagination tests for audit query.
4. Add forbidden/unauthorized tests for audit endpoints.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~Audit"`.
3. **Regression:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`.

---

## Done Criteria

- [x] Audit entity and persistence mapping are added with required indexes.
- [x] Login attempt, role change, and relevant mutation events are captured.
- [x] Admin audit query API supports filter + pagination contract.
- [x] Audit access is permission-gated.
- [x] Tests verify event capture and query behavior.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 10.**

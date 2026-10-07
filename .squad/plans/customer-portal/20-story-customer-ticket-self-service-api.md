# Story 20 — Customer Ticket Self-Service API Enablement

## Prerequisites

- Story 13 completed: `../ticket-management/13-story-ticket-management.md`.
- Story 19 completed: `../ticket-conversation/19-story-ticket-conversation.md` (introduces `TicketMessage`, `/api/ticket-messages`, and the customer-visible conversation contract this story extends).

---

## Story Goal

Let an authenticated customer, through the existing `/api/tickets` and `/api/ticket-messages` contracts:

1. Submit a new ticket without choosing a priority (the support team decides priority after triage) and optionally attach one or more files at or after submission.
2. Post a reply on one of their own tickets, with the ticket status moving automatically when the reply means the customer is no longer the one the team is waiting on.

Everything else about ticket visibility and ownership (a customer only ever sees/acts on tickets linked to their own `Customer` row) is already enforced by `ShouldApplyCustomerScope` / `ResolveOwnedCustomerIdsAsync` in `TicketEndpoints.cs` and `CanAccessTicketAsync` in `TicketMessagesEndpoints.cs` — this story does not change that scoping, it only widens what a customer is allowed to do inside their own scope.

Out of scope for this story:

- Any UI work (covered by Story 23).
- Gateway routing for the new attachment endpoints (covered by Story 23).
- Letting a customer choose ticket priority or reopen a ticket through `/api/tickets/{ticketId}/reopen` directly — reopening-by-reply is handled entirely inside the message-post endpoint added here.

Product rules decided for this story (resolving the intake's open questions):

- **Reply on a `Resolved` ticket:** allowed, and it automatically transitions the ticket back to `InProgress` (the customer's reply is evidence the issue isn't settled from their side).
- **Reply on a `WaitingOnCustomer` ticket:** allowed, and it automatically transitions the ticket to `InProgress` (the customer has now responded).
- **Reply on a `Closed` ticket:** not allowed. `Closed` stays final; the endpoint returns `400 Bad Request`.
- **Reply on `New` / `InProgress`:** allowed, no status change.
- **Priority on customer-submitted tickets:** the customer never supplies one. The server resolves a default active priority server-side; the support team can change it later through the existing `PUT /api/tickets/{ticketId}` endpoint.

---

## Context — Read These Files First

1. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` — `CreateTicketAsync` (~lines 71–110), `ResolveCreateCustomerIdAsync` (~lines 1123–1160), `ValidateCreateTicketAsync` (starts ~line 812), `CanTransition` (~lines 1004–1018), and the duplicated `IsAdmin`/`IsAgent`/`IsCustomer`/`ResolveActorUserId`/`ResolveActorEmail`/`ShouldApplyCustomerScope`/`ResolveOwnedCustomerIdsAsync` helpers (~lines 1030–1160). Note this file already duplicates these helpers rather than sharing them from `TicketMessagesEndpoints.cs` — follow that same duplication convention for the new `TicketAttachmentEndpoints.cs`.
2. `src/CustomerManagement.Api/Contracts/Tickets/TicketContracts.cs` — `CreateTicketRequest` record (`CustomerUserId`, `CategoryId`, `PriorityId`, `Subject`, `Description`).
3. `src/CustomerManagement.Api/Endpoints/Tickets/TicketMessagesEndpoints.cs` — `CreateAsync` (~lines 137–215, including the `if (IsCustomer(httpContext.User)) return Results.Forbid();` block to remove), `CanAccessTicketAsync` (~lines 284–310), `TicketScopeProjection` usage, and `AddHistory`-equivalent inline `TicketHistoryEntries.Add` call in `CreateAsync`.
4. `src/CustomerManagement.Api/Domain/Tickets/Ticket.cs` — fields `Status`, `PriorityId`, `RowVersion`; `src/CustomerManagement.Api/Domain/Tickets/TicketStatus.cs` — enum values `New=1, InProgress=2, WaitingOnCustomer=3, Resolved=4, Closed=5`.
5. `src/CustomerManagement.Api/Domain/Customers/CustomerAttachment.cs` and `src/CustomerManagement.Api/Endpoints/Customers/CustomerAttachmentEndpoints.cs` (full file) — structural precedent for the new ticket attachment entity/endpoints, including the `Validate` method (~lines 162–199) and the `IAttachmentStorage.SaveAsync` / `DeleteAsync` usage.
6. `src/CustomerManagement.Api/Infrastructure/Attachments/AttachmentStorageOptions.cs` — `MaxFileSizeBytes`, `AllowedContentTypes` used by the existing `Validate` method; reuse the same options for ticket attachments (no new config section).
7. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` — `modelBuilder.Entity<Customer>` (~line 219, for the FK style), `modelBuilder.Entity<TicketCategory>` (~line 352, for a simple entity mapping style), `modelBuilder.Entity<TicketMessage>` (~line 645, for the closest FK-to-Ticket + FK-to-ApplicationUser mapping precedent); the `DbSet<TicketMessage>` and other `DbSet<...>` declarations near the top of the class.
8. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — full constant list; `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` — `baselinePermissions` dictionary and the `assignments` dictionary for `AuthRoles.Customer` (currently `TicketsRead, TicketsWrite, TicketsClose, TicketMessagesRead`), and `EnsureTicketTaxonomyAsync` (~lines 283–350) for the seeded priority names (`Low=10, Normal=20, High=30, Urgent=40`).
9. `src/CustomerManagement.Api/Program.cs` — `app.MapTicketEndpoints();` / `app.MapTicketMessagesEndpoints();` registration order (~lines 95–99), to register `app.MapTicketAttachmentEndpoints();` alongside them.
10. Precedent plan for tone/structure: `../ticket-conversation/19-story-ticket-conversation.md`.

---

## Backend Tasks

### 1. Add `TicketAttachment` domain entity

Create file: `src/CustomerManagement.Api/Domain/Tickets/TicketAttachment.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Tickets;

public sealed class TicketAttachment
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public string UploadedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
```

### 2. Add persistence mapping and migration

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();` next to the existing `DbSet<TicketMessage>` declaration.
- Add a mapping block modeled on `modelBuilder.Entity<TicketCategory>` (~line 352) and the FK style in `modelBuilder.Entity<TicketMessage>` (~line 645):
  - `ToTable("TicketAttachments")`
  - PK `Id`
  - `OriginalFileName` max 260, required
  - `ContentType` max 200, required
  - `SizeBytes` required
  - `StorageKey` max 400, required
  - `UploadedByUserId` required
  - `UploadedByDisplayName` max 200, required
  - `CreatedAtUtc` required
  - FK `TicketId -> Tickets(Id)` with `DeleteBehavior.Cascade`
  - Index on `(TicketId, CreatedAtUtc)`

Create a migration under `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/` for the new `TicketAttachments` table.

### 3. Add permissions

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- No new permission constants are needed: ticket attachment endpoints reuse `Permissions.TicketsRead` / `Permissions.TicketsWrite`, which `Customer` already has.

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Update the `[Permissions.TicketMessagesRead]` entry and the `[AuthRoles.Customer]` assignment list so `Customer` also includes `Permissions.TicketMessagesWrite` (it is already defined and described; only the `Customer` role assignment array changes).
- Update the description for `Permissions.TicketMessagesWrite` from `"Post customer-visible ticket conversation messages."` to `"Post customer-visible ticket conversation messages (agent, admin, or the owning customer)."` so the baseline permission catalog description stays accurate.

### 4. Add contracts for ticket attachments

Create file: `src/CustomerManagement.Api/Contracts/Tickets/TicketAttachmentContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.Tickets;

public sealed record TicketAttachmentResponse(
    Guid Id,
    Guid TicketId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    string UploadedByDisplayName,
    DateTime CreatedAtUtc);
```

### 5. Make `PriorityId` optional on ticket creation and resolve a default for customers

File: `src/CustomerManagement.Api/Contracts/Tickets/TicketContracts.cs`

- Change `CreateTicketRequest` from `Guid PriorityId` to `Guid? PriorityId`.

File: `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs`

- In `CreateTicketAsync` (~lines 71–110), before calling `ValidateCreateTicketAsync`:
  - If `IsCustomer(httpContext.User)`, ignore any `request.PriorityId` the client sent and resolve a default priority id via a new helper `ResolveDefaultPriorityIdAsync`, described below. Build an effective `CreateTicketRequest` (or a local `Guid priorityId` variable threaded through) using the resolved value instead of `request.PriorityId`.
  - If not a customer actor, `request.PriorityId` remains required: if it is `null` or `Guid.Empty`, add a validation error `["priorityId"] = ["PriorityId is required."]` the same way `ValidateCreateTicketAsync` already reports other field errors, and short-circuit before creating the ticket.
- Add helper:

```csharp
private static async Task<Guid?> ResolveDefaultPriorityIdAsync(
    CustomerManagementDbContext dbContext,
    CancellationToken cancellationToken)
{
    var activePriorities = await dbContext.TicketPriorities
        .AsNoTracking()
        .Where(priority => priority.IsActive)
        .Select(priority => new { priority.Id, priority.Name, priority.SortOrder })
        .ToListAsync(cancellationToken);

    var normal = activePriorities
        .FirstOrDefault(priority => string.Equals(priority.Name, "Normal", StringComparison.OrdinalIgnoreCase));

    if (normal is not null)
    {
        return normal.Id;
    }

    return activePriorities
        .OrderBy(priority => priority.SortOrder)
        .Select(priority => (Guid?)priority.Id)
        .FirstOrDefault();
}
```

- If `ResolveDefaultPriorityIdAsync` returns `null` (no active priority configured at all), return `400 Bad Request` via `Results.ValidationProblem` with `["priorityId"] = ["No active ticket priority is configured; contact an administrator."]` instead of creating the ticket.
- `ValidateCreateTicketAsync` (starts ~line 812) currently validates `request.PriorityId` against `dbContext.TicketPriorities`; keep that existing lookup/validation for the final resolved id (customer-resolved or agent/admin-supplied) so an invalid/inactive priority id still fails validation the same way it does today.

### 6. Implement ticket attachment endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketAttachmentEndpoints.cs`

Route group: `/api/tickets/{ticketId:guid}/attachments`, mirroring `CustomerAttachmentEndpoints.cs` structure exactly (same `Validate`, same `IAttachmentStorage` usage, same try/catch cleanup on `SaveChangesAsync` failure), with ticket ownership scoping added:

- `POST /api/tickets/{ticketId:guid}/attachments`
  - `.RequireAuthorization("Permission:" + Permissions.TicketsWrite)`
  - `.Accepts<IFormFile>("multipart/form-data").DisableAntiforgery()`
  - Behavior:
    - Validate `file` the same way `CustomerAttachmentEndpoints.Validate` does (required, non-empty, within `AttachmentStorageOptions.MaxFileSizeBytes`, content type in `AttachmentStorageOptions.AllowedContentTypes`).
    - Load the ticket; `404` if missing.
    - Apply the same customer-ownership check as `TicketEndpoints.ShouldApplyCustomerScope` / `ResolveOwnedCustomerIdsAsync` (duplicate the same small helper pattern locally, consistent with how `TicketMessagesEndpoints.cs` duplicates `IsAdmin`/`IsAgent`/`IsCustomer` rather than sharing them); agents follow the same "unassigned or assigned-to-me" rule used in `CanAccessTicketAsync`.
    - Resolve the actor's user id and display name the same way `TicketMessagesEndpoints.CreateAsync` resolves `sender` (`dbContext.Users.FirstOrDefaultAsync(row => row.Id == actorUserId && row.IsActive)`).
    - Save the file via `IAttachmentStorage.SaveAsync`, persist a `TicketAttachment` row, add a `TicketHistoryEntry` with `ActionType = "ticket.attachment.added"`, `FieldName = "attachmentId"`, `NewValue = attachment.Id.ToString()`.
    - On `SaveChangesAsync` failure, call `attachmentStorage.DeleteAsync(stored.StorageKey, cancellationToken)` and rethrow, exactly like `CustomerAttachmentEndpoints`.
  - Responses: `201 Created` with `TicketAttachmentResponse`; `400` validation; `403`/`404` per ownership result; same pattern as other ticket endpoints (`Forbid` for agent scope mismatch, `NotFound` for customer scope mismatch, matching `CanAccessTicketAsync`'s `AccessResult.Forbid()` vs `AccessResult.NotFound()` distinction).

- `GET /api/tickets/{ticketId:guid}/attachments`
  - `.RequireAuthorization("Permission:" + Permissions.TicketsRead)`
  - Same ownership scoping as above (read-only).
  - Returns `IReadOnlyList<TicketAttachmentResponse>` ordered by `CreatedAtUtc` descending.

- `GET /api/tickets/{ticketId:guid}/attachments/{attachmentId:guid}/content`
  - `.RequireAuthorization("Permission:" + Permissions.TicketsRead)`
  - Same ownership scoping; `404` if the attachment does not belong to the given ticket.
  - Stream content via `IAttachmentStorage` the same way `CustomerAttachmentEndpoints`'s `/content` route does.

Register in `src/CustomerManagement.Api/Program.cs` immediately after `app.MapTicketEndpoints();` and before `app.MapTicketMessagesEndpoints();`.

### 7. Allow customer replies in `TicketMessagesEndpoints.CreateAsync`

File: `src/CustomerManagement.Api/Endpoints/Tickets/TicketMessagesEndpoints.cs`

- Remove the block:
  ```csharp
  if (IsCustomer(httpContext.User))
  {
      return Results.Forbid();
  }

  if (!IsAdmin(httpContext.User) && !IsAgent(httpContext.User))
  {
      return Results.Forbid();
  }
  ```
- After `CanAccessTicketAsync` passes (so ownership/assignment scoping already applied, including the customer-ownership branch), load the full tracked `Ticket` entity (the method already does `dbContext.Tickets.FirstOrDefaultAsync(row => row.Id == request.TicketId, ...)` for `ticket` — keep using that tracked instance for the status mutation below instead of only the `scopeProjection`).
- Determine `isCustomerSender = IsCustomer(httpContext.User)`.
- If `isCustomerSender`:
  - If `ticket.Status == TicketStatus.Closed`, return `Results.BadRequest(new ProblemDetails { Title = "Invalid operation", Detail = "This ticket is closed and can no longer receive replies.", Status = StatusCodes.Status400BadRequest })`.
  - If `ticket.Status is TicketStatus.Resolved or TicketStatus.WaitingOnCustomer`, set `ticket.Status = TicketStatus.InProgress`, set `ticket.UpdatedAtUtc = now`, and add a `TicketHistoryEntry` with `ActionType = "ticket.status.auto-reopened"`, `FieldName = "status"`, `OldValue` = the previous status via the existing `ToApiStatus` helper (reuse `TicketEndpoints`-style naming but implemented locally in this file, consistent with its existing duplicated helpers), `NewValue = "in_progress"`.
- Set `message.SenderType = isCustomerSender ? TicketMessageSenderType.Customer : TicketMessageSenderType.Agent` instead of the current hardcoded `TicketMessageSenderType.Agent`.
- Keep the existing `ticket.message.posted` history entry write unchanged.
- No `RowVersion`/optimistic-concurrency check is added for this automatic status transition (see Edge Cases).

### 8. Update permission seeding/assignment

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- In the `assignments` dictionary, change `[AuthRoles.Customer]` from:
  ```csharp
  [AuthRoles.Customer] =
  [
      Permissions.TicketsRead,
      Permissions.TicketsWrite,
      Permissions.TicketsClose,
      Permissions.TicketMessagesRead
  ],
  ```
  to:
  ```csharp
  [AuthRoles.Customer] =
  [
      Permissions.TicketsRead,
      Permissions.TicketsWrite,
      Permissions.TicketsClose,
      Permissions.TicketMessagesRead,
      Permissions.TicketMessagesWrite
  ],
  ```

---

## Edge Cases & Failure Modes

- Customer replies on a `Closed` ticket: blocked with `400 Bad Request`; enforced in `TicketMessagesEndpoints.CreateAsync` (new status check added in Backend Task 7).
- Customer replies on a `Resolved` or `WaitingOnCustomer` ticket: allowed and auto-transitions to `InProgress`; history entry `ticket.status.auto-reopened` records the old/new status for audit.
- Customer attempts to read/post messages or upload attachments on a ticket outside their own `Customer` scope: `404 Not Found` (not `403`), consistent with the existing `AccessResult.NotFound()` branch in `CanAccessTicketAsync` and `ResolveOwnedCustomerIdsAsync`-based scoping — never reveals the ticket exists.
- Agent tries to upload an attachment on a ticket assigned to another agent (and not unassigned): `403 Forbid`, mirroring `CanAccessTicketAsync`'s agent branch.
- No active `TicketPriority` rows exist when a customer creates a ticket: `400 Bad Request` with a `priorityId` validation error rather than a server error or a ticket created with an invalid priority reference.
- Non-customer actor omits `PriorityId` on ticket creation: `400 Bad Request` (`priorityId` required for agent/admin-created tickets, since they can and should pick one explicitly).
- Attachment upload exceeding `AttachmentStorageOptions.MaxFileSizeBytes` or with a content type outside `AttachmentStorageOptions.AllowedContentTypes`: `400 Bad Request`, same validation shape as `CustomerAttachmentEndpoints.Validate`.
- Concurrent auto-reopen race: if an agent changes ticket status via `PUT /api/tickets/{ticketId}/status` at the same moment a customer's reply auto-reopens it, the message endpoint's `SaveChangesAsync` call has no explicit `RowVersion` precondition for the status field (unlike `UpdateTicketStatusAsync`/`ReopenTicketAsync`, which both require a client-supplied `RowVersion`). This is an accepted simplification: the automatic transition only ever moves a ticket toward `InProgress`, which is a safe, idempotent target even if it races with another write; any further explicit status change continues to go through the existing `RowVersion`-gated `PUT /status` or `POST /reopen` endpoints.
- Orphaned storage file if `TicketAttachment` persistence fails after `IAttachmentStorage.SaveAsync` succeeds: cleaned up via `attachmentStorage.DeleteAsync` in the `catch` block, identical to `CustomerAttachmentEndpoints`.

---

## Test Plan

1. `tests/CustomerManagement.Api.Tests/TicketEndpointsTests.cs`:
   - Customer creates a ticket without sending `priorityId` → ticket is created with the "Normal" active priority (or the lowest-`SortOrder` active priority if "Normal" is deactivated in the test setup).
   - Agent/admin creates a ticket without sending `priorityId` → `400` validation error on `priorityId`.
   - Customer-sent `priorityId` on create is ignored in favor of the server-resolved default (assert response priority does not equal the customer-sent value when they differ).
   - No active priorities configured → `400` with `priorityId` error instead of a server error.
2. Create `tests/CustomerManagement.Api.Tests/TicketAttachmentEndpointsTests.cs`:
   - Customer uploads an attachment to their own ticket → `201` with correct `TicketAttachmentResponse`.
   - Customer uploads to another customer's ticket → `404`.
   - Agent uploads to a ticket assigned to another agent → `403`.
   - Agent uploads to an unassigned ticket → `201`.
   - Oversized file / disallowed content type → `400`.
   - List and content-download endpoints respect the same ownership scoping as upload.
   - Upload failure cleans up the stored file (storage `DeleteAsync` invoked) — mirror the equivalent `CustomerAttachmentEndpointsTests.cs` case if one exists, otherwise add a new case following that file's test setup conventions.
3. Extend `tests/CustomerManagement.Api.Tests/TicketMessagesEndpointsTests.cs`:
   - Customer can post a reply on their own `New`/`InProgress` ticket; `SenderType` in the response is `"customer"`.
   - Customer reply on a `Resolved` ticket: `201` returned, and a follow-up `GET /api/tickets/{ticketId}` (or history check) shows `status = "in_progress"`.
   - Customer reply on a `WaitingOnCustomer` ticket: same auto-transition to `in_progress`.
   - Customer reply on a `Closed` ticket: `400 Bad Request`, ticket status unchanged, no message persisted.
   - Customer reply on another customer's ticket: `404`.
   - Agent/admin posting still behaves exactly as before (regression check against the existing test cases in this file).
4. Extend permission-seeding tests (wherever `IdentitySeedData` role/permission assignments are currently asserted, e.g. in `AuthEndpointsTests.cs` or a dedicated seeding test) to confirm `Customer` role now includes `ticket-messages.write`.

---

## Migration / Rollback

- Migration adds the `TicketAttachments` table and its indexes only; no changes to existing tables.
- Rollback plan:
  1. Revert `CreateTicketRequest.PriorityId` to non-nullable and remove `ResolveDefaultPriorityIdAsync`.
  2. Revert the `TicketMessagesEndpoints.CreateAsync` customer-forbid removal (restore the original forbid block) and the `SenderType` assignment.
  3. Remove `Permissions.TicketMessagesWrite` from the `Customer` assignment list in `IdentitySeedData.cs`.
  4. Remove `app.MapTicketAttachmentEndpoints();` from `Program.cs`.
  5. Roll back the EF migration (drop `TicketAttachments`).

---
## Review — completed 2026-10-07

- [x] TicketEndpointsTests.cs — priority default/ignored/no-active-priority cases: added, passing
- [x] TicketAttachmentEndpointsTests.cs — all 7 ownership/validation/cleanup scenarios: added, passing
- [x] TicketMessagesEndpointsTests.cs — customer reply + auto-reopen + closed-ticket-blocked cases: added, passing
- [x] Permission seeding — ticket-messages.write confirmed for Customer role (AuthEndpointsTests.cs): added, passing

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 21.**

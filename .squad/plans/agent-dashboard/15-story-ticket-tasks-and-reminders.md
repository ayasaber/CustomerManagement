# Story 15 - Ticket Tasks And Reminders

## Prerequisites

- Story 14 completed: `14-story-dashboard-assigned-work-and-customer-context-apis.md`.
- Story 13 completed: `../ticket-management/13-story-ticket-management.md`.

---

## Story Goal

Add ticket-linked tasks/reminders so agents can create follow-up actions tied to a ticket, view open tasks across their work, and mark tasks as done with optimistic concurrency.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` for mutable entity mapping conventions.
2. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` for ticket authorization scope checks.
3. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` for permission naming.
4. `src/CustomerManagement.Api/Endpoints/Customers/CustomerProfileEndpoints.cs` for rowversion conflict handling style.

---

## Backend Tasks

### 1. Add task entity and EF mapping

Create file: `src/CustomerManagement.Api/Domain/Dashboard/TicketTask.cs`

Entity fields:

- `Guid Id`
- `Guid TicketId` (required FK -> `Ticket`)
- `Guid CreatedByUserId` (required FK -> `ApplicationUser`)
- `Guid AssignedToUserId` (required FK -> `ApplicationUser`)
- `string Description` (required, max `500`)
- `DateTime DueAtUtc` (required)
- `TicketTaskStatus Status` (enum: `Open`, `Done`)
- `DateTime CreatedAtUtc`
- `DateTime UpdatedAtUtc`
- `DateTime? CompletedAtUtc`
- `Guid? CompletedByUserId`
- `byte[] RowVersion` (`[Timestamp]`)

Create file: `src/CustomerManagement.Api/Domain/Dashboard/TicketTaskStatus.cs`

- Enum values: `Open = 1`, `Done = 2`.

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<TicketTask> TicketTasks`.
- Map table `TicketTasks`.
- Indexes:
  - `(AssignedToUserId, Status, DueAtUtc)`
  - `(TicketId, Status, DueAtUtc)`
  - `(CreatedByUserId, CreatedAtUtc)`
- Apply `IsRowVersion()`.

### 2. Add permissions and seed

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `TicketTasksRead = "ticket-tasks.read"`
  - `TicketTasksWrite = "ticket-tasks.write"`
  - `TicketTasksComplete = "ticket-tasks.complete"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Seed all three permissions.
- Role assignments:
  - `admin`: all.
  - `agent`: all.
  - `customer`: none.

### 3. Add contracts

Create folder: `src/CustomerManagement.Api/Contracts/TicketTasks/`

- `CreateTicketTaskRequest`
  - `Guid TicketId`
  - `string Description`
  - `DateTime DueAtUtc`
  - `Guid? AssignedToUserId` (default to current user when null)
- `UpdateTicketTaskRequest`
  - `string Description`
  - `DateTime DueAtUtc`
  - `Guid AssignedToUserId`
  - `byte[] RowVersion`
- `CompleteTicketTaskRequest`
  - `byte[] RowVersion`
- `TicketTaskResponse`
  - Full entity shape plus `TicketNumber`, `AssignedToDisplayName`.
- `TicketTaskListResponse`
  - `int TotalCount`
  - `IReadOnlyList<TicketTaskResponse> Items`

### 4. Implement task endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Dashboard/TicketTasksEndpoints.cs`

Route group: `/api/ticket-tasks`

- `GET /api/ticket-tasks`
  - Permission: `ticket-tasks.read`.
  - Query: `ticketId?`, `status?`, `assignedToMeOnly=true|false`, `page=1`, `pageSize=20`.
  - Agent default: `assignedToMeOnly=true`.
  - Admin default: `assignedToMeOnly=false`.
  - `200`, `400`, `401`, `403`.

- `POST /api/ticket-tasks`
  - Permission: `ticket-tasks.write`.
  - Validate referenced ticket exists and caller can view it.
  - Validate assignee exists and has `agent` role if `AssignedToUserId` provided.
  - `201` returns `TicketTaskResponse`.
  - `400` validation, `404` ticket/user not found, `403` forbidden.

- `PUT /api/ticket-tasks/{taskId:guid}`
  - Permission: `ticket-tasks.write`.
  - Only creator, assignee, or admin may update.
  - `200` success.
  - `409` stale rowversion.
  - `404` not found.

- `PUT /api/ticket-tasks/{taskId:guid}/complete`
  - Permission: `ticket-tasks.complete`.
  - Assignee or admin can mark done.
  - Transition `Open -> Done` only.
  - If already done, return `409` with deterministic error code `task_already_completed`.
  - `200` with updated `TicketTaskResponse`.

### 5. Wire Story 14 placeholder endpoint to real task data

File: `src/CustomerManagement.Api/Endpoints/Dashboard/AgentDashboardEndpoints.cs`

- Replace empty `/api/dashboard/me/open-tasks` implementation with query against `TicketTasks` where status is `Open` and assignee is current user (or all for admin unless `assignedToMeOnly=true`).

---

## Edge Cases & Failure Modes

- Due date in past: allowed; no validation rejection.
- Due date min value or non-UTC: normalize to UTC and reject impossible values.
- Agent tries to complete another agent's task: `403`.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/TicketTasksEndpointsTests.cs`.
2. Cover create/list/update/complete success flows.
3. Cover role/scope rules (agent vs admin).
4. Cover rowversion conflicts and already-completed conflict.
5. Add regression test for `/api/dashboard/me/open-tasks` now returning real open tasks.

---

## Verification Steps

1. `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`
2. `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~TicketTasksEndpoints"`

---

## Done Criteria

- [x] `TicketTask` entity exists with rowversion and required FKs.
- [x] CRUD-lite and completion endpoints exist with explicit status behavior.
- [x] Dashboard open-tasks endpoint is backed by real ticket-task data.
- [x] Tests cover optimistic concurrency and actor scope rules.

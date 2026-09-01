# Story 14 - Dashboard Assigned Work And Customer Context APIs

## Prerequisites

- Story 13 completed: `../ticket-management/13-story-ticket-management.md`.
- Story 11 completed: `../security-administration/11-story-gateway-auth-forwarding-and-policy-consumption.md`.

---

## Story Goal

Deliver dashboard read APIs that let an authenticated agent land on a single page showing:

1. Tickets assigned to them.
2. Their open ticket-linked tasks/reminders summary (initially empty until Story 15 lands).
3. Customer context for a selected ticket (profile, primary contact, interaction history) via existing Customer Management read endpoints/data.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` for ticket list filtering and visibility rules.
2. `src/CustomerManagement.Api/Endpoints/Customers/CustomerProfileEndpoints.cs` for customer read contract shape and optimistic concurrency response style.
3. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` for permission naming style.
4. `src/CustomerManagement.Api/Program.cs` for endpoint mapping conventions.
5. `tests/CustomerManagement.Api.Tests/*` for minimal API test conventions.

---

## Backend Tasks

### 1. Add dashboard permissions and baseline seed

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `DashboardRead = "dashboard.read"`
  - `DashboardCustomerContextRead = "dashboard.customer-context.read"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Add both permissions to baseline set.
- Assign permissions:
  - `admin`: both.
  - `agent`: both.
  - `customer`: none.

### 2. Add dashboard contracts

Create folder: `src/CustomerManagement.Api/Contracts/Dashboard/`

Create DTOs:

- `DashboardAssignedTicketItemResponse`
  - `Guid TicketId`
  - `string TicketNumber`
  - `string Subject`
  - `string Status`
  - `string Priority`
  - `string Category`
  - `bool IsEscalated`
  - `DateTime CreatedAtUtc`
  - `DateTime UpdatedAtUtc`
  - `Guid CustomerId`
  - `string CustomerDisplayName`
- `DashboardAssignedTicketsResponse`
  - `int TotalCount`
  - `IReadOnlyList<DashboardAssignedTicketItemResponse> Items`
- `DashboardOpenTaskSummaryItemResponse`
  - `Guid TaskId`
  - `Guid TicketId`
  - `string TicketNumber`
  - `string Description`
  - `DateTime DueAtUtc`
  - `DateTime CreatedAtUtc`
- `DashboardOpenTaskSummaryResponse`
  - `int TotalCount`
  - `IReadOnlyList<DashboardOpenTaskSummaryItemResponse> Items`
- `DashboardCustomerContextResponse`
  - `Guid TicketId`
  - `Guid CustomerId`
  - `string CustomerDisplayName`
  - `string? Company`
  - `string? PrimaryEmail`
  - `string? PrimaryPhone`
  - `IReadOnlyList<DashboardCustomerInteractionItemResponse> RecentInteractions`
- `DashboardCustomerInteractionItemResponse`
  - `Guid InteractionId`
  - `string Type`
  - `string Summary`
  - `DateTime OccurredAtUtc`

### 3. Implement dashboard read endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Dashboard/AgentDashboardEndpoints.cs`

Route group: `/api/dashboard`

- `GET /api/dashboard/me/assigned-tickets`
  - Auth required + permission `dashboard.read`.
  - Query params:
    - `status` optional string enum filter.
    - `page` default `1`, min `1`.
    - `pageSize` default `20`, min `1`, max `100`.
  - Behavior:
    - For `agent`: only tickets where `AssignedToUserId == currentUserId`.
    - For `admin`: return all tickets unless `assignedToMeOnly=true` is passed; default `false` for admin.
  - Responses:
    - `200` with `DashboardAssignedTicketsResponse`.
    - `400` invalid pagination/filter.
    - `401` unauthenticated.
    - `403` missing permission.

- `GET /api/dashboard/me/open-tasks`
  - Auth required + permission `dashboard.read`.
  - Query params:
    - `page` default `1`, min `1`.
    - `pageSize` default `20`, min `1`, max `100`.
  - Behavior:
    - Until Story 15 tables exist, return `200` with empty list and `TotalCount = 0`.
    - Keep response contract stable so UI can integrate now.
  - Responses: `200`, `400`, `401`, `403`.

- `GET /api/dashboard/tickets/{ticketId:guid}/customer-context`
  - Auth required + permission `dashboard.customer-context.read`.
  - Behavior:
    - Verify ticket exists and caller is allowed to view ticket by same scope rules as ticket read endpoint.
    - Resolve customer from ticket `CustomerId`; do not duplicate customer data in dashboard tables.
    - Include recent interactions sorted descending by `OccurredAtUtc`, default top `10`.
  - Responses:
    - `200` with `DashboardCustomerContextResponse`.
    - `404` ticket missing or customer link missing.
    - `401` unauthenticated.
    - `403` forbidden.

### 4. Register endpoint group

File: `src/CustomerManagement.Api/Program.cs`

- Map `AgentDashboardEndpoints` in existing endpoint registration section.

---

## Edge Cases & Failure Modes

- Agent requests a ticket not assigned to them: return `404` (avoid leaking existence).
- Invalid status filter token: return `400` with validation detail.
- Customer profile deleted/disabled while ticket exists: return `404` with safe message; keep ticket data intact.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/AgentDashboardEndpointsTests.cs`.
2. Verify assigned tickets filter behavior for agent vs admin.
3. Verify customer-context endpoint enforces ticket scope and returns interactions ordered newest-first.
4. Verify open-tasks endpoint returns empty contract pre-Story-15.
5. Verify permission failures (`403`) and unauthenticated calls (`401`).

---

## Verification Steps

1. `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`
2. `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~AgentDashboardEndpoints"`

---

## Done Criteria

- [x] Dashboard read permissions exist and are seeded to admin/agent roles.
- [x] `/api/dashboard` endpoints provide stable assigned-ticket and customer-context contracts.
- [x] Open-tasks endpoint contract exists with empty placeholder behavior pending Story 15.
- [x] API tests validate scope, ordering, and auth/permission behavior.

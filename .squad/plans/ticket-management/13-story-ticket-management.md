# Story 13 - Ticket Management

## Prerequisites

- Story 01 completed: `../customer-management/01-story-customer-profile-and-contact-foundation.md`.
- Story 02 completed: `../customer-management/02-story-customer-notes-and-attachments.md`.
- Story 03 completed: `../customer-management/03-story-customer-interaction-history-read-model.md`.
- Story 04 completed: `../customer-management/04-story-customer-management-gateway-routing.md`.
- Story 05 completed: `../customer-management/05-story-agent-customer-management-ui.md`.
- Story 06 completed: `../security-administration/06-story-identity-auth-foundation.md`.
- Story 08 completed: `../security-administration/08-story-permission-model-and-policy-administration.md`.
- Story 09 completed: `../security-administration/09-story-audit-log-capture-and-query.md`.
- Story 10 completed: `../security-administration/10-story-system-settings-api-and-policy-enforcement.md`.
- Story 11 completed: `../security-administration/11-story-gateway-auth-forwarding-and-policy-consumption.md`.

---

## Story Goal

Deliver a complete ticket-management capability in the existing shared backend + gateway + Angular UI stack.

User-visible outcomes:

1. Agents/Admins can create tickets for existing customers and manage lifecycle to closure.
2. Categories/priorities are fixed, admin-managed lists (add/edit/deactivate), not free text.
3. Assignment, escalation, and status history are tracked with actor + UTC timestamps.
4. Agents can list/filter/open tickets quickly from a dedicated UI.

Out of scope:

- Customer-initiated ticket creation from Customer Portal.
- Automatic/time-based escalation rules.
- Communication-channel auto-ingestion into tickets.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - read ~lines 1-120 for endpoint registration, JWT auth, and policy wiring (`Map*Endpoints`, `PermissionPolicyProvider`).
2. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - read ~lines 1-330 for entity mapping conventions (Guid ids, `RowVersion`, FK/index patterns, max lengths).
3. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` - read full file (~lines 1-12) for permission naming conventions.
4. `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` - read ~lines 70-170 for baseline permission seeding and role-permission assignment pattern.
5. `src/CustomerManagement.Api/Endpoints/Customers/CustomerProfileEndpoints.cs` - read ~lines 12-260 for minimal-API structure, validation pattern, optimistic concurrency handling, and route style.
6. `src/CustomerManagement.Api/Endpoints/Admin/PermissionsEndpoints.cs` - read ~lines 13-260 for admin-managed catalog CRUD conventions.
7. `src/CustomerManagement.Api/Endpoints/Admin/SystemSettingsEndpoints.cs` - read ~lines 17-230 for rowversion conflict handling + audit write pattern.
8. `src/CustomerManagement.Gateway/ocelot.json` - read full file (~lines 1-190) for upstream/downstream route contract shape and auth wiring.
9. `src/CustomerManagement.Gateway/ROUTES.md` - read full file for route documentation style.
10. `tests/CustomerManagement.Api.Tests/CustomerProfileEndpointsTests.cs` - read full file (~lines 1-200) for API test style and request helpers.
11. `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` - read full file (~lines 1-220) for gateway propagation/error behavior tests.
12. `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` - read full file (~lines 1-80) for route-count/contract assertions.
13. `src/CustomerManagement.Ui/src/app/app.routes.ts` - read full file (~lines 1-90) for route and guard conventions.
14. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` - read full file (~lines 1-170) for API service + `withAutoRefresh` + error mapping pattern.
15. `src/CustomerManagement.Ui/src/app/features/customer-management/customer-management-page.component.ts` - read ~lines 1-260 for container orchestration, loading state handling, and message pattern.
16. `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts` - read full file (~lines 1-30) for admin shell/nav composition.

---

## Product rules (from story)

- Categories and priorities are controlled vocabularies with soft-deactivate; deactivated values remain visible on existing tickets but cannot be selected for new/updated tickets.
- Ticket assignment must target an existing `ApplicationUser` that currently has `agent` role.
- Authorization must use permissions, not hardcoded role checks in endpoint logic.
- Escalation is manual only.
- Ticket history must record who/when/old->new for lifecycle changes.

---

## Backend Tasks

### 1. Add ticket domain model + EF mapping

File: `src/CustomerManagement.Api/Domain/` (create `Tickets/` folder)

- Create entities:
  - `Ticket`:
    - `Guid Id`
    - `Guid CustomerId` (required FK -> `Customer`)
    - `Guid? AssignedToUserId` (nullable FK -> `ApplicationUser`)
    - `Guid CategoryId` (required FK)
    - `Guid PriorityId` (required FK)
    - `string Subject` (required, max 200)
    - `string Description` (required, max 4000)
    - `TicketStatus Status` (enum)
    - `bool IsEscalated`
    - `DateTime CreatedAtUtc`, `UpdatedAtUtc`
    - `byte[] RowVersion` (`[Timestamp]`)
  - `TicketCategory`:
    - `Guid Id`, `string Name` (max 100, unique), `string? Description` (max 500)
    - `bool IsActive`
    - `DateTime CreatedAtUtc`, `UpdatedAtUtc`
    - `byte[] RowVersion`
  - `TicketPriority`:
    - `Guid Id`, `string Name` (max 100, unique), `int SortOrder`
    - `bool IsActive`
    - `DateTime CreatedAtUtc`, `UpdatedAtUtc`
    - `byte[] RowVersion`
  - `TicketHistoryEntry`:
    - `Guid Id`, `Guid TicketId`
    - `string ActionType` (max 100)
    - `string FieldName` (max 100)
    - `string? OldValue` (max 500)
    - `string? NewValue` (max 500)
    - `Guid? ActorUserId`
    - `string? ActorEmail` (max 320)
    - `DateTime OccurredAtUtc`

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<Ticket>`, `DbSet<TicketCategory>`, `DbSet<TicketPriority>`, `DbSet<TicketHistoryEntry>`.
- Configure tables:
  - `Tickets`, `TicketCategories`, `TicketPriorities`, `TicketHistoryEntries`.
- Add indexes:
  - `Tickets`: `(CustomerId, CreatedAtUtc desc)`, `(Status, CreatedAtUtc desc)`, `(AssignedToUserId, Status, CreatedAtUtc desc)`, `(CategoryId)`, `(PriorityId)`.
  - `TicketCategories`: unique index on `Name`.
  - `TicketPriorities`: unique index on `Name`, index on `SortOrder`.
  - `TicketHistoryEntries`: `(TicketId, OccurredAtUtc)`.
- Use `IsRowVersion()` for `Ticket`, `TicketCategory`, `TicketPriority`.

### 2. Add permissions + baseline seed

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `TicketsRead = "tickets.read"`
  - `TicketsWrite = "tickets.write"`
  - `TicketsAssign = "tickets.assign"`
  - `TicketsEscalate = "tickets.escalate"`
  - `TicketsTaxonomyManage = "tickets.taxonomy.manage"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Add these permissions to `baselinePermissions`.
- Add role assignments:
  - `admin`: all ticket permissions.
  - `agent`: `tickets.read`, `tickets.write`, `tickets.assign`, `tickets.escalate`.
  - `customer`: none.
- Add default ticket taxonomy seeding (idempotent by name, case-insensitive):
  - Categories: `General Inquiry`, `Technical Issue`, `Billing`, `Account Access`, `Feature Request`.
  - Priorities: `Low` (10), `Normal` (20), `High` (30), `Urgent` (40).
  - Behavior: add missing values only; do not overwrite existing admin-managed rows.

### 3. Add contracts for ticket APIs

Create folder: `src/CustomerManagement.Api/Contracts/Tickets/`

- Create request/response DTO records for:
  - Taxonomy:
    - `CreateTicketCategoryRequest`, `UpdateTicketCategoryRequest`, `TicketCategoryResponse`
    - `CreateTicketPriorityRequest`, `UpdateTicketPriorityRequest`, `TicketPriorityResponse`
  - Ticket lifecycle:
    - `CreateTicketRequest`
    - `UpdateTicketCoreRequest` (subject/description/category/priority/rowVersion)
    - `UpdateTicketStatusRequest` (targetStatus, rowVersion)
    - `AssignTicketRequest` (assigneeUserId, rowVersion)
    - `SelfAssignTicketRequest` (rowVersion)
    - `EscalateTicketRequest` (reason, rowVersion)
    - `ReopenTicketRequest` (reason, rowVersion)
    - `TicketResponse`
  - Query/history:
    - `TicketListItemResponse`
    - `TicketListResponse` (page/pageSize/totalCount/items)
    - `TicketHistoryItemResponse`
    - `TicketHistoryResponse`

### 4. Implement taxonomy endpoints (`/api/tickets/categories`, `/api/tickets/priorities`)

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketCategoryEndpoints.cs`

- `GET /api/tickets/categories` (auth + `tickets.read`) returns all categories sorted by name; include `isActive`.
- `POST /api/tickets/categories` (auth + `tickets.taxonomy.manage`) creates category.
- `PUT /api/tickets/categories/{categoryId:guid}` (auth + `tickets.taxonomy.manage`) updates name/description/isActive with rowversion conflict = `409`.

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketPriorityEndpoints.cs`

- `GET /api/tickets/priorities` (auth + `tickets.read`) returns priorities sorted by `sortOrder`, then name.
- `POST /api/tickets/priorities` (auth + `tickets.taxonomy.manage`) creates priority.
- `PUT /api/tickets/priorities/{priorityId:guid}` (auth + `tickets.taxonomy.manage`) updates name/sortOrder/isActive with rowversion conflict = `409`.

Validation rules:

- Name required, trimmed, max 100, unique case-insensitive.
- Deactivate allowed even if referenced by tickets.
- Hard delete not exposed.

### 5. Implement ticket endpoints (`/api/tickets`)

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs`

- `POST /api/tickets` (`tickets.write`)
  - Validates customer exists.
  - Validates category/priority exist and are active.
  - Sets `Status = New`, `IsEscalated = false`.
  - Returns `201 Created` + `TicketResponse`.
- `GET /api/tickets/{ticketId:guid}` (`tickets.read`) returns detail or `404`.
- `GET /api/tickets` (`tickets.read`) with query params:
  - `page`, `pageSize`, `status`, `categoryId`, `priorityId`, `assignedToUserId`, `customerId`, `isEscalated`.
  - Returns paginated `TicketListResponse`.
- `PUT /api/tickets/{ticketId:guid}` (`tickets.write`)
  - Updates subject/description/category/priority with rowversion.
  - Category/priority must exist; active required for updates.
  - Returns `409` on stale rowversion.
- `PUT /api/tickets/{ticketId:guid}/status` (`tickets.write`)
  - Enforces state machine below.
  - Returns `400` for invalid transition.
- `PUT /api/tickets/{ticketId:guid}/self-assign` (`tickets.assign`)
  - Actor must be authenticated and have role `agent` or `admin` claim set via Identity role.
  - Sets `AssignedToUserId` to actor.
- `PUT /api/tickets/{ticketId:guid}/assign` (`tickets.assign`)
  - Assign/reassign explicit `assigneeUserId`.
  - Target user must exist, active, and have `agent` role.
- `POST /api/tickets/{ticketId:guid}/escalate` (`tickets.escalate`)
  - Manual action only; sets `IsEscalated=true`.
  - No-op guard: if already escalated, return `409` or `400` with clear message (pick one and keep consistent in tests).
- `POST /api/tickets/{ticketId:guid}/reopen` (`tickets.write`)
  - Explicit reopen action only; transitions `Closed -> InProgress`.
  - Clears closure-only constraints as needed.

### 6. Implement ticket history endpoint and history writer

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketHistoryEndpoints.cs`

- `GET /api/tickets/{ticketId:guid}/history` (`tickets.read`) returns chronological items.

In ticket mutation endpoints, write a `TicketHistoryEntry` for each meaningful change:

- status changes
- assignee changes
- category/priority changes
- escalation toggle
- reopen

Recorded fields per entry:

- `actionType`
- `fieldName`
- `oldValue`
- `newValue`
- `actorUserId`
- `actorEmail`
- `occurredAtUtc`

### 7. Status state machine (explicit assumption)

Create enum: `TicketStatus`

- `New`
- `InProgress`
- `WaitingOnCustomer`
- `Resolved`
- `Closed`

Allowed transitions in generic status endpoint:

- `New -> InProgress | WaitingOnCustomer | Resolved | Closed`
- `InProgress -> WaitingOnCustomer | Resolved | Closed`
- `WaitingOnCustomer -> InProgress | Resolved | Closed`
- `Resolved -> Closed`
- `Closed -> (none)`

Reopen behavior:

- `Closed -> InProgress` allowed only via `POST /api/tickets/{ticketId}/reopen`.

### 8. Wire endpoints and gateway contracts

File: `src/CustomerManagement.Api/Program.cs`

- Register ticket endpoint maps beside existing customer/admin endpoints.

File: `src/CustomerManagement.Gateway/ocelot.json`

- Add authenticated routes:
  - `/api/tickets` (`GET`, `POST`)
  - `/api/tickets/{ticketId}` (`GET`, `PUT`)
  - `/api/tickets/{ticketId}/status` (`PUT`)
  - `/api/tickets/{ticketId}/self-assign` (`PUT`)
  - `/api/tickets/{ticketId}/assign` (`PUT`)
  - `/api/tickets/{ticketId}/escalate` (`POST`)
  - `/api/tickets/{ticketId}/reopen` (`POST`)
  - `/api/tickets/{ticketId}/history` (`GET`)
  - `/api/tickets/categories` (`GET`, `POST`)
  - `/api/tickets/categories/{categoryId}` (`PUT`)
  - `/api/tickets/priorities` (`GET`, `POST`)
  - `/api/tickets/priorities/{priorityId}` (`PUT`)
- Mirror existing customer route pattern for `AuthenticationOptions` and forwarded identity headers.

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Document new public upstream contract table rows.

### 9. Add EF migration

Create migration under: `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/`

- Name: `Story13TicketManagement`.
- Include all ticket tables, FKs, indexes, rowversion columns.

Required execution sequence (do not skip):

1. From `src/CustomerManagement.Api` generate migration after all entity/dbcontext changes:
  - `dotnet ef migrations add Story13TicketManagement --output-dir Infrastructure/Persistence/Migrations`
2. Apply it before running API manually or running end-to-end/manual smoke:
  - `dotnet ef database update`
3. If migration already exists, still run update to align target DB:
  - `dotnet ef database update`

Troubleshooting guardrails:

- If you hit `PendingModelChangesWarning` / "The model for context has pending changes", stop and do one of:
  - create missing migration, then `dotnet ef database update`; or
  - if model change was accidental, revert model changes before running update.
- Do **not** suppress `PendingModelChangesWarning` in Story delivery; treat it as a required synchronization failure.
- If build/test fails because `CustomerManagement.Api.exe` is locked, stop running API process first, then rerun build/test/migration commands.

---

## Frontend Tasks

### 1. Add ticket models and API service methods

File: `src/CustomerManagement.Ui/src/app/core/models/` (create `ticket-management.models.ts`)

- Define DTO interfaces that match backend contracts exactly.

File: `src/CustomerManagement.Ui/src/app/core/services/` (create `ticket-management-api.service.ts`)

- Base URL: `http://localhost:5101/api/tickets` and taxonomy URLs.
- Use `AuthService.withAutoRefresh(...)` + centralized error mapping pattern from `customer-management-api.service.ts`.

Methods:

- `listTickets(query)`
- `getTicket(ticketId)`
- `createTicket(request)`
- `updateTicket(ticketId, request)`
- `updateTicketStatus(ticketId, request)`
- `assignSelf(ticketId, rowVersion)`
- `assignTicket(ticketId, request)`
- `escalateTicket(ticketId, request)`
- `reopenTicket(ticketId, request)`
- `getTicketHistory(ticketId)`
- `listCategories()` / `createCategory()` / `updateCategory()`
- `listPriorities()` / `createPriority()` / `updatePriority()`

### 2. Add routes and guarded pages

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add agent/admin routes:
  - `/tickets`
  - `/tickets/:ticketId`
- Add admin taxonomy routes (or place under existing `/admin` children):
  - `/admin/ticket-categories`
  - `/admin/ticket-priorities`

File: `src/CustomerManagement.Ui/src/app/app.ts`

- Add shell nav links for ticket workspace and admin taxonomy pages based on ticket permissions.

### 3. Build ticket list + filters page

Create file: `src/CustomerManagement.Ui/src/app/features/ticket-management/ticket-list-page.component.ts`

- Layout:
  - filter bar (status/category/priority/assigned agent/customer/escalated)
  - primary action: create ticket
  - paginated table
- Table columns:
  - Ticket Id
  - Subject
  - Customer
  - Status
  - Category
  - Priority
  - Assignee
  - Escalated
  - Updated (UTC)

### 4. Build ticket detail page with action panels

Create file: `src/CustomerManagement.Ui/src/app/features/ticket-management/ticket-detail-page.component.ts`

- Sections:
  - Summary + editable fields
  - Status transition control
  - Assign/self-assign controls
  - Escalation + reopen actions
  - History timeline
- Include rowversion handling from response in every mutable action request.
- Show conflict banner on `409` and force reload latest ticket.

### 5. Build admin taxonomy pages

Create files:

- `src/CustomerManagement.Ui/src/app/features/admin/tickets/admin-ticket-categories-page.component.ts`
- `src/CustomerManagement.Ui/src/app/features/admin/tickets/admin-ticket-priorities-page.component.ts`

Behavior:

- list/add/edit/deactivate values
- keep deactivated values visible in table with inactive badge
- prevent delete UI action (deactivate only)

### 6. UI consistency pass

File group: ticket-management components + styles

- Reuse panel/table/form rhythm from customer/admin pages.
- Include clear inline validation + top-level status banners.
- Ensure responsive layout at laptop and mobile widths.

---

## Edge Cases & Failure Modes

- Unknown customer on create (`CustomerId` not found): return `404` from `POST /api/tickets`, no partial inserts.
- Inactive category/priority selected during create/update: return `400` with validation payload.
- Assignee user exists but is inactive/non-agent: return `400` with clear reason.
- Concurrent status/assignment updates with stale `RowVersion`: return `409` conflict payload and preserve current server state.
- Duplicate category/priority names (case-insensitive): return `409` conflict.
- Generic status update trying `Closed -> New` or `Closed -> InProgress`: return `400`; reopen only through explicit reopen endpoint.
- Escalate called repeatedly on already escalated ticket: deterministic error (`400` or `409`) and no duplicate history rows.
- History endpoint on missing ticket: `404`.
- Ticket list filters with invalid pagination/pageSize: `400` validation problem.
- EF model and migrations are out of sync (`PendingModelChangesWarning`): treat as release blocker; create/apply migration before validation runs.

---

## Execution Bug Register (Living)

Purpose:

- Capture every bug found during implementation/QA and immediately convert it into either a story task, a regression test, or an operational guardrail.
- Keep this section updated during execution; do not defer bug capture to the end.

Process rule (mandatory):

1. When a bug is reported, add an item here with: symptom, root cause, fix, and prevention test/check.
2. Add or update automated tests before marking bug as closed.
3. If bug is environment/runtime related, add explicit runbook steps under Verification or Troubleshooting guardrails.

Current bugs captured in this execution:

1. Symptom: Login returned `307 Temporary Redirect` and UI showed `Failed to fetch`.
  - Root cause: API/Gateway applied HTTPS redirection in Development while UI called HTTP gateway URL.
  - Fix: gate `UseHttpsRedirection()` to non-Development environments.
  - Prevention: add startup smoke check ensuring `/api/auth/login` on configured dev gateway URL does not redirect.
2. Symptom: Ticket category/priority dropdowns were empty on fresh DB.
  - Root cause: no baseline seeding for ticket taxonomy tables.
  - Fix: add idempotent startup seed for default categories/priorities.
  - Prevention: add verification step that taxonomy endpoints return baseline rows after bootstrap.
3. Symptom: `GET /api/tickets` failed with `status: Status filter is invalid` when no status filter was provided.
  - Root cause: list endpoint validated empty status as invalid instead of optional.
  - Fix: validate status only when provided.
  - Prevention: add regression API test for list-without-status query.
4. Symptom: Status update button looked non-functional in UI flow.
  - Root cause: workflow depended on manually typing ticket id, causing frequent mismatch/human error.
  - Fix: add ticket selector bound to loaded rows and row action to prefill status target ticket.
  - Prevention: add component test for select-and-update flow.
5. Symptom: Ambiguity around accepting customer name/email for ticket creation.
  - Root cause: changing input identity during execution introduced instability and confusion.
  - Fix: standardize ticket creation on `customerId` only.
  - Prevention: keep contract explicit (`customerId` required) and reject alternate customer identity fields.
6. Symptom: Status update returned `Invalid operation` for transitions users expected to work (example: `New -> Closed`).
  - Root cause: implemented state machine was stricter than story definition.
  - Fix: align `CanTransition` rules with story state matrix.
  - Prevention: add transition matrix tests for all allowed and blocked transitions and include FE message check for ProblemDetails detail text.

---

## Test Plan

1. Add API endpoint tests for ticket lifecycle:
  - create/list/get/update/status/self-assign/assign/escalate/reopen/history.
   - File: `tests/CustomerManagement.Api.Tests/TicketEndpointsTests.cs`.
2. Add API tests for taxonomy CRUD/deactivate and uniqueness rules.
   - Files:
     - `tests/CustomerManagement.Api.Tests/TicketCategoryEndpointsTests.cs`
     - `tests/CustomerManagement.Api.Tests/TicketPriorityEndpointsTests.cs`
3. Add API concurrency tests (`409` stale rowversion) for mutable ticket fields.
4. Add gateway route propagation and contract tests:
   - update `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs`
   - update `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` for route count and signatures.
5. Add Angular service tests for `ticket-management-api.service.ts` (auth header, auto-refresh passthrough, error mapping).
6. Add component tests:
   - ticket list filters + pagination request mapping.
   - ticket detail actions with status/assignment/escalation/reopen paths.
   - admin taxonomy add/edit/deactivate flows.
7. Add smoke flow test:
   - create ticket -> self-assign -> move through status -> escalate -> resolve/close -> reopen -> verify history entries order.
8. Add regression tests for execution bugs:
  - login endpoint in dev should not redirect on configured gateway URL.
  - ticket list without status filter should return `200`.
  - create ticket without `customerId` should return `400`.
  - status transitions from `new` and `waiting_on_customer` to `closed` should follow story matrix and return `200` for authorized users.

---

## Migration / Rollback

- Migration adds 4 new tables + FK constraints to `Customers` and `Users`.
- Rollback path:
  1. Stop API.
  2. `dotnet ef database update <previous-migration>` for API project.
  3. Revert gateway route additions if deployment is partial.
- Half-applied risk: gateway routes deployed before API migration causes upstream `404/500` for ticket endpoints.

---

## Verification Steps

0. **Migration sync (mandatory before runtime smoke):** from `src/CustomerManagement.Api` run:
  - `dotnet ef migrations add Story13TicketManagement --output-dir Infrastructure/Persistence/Migrations` (if not already created)
  - `dotnet ef database update`

1. **Backend builds:** from repo root run:
   - `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj`
   - `dotnet build src/CustomerManagement.Gateway/CustomerManagement.Gateway.csproj`
2. **API tests:**
   - `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj`
3. **Gateway tests:**
   - `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj`
4. **Frontend build/tests:** from `src/CustomerManagement.Ui` run:
   - `npm run build`
   - `npm test -- --watch=false --browsers=ChromeHeadless`
5. **Manual smoke:** run API + gateway + UI, then validate ticket create/assign/status/escalate/reopen/history and taxonomy deactivation behavior.

---

## Done Criteria

- [ ] Ticket domain entities, DTOs, and migration are added in the existing shared API project.
- [ ] Story DB is migrated (`dotnet ef database update`) with no pending model changes warning.
- [ ] `/api/tickets`, `/api/tickets/categories`, and `/api/tickets/priorities` endpoints are implemented with permission-based authorization.
- [ ] Ticket status transition rules are enforced server-side with explicit reopen action.
- [ ] Ticket mutation endpoints use rowversion optimistic concurrency and return `409` on stale writes.
- [ ] Ticket history/audit entries capture actor, UTC time, and old/new values for meaningful changes.
- [ ] Categories/priorities support add/edit/deactivate and prevent selection of inactive values for new updates.
- [ ] Gateway route contract includes all ticket and taxonomy endpoints and tests are updated.
- [ ] Angular ticket list/detail/admin taxonomy pages are implemented and integrated into app navigation.
- [ ] API, gateway, and frontend automated tests pass with added ticket-management coverage.

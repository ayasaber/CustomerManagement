# Story 18 - Agent Dashboard UI And Gateway Routing

## Prerequisites

- Story 14 completed: `14-story-dashboard-assigned-work-and-customer-context-apis.md`.
- Story 15 completed: `15-story-ticket-tasks-and-reminders.md`.
- Story 16 completed: `16-story-shared-quick-replies-library.md`.
- Story 17 completed: `17-story-ticket-internal-notes-mentions-and-handoff.md`.

---

## Story Goal

Deliver end-to-end Agent Dashboard user experience in Angular and expose all required gateway routes so agents/admins can use assigned-ticket workflows, ticket context, tasks, quick replies, and internal collaboration from one workspace.

---

## Context - Read These Files First

1. `src/CustomerManagement.Gateway/ocelot.json` for route schema style.
2. `src/CustomerManagement.Gateway/ROUTES.md` for route documentation format.
3. `src/CustomerManagement.Ui/src/app/app.routes.ts` for Angular route guards and shell structure.
4. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` for HTTP and auth-refresh patterns.
5. `src/CustomerManagement.Ui/src/app/features/customer-management/*` for component organization and responsive layout conventions.

---

## Gateway Tasks

### 1. Add route mappings

File: `src/CustomerManagement.Gateway/ocelot.json`

Add upstream routes mapped to API service:

- `GET /api/dashboard/me/assigned-tickets`
- `GET /api/dashboard/me/open-tasks`
- `GET /api/dashboard/tickets/{ticketId}/customer-context`
- `GET /api/ticket-tasks`
- `POST /api/ticket-tasks`
- `PUT /api/ticket-tasks/{taskId}`
- `PUT /api/ticket-tasks/{taskId}/complete`
- `GET /api/quick-replies`
- `POST /api/quick-replies`
- `PUT /api/quick-replies/{quickReplyId}`
- `GET /api/ticket-notes`
- `POST /api/ticket-notes`
- `POST /api/ticket-notes/{noteId}/handoff-requests`
- `GET /api/ticket-notes/handoff-requests/me`
- `POST /api/ticket-notes/handoff-requests/{handoffRequestId}/respond`

For each route:

- Preserve JWT claims/headers exactly as existing authenticated routes.
- Keep downstream path templates and HTTP method constraints explicit.

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Document each new route in same table style used for existing entries.

### 2. Add gateway tests

Create/extend: `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs`

- Verify each upstream path reaches expected downstream path and preserves method.
- Verify unauthorized/forbidden responses are propagated.

Create/extend: `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs`

- Update expected route count.
- Assert no duplicate upstream method/path tuples.

---

## Frontend Tasks

### 1. Add dashboard route and shell

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add route: `/agent/dashboard` guarded by authenticated + agent-capable guard.

Create file: `src/CustomerManagement.Ui/src/app/features/agent-dashboard/agent-dashboard-page.component.ts`

Page layout sections:

- Left column:
  - Assigned tickets table.
  - Open tasks panel.
- Right column:
  - Selected ticket customer context.
  - Internal notes and mentions panel.
  - Quick-reply picker panel.

### 2. Add dashboard API service

Create file: `src/CustomerManagement.Ui/src/app/core/services/agent-dashboard-api.service.ts`

Methods:

- `getAssignedTickets(params)`
- `getOpenTasks(params)`
- `getTicketCustomerContext(ticketId)`
- `listTicketNotes(ticketId, page, pageSize)`
- `createTicketNote(request)`
- `createHandoffRequest(noteId, request)`
- `getMyHandoffRequests(params)`
- `respondToHandoffRequest(handoffRequestId, request)`
- `listQuickReplies(params)`
- `createQuickReply(request)` admin-only call path
- `updateQuickReply(id, request)` admin-only call path
- `createTicketTask(request)`
- `updateTicketTask(id, request)`
- `completeTicketTask(id, request)`

### 3. Build concrete component sections

Create components under `src/CustomerManagement.Ui/src/app/features/agent-dashboard/components/`:

- `assigned-tickets-table.component`
  - Columns: Ticket #, Subject, Customer, Priority, Status, Escalated, Updated (UTC).
  - Filters: status, priority, escalated toggle.
  - Sorting default: Updated desc.

- `open-tasks-list.component`
  - Rows: Description, Ticket #, Due (UTC), Created (UTC), Actions.
  - Action: Mark done.

- `customer-context-panel.component`
  - Blocks: Profile summary, primary contact, recent interactions list (type, summary, occurred).

- `ticket-notes-panel.component`
  - Thread list (newest first), compose box, mention multi-select (agent users only).
  - Post action supports optional "Mention and offer handoff" checkbox.

- `quick-reply-picker.component`
  - Search input, tag chips, list of active replies (title + preview).
  - Insert action emits selected reply body into parent compose area.

- `quick-reply-admin-panel.component`
  - Visible only for admin users.
  - Table columns: Title, Active, Updated (UTC), Actions.
  - Actions: add, edit, deactivate.

### 4. UX behavior contracts

- Dashboard initial load:
  - Calls assigned tickets and open tasks in parallel.
  - Auto-selects first ticket if available; then loads customer context and notes.
- Ticket selection change reloads only ticket-scoped panels, not full page.
- Mention flow:
  - Posting note with mentions shows per-user delivery indicator from API response.
  - Triggering agent can open handoff modal only when user selected "offer handoff" and has handoff-request permission.
  - Target assignee sees pending handoff inbox panel with `Accept` and `Reject` actions.
  - Accept assigns ticket to target assignee; Reject leaves assignment unchanged.
- Errors:
  - `409` shows conflict banner with reload CTA.
  - `403` shows permission message; hides blocked actions.

### 5. Visual consistency expectations

- Match typography density and spacing scale used by existing CRM pages.
- Provide responsive behavior for `>=1280px` two-column layout, `<1280px` stacked layout.
- Keep modern but consistent styling; avoid unstyled scaffold components.

---

## Edge Cases & Failure Modes

- No assigned tickets: render zero-state panel with guidance to pull unassigned tickets via ticket list page.
- Quick replies empty: show informational empty state, not error banner.
- Mention delivery false for one recipient: show non-blocking warning and keep note persisted.
- Handoff request responded by someone else first: surface conflict toast and reload request list.

---

## Test Plan

1. Add `agent-dashboard-api.service` tests for request/response and conflict mapping.
2. Add component tests:
  - assigned tickets render + selection.
  - task completion action updates list.
  - quick-reply insert emits expected text.
  - notes mention compose validation.
3. Add route guard test for `/agent/dashboard`.
4. Add gateway route tests for all dashboard/task/reply/note routes.

---

## Verification Steps

1. `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj /p:UseAppHost=false`
2. `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false`
3. From `src/CustomerManagement.Ui`: `npm test -- --watch=false --browsers=ChromeHeadless`
4. From `src/CustomerManagement.Ui`: `npm run build`

---

## Done Criteria

- [x] Gateway exposes documented and tested routes for all agent-dashboard APIs.
- [x] Angular route `/agent/dashboard` and component shell are implemented.
- [x] Assigned tickets, open tasks, customer context, notes/mentions, and quick replies are integrated.
- [x] Admin-only quick-reply management appears conditionally.
- [x] UI and route/service tests cover critical interactions.

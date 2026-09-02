# Story 19 - Ticket Conversation

## Prerequisites

- Story 13 completed: `../ticket-management/13-story-ticket-management.md`.
- Story 16 completed: `../agent-dashboard/16-story-shared-quick-replies-library.md`.
- Story 17 completed: `../agent-dashboard/17-story-ticket-internal-notes-mentions-and-handoff.md`.
- Story 18 completed: `../agent-dashboard/18-story-agent-dashboard-ui-and-gateway-routing.md`.

---

## Story Goal

Deliver a customer-visible ticket conversation thread where authenticated agents and customers can read ticket messages from the ticket module, while only agents/admins can post in this story.

Conversation baseline rule: the ticket creation description is the first customer-visible conversation comment and must be returned/rendered as the first chronological entry in every thread.

For Story 19 UX scope, quick replies are consumed through a compose helper UI (picker popup/dropdown) inside the ticket conversation compose section (agent/admin only) and are not rendered as a standalone quick-replies panel in dashboard context.

Out of scope for this story:

- Customer login/portal UI and customer-initiated posting flows.
- Real-time push/websocket updates.
- Message attachments.

Assumption for this story:

- Ticket conversation messages are immutable after posting (no edit/delete endpoints in this story).

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Program.cs` - review endpoint registration around `app.MapTicketEndpoints()`, `app.MapTicketNotesEndpoints()`, `app.MapQuickRepliesEndpoints()` (~lines 95-115) to register `MapTicketMessagesEndpoints()` in the same pattern.
2. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` - review existing ticket and internal-note mappings (~lines 444-670) to ensure new `TicketMessages` table is separate from `TicketInternalNotes` and `TicketInternalNoteMentions`.
3. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` - review ticket scoping and history patterns in `UpdateTicketStatusAsync`, `AssignTicketAsync`, and `GetTicketHistoryAsync` (~lines 350-770) to mirror visibility and history-write conventions.
4. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` and `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` - review permission constants and baseline role assignments (~lines 13-35 in `Permissions.cs`, ~lines 70-210 in `IdentitySeedData.cs`).
5. `src/CustomerManagement.Gateway/ocelot.json` and `src/CustomerManagement.Gateway/ROUTES.md` - follow authenticated route schema and contract table format; dashboard and ticket surfaces are defined around the route block that already includes `/api/tickets/*`, `/api/ticket-notes/*`, and `/api/quick-replies/*`.
6. `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` and `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs` - route count and override loop are currently synchronized at 36; update both when adding conversation routes.
7. `src/CustomerManagement.Ui/src/app/features/tickets/ticket-management-page.component.ts` and `src/CustomerManagement.Ui/src/app/features/tickets/ticket-management-page.component.html` - this is already accessible under `/tickets` for authenticated users; integrate conversation thread here so customers and agents can read ticket messages in current product surface.
8. `src/CustomerManagement.Ui/src/app/features/agent-dashboard/agent-dashboard-page.component.ts` and `src/CustomerManagement.Ui/src/app/features/agent-dashboard/components/ticket-notes-panel.component.ts` - keep agent-only internal notes section clearly separated from customer-visible conversation where both surfaces coexist for agent workflows; do not add standalone quick-reply panel for Story 19 conversation.
9. Attachment reference: `.squad/stories/ticket-conversation/ticket-conversation/attachments/conversation-thread-reference.png` - implement the message list as avatar + sender/timestamp line + body text, stacked vertically without chat bubbles.

---

## Backend Tasks

### 1. Add conversation domain model (separate from internal notes)

Create file: `src/CustomerManagement.Api/Domain/Tickets/TicketMessage.cs`

- Fields:
  - `Guid Id`
  - `Guid TicketId` (required FK -> `Ticket`)
  - `TicketMessageSenderType SenderType` (required enum, values: `Customer = 1`, `Agent = 2`)
  - `Guid SenderUserId` (required FK -> `ApplicationUser`; every message must be attributable to a real person)
  - `string SenderDisplayName` (required, max 200; snapshot at send time)
  - `string Body` (required, max 4000)
  - `DateTime CreatedAtUtc`
  - `byte[] RowVersion` (`[Timestamp]`)
- Navigation:
  - `Ticket Ticket`
  - `ApplicationUser SenderUser`

Create file: `src/CustomerManagement.Api/Domain/Tickets/TicketMessageSenderType.cs`

- Enum values:
  - `Customer = 1`
  - `Agent = 2`

Explicit separation rule:

- Do not add visibility flags or shared-table polymorphism with `TicketInternalNote`.
- Conversation and internal notes remain separate entities, tables, and endpoints.

Sender identity invariant:

- `SenderUserId` is non-nullable at domain and DB levels.
- Every row must reference an existing `Users` row.
- Endpoint validation must reject writes if actor user id cannot be resolved.

### 2. Add persistence mapping and migration

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<TicketMessage> TicketMessages`.
- Map table `TicketMessages` with:
  - PK: `Id`
  - `SenderType` as int conversion
  - `SenderDisplayName` max 200 required
  - `Body` max 4000 required
  - `CreatedAtUtc` required
  - `RowVersion` rowversion
  - FK `TicketId -> Tickets(Id)` with `DeleteBehavior.Cascade`
  - FK `SenderUserId -> Users(Id)` with `DeleteBehavior.Restrict`
- Indexes:
  - `(TicketId, CreatedAtUtc, Id)` for chronological paging
  - `(SenderUserId, CreatedAtUtc)`

Create migration under `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/`.

### 3. Add permissions and role assignments

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `TicketMessagesRead = "ticket-messages.read"`
  - `TicketMessagesWrite = "ticket-messages.write"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Add permission descriptions to baseline permission dictionary.
- Role assignments:
  - Admin: include both new permissions.
  - Agent: include both new permissions.
  - Customer: include `ticket-messages.read` only.

### 4. Add contracts for message list/post APIs

Create folder: `src/CustomerManagement.Api/Contracts/TicketMessages/`

Create file: `TicketMessageContracts.cs` with records:

```csharp
namespace CustomerManagement.Api.Contracts.TicketMessages;

public sealed record TicketMessageResponse(
    Guid Id,
    Guid TicketId,
    string SenderType,
  Guid SenderUserId,
    string SenderDisplayName,
    string Body,
    DateTime CreatedAtUtc,
    byte[] RowVersion);

public sealed record TicketMessageListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TicketMessageResponse> Items);

public sealed record CreateTicketMessageRequest(
    Guid TicketId,
    string Body);

public sealed record CreateTicketMessageResponse(
    TicketMessageResponse Message);
```

### 5. Implement ticket-message endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Tickets/TicketMessagesEndpoints.cs`

Route group: `/api/ticket-messages`

- `GET /api/ticket-messages`
  - Authorization: `RequireAuthorization("Permission:" + Permissions.TicketMessagesRead)`
  - Query params:
    - `Guid ticketId` (required)
    - `int page = 1`
    - `int pageSize = 20` (max 100)
  - Behavior:
    - Validate params and ticket existence.
    - Include the ticket creation description as the first conversation entry before subsequent posted `TicketMessages` rows.
    - Apply ticket visibility scope:
      - Admin: all tickets.
      - Agent: only if ticket is assigned to current agent OR unassigned.
      - Customer: only tickets linked to their owned customer profile (same ownership-scoped behavior used in ticket endpoints).
    - Return messages ordered oldest-to-newest (`CreatedAtUtc`, then `Id`).
  - Responses:
    - `200 OK` with `TicketMessageListResponse`
    - `400 BadRequest` for invalid query values
    - `403 Forbid` when user identity missing or unauthorized for ticket
    - `404 NotFound` when ticket does not exist

- `POST /api/ticket-messages`
  - Authorization: `RequireAuthorization("Permission:" + Permissions.TicketMessagesWrite)`
  - Body: `CreateTicketMessageRequest`
  - Behavior:
    - Validate `ticketId` non-empty.
    - Validate trimmed `body` non-empty and `<= 4000`.
    - Enforce ticket visibility scope (same as GET).
    - Allow posting only for admin/agent actors in this story.
    - Resolve current user identity and display name from `Users` table.
    - Persist message with:
      - `SenderType = Agent`
      - `SenderUserId = current user id`
      - `SenderDisplayName = DisplayName` fallback to email
    - Add ticket history entry:
      - `ActionType = "ticket.message.posted"`
      - `FieldName = "messageId"`
      - `OldValue = null`
      - `NewValue = message.Id.ToString()`
      - Actor metadata from claims
    - Never overwrite or mutate the original ticket description entry.
  - Responses:
    - `201 Created` with `CreateTicketMessageResponse`
    - `400 BadRequest` for invalid body/ticket id
    - `403 Forbid` for scope/identity mismatch or customer attempting to post
    - `404 NotFound` when ticket or sender user missing

Register endpoint in `src/CustomerManagement.Api/Program.cs` after `app.MapTicketEndpoints();` and before dashboard mappings.

### 6. Keep internal notes and conversation completely isolated

Guardrails to enforce in code:

- Do not query `TicketInternalNotes` in `TicketMessagesEndpoints`.
- Do not expose `TicketMessages` through `/api/ticket-notes`.
- Keep separate DTO namespaces:
  - `Contracts.TicketNotes`
  - `Contracts.TicketMessages`

---

## Gateway Tasks

### 1. Add gateway route mapping

File: `src/CustomerManagement.Gateway/ocelot.json`

- Add route:
  - Upstream: `/api/ticket-messages`
  - Downstream: `/api/ticket-messages`
  - Methods: `GET`, `POST`
  - Keep `AuthenticationProviderKey = "Bearer"`
  - Keep forwarded headers `X-User-Id`, `X-User-Email`

### 2. Update route contract docs

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Add route row under ticket or dashboard contract section for `/api/ticket-messages` with methods `GET, POST`.
- Purpose text must explicitly say customer-visible ticket conversation messages (distinct from internal notes).

### 3. Update gateway tests

Files:

- `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs`
- `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs`
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs`
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/DownstreamStubServer.cs`

Required updates:

- Route count from `36` to `37`.
- Add route assertion for `/api/ticket-messages` with `GET`, `POST`.
- Add routing theory cases for `GET /api/ticket-messages?ticketId=...` and `POST /api/ticket-messages`.
- Increase factory override loop from `36` to `37`.
- Stub permission resolver: `/api/ticket-messages` should require `ticket-messages.read` for route tests.

---

## Frontend Tasks

### 1. Add conversation models and API service methods

File: `src/CustomerManagement.Ui/src/app/core/models/ticket-management.models.ts`

- Add interfaces:
  - `TicketMessageResponse`
  - `TicketMessageListResponse`
  - `CreateTicketMessageRequest`
  - `CreateTicketMessageResponse`

File: `src/CustomerManagement.Ui/src/app/core/services/ticket-management-api.service.ts`

- Add methods:
  - `listTicketMessages(ticketId: string, page?: number, pageSize?: number)`
  - `createTicketMessage(request: CreateTicketMessageRequest)`
- Reuse existing auth header + `withAutoRefresh` + `mapError` pattern.
- Base endpoint path: `http://localhost:5101/api/ticket-messages`.

### 2. Add dedicated conversation panel component

Create file: `src/CustomerManagement.Ui/src/app/features/agent-dashboard/components/ticket-conversation-panel.component.ts`

Requirements from reference image:

- Vertical list layout, one row per message.
- Row structure:
  - Circular avatar image at left.
  - Sender display name + timestamp on one line.
  - Body text below.
- No chat bubbles and no left/right split lanes.
- Consistent spacing between entries.
- Empty state text when no messages.
- Timestamp format in UI: date + time on sender line (for example `05-08-2026 10:38 am`) to match attachment intent.

Compose area in same component:

- Multiline message textbox.
- Quick-reply helper trigger in compose area (agent/admin only), implemented as a picker popup or anchored dropdown.
- Helper list shows quick-reply title and short preview, supports choosing one item, and inserts/appends selected body text into compose input.
- Helper closes after selection and returns focus to compose input.
- Send button.
- Loading and inline validation states.

Create parallel reusable component for ticket module consumption:

- `src/CustomerManagement.Ui/src/app/features/tickets/components/ticket-conversation-panel.component.ts`
- Shared UI rules must match attachment layout exactly.

### 3. Integrate conversation panel into ticket module (primary) and agent dashboard (secondary)

Files:

- `src/CustomerManagement.Ui/src/app/features/tickets/ticket-management-page.component.ts`
- `src/CustomerManagement.Ui/src/app/features/tickets/ticket-management-page.component.html`
- `src/CustomerManagement.Ui/src/app/features/agent-dashboard/agent-dashboard-page.component.ts`

- Ticket module (`/tickets`) integration:
  - Add `ticketMessages` signal and paging state on selected ticket.
  - Load conversation messages when ticket selection changes.
  - Render conversation panel for all authenticated users who can view selected ticket.
  - Show compose/send UI only for agent/admin roles.
  - Load active quick replies only for agent/admin and expose them through the compose helper popup/dropdown.
- Agent dashboard integration:
  - Keep conversation panel and internal notes visibly separate when both are shown to agent/admin.
  - Internal: "Internal Notes & Mentions (Agent-only)"
  - Conversation: "Customer-visible Conversation"
  - If conversation compose exists in dashboard, quick replies must be available via the same compose helper popup/dropdown (no separate quick-reply picker panel for this story).

Visibility location contract:

- Agent side (in this story): can view conversation in ticket module and agent dashboard; can send from both surfaces where compose is enabled.
- Customer side (in this story): can view all messages for owned tickets in ticket module (`/tickets`) as read-only.
- Future customer portal (out of scope): will consume the same `/api/ticket-messages` list contract.

### 4. Distinguish internal notes from customer-visible conversation in UI

Files:

- `src/CustomerManagement.Ui/src/app/features/agent-dashboard/components/ticket-notes-panel.component.ts`
- `src/CustomerManagement.Ui/src/app/features/agent-dashboard/agent-dashboard-page.component.ts`

UI contract:

- Internal notes section must show an explicit "Agent-only" label.
- Conversation section must show explicit "Visible to customer" label.
- Quick-reply helper/popup is available only as compose assist (agent/admin), must not auto-post, and must not appear as a standalone panel in Story 19 scope.
- Helper interaction contract:
  - Open helper from compose area via button/icon.
  - Filter list by title/tags using a small search field (optional but recommended for long lists).
  - Insert selected text at cursor position when possible; if cursor position is unavailable, append to end with one newline separator.

---

## Edge Cases & Failure Modes

- Ticket id missing in list/send requests: return `400` with validation detail; UI shows inline form error and does not post.
- Agent tries to access ticket not assigned to them and not unassigned: return `403`; UI shows permission message.
- Customer tries to access messages for a ticket outside owned customer scope: return `404`/not visible, consistent with ticket endpoint scoping.
- Ticket not found: return `404`; UI clears thread and shows not-found state.
- Long threads (`> pageSize`): `GET /api/ticket-messages` returns deterministic chronological slices; UI supports loading more pages without duplicates.
- Ticket description bootstrap: if no posted `TicketMessages` rows exist yet, thread still returns one entry derived from ticket description as first comment.
- Internal-note leakage risk: no endpoint or query path may return `TicketInternalNote` rows from conversation APIs.
- Empty or whitespace-only message body: blocked both client-side and server-side.
- Concurrency on rapid sends: immutable message model avoids update conflicts; each successful post produces exactly one `TicketMessage` row and one `ticket.message.posted` history entry.

---

## Test Plan

1. Create API tests in `tests/CustomerManagement.Api.Tests/TicketMessagesEndpointsTests.cs`:
  - list includes ticket description as the first chronological comment.
   - list returns chronological order with paging.
   - agent can post message on assigned ticket.
   - agent can post message on unassigned ticket.
   - agent forbidden on ticket assigned to another agent.
  - customer can read messages for owned ticket.
  - customer cannot read messages for non-owned ticket.
  - customer cannot post messages in this story.
   - message post writes `ticket.message.posted` history entry.
   - internal notes are not returned from message endpoint.
2. Extend permission-seeding/auth tests as needed:
   - `ticket-messages.read` and `ticket-messages.write` are seeded and assigned to admin/agent roles.
3. Extend gateway tests:
   - route contract count +1.
   - route forwarding and auth propagation for `/api/ticket-messages` GET/POST.
4. Frontend tests:
   - `ticket-management-api.service.spec.ts` for new methods and error mapping.
  - ticket module component/page tests for conversation rendering for customer role (read-only) and agent role (send enabled).
  - ticket module component/page tests verify quick-reply helper visibility for agent/admin and hidden state for customer role.
  - ticket module component/page tests verify helper open/close behavior and selection inserts/appends expected text into compose input.
  - `agent-dashboard-components.spec.ts` for conversation panel rendering and send validation where dashboard integration exists.
   - page-level test ensures conversation and internal notes are both rendered and clearly labeled as distinct sections.

---

## Migration / Rollback

- Migration adds `TicketMessages` table and indexes only.
- Rollback plan:
  1. Remove gateway route from `ocelot.json` and `ROUTES.md`.
  2. Revert API endpoint mapping from `Program.cs`.
  3. Roll back EF migration (drop `TicketMessages`).
  4. Revert frontend panel/service/model changes.
- Half-applied risk:
  - If DB migration is applied but gateway route is missing, API works directly but UI/gateway calls fail with 404.
  - If gateway route exists without API deployment, gateway returns downstream 404/502.

---

## Verification Steps

1. **Backend builds:**
   - From repo root: `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`
2. **Backend tests:**
   - From repo root: `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~TicketMessagesEndpointsTests"`
3. **Gateway regression:**
   - From repo root: `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj /p:UseAppHost=false`
4. **Frontend tests:**
   - From `src/CustomerManagement.Ui`: `npm test -- --watch=false --browsers=ChromeHeadless`
5. **Frontend build:**
   - From `src/CustomerManagement.Ui`: `npm run build`

---

## Done Criteria

- [ ] Conversation messages are stored in a dedicated `TicketMessages` table and never mixed with `TicketInternalNotes`.
- [ ] Ticket creation description is always exposed as the first conversation comment.
- [ ] Agent/admin can list ticket messages chronologically with paging via `/api/ticket-messages`.
- [ ] Customer can list ticket messages for owned tickets via `/api/ticket-messages` from ticket module.
- [ ] Agent/admin can post customer-visible messages via `/api/ticket-messages`.
- [ ] Customer cannot post ticket messages in this story.
- [ ] Ticket visibility scoping is enforced: agents only on assigned/unassigned tickets; admin unrestricted.
- [ ] New message permissions are seeded and enforced through existing permission policies.
- [ ] Gateway exposes and documents `/api/ticket-messages` GET/POST and tests pass with updated route counts.
- [ ] Ticket module shows conversation thread for both agent and customer, with customer read-only compose behavior.
- [ ] Agent-side UI includes a clearly labeled customer-visible conversation section distinct from internal notes wherever both appear.
- [ ] Quick replies can be inserted through a compose helper popup/dropdown (agent/admin only) without duplicating quick-reply storage.
- [ ] Story 19 does not introduce a standalone quick-replies display panel for conversation flows.
- [ ] Posting a conversation message writes a ticket history entry (`ticket.message.posted`) for auditability.
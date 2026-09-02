# Story 17 - Ticket Internal Notes Mentions And Handoff

## Prerequisites

- Story 15 completed: `15-story-ticket-tasks-and-reminders.md`.
- Story 13 completed: `../ticket-management/13-story-ticket-management.md`.

---

## Story Goal

Enable ticket-internal collaboration: private notes visible only to agent/admin users, `@mention` teammate notifications, and explicit optional ticket reassignment handoff after posting a mention, including assignee-side accept/reject.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` for assignment endpoint behavior.
2. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` and role/permission enforcement patterns.
3. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` for entity mapping and rowversion style.
4. `src/CustomerManagement.Api/Domain/Security/*` for user identity linkage.

---

## Backend Tasks

### 1. Add internal-note and mention entities

Create file: `src/CustomerManagement.Api/Domain/Dashboard/TicketInternalNote.cs`

Fields:

- `Guid Id`
- `Guid TicketId` (required FK -> `Ticket`)
- `Guid AuthorUserId` (required FK -> `ApplicationUser`)
- `string Body` (required, max `4000`)
- `DateTime CreatedAtUtc`
- `DateTime UpdatedAtUtc`
- `byte[] RowVersion` (`[Timestamp]`)

Create file: `src/CustomerManagement.Api/Domain/Dashboard/TicketInternalNoteMention.cs`

Fields:

- `Guid Id`
- `Guid TicketInternalNoteId` (required FK)
- `Guid MentionedUserId` (required FK -> `ApplicationUser`)
- `DateTime MentionedAtUtc`
- `bool NotificationDelivered`
- `DateTime? NotificationDeliveredAtUtc`
- Unique index on `(TicketInternalNoteId, MentionedUserId)`.

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<TicketInternalNote>` and `DbSet<TicketInternalNoteMention>`.
- Map tables: `TicketInternalNotes`, `TicketInternalNoteMentions`.
- Indexes:
  - Notes: `(TicketId, CreatedAtUtc)`.
  - Mentions: `(MentionedUserId, MentionedAtUtc)`, unique composite above.
- Rowversion on `TicketInternalNote`.

### 2. Add permissions and seeding

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `TicketInternalNotesRead = "ticket-notes.read"`
  - `TicketInternalNotesWrite = "ticket-notes.write"`
  - `TicketMentionsNotify = "ticket-mentions.notify"`
  - `TicketHandoffRequestCreate = "ticket-handoff.request.create"`
  - `TicketHandoffRespond = "ticket-handoff.respond"`
  - `TicketHandoffForceAssign = "ticket-handoff.force-assign"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Seed these permissions.
- Assign:
  - `admin`: all.
  - `agent`: `ticket-notes.read`, `ticket-notes.write`, `ticket-mentions.notify`, `ticket-handoff.request.create`, `ticket-handoff.respond`.
  - `customer`: none.

### 3. Add contracts

Create folder: `src/CustomerManagement.Api/Contracts/TicketNotes/`

- `CreateTicketInternalNoteRequest`
  - `Guid TicketId`
  - `string Body`
  - `Guid[] MentionedUserIds`
  - `bool OfferReassign`
  - `Guid? ReassignToUserId`
- `TicketInternalNoteResponse`
  - `Guid Id`
  - `Guid TicketId`
  - `Guid AuthorUserId`
  - `string AuthorDisplayName`
  - `string Body`
  - `DateTime CreatedAtUtc`
  - `DateTime UpdatedAtUtc`
  - `byte[] RowVersion`
  - `IReadOnlyList<TicketInternalNoteMentionResponse> Mentions`
- `TicketInternalNoteMentionResponse`
  - `Guid MentionedUserId`
  - `string MentionedDisplayName`
  - `DateTime MentionedAtUtc`
  - `bool NotificationDelivered`
- `TicketInternalNoteListResponse`
  - `int TotalCount`
  - `IReadOnlyList<TicketInternalNoteResponse> Items`
- `CreateTicketHandoffRequest`
  - `Guid NoteId`
  - `Guid TicketId`
  - `Guid TargetAssigneeUserId`
  - `string? Message`
  - `byte[] TicketRowVersion`
- `TicketHandoffRequestResponse`
  - `Guid Id`
  - `Guid TicketId`
  - `Guid NoteId`
  - `Guid RequestedByUserId`
  - `Guid TargetAssigneeUserId`
  - `string Status` (`Pending`, `Accepted`, `Rejected`, `Cancelled`)
  - `string? ResponseMessage`
  - `DateTime RequestedAtUtc`
  - `DateTime? RespondedAtUtc`
- `RespondTicketHandoffRequest`
  - `bool Accept`
  - `string? Message`
  - `byte[] TicketRowVersion`

### 4. Implement internal-note endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Dashboard/TicketNotesEndpoints.cs`

Route group: `/api/ticket-notes`

- `GET /api/ticket-notes`
  - Permission: `ticket-notes.read`.
  - Query: `ticketId` required, `page`, `pageSize`.
  - Returns notes newest-first.
  - `200`, `400`, `401`, `403`, `404` (ticket missing or out-of-scope).

- `POST /api/ticket-notes`
  - Permission: `ticket-notes.write`.
  - Validations:
    - Ticket exists and caller can view ticket.
    - Mention targets must exist and have agent role.
    - Caller cannot mention self.
    - Mention list distinct max `10` users.
    - `OfferReassign=true` requires `ReassignToUserId` present and included in mentions.
  - Behavior:
    - Create note.
    - Create mention rows.
    - Trigger in-app notifications (assumption below).
    - If `OfferReassign=true`, return `201` note created plus `reassignProposal` payload; do not mutate ticket assignment automatically.
  - `201`, `400`, `401`, `403`, `404`, `409` (duplicate mention pair race).

- `POST /api/ticket-notes/{noteId:guid}/handoff-requests`
  - Permission: `ticket-handoff.request.create`.
  - Creates a pending handoff request for an eligible teammate agent.
  - Preconditions:
    - Note exists.
    - Note belongs to ticket.
    - `TargetAssigneeUserId` exists, is active, has agent role.
    - `TargetAssigneeUserId` is not the requester.
  - Behavior:
    - Persist `Pending` handoff request.
    - Create in-app notification for target assignee (`Type = "ticket_handoff_request"`).
    - Write ticket history entry (`ActionType = "ticket.handoff.requested"`) with target assignee transition context.
    - Does not change ticket assignee.
  - Responses: `201`, `400`, `401`, `403`, `404`, `409`.

- `GET /api/ticket-notes/handoff-requests/me`
  - Permission: `ticket-handoff.respond`.
  - Returns pending/recent handoff requests where current user is target assignee.
  - Query: `status?`, `page`, `pageSize`.
  - Responses: `200`, `400`, `401`, `403`.

- `POST /api/ticket-notes/handoff-requests/{handoffRequestId:guid}/respond`
  - Permission: `ticket-handoff.respond`.
  - Request: `RespondTicketHandoffRequest`.
  - Preconditions:
    - Current user is the target assignee or has `ticket-handoff.force-assign` (admin override).
    - Request is still `Pending`.
  - Behavior:
    - If `Accept=true`: set status `Accepted`, assign ticket to target assignee using same assignment rules/path, record response metadata.
    - If `Accept=false`: set status `Rejected`, keep current assignee unchanged, record response metadata.
    - Always write ticket history entry (`ActionType = "ticket.handoff.responded"`) with `Pending -> Accepted/Rejected` status change.
    - On accept, also write assignment history entry (`ActionType = "ticket.assigned"`, `assignedToUserId` old/new).
  - Responses:
    - `200` with `TicketHandoffRequestResponse` and updated ticket summary for accepted case.
    - `400` invalid state transition.
    - `403` forbidden responder.
    - `404` request or ticket not found.
    - `409` stale rowversion or non-pending request conflict.

### 5. Explicit mention and handoff notification persistence (required)

This story MUST persist in-app notifications as part of completion scope.

- Create and map `UserNotification` entity/table with fields:
  - `Guid Id`
  - `string Type`
  - `Guid RecipientUserId`
  - `string PayloadJson`
  - `DateTime CreatedAtUtc`
  - `DateTime? ReadAtUtc`
- Register `DbSet<UserNotification>` in `CustomerManagementDbContext`.
- For each note mention, create one notification row with:
  - `Type = "ticket-note-mention"`
  - `RecipientUserId = mentioned user`
  - `PayloadJson` containing at minimum: `ticketId`, `noteId`, `mentionedByUserId`, `mentionedUserId`, `createdAtUtc`.
- For each handoff request creation, create one notification row with:
  - `Type = "ticket-handoff-request"`
  - `RecipientUserId = target assignee`
  - `PayloadJson` containing at minimum: `handoffRequestId`, `ticketId`, `noteId`, `requestedByUserId`, `targetAssigneeUserId`, `createdAtUtc`.
- Notification channel remains in-app only for this story; email/push are out of scope.

### 6. Explicit handoff behavior split

- Triggering agent can request handoff.
- Target agent can accept or reject.
- Admin can override with force-assign permission when required for operations continuity.
- Mention alone never reassigns, and creating a handoff request also never reassigns until accepted/forced.

---

## Edge Cases & Failure Modes

- Mentioned user loses agent role between note create and handoff call: handoff returns `400` with `assignee_not_eligible`.
- Customer-authenticated caller attempts access: always `403`.
- Note body includes `@text` that does not map to explicit `MentionedUserIds`: keep plain text, no implicit mention creation.
- Target assignee rejects handoff after note creation: ticket remains with current assignee and request status is `Rejected`.
- Two responders race on same request: first valid response wins; second gets `409` non-pending conflict.
- Handoff requested/replied actions missing from ticket timeline: considered implementation bug; Story 17 requires persistent history entries for request + response (+ assignment on accept).

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/TicketNotesEndpointsTests.cs`.
2. Cover note create/list with valid mentions.
3. Verify mention target role checks and max mention limits.
4. Verify in-app notification records are created.
5. Verify handoff request create does not auto-assign.
6. Verify target agent can accept and gets assignment applied.
7. Verify target agent can reject and assignment remains unchanged.
8. Verify admin force-assign override path.
9. Verify ticket history entries are written for handoff request, response, and assignment on accept.

---

## Verification Steps

1. `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`
2. `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~TicketNotesEndpoints"`

---

## Done Criteria

- [x] Internal notes are persisted and scoped to agent/admin visibility only.
- [x] Mentions are explicit by user id and generate persisted in-app notification records.
- [x] Reassignment remains explicit, never automatic from mention creation or handoff request creation.
- [x] Handoff supports target-agent accept/reject with deterministic status transitions.
- [x] Handoff request creation generates persisted in-app notification records for the target assignee.
- [x] Tests verify separation between mention, handoff request, and assignment side effects.
- [x] Handoff lifecycle is auditable through `TicketHistoryEntries` with request/respond/assignment records.

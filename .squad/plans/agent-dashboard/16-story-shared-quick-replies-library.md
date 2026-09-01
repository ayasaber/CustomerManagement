# Story 16 - Shared Quick Replies Library

## Prerequisites

- Story 15 completed: `15-story-ticket-tasks-and-reminders.md`.
- Story 08 completed: `../security-administration/08-story-permission-model-and-policy-administration.md`.

---

## Story Goal

Introduce an admin-managed shared quick-reply library that agents can browse and insert into ticket responses, with soft-deactivation and optimistic concurrency.

---

## Context - Read These Files First

1. `src/CustomerManagement.Api/Endpoints/Admin/PermissionsEndpoints.cs` for admin-managed catalog endpoint style.
2. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` for unique index and rowversion patterns.
3. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` for permission constants.

---

## Backend Tasks

### 1. Add quick-reply entity and mapping

Create file: `src/CustomerManagement.Api/Domain/Dashboard/QuickReply.cs`

Fields:

- `Guid Id`
- `string Title` (required, max `120`)
- `string Body` (required, max `4000`)
- `string? TagsCsv` (optional, max `500`)
- `bool IsActive`
- `Guid CreatedByUserId` (required)
- `Guid? UpdatedByUserId`
- `DateTime CreatedAtUtc`
- `DateTime UpdatedAtUtc`
- `byte[] RowVersion` (`[Timestamp]`)

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `DbSet<QuickReply> QuickReplies`.
- Map table `QuickReplies`.
- Indexes:
  - unique on normalized `Title` (case-insensitive behavior).
  - `(IsActive, Title)`.
- Apply `IsRowVersion()`.

### 2. Add permissions and seeding

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `QuickRepliesRead = "quick-replies.read"`
  - `QuickRepliesManage = "quick-replies.manage"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Seed both permissions.
- Assign:
  - `admin`: read + manage.
  - `agent`: read only.
  - `customer`: none.
- Admin management scope in this story:
  - Admin can add quick reply (`POST`).
  - Admin can edit quick reply (`PUT`).
  - Admin can deactivate/activate quick reply (`PUT IsActive`).
  - Admin can query active + inactive (`GET activeOnly=false`).
  - Agent cannot call manage endpoints and receives `403`.

### 3. Add contracts

Create folder: `src/CustomerManagement.Api/Contracts/QuickReplies/`

- `CreateQuickReplyRequest`
  - `string Title`
  - `string Body`
  - `string[]? Tags`
- `UpdateQuickReplyRequest`
  - `string Title`
  - `string Body`
  - `string[]? Tags`
  - `bool IsActive`
  - `byte[] RowVersion`
- `QuickReplyResponse`
  - full shape including `IsActive`, timestamps, rowversion.
- `QuickReplyListResponse`
  - `int TotalCount`
  - `IReadOnlyList<QuickReplyResponse> Items`

### 4. Implement quick-reply endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Dashboard/QuickRepliesEndpoints.cs`

Route group: `/api/quick-replies`

- `GET /api/quick-replies`
  - Permission: `quick-replies.read`.
  - Query params:
    - `activeOnly=true|false` default `true`.
    - `search?` title/body contains.
    - `tag?` optional exact tag filter.
    - `page`, `pageSize`.
  - Agent callers always receive active rows only, regardless of `activeOnly` input.
  - Admin callers can include inactive by passing `activeOnly=false`.
  - `200`, `400`, `401`, `403`.

- `POST /api/quick-replies`
  - Permission: `quick-replies.manage`.
  - Admin-only by permission mapping.
  - `201` created.
  - `409` on duplicate title.

- `PUT /api/quick-replies/{quickReplyId:guid}`
  - Permission: `quick-replies.manage`.
  - Supports edit and soft deactivate (`IsActive=false`); no hard delete endpoint.
  - `200` success.
  - `404` missing row.
  - `409` stale rowversion or duplicate title.

Validation:

- `Title` required, trimmed, max `120`.
- `Body` required, max `4000`.
- `Tags` each max `50`, up to `20` tags.

### 5. Add "usage snapshot" contract for ticket replies

Create file: `src/CustomerManagement.Api/Contracts/QuickReplies/QuickReplyUsageSnapshot.cs`

- `Guid QuickReplyId`
- `string TitleAtUse`
- `string BodyAtUse`
- `DateTime UsedAtUtc`
- `Guid UsedByUserId`

Implementation note for later ticket-reply integration:

- When a quick reply is inserted into a response draft/send action, store snapshot fields on that message entity so later deactivation/edits do not change historical sent content.

---

## Edge Cases & Failure Modes

- Deactivated reply requested by id for agent caller: return `404` to avoid exposing inactive content.
- Massive tag payload: return `400` with explicit `tags_limit_exceeded` error code.

---

## Test Plan

1. Add `tests/CustomerManagement.Api.Tests/QuickRepliesEndpointsTests.cs`.
2. Cover list behavior split (agent active-only vs admin include inactive).
3. Cover create/update/deactivate and duplicate title conflicts.
4. Cover rowversion conflict and validation limits.
5. Cover authorization split: agent forbidden on `POST/PUT`, admin allowed.

---

## Verification Steps

1. `dotnet build src/CustomerManagement.Api/CustomerManagement.Api.csproj /p:UseAppHost=false`
2. `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj /p:UseAppHost=false --filter "FullyQualifiedName~QuickRepliesEndpoints"`

---

## Done Criteria

- [x] Shared quick-reply entity and endpoints exist with soft deactivation only.
- [x] Permission model allows admin manage, agent read-only.
- [x] List/filter contracts are explicit and tested.
- [x] Historical usage snapshot contract is defined for downstream message persistence.

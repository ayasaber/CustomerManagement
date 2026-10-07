# Story 22 — Customer Feedback API

## Prerequisites

- None at the domain level (feedback is a standalone entity). The customer-submission flow assumes a logged-in customer already has a linked `Customer` row, which is the same precondition `TicketEndpoints.CreateTicketAsync` relies on today (see `ResolveCreateCustomerIdAsync` in `../customer-portal/20-story-customer-ticket-self-service-api.md`'s context, or directly in `TicketEndpoints.cs`).

---

## Story Goal

1. Let a logged-in customer submit feedback (a 1–5 rating plus an optional written comment) at any time, independent of any ticket.
2. Let admins and agents read submitted feedback.

Feedback is always tied to the submitting customer's `Customer` record — there is no anonymous option (per intake's stated assumption). Feedback is read-only once submitted: no edit/delete endpoint, and no workflow for responding to it (explicitly out of scope per intake).

Out of scope for this story:

- Any UI work (covered by Story 23).
- Gateway routing for the new endpoints (covered by Story 23).
- Any process for acting on, routing, or replying to feedback.

---

## Context — Read These Files First

1. `src/CustomerManagement.Api/Domain/Customers/Customer.cs` — `Id`, `ApplicationUserId` (nullable FK to `ApplicationUser`), used to resolve "the logged-in customer's own `Customer` row" the same way ticket creation does.
2. `src/CustomerManagement.Api/Endpoints/Tickets/TicketEndpoints.cs` — `ResolveOwnedCustomerIdsAsync` (~lines 1104–1121) and `ResolveCreateCustomerIdAsync`'s customer branch (~lines 1123–1140) as the precedent for resolving "current customer's own `Customer.Id`" from `httpContext.User`; duplicate this small lookup locally in the new endpoints file rather than sharing it, consistent with how `TicketMessagesEndpoints.cs` already duplicates `IsCustomer`/`ResolveActorUserId` instead of referencing `TicketEndpoints.cs`.
3. `src/CustomerManagement.Api/Domain/Tickets/TicketMessage.cs` and its `CustomerManagementDbContext` mapping (~line 645) — closest precedent for a simple entity with a required FK plus an immutable, append-only write pattern (no update/delete endpoint).
4. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` and `IdentitySeedData.cs` — constant/assignment conventions (same files touched in Stories 20 and 21).
5. `src/CustomerManagement.Api/Program.cs` — endpoint registration list/order.
6. `src/CustomerManagement.Api/Contracts/Tickets/TicketContracts.cs` — `TicketMessageListResponse`-equivalent pagination shape precedent lives in `src/CustomerManagement.Api/Contracts/TicketMessages/TicketMessageContracts.cs` (`TicketMessageListResponse(int Page, int PageSize, int TotalCount, IReadOnlyList<...> Items)`); reuse that same paging response shape for the feedback list.

---

## Backend Tasks

### 1. Add `Feedback` domain entity

Create file: `src/CustomerManagement.Api/Domain/Feedback/Feedback.cs`

```csharp
namespace CustomerManagement.Api.Domain.Feedback;

public sealed class Feedback
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Domain.Customers.Customer Customer { get; set; } = null!;
}
```

No `RowVersion`/`Timestamp` field — feedback rows are never updated after creation, so there is no concurrency surface to protect.

### 2. Add persistence mapping and migration

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `public DbSet<Feedback.Feedback> FeedbackEntries => Set<Feedback.Feedback>();` (name the property `FeedbackEntries` to avoid colliding with the `Feedback` type name) near the other domain `DbSet` declarations.
- Add a mapping block:
  - `ToTable("Feedback")`
  - PK `Id`
  - `Rating` required (store as `int`; range `1`–`5` enforced at the endpoint validation layer, not via a DB check constraint, consistent with how this codebase enforces most business rules in the endpoint rather than the schema)
  - `Comment` max 2000, optional
  - `CreatedAtUtc` required
  - FK `CustomerId -> Customers(Id)` with `DeleteBehavior.Cascade`
  - Index on `(CustomerId, CreatedAtUtc)`

Create a migration under `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/` for the new `Feedback` table.

### 3. Add permissions and role assignments

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `FeedbackSubmit = "feedback.submit"`
  - `FeedbackRead = "feedback.read"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Add to `baselinePermissions`:
  - `[Permissions.FeedbackSubmit] = "Submit customer experience feedback."`
  - `[Permissions.FeedbackRead] = "Read submitted customer feedback."`
- Update `assignments`:
  - `AuthRoles.Admin`: add `Permissions.FeedbackRead`.
  - `AuthRoles.Agent`: add `Permissions.FeedbackRead`.
  - `AuthRoles.Customer`: add `Permissions.FeedbackSubmit`.

### 4. Add contracts

Create file: `src/CustomerManagement.Api/Contracts/Feedback/FeedbackContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.Feedback;

public sealed record CreateFeedbackRequest(int Rating, string? Comment);

public sealed record FeedbackResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc);

public sealed record FeedbackListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FeedbackResponse> Items);
```

### 5. Implement feedback endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Feedback/FeedbackEndpoints.cs`

Route group: `/api/feedback`

- `POST /api/feedback`
  - `.RequireAuthorization("Permission:" + Permissions.FeedbackSubmit)`
  - Body: `CreateFeedbackRequest`.
  - Behavior:
    - Validate `Rating` is between `1` and `5` inclusive; `Comment`, if present, `<= 2000` characters after trimming.
    - Resolve the actor's own `Customer.Id` the same way `TicketEndpoints.ResolveCreateCustomerIdAsync`'s customer branch does (`dbContext.Customers.Where(c => c.ApplicationUserId == actorUserId).Select(c => c.Id).FirstOrDefaultAsync`); if no linked `Customer` row exists, return `403 Forbid` (a customer account must be fully provisioned before submitting feedback).
    - Persist the `Feedback` row with `CreatedAtUtc = DateTime.UtcNow`.
  - Responses: `201 Created` with `FeedbackResponse`; `400` validation; `403` if no linked customer profile.

- `GET /api/feedback`
  - `.RequireAuthorization("Permission:" + Permissions.FeedbackRead)`
  - Query params: `int page = 1`, `int pageSize = 20` (max 100), matching the validation style in `TicketMessagesEndpoints.ValidateListRequest`.
  - Returns feedback across all customers, newest first (`OrderByDescending(f => f.CreatedAtUtc)`), joined to `Customer.Name` for `CustomerName`.
  - Responses: `200 OK` with `FeedbackListResponse`; `400` for invalid paging values.

Register `app.MapFeedbackEndpoints();` in `src/CustomerManagement.Api/Program.cs` after `app.MapTicketMessagesEndpoints();`.

---

## Edge Cases & Failure Modes

- `Rating` outside `1`–`5` (including `0` or negative): `400` validation error.
- `Comment` over 2000 characters: `400` validation error.
- Customer account with no linked `Customer` row (should not normally happen given registration always creates one, but guarded defensively): `403 Forbid` rather than a server error.
- Agent/admin calling `POST /api/feedback`: `403 Forbid` — only the `Customer` role has `feedback.submit`; this is intentional (feedback is explicitly the customer's voice, not an internal agent note).
- Customer calling `GET /api/feedback`: `403 Forbid` — `feedback.read` is not assigned to `Customer`, so a customer can never see other customers' feedback or even their own list through this endpoint (acceptable: the intake only requires customers to be able to *submit* feedback, not review their own submission history).
- No feedback submitted yet: `GET /api/feedback` returns an empty `Items` list with `TotalCount = 0`, not an error.

---

## Test Plan

1. Create `tests/CustomerManagement.Api.Tests/FeedbackEndpointsTests.cs`:
   - Customer submits feedback with rating + comment → `201`, `CustomerName` resolved correctly.
   - Customer submits feedback with rating only (no comment) → `201`, `Comment` is `null`.
   - Rating `0`, `6`, or negative → `400`.
   - Comment over 2000 characters → `400`.
   - Agent/admin `POST /api/feedback` → `403`.
   - Customer `GET /api/feedback` → `403`.
   - Admin/agent `GET /api/feedback` → `200`, paginated, newest first.
   - Paging validation (`page < 1`, `pageSize` out of `1..100`) → `400`.
2. Extend permission-seeding assertions to confirm `feedback.submit` is seeded only for `Customer`, and `feedback.read` only for `Admin`/`Agent`.

---

## Migration / Rollback

- Migration adds the `Feedback` table only; no changes to existing tables.
- Rollback plan:
  1. Remove `app.MapFeedbackEndpoints();` from `Program.cs`.
  2. Remove `Permissions.FeedbackSubmit` / `Permissions.FeedbackRead` from `IdentitySeedData.cs`.
  3. Roll back the EF migration (drop `Feedback`).

---
## Review — completed 2026-10-07

- [x] FeedbackEndpointsTests.cs — all 8 Test Plan cases (submit with rating+comment 201 w/ resolved CustomerName, rating-only 201 w/ null Comment, rating out of range 400, comment too long 400, agent/admin submit 403, customer list 403, admin/agent list paginated newest-first, invalid paging 400): added, passing
- [x] Permission seeding — `feedback.submit` confirmed customer-only, `feedback.read` confirmed admin/agent-only: added, passing

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 23.**

# Story 21 — FAQ Management API

## Prerequisites

- None. This story introduces a standalone domain (`FaqEntry`) with no dependency on ticket or customer-portal work; it can land independently of Story 20.

---

## Story Goal

1. Let an admin maintain a catalog of FAQ entries (question, answer, topic) and retire an entry so it stops appearing to customers.
2. Let any authenticated user (customer, agent, admin) browse the currently-active FAQ entries, with enough structure in the response (a `Topic` field) for the UI to group them by topic.

Out of scope for this story:

- Any UI work (covered by Story 23).
- Gateway routing for the new endpoints (covered by Story 23).
- Automatic FAQ suggestions based on ticket text (explicitly out of scope per intake).

---

## Context — Read These Files First

1. `src/CustomerManagement.Api/Domain/Tickets/TicketCategory.cs` — structural precedent for a simple admin-managed lookup entity (`Id`, `Name`, `Description`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`).
2. `src/CustomerManagement.Api/Endpoints/Tickets/TicketCategoryEndpoints.cs` (full file) — precedent for the list/create/update endpoint shape, including `ListCategoriesAsync`'s `activeOnly` query parameter (~lines 33–55) and the authorization split (`RequireAuthorization()` for read vs a manage-permission policy for write, ~lines 16–29).
3. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` — `modelBuilder.Entity<TicketCategory>` mapping (~line 352) as the direct structural precedent; the `DbSet<TicketCategory>` declaration near the top of the class for where to add the new `DbSet<FaqEntry>`.
4. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — full constant list and naming convention (`area.verb` or `area.manage`/`area.read`).
5. `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` — `baselinePermissions` dictionary and the three `assignments` entries (`AuthRoles.Admin`, `AuthRoles.Agent`, `AuthRoles.Customer`) for where new permissions are added and assigned; `EnsureTicketTaxonomyAsync` (~lines 283–350) as the precedent for an idempotent-by-name startup seed routine, and the `EnsureSeededAsync` method body (~lines 58–61) for where to add a new `EnsureFaqEntriesAsync` call.
6. `src/CustomerManagement.Api/Program.cs` — `app.MapTicketCategoryEndpoints();` registration (~line 94) as the precedent location/style for registering `app.MapFaqEndpoints();`.
7. `src/CustomerManagement.Api/Infrastructure/Auth/AuthRoles.cs` — `Admin`, `Agent`, `Customer` role constants.

---

## Backend Tasks

### 1. Add `FaqEntry` domain entity

Create file: `src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.Faq;

public sealed class FaqEntry
{
    public Guid Id { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
```

### 2. Add persistence mapping and migration

File: `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`

- Add `public DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();` near the other lookup-style `DbSet` declarations (next to `TicketCategories`/`TicketPriorities`).
- Add a mapping block modeled directly on `modelBuilder.Entity<TicketCategory>` (~line 352):
  - `ToTable("FaqEntries")`
  - PK `Id`
  - `Topic` max 150, required
  - `Question` max 500, required
  - `Answer` max 4000, required
  - `SortOrder` required
  - `IsActive` required
  - `CreatedAtUtc` / `UpdatedAtUtc` required
  - `RowVersion` is rowversion
  - Index on `(Topic, SortOrder)` for the grouped-browse read path (no uniqueness constraint — a topic can have many entries).

Create a migration under `src/CustomerManagement.Api/Infrastructure/Persistence/Migrations/` for the new `FaqEntries` table.

### 3. Add permissions and role assignments

File: `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs`

- Add constants:
  - `FaqRead = "faq.read"`
  - `FaqManage = "faq.manage"`

File: `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`

- Add to `baselinePermissions`:
  - `[Permissions.FaqRead] = "Browse active FAQ entries."`
  - `[Permissions.FaqManage] = "Create, update, and retire FAQ entries."`
- Update `assignments`:
  - `AuthRoles.Admin`: add `Permissions.FaqRead`, `Permissions.FaqManage`.
  - `AuthRoles.Agent`: add `Permissions.FaqRead` (agents answer tickets and benefit from the same reference material customers see).
  - `AuthRoles.Customer`: add `Permissions.FaqRead`.
- No baseline FAQ content is seeded; the admin populates entries through the new endpoints. (If the team wants starter content later, that is a follow-up, not part of this story.)

### 4. Add contracts

Create file: `src/CustomerManagement.Api/Contracts/Faq/FaqContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.Faq;

public sealed record CreateFaqEntryRequest(string Topic, string Question, string Answer, int SortOrder);

public sealed record UpdateFaqEntryRequest(
    string Topic,
    string Question,
    string Answer,
    int SortOrder,
    bool IsActive,
    byte[] RowVersion);

public sealed record FaqEntryResponse(
    Guid Id,
    string Topic,
    string Question,
    string Answer,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
```

### 5. Implement FAQ endpoints

Create file: `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs`

Route group: `/api/faq`, modeled on `TicketCategoryEndpoints.cs`:

- `GET /api/faq`
  - `.RequireAuthorization("Permission:" + Permissions.FaqRead)`
  - Query param: `bool? activeOnly`.
  - Behavior:
    - If the caller is in the `Customer` role (checked the same way `TicketEndpoints`/`TicketMessagesEndpoints` check role membership — `httpContext.User.IsInRole(AuthRoles.Customer)` plus the claims fallback used elsewhere in this codebase), force `activeOnly = true` regardless of the query value, so a customer can never retrieve a retired entry even by guessing the parameter.
    - Otherwise, honor the `activeOnly` query value (admins managing the catalog default to seeing everything, i.e. omit the parameter to get all entries including retired ones).
    - Order by `Topic`, then `SortOrder`, then `Question`.
  - Returns `200 OK` with `IReadOnlyList<FaqEntryResponse>`.

- `POST /api/faq`
  - `.RequireAuthorization("Permission:" + Permissions.FaqManage)`
  - Body: `CreateFaqEntryRequest`.
  - Validate: `Topic` required (max 150), `Question` required (max 500), `Answer` required (max 4000).
  - Creates with `IsActive = true`.
  - `201 Created` with `FaqEntryResponse`; `400` on validation failure.

- `PUT /api/faq/{faqId:guid}`
  - `.RequireAuthorization("Permission:" + Permissions.FaqManage)`
  - Body: `UpdateFaqEntryRequest`.
  - Same field validation as create, plus standard optimistic-concurrency handling: set `dbContext.Entry(entry).Property(e => e.RowVersion).OriginalValue = request.RowVersion;` and catch `DbUpdateConcurrencyException` → `409 Conflict`, mirroring `TicketCategoryEndpoints`'s update pattern.
  - Setting `IsActive = false` is how an admin retires an entry (no separate delete/retire endpoint).
  - `200 OK` with updated `FaqEntryResponse`; `404` if `faqId` does not exist; `400` validation; `409` concurrency conflict.

Register `app.MapFaqEndpoints();` in `src/CustomerManagement.Api/Program.cs` next to the other ticket-taxonomy-style registrations (after `app.MapTicketPriorityEndpoints();`).

---

## Edge Cases & Failure Modes

- Customer passes `activeOnly=false` on `GET /api/faq`: server ignores it and still returns only active entries (enforced server-side in the endpoint, not just hidden in the UI).
- Agent/admin omit `activeOnly`: receive all entries, including retired ones, so the admin management UI can show and re-activate retired entries.
- `PUT /api/faq/{faqId}` with a stale `RowVersion`: `409 Conflict`, same shape as `TicketCategoryEndpoints`/`TicketPriorityEndpoints` concurrency handling.
- Empty `Topic`/`Question`/`Answer` or over max length: `400` with per-field validation errors.
- Unknown `faqId` on update: `404 Not Found`.
- No FAQ entries exist yet: `GET /api/faq` returns an empty list (not an error); the customer UI shows an empty state rather than failing.

---

## Test Plan

1. Create `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs`:
   - Admin creates an FAQ entry → `201`.
   - Non-admin (agent/customer) attempts create/update → `403`.
   - Customer `GET /api/faq` only returns active entries, even when passing `activeOnly=false`.
   - Agent/admin `GET /api/faq` without `activeOnly` returns both active and retired entries.
   - Admin retires an entry (`PUT` with `isActive=false`) → entry no longer appears in a subsequent customer `GET /api/faq` call.
   - Update with stale `RowVersion` → `409`.
   - Update with unknown `faqId` → `404`.
   - Validation errors for missing/too-long `Topic`/`Question`/`Answer`.
2. Extend permission-seeding assertions (wherever `IdentitySeedData` assignments are currently tested) to confirm `faq.read` is seeded for admin, agent, and customer, and `faq.manage` only for admin.

---

## Migration / Rollback

- Migration adds the `FaqEntries` table only; no changes to existing tables.
- Rollback plan:
  1. Remove `app.MapFaqEndpoints();` from `Program.cs`.
  2. Remove `Permissions.FaqRead` / `Permissions.FaqManage` from `IdentitySeedData.cs` assignments and `baselinePermissions`.
  3. Roll back the EF migration (drop `FaqEntries`).

---
## Review — completed 2026-10-07

### Implementation Review

- [x] `FaqEntry` CRUD endpoints — admin create/update/retire
- [x] Customer list enforces active-only regardless of an `activeOnly=false` override; agent/admin list supports active+retired
- [x] `faq.read` (admin/agent/customer) and `faq.manage` (admin-only) permissions seeded

### Test Coverage

- [x] FaqEndpointsTests.cs — all 8 Test Plan cases (admin create 201, non-admin create/update 403, customer list active-only even with `activeOnly=false`, agent/admin list active+retired, admin retire removes entry from customer list, stale RowVersion 409, unknown id 404, missing/too-long field validation)
- [x] Permission seeding — `faq.read` confirmed for admin/agent/customer, `faq.manage` confirmed admin-only

### Verification

| Command | Result | Evidence |
|---|---|---|
| `dotnet build CustomerSupportCRM.slnx` | Passed | 0 warnings, 0 errors (re-verified 2026-10-07) |
| `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj` | Passed | 185/185 passing, including `FaqEndpointsTests.cs` (re-verified 2026-10-07) |
| `ng build` / `ng test` | N/A | This story is API-only; the customer FAQ browse UI and admin FAQ page are built in Story 23 |

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 22.**

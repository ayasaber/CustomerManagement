# Story 25 — Solutions & Guides API

## Prerequisites

- None. Standalone domain, independent of [Story 24 — Help Articles API](24-story-help-articles-api.md) and of FAQs/feedback/tickets. Can be planned/executed in parallel with Story 24; sequenced after it here only for a single global execution order (same approach used for Stories 21/22 in [customer-portal/00-overview.md](../customer-portal/00-overview.md)).
- Follows the same admin-manage / agent-and-customer-browse access pattern as [FAQ Management API](../customer-portal/21-story-faq-management-api.md) and [Story 24](24-story-help-articles-api.md).

---

## Story Goal

Give admins a way to author step-by-step solutions/guides (a title plus an ordered list of steps) that agents and customers can browse to work through a problem on their own, while keeping authoring strictly admin-only.

Out of scope for this story:

- Unified search across FAQs, help articles, and guides — that is [Story 26](26-story-knowledge-base-search-api.md).
- Any UI — that is [Story 27](27-story-knowledge-base-ui-and-gateway-routing.md). This story is API-only.
- Branching/conditional steps (e.g. "if X, go to step 5") — the intake describes a linear sequence only (`try this, then this, then this`); steps are a simple ordered list.
- Any change to FAQ or help-article data or endpoints.

---

## Context — Read These Files First

1. `src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs` and [Story 24](24-story-help-articles-api.md)'s `HelpArticle` entity — precedent for the parent `Guide` entity shape (`Id`, `Title`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `[Timestamp] RowVersion`).
2. `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs` (full file) — precedent for `ListAsync` (customer-forced active-only), `CreateAsync`, `UpdateAsync` with `DbUpdateConcurrencyException` → `409`, and the per-file `IsCustomer(ClaimsPrincipal)` helper duplication convention.
3. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`:
   - Lines 1–45 — `DbSet<...>` declaration list; add `DbSet<Guide> Guides` and `DbSet<GuideStep> GuideSteps` here.
   - Lines 406–427 — `modelBuilder.Entity<FaqEntry>(entity => { ... })` — precedent for the `Guide` configuration block (no child-collection precedent exists for FAQ, so the `GuideStep` configuration below is new but follows the same `ToTable`/`HasMaxLength`/`IsRowVersion` conventions).
4. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — lines 40–41 (`FaqRead` / `FaqManage`) — add `GuidesRead = "guides.read"` and `GuidesManage = "guides.manage"` next to them (and next to the `HelpArticlesRead`/`HelpArticlesManage` constants added by Story 24, if Story 24 is already merged).
5. `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` — same three locations as in [Story 24](24-story-help-articles-api.md)'s Context section (`baselinePermissions` ~line 111; Admin array ~lines 160–186; Agent array ~lines 188–209; Customer array ~lines 211–219) — add `GuidesRead`/`GuidesManage` to Admin, `GuidesRead` only to Agent and Customer.
6. `src/CustomerManagement.Api/Program.cs` — line 113 `app.MapFaqEndpoints();` — add `app.MapGuideEndpoints();` near it (after `app.MapHelpArticleEndpoints();` if Story 24 is already merged).
7. `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs` (full file) — exact precedent for the new `GuideEndpointsTests.cs` test structure, including the RowVersion-sentinel pattern for successful-update tests (set `RowVersion` via a `_factory.Services.CreateScope()` `DbContext` scope right after HTTP-creating the guide).
8. `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs` — lines 79, 105, 115 (`Permissions.FaqRead` in the Admin/Agent/Customer arrays) — add `Permissions.GuidesRead` to all three arrays and `Permissions.GuidesManage` to the Admin array only.

---

## Backend Tasks

### 1. Add the `Guide` and `GuideStep` domain entities

Create file: `src/CustomerManagement.Api/Domain/KnowledgeBase/Guide.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.KnowledgeBase;

public sealed class Guide
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<GuideStep> Steps { get; set; } = [];
}
```

Create file: `src/CustomerManagement.Api/Domain/KnowledgeBase/GuideStep.cs`

```csharp
namespace CustomerManagement.Api.Domain.KnowledgeBase;

public sealed class GuideStep
{
    public Guid Id { get; set; }

    public Guid GuideId { get; set; }

    public int StepNumber { get; set; }

    public string Instruction { get; set; } = string.Empty;
}
```

### 2. Add contracts

Create file: `src/CustomerManagement.Api/Contracts/KnowledgeBase/GuideContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public sealed record CreateGuideRequest(string Title, IReadOnlyList<string> Steps);

public sealed record UpdateGuideRequest(string Title, IReadOnlyList<string> Steps, bool IsActive, byte[] RowVersion);

public sealed record GuideStepResponse(int StepNumber, string Instruction);

public sealed record GuideResponse(
    Guid Id,
    string Title,
    bool IsActive,
    IReadOnlyList<GuideStepResponse> Steps,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
```

Validation (mirror `FaqEndpoints.ValidateCreateRequest` style): `Title` required, max 200 characters; `Steps` must contain at least 1 entry and at most 50; each step instruction required, max 1000 characters.

### 3. Add endpoints

Create file: `src/CustomerManagement.Api/Endpoints/KnowledgeBase/GuideEndpoints.cs`, mirroring `FaqEndpoints.cs`:

```csharp
public static class GuideEndpoints
{
    public static IEndpointRouteBuilder MapGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guides").WithTags("Guides");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesRead)
            .WithName("ListGuides")
            .WithSummary("List solutions and guides");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesManage)
            .WithName("CreateGuide")
            .WithSummary("Create a solution/guide");

        group.MapPut("/{guideId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.GuidesManage)
            .WithName("UpdateGuide")
            .WithSummary("Update a solution/guide");

        return app;
    }
}
```

- `ListAsync`: `dbContext.Guides.Include(g => g.Steps).AsNoTracking()`; same customer-forced-active-only rule as `FaqEndpoints.ListAsync`; order the outer query by `Title`, and project `Steps` ordered by `StepNumber`.
- `CreateAsync`: build the `Guide` plus its `GuideStep` child rows in one `SaveChangesAsync` call (`StepNumber` assigned from the request list's index + 1); EF Core's default cascade-insert via the navigation property handles the parent/child insert together — no separate `dbContext.Add` call per step is required beyond populating `guide.Steps`.
- `UpdateAsync`: load the guide **with** `.Include(g => g.Steps)`, apply `request.RowVersion` as `OriginalValue` on `RowVersion` same as `FaqEndpoints.UpdateAsync`, then **replace the full step list**: `dbContext.GuideSteps.RemoveRange(entry.Steps)` followed by assigning a freshly built `List<GuideStep>` from `request.Steps`, before `SaveChangesAsync`. This "replace-all" approach is the simplest correct way to keep step ordering/content in sync with the request without diffing individual steps, and matches the intake's framing of a guide as a single ordered list owned entirely by one edit.

### 4. Wire up DI/EF/permissions

- `CustomerManagementDbContext.cs` — add `public DbSet<Guide> Guides => Set<Guide>();` and `public DbSet<GuideStep> GuideSteps => Set<GuideStep>();` near the other `DbSet` declarations (after line 41 / after `HelpArticles` if Story 24 already merged).
- Add a `modelBuilder.Entity<Guide>(entity => { ... })` block: `ToTable("Guides")`, `Title` `HasMaxLength(200)` required, `IsActive`/`CreatedAtUtc`/`UpdatedAtUtc` required, `RowVersion` `.IsRowVersion()`, `entity.HasIndex(g => g.Title);`, and `entity.HasMany(g => g.Steps).WithOne().HasForeignKey(s => s.GuideId).OnDelete(DeleteBehavior.Cascade);`.
- Add a `modelBuilder.Entity<GuideStep>(entity => { ... })` block: `ToTable("GuideSteps")`, `Instruction` `HasMaxLength(1000)` required, `StepNumber` required, `entity.HasIndex(s => new { s.GuideId, s.StepNumber }).IsUnique();`.
- `Permissions.cs` — add:
  ```csharp
  public const string GuidesRead = "guides.read";
  public const string GuidesManage = "guides.manage";
  ```
- `IdentitySeedData.cs` — add `[Permissions.GuidesRead] = "Browse active solutions and guides."` and `[Permissions.GuidesManage] = "Create, update, and retire solutions and guides."` to `baselinePermissions`; add `Permissions.GuidesRead, Permissions.GuidesManage` to the Admin assignment array; add `Permissions.GuidesRead` only to Agent and Customer assignment arrays.
- `Program.cs` — add `app.MapGuideEndpoints();`.

---

## Edge Cases & Failure Modes

- `Steps` list is empty on create/update: rejected with `400` ("at least 1 step required") — enforced in `ValidateCreateRequest`/`ValidateUpdateRequest`, since a guide with zero steps is not a usable guide.
- `Steps` list exceeds 50 entries, or any single step instruction exceeds 1000 characters: rejected with `400`.
- Update replaces the step list entirely: a client that omits a previously-existing step is treated as intentionally removing it (no partial-step-update API is offered) — document this as the expected behavior in the endpoint's `WithSummary`.
- Customer passes `?activeOnly=false`: ignored server-side, same as `FaqEndpoints.ListAsync` and [Story 24](24-story-help-articles-api.md)'s `HelpArticleEndpoints.ListAsync`.
- Update with a stale `RowVersion`: `409 Conflict`, same `DbUpdateConcurrencyException` handling as `FaqEndpoints.UpdateAsync`.
- Update with an unknown `guideId`: `404`.
- Non-admin calls `POST`/`PUT`: `403` via `RequireAuthorization("Permission:" + Permissions.GuidesManage)`.
- EF Core InMemory test provider never auto-generates `RowVersion` — set a deterministic sentinel value (e.g. `[1]`) via a DbContext scope after HTTP-creating the guide before any successful-update test, same pattern as `FaqEndpointsTests.cs`.
- Concurrent update races on step replacement: the existing `RowVersion` optimistic-concurrency check on the parent `Guide` row already guards this — a second writer's stale `RowVersion` causes `SaveChangesAsync` to throw before the step `RemoveRange`/re-add is committed, so steps are never partially replaced.

---

## Test Plan

1. Create `tests/CustomerManagement.Api.Tests/GuideEndpointsTests.cs`, mirroring `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs`:
   - Admin creates a guide with 3 ordered steps → `201`, response `Steps` returned in `StepNumber` order.
   - Non-admin (agent/customer) attempts create/update → `403`.
   - Create with an empty `Steps` list → `400`.
   - Customer `GET /api/guides` only returns active entries, even when passing `activeOnly=false`.
   - Agent/admin `GET /api/guides` without `activeOnly` returns both active and retired entries.
   - Admin updates a guide's steps (different count/order/content than on create) → the returned `Steps` reflect only the new list, not a merge with the old one.
   - Admin retires a guide (`PUT` with `isActive=false`) → no longer appears in a subsequent customer `GET /api/guides` call.
   - Update with stale `RowVersion` → `409`.
   - Update with unknown `guideId` → `404`.
   - Validation errors for missing `Title`, too-long step instruction, and too-many steps.
2. Extend `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs`'s Admin/Agent/Customer permission arrays and add/extend a permission-seeding assertion test confirming `guides.read` is seeded for admin, agent, and customer, and `guides.manage` only for admin.

---

## Migration / Rollback

- Migration adds the `Guides` and `GuideSteps` tables only; no changes to existing tables.
- Rollback plan:
  1. Remove `app.MapGuideEndpoints();` from `Program.cs`.
  2. Remove `Permissions.GuidesRead` / `Permissions.GuidesManage` from `IdentitySeedData.cs` assignments and `baselinePermissions`.
  3. Roll back the EF migration (drop `GuideSteps` then `Guides`, respecting the FK).

---

## Verification Steps

1. **Backend builds:** `dotnet build` in `src/CustomerManagement.Api`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj`.
3. **Regression:** run the full `dotnet test CustomerSupportCRM.slnx` to confirm no existing FAQ/help-article/ticket/feedback tests broke.

---
## Review — completed 2026-10-07

- [x] Guide/GuideStep entities, contracts, endpoints, DbContext wiring (cascade-delete FK, unique `(GuideId, StepNumber)` index), Permissions.cs, IdentitySeedData.cs, Program.cs registration, EF migration (`Story25SolutionsGuidesApi`): added
- [x] Implementation deviation from the plan's `UpdateAsync` sketch: loading the guide via `.Include(g => g.Steps)` and reassigning `entry.Steps` to a new list (as originally sketched) triggered a spurious `DbUpdateConcurrencyException` ("entity does not exist in the store") against the EF Core InMemory provider specifically on the parent `Guide` row's concurrency check, reproducible on every update. Fixed by loading the `Guide` without `Include`, querying/removing existing `GuideStep` rows separately by `GuideId`, and `AddRange`-ing new ones with `GuideId` set explicitly (no navigation-fixup reliance) — same RowVersion-`OriginalValue` mechanism as `FaqEndpoints`/`HelpArticleEndpoints` otherwise.
- [x] GuideEndpointsTests.cs — all 10 Test Plan cases (admin create with ordered steps 201, non-admin create/update 403, empty steps list 400, customer list active-only even with `activeOnly=false`, agent/admin list active+retired, update replaces step list without merging, admin retire removes entry from customer list, stale RowVersion 409, unknown id 404, missing title / too-long step / too-many-steps validation) + permission-seeding test: added, passing
- [x] TestAuthHandler.cs updated with `guides.read` (admin/agent/customer) and `guides.manage` (admin only)
- [x] Full regression: `dotnet test CustomerSupportCRM.slnx` → 233/233 passing (216 pre-existing + 17 new)

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 26.**

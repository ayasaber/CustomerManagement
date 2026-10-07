# Story 24 — Help Articles API

## Prerequisites

- None. This story introduces a new, standalone domain (`HelpArticle`) that does not depend on tickets, FAQs, or feedback. It follows the same admin-manage / agent-and-customer-browse access pattern already established by [FAQ Management API](../customer-portal/21-story-faq-management-api.md) (`src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs`, `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs`), which is the direct precedent to mirror.
- Coordination note: this story does **not** modify `src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs`, `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs`, or any existing FAQ contract — FAQs remain exactly as implemented in Story 21. [Story 26 — Unified Knowledge Base Search API](26-story-knowledge-base-search-api.md) will later read `FaqEntries` read-only to include FAQs in combined search results.

---

## Story Goal

Give admins a way to author longer-form reference content ("help articles": a title plus a structured/long-form body) that agents and customers can browse, while keeping authoring strictly admin-only — the same access split already in place for FAQs, ticket categories, and ticket priorities.

Out of scope for this story:

- Unified search across FAQs, help articles, and guides — that is [Story 26](26-story-knowledge-base-search-api.md).
- Any UI — that is [Story 27](27-story-knowledge-base-ui-and-gateway-routing.md). This story is API-only.
- Any change to FAQ data or endpoints.
- Grouping articles by topic/category (not requested by the intake; articles are a flat, title-ordered list, unlike FAQs which group by `Topic`).

---

## Context — Read These Files First

1. `src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs` (full file, 23 lines) — direct precedent for the new `HelpArticle` entity shape (`Id`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `[Timestamp] RowVersion`).
2. `src/CustomerManagement.Api/Contracts/Faq/FaqContracts.cs` (full file) — precedent for `CreateHelpArticleRequest` / `UpdateHelpArticleRequest` / `HelpArticleResponse` record shapes.
3. `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs` (full file, ~220 lines) — exact precedent to mirror for the new `HelpArticleEndpoints.MapHelpArticleEndpoints`: `MapGroup`, `ListAsync` (customer-forced `activeOnly`, admin/agent sees all), `CreateAsync`, `UpdateAsync` with `DbUpdateConcurrencyException` → `409`, `IsCustomer(ClaimsPrincipal)` local helper (do not extract to a shared helper — this repo duplicates this helper per endpoint file; see note below).
4. `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs`:
   - Lines 1–45 — `DbSet<FaqEntry> FaqEntries => Set<FaqEntry>();` (line 41) is where the new `DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();` goes (add immediately after it).
   - Lines 406–427 — `modelBuilder.Entity<FaqEntry>(entity => { ... })` configuration block — mirror this exactly for `HelpArticle` (`ToTable("HelpArticles")`, `HasMaxLength` on string properties, `IsRowVersion()` on `RowVersion`).
5. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — lines 40–41 (`FaqRead` / `FaqManage` constants) — add `HelpArticlesRead = "help-articles.read"` and `HelpArticlesManage = "help-articles.manage"` next to them.
6. `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs`:
   - Lines 111–114 (`baselinePermissions` dictionary) — add descriptions for the two new permissions.
   - Lines ~160–186 (Admin assignment array, includes `Permissions.FaqRead, Permissions.FaqManage, Permissions.FeedbackRead`) — add `Permissions.HelpArticlesRead, Permissions.HelpArticlesManage`.
   - Lines ~188–209 (Agent assignment array, includes `Permissions.FaqRead, Permissions.FeedbackRead`) — add `Permissions.HelpArticlesRead` only.
   - Lines ~211–219 (Customer assignment array, includes `Permissions.FaqRead, Permissions.FeedbackSubmit`) — add `Permissions.HelpArticlesRead` only.
7. `src/CustomerManagement.Api/Program.cs` — line 113 `app.MapFaqEndpoints();` — add `app.MapHelpArticleEndpoints();` immediately after it.
8. `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs` (full file) — exact precedent for the new `HelpArticleEndpointsTests.cs` test structure (ownership of `_factory`, `IClassFixture<CustomerManagementApiFactory>`, RowVersion-sentinel pattern for a successful update test).
9. `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs` — lines 79, 105, 115 (`Permissions.FaqRead` in the Admin/Agent/Customer permission arrays) — add `Permissions.HelpArticlesRead` to all three arrays and `Permissions.HelpArticlesManage` to the Admin array only, in the same positions as the `FaqRead`/`FaqManage` entries.

---

## Backend Tasks

### 1. Add the `HelpArticle` domain entity

Create file: `src/CustomerManagement.Api/Domain/KnowledgeBase/HelpArticle.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace CustomerManagement.Api.Domain.KnowledgeBase;

public sealed class HelpArticle
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
```

`Body` holds the structured/long-form content as plain text (no rich-text/HTML model introduced — matches the intake's "structured content... closer to a short document" without inventing a content-block schema the intake never asked for).

### 2. Add contracts

Create file: `src/CustomerManagement.Api/Contracts/KnowledgeBase/HelpArticleContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public sealed record CreateHelpArticleRequest(string Title, string Body);

public sealed record UpdateHelpArticleRequest(string Title, string Body, bool IsActive, byte[] RowVersion);

public sealed record HelpArticleResponse(
    Guid Id,
    string Title,
    string Body,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion);
```

Validation limits (mirror `FaqEndpoints.ValidateCreateRequest` style): `Title` required, max 200 characters; `Body` required, max 10000 characters.

### 3. Add endpoints

Create file: `src/CustomerManagement.Api/Endpoints/KnowledgeBase/HelpArticleEndpoints.cs`, mirroring `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs` structure exactly:

```csharp
public static class HelpArticleEndpoints
{
    public static IEndpointRouteBuilder MapHelpArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/help-articles").WithTags("HelpArticles");

        group.MapGet("", ListAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesRead)
            .WithName("ListHelpArticles")
            .WithSummary("List help articles");

        group.MapPost("", CreateAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesManage)
            .WithName("CreateHelpArticle")
            .WithSummary("Create a help article");

        group.MapPut("/{articleId:guid}", UpdateAsync)
            .RequireAuthorization("Permission:" + Permissions.HelpArticlesManage)
            .WithName("UpdateHelpArticle")
            .WithSummary("Update a help article");

        return app;
    }
    // ListAsync / CreateAsync / UpdateAsync / ValidateCreateRequest / ValidateUpdateRequest / IsCustomer:
    // copy FaqEndpoints' implementations, substituting FaqEntry -> HelpArticle, dbContext.FaqEntries -> dbContext.HelpArticles,
    // and the FAQ-specific OrderBy(Topic/SortOrder/Question) with OrderBy(entry => entry.Title).
}
```

- `ListAsync`: same customer-forced-active-only rule as `FaqEndpoints.ListAsync` (`IsCustomer(httpContext.User) || activeOnly.GetValueOrDefault()`); order by `Title`.
- `CreateAsync` / `UpdateAsync`: same `DbUpdateConcurrencyException` → `409` handling as `FaqEndpoints.UpdateAsync`.

### 4. Wire up DI/EF/permissions

- `src/CustomerManagement.Api/Infrastructure/Persistence/CustomerManagementDbContext.cs` — add `public DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();` after line 41, and a `modelBuilder.Entity<HelpArticle>(entity => { ... })` block mirroring the `FaqEntry` block (lines 406–427): `entity.ToTable("HelpArticles")`, `Title` `HasMaxLength(200)`, `Body` `HasMaxLength(10000)`, `IsActive`/`CreatedAtUtc`/`UpdatedAtUtc` required, `RowVersion` `.IsRowVersion()`. Add `entity.HasIndex(a => a.Title);` (no composite topic index needed, unlike FAQ).
- `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — add after line 41:
  ```csharp
  public const string HelpArticlesRead = "help-articles.read";
  public const string HelpArticlesManage = "help-articles.manage";
  ```
- `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` — add to `baselinePermissions` (near line 111): `[Permissions.HelpArticlesRead] = "Browse active help articles."` and `[Permissions.HelpArticlesManage] = "Create, update, and retire help articles."`; add `Permissions.HelpArticlesRead, Permissions.HelpArticlesManage` to the Admin assignment array; add `Permissions.HelpArticlesRead` only to the Agent and Customer assignment arrays.
- `src/CustomerManagement.Api/Program.cs` — add `app.MapHelpArticleEndpoints();` after line 113.

---

## Edge Cases & Failure Modes

- Empty/whitespace-only `Title` or `Body`: rejected with `400` by `ValidateCreateRequest`, same pattern as `FaqEndpoints.ValidateCreateRequest` (`src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs` lines ~177–205).
- `Title`/`Body` over the character limit: rejected with `400`, same pattern.
- Customer passes `?activeOnly=false`: ignored server-side — customers can never see retired articles, identical to `FaqEndpoints.ListAsync`'s `IsCustomer(httpContext.User) || activeOnly.GetValueOrDefault()` logic.
- Update with a stale `RowVersion`: `dbContext.Entry(entry).Property(row => row.RowVersion).OriginalValue = request.RowVersion;` followed by `SaveChangesAsync` throwing `DbUpdateConcurrencyException` → `409 Conflict`, same as `FaqEndpoints.UpdateAsync`.
- Update with an unknown `articleId`: `404`, same `FirstOrDefaultAsync(...) is null` check as `FaqEndpoints.UpdateAsync`.
- Non-admin (agent or customer) calls `POST`/`PUT`: blocked by `RequireAuthorization("Permission:" + Permissions.HelpArticlesManage)` → `403`, enforced by the policy infrastructure already used for `FaqManage` (no new authorization code needed).
- EF Core InMemory test provider never auto-generates `RowVersion` — any integration test that needs a genuinely successful update must first set a deterministic sentinel `RowVersion` (e.g. `[1]`) via a `_factory.Services.CreateScope()` `DbContext` scope right after creating the article via HTTP, then reuse that value in the update body (same pattern used throughout `FaqEndpointsTests.cs` and `FeedbackEndpointsTests.cs`).

---

## Test Plan

1. Create `tests/CustomerManagement.Api.Tests/HelpArticleEndpointsTests.cs`, mirroring `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs`'s fixture setup (`IClassFixture<CustomerManagementApiFactory>`):
   - Admin creates a help article → `201`.
   - Non-admin (agent/customer) attempts create/update → `403`.
   - Customer `GET /api/help-articles` only returns active entries, even when passing `activeOnly=false`.
   - Agent/admin `GET /api/help-articles` without `activeOnly` returns both active and retired entries.
   - Admin retires an article (`PUT` with `isActive=false`) → no longer appears in a subsequent customer `GET /api/help-articles` call.
   - Update with stale `RowVersion` → `409`.
   - Update with unknown `articleId` → `404`.
   - Validation errors for missing/too-long `Title`/`Body`.
2. Extend `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs`'s Admin/Agent/Customer permission arrays (lines 79, 105, 115 area) and add/extend a permission-seeding assertion test (wherever `IdentitySeedData` seeding is currently asserted, e.g. in `AuthEndpointsTests.cs`) confirming `help-articles.read` is seeded for admin, agent, and customer, and `help-articles.manage` only for admin.

---

## Migration / Rollback

- Migration adds the `HelpArticles` table only; no changes to existing tables.
- Rollback plan:
  1. Remove `app.MapHelpArticleEndpoints();` from `Program.cs`.
  2. Remove `Permissions.HelpArticlesRead` / `Permissions.HelpArticlesManage` from `IdentitySeedData.cs` assignments and `baselinePermissions`.
  3. Roll back the EF migration (drop `HelpArticles`).

---

## Verification Steps

1. **Backend builds:** `dotnet build` in `src/CustomerManagement.Api`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj`.
3. **Regression:** run the full `dotnet test CustomerSupportCRM.slnx` to confirm no existing FAQ/ticket/feedback tests broke.

---
## Review — completed 2026-10-07

### Implementation Review

- [x] `HelpArticle` entity, contracts, endpoints, `DbContext` wiring, `Permissions.cs`, `IdentitySeedData.cs`, `Program.cs` registration, EF migration (`Story24HelpArticlesApi`)
- [x] Customer list enforces active-only regardless of `activeOnly=false`, mirroring `FaqEndpoints`; agent/admin list supports active+retired
- [x] `help-articles.read` (admin/agent/customer) and `help-articles.manage` (admin-only) permissions seeded

### Test Coverage

- [x] HelpArticleEndpointsTests.cs — all 8 Test Plan cases (admin create 201, non-admin create/update 403, customer list active-only even with `activeOnly=false`, agent/admin list active+retired, admin retire removes entry from customer list, stale RowVersion 409, unknown id 404, missing/too-long field validation) + permission-seeding test
- [x] `TestAuthHandler.cs` updated with `help-articles.read` (admin/agent/customer) and `help-articles.manage` (admin-only)

### Verification

| Command | Result | Evidence |
|---|---|---|
| `dotnet build CustomerSupportCRM.slnx` | Passed | 0 warnings, 0 errors (re-verified 2026-10-07) |
| `dotnet test CustomerSupportCRM.slnx` (as originally recorded) | Passed | 216/216 passing (200 pre-existing + 16 new) — solution-wide total: 157 `CustomerManagement.Api.Tests` + 59 `CustomerManagement.Gateway.Tests` |
| `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj` (current) | Passed | 185/185 passing today, since Stories 25–27 added more API tests on top of this story's 157 (re-verified 2026-10-07) |
| `ng build` / `ng test` | N/A | This story is API-only; the admin Help Articles page is built in Story 27 |

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 25.**

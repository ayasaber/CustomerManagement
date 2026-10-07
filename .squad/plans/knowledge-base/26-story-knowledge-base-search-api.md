# Story 26 — Unified Knowledge Base Search API

## Prerequisites

- [Story 24 — Help Articles API](24-story-help-articles-api.md) completed: adds `HelpArticle` (`src/CustomerManagement.Api/Domain/KnowledgeBase/HelpArticle.cs`, `dbContext.HelpArticles`).
- [Story 25 — Solutions & Guides API](25-story-solutions-guides-api.md) completed: adds `Guide` + `GuideStep` (`src/CustomerManagement.Api/Domain/KnowledgeBase/Guide.cs`, `dbContext.Guides`).
- Reads the existing `FaqEntry` table (`src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs`, `dbContext.FaqEntries`) **read-only** — does not modify FAQ data, endpoints, or permissions in any way.

---

## Story Goal

Give agents and customers one search box that returns matching results from FAQs, help articles, and guides together — ranked by relevance, with retired content from any of the three never appearing — instead of three separate places to look.

1. A single `GET` endpoint accepts free-text input and returns a combined, ranked list of hits.
2. Each hit states which content type it is (`Faq`, `HelpArticle`, or `Guide`) before the caller opens it.
3. Search matches on **title and full body/step content**, not title only (confirmed assumption from the intake's "Extra notes" section — a guide with the right answer but a generic title must still surface).
4. Retired/inactive rows from any of the three content types are excluded unconditionally, for every caller — there is no `activeOnly` override here at all (unlike `FaqEndpoints.ListAsync`'s customer-only enforcement with the option for agents/admins to opt out); search is a browse-the-canon operation, not a management view.

Out of scope for this story:

- Any UI — that is [Story 27](27-story-knowledge-base-ui-and-gateway-routing.md). This story is API-only.
- Personalized or history-based ranking (explicitly out of scope per the intake) — ranking is relevance-to-the-typed-text only.
- Full-text indexing (e.g. SQL Server full-text catalogs) — `src/CustomerManagement.Api/Program.cs` line 26 configures `options.UseSqlServer(...)` with no full-text catalog setup anywhere in the codebase, so this story uses EF Core `Contains` (translated to `LIKE '%term%'`) plus in-memory scoring, not a database full-text feature.

---

## Context — Read These Files First

1. [Story 24](24-story-help-articles-api.md) and [Story 25](25-story-solutions-guides-api.md) — confirm the exact shape of `HelpArticle` and `Guide`/`GuideStep` before writing query code against them.
2. `src/CustomerManagement.Api/Domain/Faq/FaqEntry.cs` (full file) — `FaqEntry.Topic`, `.Question`, `.Answer` are the three text fields to search for FAQs.
3. `src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs` — lines ~36–61 (`ListAsync`) — precedent for querying `dbContext.FaqEntries.AsNoTracking()` and filtering `Where(entry => entry.IsActive)`.
4. `src/CustomerManagement.Api/Endpoints/Feedback/FeedbackEndpoints.cs` — lines ~84–112 (`ListAsync` with `page`/`pageSize`) — precedent for a paginated-looking response record shape, though this story's response is a single ranked list capped by a constant, not a page-by-page browse (see Backend Tasks below for why).
5. `src/CustomerManagement.Api/Infrastructure/Auth/Permissions.cs` — lines 40–41 (`FaqRead`/`FaqManage`) — add `KnowledgeBaseSearch = "knowledge-base.search"` next to the FAQ/help-article/guide constants.
6. `src/CustomerManagement.Api/Infrastructure/Auth/IdentitySeedData.cs` — same three assignment arrays referenced in [Story 24](24-story-help-articles-api.md)'s Context section — add `Permissions.KnowledgeBaseSearch` to Admin, Agent, and Customer (all three roles can search, same breadth as `FaqRead`).
7. `src/CustomerManagement.Api/Program.cs` — line 113 area (`app.MapFaqEndpoints();` / `app.MapHelpArticleEndpoints();` / `app.MapGuideEndpoints();`) — add `app.MapKnowledgeBaseSearchEndpoints();` after them.
8. `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs` — lines 79, 105, 115 (`Permissions.FaqRead` in all three role arrays) — add `Permissions.KnowledgeBaseSearch` to all three.

---

## Backend Tasks

### 1. Add contracts

Create file: `src/CustomerManagement.Api/Contracts/KnowledgeBase/KnowledgeBaseSearchContracts.cs`

```csharp
namespace CustomerManagement.Api.Contracts.KnowledgeBase;

public enum KnowledgeBaseContentType
{
    Faq,
    HelpArticle,
    Guide
}

public sealed record KnowledgeBaseSearchResultResponse(
    KnowledgeBaseContentType ContentType,
    Guid Id,
    string Title,
    string Snippet);

public sealed record KnowledgeBaseSearchResponse(string Query, IReadOnlyList<KnowledgeBaseSearchResultResponse> Results);
```

- `Title`: `FaqEntry.Question` for FAQs (FAQs have no separate "title" field — the question is the closest analog and is what a result list should show), `HelpArticle.Title` for articles, `Guide.Title` for guides.
- `Snippet`: `FaqEntry.Answer` (truncated to 200 characters) for FAQs, `HelpArticle.Body` (truncated to 200 characters) for articles, the first matching step's `Instruction` (or the first step if no step matched) for guides.

### 2. Add the search endpoint

Create file: `src/CustomerManagement.Api/Endpoints/KnowledgeBase/KnowledgeBaseSearchEndpoints.cs`

```csharp
public static class KnowledgeBaseSearchEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeBaseSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge-base").WithTags("KnowledgeBaseSearch");

        group.MapGet("/search", SearchAsync)
            .RequireAuthorization("Permission:" + Permissions.KnowledgeBaseSearch)
            .WithName("SearchKnowledgeBase")
            .WithSummary("Search FAQs, help articles, and guides together");

        return app;
    }
}
```

- Route: `GET /api/knowledge-base/search?q=<text>`. Empty/missing `q` (after trimming) returns `200` with an empty `Results` list — not a `400` — since an empty search box is a normal UI state (user cleared it), not a client error.
- **Query/term split:** split `q` on whitespace into distinct, case-insensitive, non-empty terms (max 10 terms; ignore extras beyond that to bound query cost).
- **Candidate fetch (match-any-term, so a guide/article with only one matching term still surfaces):** for each of the three tables, `Where(row => row.IsActive && terms.Any(term => EF.Functions.Like(row.<field>, $"%{term}%")))` across the relevant title+body fields — EF Core translates `EF.Functions.Like` directly to SQL `LIKE`, avoiding the client-eval warning that a C#-side `.Contains` combined with `.Any` over a dynamic term list can trigger against `UseSqlServer`. For guides, apply the same `Like` filter across the joined `GuideSteps.Instruction` via `Guides.Include(g => g.Steps)` and a `g.Title` / `g.Steps.Any(s => ...)` predicate.
- **In-memory scoring (after materializing each candidate set with `.ToListAsync()`):** for each term, award 3 points per case-insensitive occurrence in the title field (`Question`/`Title`) and 1 point per occurrence in the body field (`Answer`/`Body`/concatenated `Steps` instructions). Sum per result, order by score descending then by `Title`/`Question` ascending as a stable tiebreak, and cap the combined result list at the **top 50** results across all three content types (a fixed cap, not a `page`/`pageSize` pair — this is a "search box" result list, not a browsable paginated table like `FeedbackEndpoints.ListAsync`).
- Build each `Snippet` from the matched row, truncated to 200 characters with a trailing `…` when truncated.

### 3. Wire up DI/permissions

- `Permissions.cs` — add `public const string KnowledgeBaseSearch = "knowledge-base.search";`.
- `IdentitySeedData.cs` — add `[Permissions.KnowledgeBaseSearch] = "Search FAQs, help articles, and guides together."` to `baselinePermissions`; add `Permissions.KnowledgeBaseSearch` to all three (Admin, Agent, Customer) assignment arrays.
- `Program.cs` — add `app.MapKnowledgeBaseSearchEndpoints();`.

---

## Edge Cases & Failure Modes

- Empty or whitespace-only `q`: returns `200` with `Results: []`, not a validation error (searching is read-only and idempotent; an empty query is a valid "no input yet" UI state).
- `q` longer than the per-term cap (more than 10 whitespace-separated terms): only the first 10 terms are used for matching — document this explicitly since the executor must not silently drop terms without the caller knowing why a long paste returns fewer hits than expected (reflect the terms actually used in the response, or note the behavior in `WithSummary`).
- Retired FAQ/help article/guide: excluded unconditionally at the SQL `Where(row => row.IsActive && ...)` level for all three content types, for every caller (admin included) — there is no override, unlike `FaqEndpoints.ListAsync`'s agent/admin `activeOnly=false` escape hatch, because search result freshness is a hard business rule here ("retired content must never appear in search results... for agents or customers").
- A term matches a guide step but not the guide's title: the guide still surfaces (match-any-term, any-field) with its `Snippet` set to the first matching step's instruction, not a non-matching first step — confirms the "look inside body/step content, not title only" assumption from the intake.
- More than 50 combined matches exist: only the top 50 by score are returned — no pagination parameters are offered in this story's contract (see Backend Tasks for why); mark this cap in `## Done Criteria` as a documented and tested limit, not a silent truncation.
- Two or more results tie on score: ordered by `Title`/`Question` ascending as a stable, deterministic tiebreak so repeated identical searches return identical ordering.
- SQL injection via `q`: not a risk — `EF.Functions.Like` with a parameterized `$"%{term}%"` is translated to a parameterized SQL `LIKE`, never string-concatenated into raw SQL.
- A caller without `knowledge-base.search` (should not occur given all three roles are seeded with it, but defensively): `403` via `RequireAuthorization`.

---

## Test Plan

1. Create `tests/CustomerManagement.Api.Tests/KnowledgeBaseSearchEndpointsTests.cs`, using the same `IClassFixture<CustomerManagementApiFactory>` fixture pattern as `tests/CustomerManagement.Api.Tests/FaqEndpointsTests.cs`:
   - Seed (via HTTP as admin) one active FAQ, one active help article, one active guide whose title does **not** contain the search term but whose second step does; search for that term → all three appear in `Results`, the guide's `Snippet` is the matching step's instruction, and each result's `ContentType` matches its source.
   - Seed one retired FAQ, one retired help article, one retired guide, each containing the search term in their title; search for that term as admin, agent, and customer → none of the three retired rows appear in any role's results.
   - Search with a term matching only the FAQ's `Answer` (not its `Question`) → the FAQ still appears, scored lower than a result whose `Question`/`Title` matches the same term (assert relative ordering via `IndexOf`, not absolute position, per this repo's existing pagination-flakiness-avoidance convention).
   - Search with an empty `q` → `200`, empty `Results`.
   - Search with no matches anywhere → `200`, empty `Results`.
   - Non-authenticated/forbidden-permission call → `403` (if a role without `knowledge-base.search` can be constructed via `TestAuthHandler`; otherwise assert all three seeded roles succeed with `200`).
2. Extend `tests/CustomerManagement.Api.Tests/Infrastructure/TestAuthHandler.cs`'s Admin/Agent/Customer permission arrays with `Permissions.KnowledgeBaseSearch`, and add/extend a permission-seeding assertion test confirming it is seeded for all three roles.

---

## Migration / Rollback

- No schema migration — this story only reads existing tables (`FaqEntries`, `HelpArticles`, `Guides`, `GuideSteps`) and adds one new permission row via seeding.
- Rollback plan:
  1. Remove `app.MapKnowledgeBaseSearchEndpoints();` from `Program.cs`.
  2. Remove `Permissions.KnowledgeBaseSearch` from `IdentitySeedData.cs` assignments and `baselinePermissions`.
  3. Delete `KnowledgeBaseSearchEndpoints.cs` and `KnowledgeBaseSearchContracts.cs`.

---

## Verification Steps

1. **Backend builds:** `dotnet build` in `src/CustomerManagement.Api`.
2. **Backend tests:** `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj`.
3. **Regression:** run the full `dotnet test CustomerSupportCRM.slnx` to confirm no existing FAQ/help-article/guide/ticket/feedback tests broke.

---
## Review — completed 2026-10-07

- [x] KnowledgeBaseSearchContracts.cs, KnowledgeBaseSearchEndpoints.cs, Permissions.cs, IdentitySeedData.cs, Program.cs registration: added. No EF migration needed (read-only across existing tables + runtime permission seeding, no schema change).
- [x] Implementation deviation from the plan's `EF.Functions.Like` + `terms.Any(...)` sketch: implemented as `Where(entry => entry.IsActive)` at the DB level (fully translatable) followed by in-memory term-matching/scoring after `ToListAsync()`, to sidestep any SQL-translation uncertainty around a dynamic `List<string>.Any(predicate)` inside a `Where` clause. Functionally equivalent (match-any-term, case-insensitive, title 3x/body 1x weighting, top-50 cap) and fully covered by the Test Plan.
- [x] KnowledgeBaseSearchEndpointsTests.cs — all 6 Test Plan cases (all three content types returned with correct `ContentType`/snippet, retired content excluded for all three roles, body-only FAQ match scored lower via `IndexOf` ordering, empty query → empty results, no-match query → empty results, all three roles succeed) + permission-seeding test: added, passing
- [x] TestAuthHandler.cs updated with `knowledge-base.search` for admin/agent/customer
- [x] Full regression: `dotnet test CustomerSupportCRM.slnx` → 244/244 passing (233 pre-existing + 11 new)

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 27.**

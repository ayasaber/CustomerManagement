# Story 27 — Knowledge Base UI And Gateway Routing

## Prerequisites

- [Story 24 — Help Articles API](24-story-help-articles-api.md) completed.
- [Story 25 — Solutions & Guides API](25-story-solutions-guides-api.md) completed.
- [Story 26 — Unified Knowledge Base Search API](26-story-knowledge-base-search-api.md) completed.
- Story 23 completed: `../customer-portal/23-story-customer-portal-ui-and-gateway-routing.md` (this story reuses the same section-switcher pattern and `FaqApiService`/`CustomerPortalPageComponent` precedent it established).

---

## Story Goal

Wire the three new backend capabilities (help articles, guides, unified search from Stories 24–26) through the gateway and the Angular UI so that:

1. An admin can create, edit, and retire help articles and guides from dedicated admin pages (mirroring the existing `/admin/faq` page).
2. An admin can see a single place listing all help articles and guides, active and retired, for management — same as FAQ management today.
3. Agents and customers can browse active help articles and guides.
4. Agents and customers share **one search box** (a single new route, usable by any authenticated role) that searches FAQs, help articles, and guides together and shows what type each result is before it's opened.

Out of scope for this story:

- Any change to the existing FAQ admin page, FAQ browsing inside `CustomerPortalPageComponent`, or FAQ data — Story 21/23 already built this and it is untouched here.
- Any change to the agent dashboard's existing widgets (assigned tickets, tasks, quick replies, handoff inbox) — the new search surface is a separate routed page, not a new dashboard widget, to avoid touching `agent-dashboard-page.component.ts`'s existing dense state.

---

## Context — Read These Files First

1. `src/CustomerManagement.Gateway/ocelot.json` — lines 668–694 (`/api/faq` and `/api/faq/{faqId}` route entries) — exact JSON shape to copy for the 5 new routes (`DownstreamScheme: "http"`, `DownstreamHostAndPorts: [{ "Host": "localhost", "Port": 5215 }]`, `AuthenticationOptions.AuthenticationProviderKey: "Bearer"`, same `AddHeadersToRequest` block).
2. `src/CustomerManagement.Gateway/ROUTES.md` — the `## FAQ Route Contract` / `## Feedback Route Contract` tables (immediately after the `## Agent Dashboard Route Contract` section) — table format to extend with two new sections (`## Help Article Route Contract`, `## Guide Route Contract`) plus one row under a new `## Knowledge Base Search Route Contract` section.
3. `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` — line 23 `Assert.Equal(52, routes.Count);` to update, and lines 60–68 (`faqRoutes`/`feedbackRoutes` filter-and-assert pattern) as the exact precedent for the new `helpArticleRoutes`/`guideRoutes`/`knowledgeBaseSearchRoutes` assertions (lines 125–128 show the `Assert.Contains(... route.Methods.SequenceEqual(...))` pattern to copy).
4. `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs` — line 29 `for (var i = 0; i < 42; i++)` override loop — update the upper bound to match the new total route count.
5. `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` and `tests/CustomerManagement.Gateway.Tests/Infrastructure/DownstreamStubServer.cs` — existing FAQ/feedback routing-theory test cases and stub handlers — extend for the 5 new routes, following the same structure.
6. `src/CustomerManagement.Ui/src/app/app.routes.ts`:
   - Lines 34–66 — the `admin` route's `children` array (`ticket-taxonomy`, then `faq` at lines 61–66) — add `help-articles` and `guides` child routes here, same shape as the `faq` entry (`{ path: 'faq', component: AdminFaqPageComponent }`).
   - Lines 70–82 — `agent/dashboard` (`agentOnlyGuard`), `customer/portal` (`customerOnlyGuard`), `tickets` (`authenticatedGuard`) — the `tickets` entry is the precedent for a route shared by multiple roles via `authenticatedGuard`; add a new `knowledge-base` route the same way.
7. `src/CustomerManagement.Ui/src/app/features/admin/faq/admin-faq-page.component.ts` (full file) — direct precedent for the new `admin-help-articles-page.component.ts` and `admin-guides-page.component.ts` (create form + list + retire/reactivate toggle button that calls update with `isActive` flipped and the row's current `rowVersion`). For guides, the create/edit form additionally needs a repeatable ordered-steps input (add/remove/reorder step text rows before submit) — no existing component in this codebase has a repeatable-list form control, so build it as a local array of `FormControl<string>` inside the guide form, not a shared component.
8. `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts` — lines 14–19 (sidebar nav list, `<a routerLink="/admin/faq" ...>FAQ</a>` is the last entry) — add `Help Articles` and `Guides` links after it.
9. `src/CustomerManagement.Ui/src/app/core/services/faq-api.service.ts` (full file) — exact precedent for the new `help-articles-api.service.ts` and `guides-api.service.ts` (`withAutoRefresh`, `buildHeaders`, `mapError` pattern, `baseUrl` constant).
10. `src/CustomerManagement.Ui/src/app/core/models/faq.models.ts` (full file) — precedent for the new `help-article.models.ts` and `guide.models.ts` request/response interfaces (must mirror the exact field names/casing of the C# records from Stories 24–25: `HelpArticleResponse`, `CreateHelpArticleRequest`, `UpdateHelpArticleRequest`, `GuideResponse`, `GuideStepResponse`, `CreateGuideRequest`, `UpdateGuideRequest`).
11. `src/CustomerManagement.Ui/src/app/features/landing/customer-portal-page.component.ts`:
    - Line 17 (`type PortalSection = 'requests' | 'new-request' | 'faq' | 'feedback';`) and lines 120–130 (the `faq` section's template block) — precedent only (read-only reference) — this story does **not** add help-articles/guides browsing into the customer portal; browsing/search for both agents and customers lives in the new shared `knowledge-base` route (point 12 below) to avoid duplicating a browse UI in two places.
12. `src/CustomerManagement.Ui/src/app/app.routes.ts` line 76–79 (`{ path: 'tickets', canActivate: [authenticatedGuard], component: TicketManagementPageComponent }`) — exact precedent for a single route reachable by every authenticated role, which the new `knowledge-base` route copies.

---

## Gateway Tasks

### 1. Add routes

File: `src/CustomerManagement.Gateway/ocelot.json`

Add 5 new route entries, copying the exact JSON shape of the `/api/faq` / `/api/faq/{faqId}` entries at lines 668–694 (`DownstreamScheme: "http"`, `DownstreamHostAndPorts: [{ "Host": "localhost", "Port": 5215 }]`, same `AuthenticationOptions`/`AddHeadersToRequest` blocks):

| Upstream Path | Methods |
|---|---|
| `/api/help-articles` | `GET`, `POST` |
| `/api/help-articles/{articleId}` | `PUT` |
| `/api/guides` | `GET`, `POST` |
| `/api/guides/{guideId}` | `PUT` |
| `/api/knowledge-base/search` | `GET` |

That is 5 new route entries (52 → 57 total).

### 2. Update route contract docs

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Add a `## Help Article Route Contract` section (same table shape as `## FAQ Route Contract`) noting: read is customer/agent/admin, create/update requires admin `help-articles.manage`.
- Add a `## Guide Route Contract` section noting: read is customer/agent/admin, create/update requires admin `guides.manage`.
- Add a `## Knowledge Base Search Route Contract` section with the single `/api/knowledge-base/search` row noting: accessible to all three roles, returns combined FAQ/help-article/guide results, active content only.

### 3. Update gateway tests

- `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs`:
  - Change `Assert.Equal(52, routes.Count);` (line 23) to `Assert.Equal(57, routes.Count);`.
  - Add `helpArticleRoutes`, `guideRoutes`, and `knowledgeBaseSearchRoutes` filter-and-count blocks (same pattern as `faqRoutes`/`feedbackRoutes` at lines 60–68): `Assert.Equal(2, helpArticleRoutes.Count)`, `Assert.Equal(2, guideRoutes.Count)`, `Assert.Single(knowledgeBaseSearchRoutes)`.
  - Add `Assert.Contains(...)` route-presence assertions for all 5 new upstream paths with their expected methods (same pattern as lines 125–128).
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs` — change the override loop (line 29) from `42` to `47`.
- `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` — add routing-theory cases for `GET/POST /api/help-articles`, `PUT /api/help-articles/{articleId}`, `GET/POST /api/guides`, `PUT /api/guides/{guideId}`, `GET /api/knowledge-base/search`.
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/DownstreamStubServer.cs` — add stub handlers for the 5 new downstream paths, matching the existing FAQ/feedback stub style.

---

## Frontend Tasks

### 1. Add help-article and guide models and API services

Create file: `src/CustomerManagement.Ui/src/app/core/models/help-article.models.ts`, mirroring `faq.models.ts`, with `HelpArticleResponse` (`id, title, body, isActive, createdAtUtc, updatedAtUtc, rowVersion: number[]`), `CreateHelpArticleRequest` (`title, body`), `UpdateHelpArticleRequest` (`title, body, isActive, rowVersion: number[]`).

Create file: `src/CustomerManagement.Ui/src/app/core/models/guide.models.ts`, with `GuideStepResponse` (`stepNumber: number, instruction: string`), `GuideResponse` (`id, title, isActive, steps: GuideStepResponse[], createdAtUtc, updatedAtUtc, rowVersion: number[]`), `CreateGuideRequest` (`title, steps: string[]`), `UpdateGuideRequest` (`title, steps: string[], isActive, rowVersion: number[]`).

Create file: `src/CustomerManagement.Ui/src/app/core/services/help-articles-api.service.ts`, modeled exactly on `faq-api.service.ts`: `baseUrl = 'http://localhost:5101/api/help-articles'`; `listHelpArticles(activeOnly?: boolean)`, `createHelpArticle(request)`, `updateHelpArticle(articleId, request)`.

Create file: `src/CustomerManagement.Ui/src/app/core/services/guides-api.service.ts`, modeled exactly on `faq-api.service.ts`: `baseUrl = 'http://localhost:5101/api/guides'`; `listGuides(activeOnly?: boolean)`, `createGuide(request)`, `updateGuide(guideId, request)`.

### 2. Add search models and service

Create file: `src/CustomerManagement.Ui/src/app/core/models/knowledge-base-search.models.ts`:

```typescript
export type KnowledgeBaseContentType = 'Faq' | 'HelpArticle' | 'Guide';

export interface KnowledgeBaseSearchResultResponse {
  contentType: KnowledgeBaseContentType;
  id: string;
  title: string;
  snippet: string;
}

export interface KnowledgeBaseSearchResponse {
  query: string;
  results: KnowledgeBaseSearchResultResponse[];
}
```

Create file: `src/CustomerManagement.Ui/src/app/core/services/knowledge-base-search-api.service.ts`, modeled on `faq-api.service.ts`'s `buildHeaders`/`mapError`/`withAutoRefresh` pattern: `baseUrl = 'http://localhost:5101/api/knowledge-base'`; `search(query: string): Observable<KnowledgeBaseSearchResponse>` calling `GET ${baseUrl}/search` with an `HttpParams().set('q', query)`.

### 3. Add admin Help Articles and Guides pages

Create file: `src/CustomerManagement.Ui/src/app/features/admin/help-articles/admin-help-articles-page.component.ts`, modeled directly on `admin-faq-page.component.ts` (create form with `title`/`body` fields + list + a "Retire"/"Reactivate" button per row calling `updateHelpArticle` with `isActive` flipped and the row's current `rowVersion`).

Create file: `src/CustomerManagement.Ui/src/app/features/admin/guides/admin-guides-page.component.ts`, modeled on `admin-faq-page.component.ts`'s create-form-plus-list-plus-retire-toggle structure, extended with a repeatable ordered-steps input: a local array of step `FormControl<string>`s with "Add step"/"Remove step" buttons, submitted as `steps: string[]` in request order.

File: `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts`

- Add `<a routerLink="/admin/help-articles" routerLinkActive="active">Help Articles</a>` and `<a routerLink="/admin/guides" routerLinkActive="active">Guides</a>` to the sidebar, after the existing FAQ link.

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add `{ path: 'help-articles', component: AdminHelpArticlesPageComponent }` and `{ path: 'guides', component: AdminGuidesPageComponent }` under the existing `admin` route's `children` array, after the `faq` entry, with matching `import` statements.

### 4. Add the shared Knowledge Base browse/search page

Create file: `src/CustomerManagement.Ui/src/app/features/knowledge-base/knowledge-base-page.component.ts` — a standalone component, role-agnostic (reachable by any authenticated role, same access breadth as `TicketManagementPageComponent`):

- A single search input bound to a signal; on input (debounced) or explicit submit, calls `KnowledgeBaseSearchApiService.search(query)` and renders `results` as a list, each row showing its `contentType` as a visible label/badge (e.g. "FAQ" / "Help Article" / "Guide") before any detail is shown, followed by `title` and `snippet`.
- Below the search box (or as the default view when the query is empty), two browse lists: active help articles (`HelpArticlesApiService.listHelpArticles()`, no `activeOnly` argument needed — the server already forces active-only for non-admin roles the same way `FaqEndpoints.ListAsync` does) and active guides (`GuidesApiService.listGuides()`), each rendered as a simple title list; clicking a guide expands its ordered `steps` list inline (no separate detail route needed for a first version).
- Empty state when a search returns no results, and when there are no active articles/guides yet.

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add `{ path: 'knowledge-base', canActivate: [authenticatedGuard], component: KnowledgeBasePageComponent }`, following the exact shape of the existing `{ path: 'tickets', canActivate: [authenticatedGuard], component: TicketManagementPageComponent }` entry (lines 76–79), with a matching `import`.

---

## Edge Cases & Failure Modes

- Admin submits a guide create/update form with zero steps: client-side blocks submit (disable the submit button while the steps array is empty), backed by the server's `400` from Story 25 as a second line of defense if the client check is somehow bypassed.
- Admin removes all steps from an existing guide during edit and submits: same client-side block as above; the server's "at least 1 step" validation (Story 25) is the authoritative guard.
- A non-admin directly navigates to `/admin/help-articles` or `/admin/guides`: already blocked today by `adminOnlyGuard` on the parent `admin` route (`src/CustomerManagement.Ui/src/app/app.routes.ts` line 35) — unchanged by this story.
- A customer or agent navigates to `/knowledge-base` with no active content yet seeded: empty-state messages render for both the browse lists and any search attempt, not a blank screen or console error.
- Knowledge Base search request fails (gateway/API unreachable): surfaced via `KnowledgeBaseSearchApiService`'s `mapError` (mirroring `faq-api.service.ts`'s `mapError`) as an inline error message in `KnowledgeBasePageComponent`, not an unhandled promise rejection.
- Search input with only whitespace: the component does not call the API at all for a blank/whitespace query (consistent with Story 26's API-level empty-query handling, this is a client-side optimization to avoid an unnecessary round trip) and instead shows the default browse lists.

---

## Test Plan

1. Create `src/CustomerManagement.Ui/src/app/core/services/help-articles-api.service.spec.ts` and `guides-api-api.service.spec.ts`, following the same `HttpClientTestingModule`/`HttpTestingController` conventions as `src/CustomerManagement.Ui/src/app/core/services/faq-api.service.spec.ts`.
2. Create `src/CustomerManagement.Ui/src/app/core/services/knowledge-base-search-api.service.spec.ts`, same conventions, asserting the `q` query param is sent correctly.
3. Create `src/CustomerManagement.Ui/src/app/features/admin/help-articles/admin-help-articles-page.component.spec.ts` and `admin-guides-page.component.spec.ts`, following `admin-faq-page.component.spec.ts`'s existing test conventions for create + retire-toggle behavior; the guides spec additionally covers adding/removing step rows before submit.
4. Create `src/CustomerManagement.Ui/src/app/features/knowledge-base/knowledge-base-page.component.spec.ts`:
   - Typing a query and submitting renders results with a visible content-type label per row (assert the badge/label text, e.g. "FAQ"/"Help Article"/"Guide").
   - Empty search results renders an empty-state message.
   - Default view (no query yet) renders the active help-article and guide browse lists from mocked services.
   - Expanding a guide row renders its steps in order.
5. Extend the Gateway Tasks test files per the Gateway Tasks section above.

---

## Verification Steps

1. **Backend builds:** `dotnet build` in `src/CustomerManagement.Api`.
2. **Backend tests:** `dotnet test` in `tests/CustomerManagement.Api.Tests`.
3. **Gateway tests:** `dotnet test` in `tests/CustomerManagement.Gateway.Tests`.
4. **Frontend builds:** `ng build` in `src/CustomerManagement.Ui`.
5. **Frontend tests:** `ng test --watch=false --browsers=ChromeHeadless` in `src/CustomerManagement.Ui`.
6. **Regression:** run the full `dotnet test CustomerSupportCRM.slnx` and the full Angular suite to confirm no existing FAQ/ticket/customer-portal/admin flows broke.

---

## Done Criteria

- [x] An admin can create, edit, and retire help articles from `/admin/help-articles`.
- [x] An admin can create, edit, and retire solutions/guides (with an ordered step list) from `/admin/guides`.
- [x] An admin can see all help articles and all guides (active and retired) in their respective management pages, the same way FAQ management already works.
- [x] Agents and customers can browse active help articles and guides from `/knowledge-base`; neither role can create, edit, or retire any of it.
- [x] A single search box on `/knowledge-base` returns matching results from FAQs, help articles, and guides together, not as three separate result sets.
- [x] Every search result clearly shows its content type (FAQ, Help Article, or Guide) before it is opened.
- [x] Retired FAQs, help articles, or guides never appear in search results or browse lists for any role.
- [x] This feature does not add, remove, or change who can manage FAQs, and FAQ data/endpoints/UI are untouched.
- [x] All 5 new gateway routes are registered, documented in `ROUTES.md`, and covered by updated route-count/contract tests.

---
## Review — completed 2026-10-07

### Implementation Review

- [x] Gateway — `ocelot.json` (5 new routes), `ROUTES.md` (3 new contract sections), `GatewayRouteContractTests.cs` (52→57 + `helpArticleRoutes`/`guideRoutes`/`knowledgeBaseSearchRoutes` assertions), `CustomerManagementGatewayFactory.cs` (42→47), `CustomerManagementGatewayRoutingTests.cs` (7 new `InlineData` cases + permissions), `DownstreamStubServer.cs` (3 new stub route groups + permission resolution)
- [x] Bug found and fixed during implementation: `KnowledgeBaseSearchResultResponse.ContentType` was typed as the `KnowledgeBaseContentType` C# enum in Story 26, which System.Text.Json serializes as a raw integer by default — this codebase's convention (seen in `TicketResponse.Status`) is `string` via `.ToString()` at the endpoint boundary. Fixed `KnowledgeBaseSearchContracts.cs`/`KnowledgeBaseSearchEndpoints.cs` (and the Story 26 test assertions) so the Angular string-literal union type matches the wire format
- [x] Frontend — `help-article.models.ts`, `guide.models.ts`, `knowledge-base-search.models.ts`, `help-articles-api.service.ts`, `guides-api.service.ts`, `knowledge-base-search-api.service.ts`, `admin-help-articles-page.component.ts`, `admin-guides-page.component.ts` (repeatable FormArray steps input), `admin-shell.component.ts` nav links, `knowledge-base-page.component.ts` (shared browse/search page), `app.routes.ts` (`/admin/help-articles`, `/admin/guides`, `/knowledge-base`)
- [x] Implementation note: the search box submits explicitly (Enter/button), not on debounced input-as-you-type, to keep behavior deterministic and testable — a reasonable simplification of the plan's "on input (debounced) or explicit submit" wording

### Test Coverage

- [x] `help-articles-api.service.spec.ts`, `guides-api.service.spec.ts`, `knowledge-base-search-api.service.spec.ts`
- [x] `admin-help-articles-page.component.spec.ts`, `admin-guides-page.component.spec.ts`
- [x] `knowledge-base-page.component.spec.ts` (default browse lists, content-type badges per result, empty-results state, guide step expansion)

### Verification

| Command | Result | Evidence |
|---|---|---|
| `dotnet build CustomerSupportCRM.slnx` | Passed | 0 warnings, 0 errors (re-verified 2026-10-07) |
| `dotnet test tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj` | Passed | 185/185 passing — unchanged from Story 26 (this story only fixed 3 existing assertion lines, adding/removing no tests) |
| `dotnet test tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj` | Passed | 66/66 passing (59 pre-existing + 7 new routes) |
| `ng build` | Passed | 0 errors |
| `ng test --watch=false --browsers=ChromeHeadless` | Passed | 84/84 passing (60 pre-existing + 24 new) |

Combined solution total: 251/251 (244 pre-existing + 7 new Gateway tests). Reconciling with Story 26's "244/244": that figure was Story 26's own combined API+Gateway total (185 API + 59 Gateway), not an API-only count — this story's "185/185 API" is consistent with it, not a regression. No tests were lost between Story 26 and this story.

# knowledge-base — plan overview

Entry point for the **knowledge-base** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 24 | [24-story-help-articles-api.md](24-story-help-articles-api.md) | Help Articles API | none | None |
| 25 | [25-story-solutions-guides-api.md](25-story-solutions-guides-api.md) | Solutions & Guides API | none | None |
| 26 | [26-story-knowledge-base-search-api.md](26-story-knowledge-base-search-api.md) | Unified Knowledge Base Search API | none | 24, 25 |
| 27 | [27-story-knowledge-base-ui-and-gateway-routing.md](27-story-knowledge-base-ui-and-gateway-routing.md) | Knowledge Base UI And Gateway Routing | none | 24, 25, 26 |

## Dependency notes

- Story 24 (Help Articles) and Story 25 (Guides) are independent standalone domains with no dependency on each other or on the existing FAQ feature; they can be planned/executed in parallel, though they are sequenced 24 then 25 here for a single global execution order.
- Story 26 is the unified search layer across FAQs (existing, read-only), help articles (Story 24), and guides (Story 25) — it requires both 24 and 25 to be merged first, since it queries their tables directly.
- Story 27 is the end-to-end integration layer (gateway routing + Angular UI) after all three backend stories (24, 25, 26) are stable. It adds admin management pages for help articles and guides (mirroring the existing `/admin/faq` page) and a new shared `/knowledge-base` route for agent/customer browse + unified search, without touching the existing FAQ feature's data, endpoints, or UI.

## Requirement Source

* [Knowledge Base Intake](../../stories/knowledge-base/knowledge-base/intake.md)

## Story Plans

* [Story 24 — Help Articles API](24-story-help-articles-api.md)
* [Story 25 — Solutions & Guides API](25-story-solutions-guides-api.md)
* [Story 26 — Unified Knowledge Base Search API](26-story-knowledge-base-search-api.md)
* [Story 27 — Knowledge Base UI and Gateway Routing](27-story-knowledge-base-ui-and-gateway-routing.md)

## API Contract and Implementation

* [Gateway API Route Contract](../../../src/CustomerManagement.Gateway/ROUTES.md)
* Existing FAQ precedent (unmodified by this feature): [FAQ Endpoints](../../../src/CustomerManagement.Api/Endpoints/Faq/FaqEndpoints.cs)

## Verification References

* [API Test Project](../../../tests/CustomerManagement.Api.Tests/CustomerManagement.Api.Tests.csproj)
* [Gateway Test Project](../../../tests/CustomerManagement.Gateway.Tests/CustomerManagement.Gateway.Tests.csproj)
* [Gateway Route Contract Tests](../../../tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs)

# ticket-management — plan overview

Entry point for the **ticket-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 13 | `13-story-ticket-management.md` | Ticket Management | _none_ | 01-05, 06, 08-11 |

## Dependency notes

- Story 13 depends on Customer Management entity contracts (`Customer`) and Security & Administration identity/permission model (`ApplicationUser`, permission claims).
- Ticket endpoints, gateway routes, and Angular pages are planned in one contract-first story so API, gateway, and UI stay aligned on route/DTO names.

# agent-dashboard — plan overview

Entry point for the **agent-dashboard** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 14 | [14-story-dashboard-assigned-work-and-customer-context-apis.md](14-story-dashboard-assigned-work-and-customer-context-apis.md) | Dashboard Assigned Work And Customer Context APIs | none | 13 (ticket-management), 11 (security-administration) |
| 15 | [15-story-ticket-tasks-and-reminders.md](15-story-ticket-tasks-and-reminders.md) | Ticket Tasks And Reminders | none | 14, 13 |
| 16 | [16-story-shared-quick-replies-library.md](16-story-shared-quick-replies-library.md) | Shared Quick Replies Library | none | 15, 08 (security-administration) |
| 17 | [17-story-ticket-internal-notes-mentions-and-handoff.md](17-story-ticket-internal-notes-mentions-and-handoff.md) | Ticket Internal Notes Mentions And Handoff | none | 15, 13 |
| 18 | [18-story-agent-dashboard-ui-and-gateway-routing.md](18-story-agent-dashboard-ui-and-gateway-routing.md) | Agent Dashboard UI And Gateway Routing | none | 14, 15, 16, 17 |

## Dependency notes

- Story 14 establishes dashboard read contracts early so UI can integrate against stable API shapes.
- Story 15 introduces ticket-linked tasks and backfills Story 14 open-task placeholder with real data.
- Story 16 isolates quick-reply catalog management to admin/agent permission boundaries.
- Story 17 separates collaboration notes/mentions from ticket assignment side effects and keeps handoff explicit.
- Story 18 is the end-to-end integration layer (gateway + Angular) after backend contracts are stable.

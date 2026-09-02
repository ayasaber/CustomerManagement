# ticket-conversation — plan overview

Entry point for the **ticket-conversation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 19 | [19-story-ticket-conversation.md](19-story-ticket-conversation.md) | Ticket Conversation | none | 13 (ticket-management), 16-18 (agent-dashboard) |

## Dependency notes

- Story 19 creates a customer-visible ticket conversation thread and keeps it strictly separate from agent-only internal notes from Story 17.
- Gateway contract changes in Story 19 must update route count assertions and downstream override loops in gateway tests to avoid brittle failures.
- UI integration in Story 19 reuses the quick-reply library introduced in Story 16 and must not duplicate quick-reply storage.

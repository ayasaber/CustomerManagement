# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-customer-profile-and-contact-foundation.md](01-story-customer-profile-and-contact-foundation.md) | Customer Profile and Contact Foundation | none | none |
| 02 | [02-story-customer-notes-and-attachments.md](02-story-customer-notes-and-attachments.md) | Customer Notes and Attachments | none | 01 |
| 03 | [03-story-customer-interaction-history-read-model.md](03-story-customer-interaction-history-read-model.md) | Customer Interaction History Read Model | none | 01 |
| 04 | [04-story-customer-management-gateway-routing.md](04-story-customer-management-gateway-routing.md) | Customer Management Gateway Routing | none | 01, 02, 03 |
| 05 | [05-story-agent-customer-management-ui.md](05-story-agent-customer-management-ui.md) | Agent Customer Management UI | none | 01, 02, 03, 04 |

## Dependency notes

- Story 01 establishes canonical customer identity and core profile/contact contracts used by all later stories.
- Stories 02 and 03 can proceed in parallel after Story 01 because they share the same customer identity but separate bounded concerns.
- Story 04 must follow backend endpoint delivery to avoid gateway routes pointing at incomplete downstream APIs.
- Story 05 depends on all prior stories for complete end-to-end behavior and stable API contracts.
- Communication Channels is expected to populate interaction events consumed by Story 03; this plan only covers the read/query side.
- Ticket Management is expected to consume customer identifiers produced by Story 01.

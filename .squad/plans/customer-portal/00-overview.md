# customer-portal — plan overview

Entry point for the **customer-portal** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 20 | [20-story-customer-ticket-self-service-api.md](20-story-customer-ticket-self-service-api.md) | Customer Ticket Self-Service API Enablement | none | 13 (ticket-management), 19 (ticket-conversation) |
| 21 | [21-story-faq-management-api.md](21-story-faq-management-api.md) | FAQ Management API | none | None |
| 22 | [22-story-customer-feedback-api.md](22-story-customer-feedback-api.md) | Customer Feedback API | none | None |
| 23 | [23-story-customer-portal-ui-and-gateway-routing.md](23-story-customer-portal-ui-and-gateway-routing.md) | Customer Portal UI And Gateway Routing | none | 20, 21, 22, 19 (ticket-conversation) |

## Dependency notes

- Story 20 extends the existing ticket and ticket-conversation APIs (Stories 13 and 19) with customer-initiated attachments and replies; it does not change ticket/message ownership scoping, only what a customer is allowed to do inside their own scope.
- Story 21 (FAQ) and Story 22 (Feedback) are independent standalone domains with no dependency on each other or on Story 20; they can be planned/executed in parallel if desired, though they are sequenced 21 then 22 here for a single global execution order.
- Story 23 is the end-to-end integration layer (gateway routing + Angular UI) after all three backend stories (20, 21, 22) are stable, and reuses the `TicketConversationPanelComponent` built in Story 19 rather than building a new conversation UI.
- Story 23 also replaces the customer portal's route guard from the generic `authenticatedGuard` to a dedicated `customerOnlyGuard`, to keep the customer-facing area clearly separated from agent/admin screens.

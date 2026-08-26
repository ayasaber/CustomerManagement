# 03 - Customer Interaction History Read Model

## Goal

Provide a chronological, read-only interaction history for each customer so agents can understand prior communications without asking customers to repeat context.

## Scope

- Define interaction history read model contract.
- Implement API endpoint to query timeline by customer.
- Normalize supported channels for display (email, WhatsApp, live chat, SMS, web forms).
- Return reverse-chronological timeline with source metadata.

## In Scope Acceptance Criteria

- Agents can view a chronological interaction history for a customer across supported channels.

## Out of Scope

- Manual interaction logging by agents.
- Channel ingestion/integration mechanics that create source events.
- Frontend rendering and gateway routing.

## Implementation Notes

- Treat interaction history as read-only projection of channel events.
- Include stable fields: interactionId, channel, direction, timestamp, summary/snippet, sourceRef.
- Support pagination and filtering for scalable timelines.
- Define behavior for empty timelines and partial source data.

## Tasks

1. [x] Define interaction event projection schema.
2. [x] Implement query endpoint by customer identifier.
3. [x] Add ordering/pagination support.
4. [x] Add channel normalization and DTO mapping.
5. [x] Add authorization checks.
6. [x] Add tests for ordering, filtering, and empty-result behavior.

## Test Plan

- Unit tests for mapping and normalization.
- Integration tests for query ordering/pagination.
- Authorization tests for agent-only access.

## Dependencies

- 01 - Customer Profile and Contact Foundation.
- Upstream communication channel events are assumed to exist in source stores.

## Produces

- Timeline API consumed by Story 05 UI.

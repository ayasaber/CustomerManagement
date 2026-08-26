# 04 - Customer Management Gateway Routing

## Goal

Expose all customer-management capabilities through Ocelot under a dedicated /api/customers route surface.

## Scope

- Add Ocelot routes for profile, contact details, notes, attachments, and interaction history endpoints.
- Validate downstream mapping, auth forwarding, and error propagation.
- Document route contract for consumers.

## In Scope Acceptance Criteria

- All customer data is exposed through API gateway (Ocelot) under dedicated customer-management routes.

## Out of Scope

- Backend domain/API feature implementation itself.
- Frontend implementation.

## Implementation Notes

- Route prefix target: /api/customers.
- Preserve consistent method/path semantics from downstream API.
- Ensure status codes and validation errors are transparent through gateway.

## Tasks

1. [x] Add/update Ocelot route definitions for all customer-management endpoints.
2. [x] Configure auth/claims forwarding as required for agent identity.
3. [x] Verify route conflicts do not occur with existing or planned features.
4. [x] Add integration checks for gateway path to downstream mapping.
5. [x] Document public gateway route table.

## Test Plan

- Gateway integration tests for each route and HTTP verb.
- Negative tests for unauthorized and invalid payload cases.
- Error propagation checks for downstream failures.

## Dependencies

- 01 - Customer Profile and Contact Foundation.
- 02 - Customer Notes and Attachments.
- 03 - Customer Interaction History Read Model.

## Produces

- Stable external route surface for Story 05 and other consumers.

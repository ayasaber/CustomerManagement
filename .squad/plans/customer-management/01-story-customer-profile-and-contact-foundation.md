# 01 - Customer Profile and Contact Foundation

## Goal

Deliver the backend foundation for customer records so agents can create, view, and edit profiles and view complete contact details in one place.

## Scope

- Define customer entity and persistence schema for core fields.
- Implement API endpoints for create, read, and update customer profile.
- Implement API endpoint(s) for retrieving contact details associated with a customer.
- Enforce agent-only authorization for customer profile and contact access.

## In Scope Acceptance Criteria

- Agents can create, view, and edit a customer profile (name, company, contact details, and extensible core fields).
- Agents can view all contact details associated with a customer.

## Out of Scope

- Notes, attachments, and interaction history.
- API gateway route exposure.
- Frontend pages and forms.

## Implementation Notes

- Backend stack: ASP.NET Core Web API + EF Core.
- Introduce stable customer identifier contract for cross-feature usage.
- Include optimistic concurrency for profile updates.
- Validate core fields server-side and return consistent validation errors.

## Tasks

1. [x] Create persistence model and migration for customer + contact details.
2. [x] Implement customer profile create endpoint.
3. [x] Implement customer profile read endpoint.
4. [x] Implement customer profile update endpoint.
5. [x] Implement contact details retrieval endpoint.
6. [x] Add auth policy checks for agent-only access.
7. [x] Add API tests for success and validation/failure paths.

## Test Plan

- Unit tests for validation and domain invariants.
- Integration tests for CRUD and contact retrieval.
- Authorization tests for non-agent denial.

## Dependencies

- None.

## Produces

- Stable customer identity and profile/contact API contract used by Stories 02-05.

# 02 - Customer Notes and Attachments

## Goal

Enable agents to add durable notes and attach files to a customer record so important context is retained and retrievable.

## Scope

- Implement notes add/list APIs for customer records.
- Implement attachment metadata APIs plus upload/retrieve behavior.
- Integrate attachment storage through blob/object storage abstraction.
- Enforce file constraints and access checks.

## In Scope Acceptance Criteria

- Agents can add free-text notes to a customer record.
- Agents can attach files to a customer record and later retrieve them.

## Out of Scope

- Customer profile CRUD and contact details foundation.
- Interaction history timeline.
- Frontend implementation and gateway routing.

## Implementation Notes

- Store file binary in blob/object storage; persist metadata in relational DB.
- Capture audit metadata: createdBy, createdAt, filename, contentType, size.
- Define attachment limits (size and allowed MIME types) as configuration.
- Add storage failure handling and trace logging.

## Tasks

1. [x] Create persistence models/migrations for notes and attachment metadata.
2. [x] Implement add/list notes endpoints by customer.
3. [x] Implement attachment upload endpoint with storage abstraction.
4. [x] Implement attachment list endpoint.
5. [x] Implement attachment download/retrieve endpoint.
6. [x] Add authorization and ownership checks.
7. [x] Add tests for upload constraints and retrieval behavior.

## Test Plan

- Unit tests for note/attachment validation.
- Integration tests for upload/list/retrieve and storage failures.
- Security tests for unauthorized access.

## Dependencies

- 01 - Customer Profile and Contact Foundation.

## Produces

- Notes and attachment APIs consumed by Story 05 UI.

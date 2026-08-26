# 05 - Agent Customer Management UI

## Goal

Deliver an Angular agent interface for customer management that supports profile/contact viewing and editing, interaction history viewing, notes creation, and attachment upload/retrieval.

## Scope

- Build customer details workspace in Angular.
- Implement profile create/edit UI backed by customer-management APIs.
- Implement contact details view, interaction timeline view, notes view/composer, and attachments panel.
- Handle API validation and loading/error states.

## In Scope Acceptance Criteria

- Agents can create, view, and edit customer profiles.
- Agents can view customer contact details.
- Agents can view chronological interaction history.
- Agents can add free-text notes.
- Agents can attach and retrieve files.

## Out of Scope

- Customer-facing self-service UI.
- Ticket creation/management workflows.
- Communication channel integration mechanics.

## Implementation Notes

- Use agent-only route guards.
- Keep UI modular (profile/contact/history/notes/attachments sections).
- Provide upload progress and clear failure messaging.
- Align form validation with backend constraints.

## Tasks

1. [x] Create customer management page shell and routing.
2. [x] Implement profile create/edit forms.
3. [x] Implement contact details display section.
4. [x] Implement read-only interaction history timeline section.
5. [x] Implement notes composer and notes list section.
6. [x] Implement attachments upload/list/download section.
7. [x] Add client-side service layer and API error handling.
8. [x] Add component/service tests and key interaction tests.

## Test Plan

- Component tests for each section.
- Service tests for API integration and error handling.
- E2E smoke flow: create/update customer, add note, upload/download file, verify timeline display.

## Dependencies

- 01 - Customer Profile and Contact Foundation.
- 02 - Customer Notes and Attachments.
- 03 - Customer Interaction History Read Model.
- 04 - Customer Management Gateway Routing.

## Produces

- Agent-facing end-to-end feature implementation ready for incremental enhancements.

# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-management/ticket-management/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, tickets`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Ticket Management
```

---

## Description

```
Part of the Customer Support CRM. This feature lets agents create and
track support tickets raised against a customer, categorize and
prioritize them using fixed, admin-managed lists, assign them to
agents (either by self-assignment or by an admin/lead assigning), and
manage their status through to resolution, including manual escalation.

This feature lives in the same backend project as Customer Management
and Security & Administration (the team's current architecture is one
shared backend project behind the Ocelot gateway, not a per-feature
microservice split). Tickets reference Customer and User/Agent records
directly via EF Core foreign keys — no cross-service API calls are
needed for these relationships since everything is in the same
database/project.

This feature depends on:
- Customer Management (a ticket must reference an existing Customer).
- Security & Administration (a ticket's assignee must be a real User
  with the Agent role; permission checks for who can escalate, change
  category/priority lists, etc. come from that feature's permission
  model, not a hardcoded role check).

User stories:

1. As an agent or admin, I want to create a ticket for a customer, so
   that a reported issue is tracked from the moment it's raised.
   (Customers do not create tickets directly in this story's scope —
   customer-initiated creation is deferred to the separate Customer
   Portal feature, not yet planned.)

2. As an agent, I want to assign a category and priority to a ticket
   from fixed, admin-managed lists, so that tickets are consistently
   classified across the team.

3. As an agent, I want to assign a ticket to myself, so that I can
   pick up unassigned work without waiting on someone else.

4. As an admin or team lead, I want to assign or reassign a ticket to
   a specific agent, so that I can balance workload or route tickets
   to the right specialist.

5. As an agent, I want to update a ticket's status as I work it (e.g.
   New, In Progress, Waiting on Customer, Resolved, Closed), so that
   its current state is always visible to the team.

6. As an agent or admin, I want to manually escalate a ticket, so
   that urgent or stuck issues get visibility and priority attention.
   Escalation is a manual action — the system does not escalate
   tickets automatically based on time or inactivity.

7. As an agent, I want to see a ticket's full history (status changes,
   assignments, escalations, category/priority changes, with who made
   each change and when), so that I can understand what's already been
   done before taking further action.

8. As an admin, I want to manage the fixed list of categories and the
   fixed list of priorities (add/edit/deactivate values), so that the
   classification options can evolve without a code change, while
   still being a controlled, consistent list rather than free text.

9. As an agent, I want a UI to list, filter, and open tickets (by
   status, category, priority, assigned agent, customer), so that I
   can find and manage relevant work quickly.
```

---

## Acceptance criteria

```
- Categories and priorities are each a fixed, admin-managed list
  (not free text). Admins can add, edit, and deactivate (not
  hard-delete) values in each list. A deactivated value already in use
  on existing tickets remains visible on those tickets but is not
  selectable for new/updated tickets.
- A ticket is always linked to exactly one existing Customer
  (foreign key to the Customer Management feature's Customer entity)
  and, once assigned, exactly one User with the Agent role (foreign
  key to Security & Administration's User entity).
- A ticket can be created by any authenticated User with the Agent or
  Admin role, on behalf of an existing customer. Ticket creation is
  not exposed to the Customer role in this story.
- Both agent self-assignment and admin/lead assignment/reassignment of
  any ticket are supported through the API. Both paths are subject to
  a permission check (from Security & Administration's permission
  model) rather than a hardcoded role name check.
- A ticket has a status field with a fixed set of states (at minimum:
  New, In Progress, Waiting on Customer, Resolved, Closed) and valid
  transitions between them are enforced server-side (e.g. a Closed
  ticket cannot silently become New again without an explicit reopen
  action).
- Escalation is a manual, explicit action (an API call an agent or
  admin triggers), never automatic/time-based. An escalated ticket is
  visibly flagged as escalated and this is reflected in ticket history.
- Every meaningful change to a ticket (status change, assignment
  change, category/priority change, escalation, reopen) is recorded in
  a ticket history/audit trail with: who made the change, what changed
  (old value -> new value), and when (UTC).
- Agents can retrieve ticket history for a given ticket, ordered
  chronologically.
- Tickets can be listed and filtered by status, category, priority,
  assigned agent, and customer, with pagination for large result sets.
- All ticket-management endpoints require authentication and enforce
  permission checks from the Security & Administration feature (no
  hardcoded "if role == Agent" checks — same retrofit pattern already
  planned for Customer Management should be followed here from the
  start, not retrofitted later).
- Mutable ticket fields (status, category, priority, assignee) use the
  same RowVersion-based optimistic concurrency pattern already
  established on Customer, since two agents editing the same ticket
  simultaneously (e.g. both trying to assign it) is a realistic race
  condition in a support team.
- The ticket list/detail UI (Angular) is clean, modern, and consistent
  with the Customer Management and Security & Administration UI
  already built or planned — not a bare/unstyled scaffold.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** Should be planned/implemented after
  Security & Administration's core User/Role/Permission model exists,
  since ticket assignment and permission checks depend on it.
- **Depends on code areas or other stories:**
  - Customer Management (Customer entity, foreign key target).
  - Security & Administration (User entity for assignee, permission
    model for authorization checks on assignment/escalation/category
    management).

## Extra notes (optional)

- This feature stays in the same backend project as Customer
  Management and Security & Administration (current team decision:
  one shared backend project behind the Ocelot gateway, not a
  per-feature microservice split). Do not scaffold a new/separate
  project for this feature.
- Ocelot routing: expose this feature's endpoints under a dedicated
  `/api/tickets` route prefix (and `/api/ticket-categories`,
  `/api/ticket-priorities` for the admin-managed lists), consistent
  with the `/api/customers` pattern already used.
- Open question for planning to surface explicitly rather than assume
  silently: exact list of valid status values and which transitions
  between them are allowed/disallowed (e.g. can a Closed ticket be
  reopened, and by whom). State the assumed state machine explicitly
  in the plan so it can be reviewed and corrected if wrong.

## Plan specificity requirements (read carefully)

This is a spec-driven-development (SDD) workflow: the plan produced
from this intake is the contract that any coding agent (Copilot today,
a different agent or developer later) will implement. Two different
agents given the same plan must produce materially the same result.
To achieve that, every story/task in the generated plan must be
concrete, not descriptive. Specifically:

- Every API task must specify the exact route, HTTP verb, request DTO
  shape (field names and types), response DTO shape, and status codes
  per outcome (success and each failure case) — not just "implement
  the assign ticket endpoint."
- Every entity/schema task must specify exact field names, types, and
  constraints (nullable/required, max length, foreign keys) — not just
  "create a Ticket entity."
- Follow the naming conventions already established in Customer
  Management: PascalCase entities, plural controller names (e.g.
  TicketsController, TicketCategoriesController), Guid primary keys,
  `*AtUtc` suffix for timestamp fields, and a `RowVersion` optimistic
  concurrency column on Ticket (and any other mutable entity here),
  matching the pattern used on Customer.
- The ticket status state machine (valid states and valid transitions
  between them) must be spelled out explicitly as part of the plan,
  not left implicit — list every state and every allowed transition.
- Every UI task must specify concrete structure: which route/page it
  lives at, the page's layout, what components/sections it contains,
  and what data each list/table shows (exact columns, filters,
  sorting, pagination) — not a single vague "build the tickets page"
  line. Exact colors/CSS values remain an implementation-time decision.
- If the planner is uncertain about a specific value (an exact status
  code, an exact field name, an exact route, an exact state-machine
  transition), it should state its choice explicitly and note it as an
  assumption, rather than leaving the task vague.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`.
  Primary language: `csharp`.
- Backend: ASP.NET Core Web API, EF Core for persistence, in the same
  project as Customer Management and Security & Administration.
- API gateway: Ocelot — route under `/api/tickets`,
  `/api/ticket-categories`, `/api/ticket-priorities`.
- Frontend: Angular, consistent with the Customer Management and
  Security & Administration UI already built/planned.
- Authorization: use Security & Administration's permission model
  (permission-name checks), not hardcoded role checks.

## Out of scope

- Automatic/time-based escalation (explicitly manual-only per this
  story; could be a future enhancement, not built here).
- Free-text or per-agent-defined categories/priorities (fixed,
  admin-managed lists only).
- Customer-facing ticket submission/tracking (that belongs to the
  separate Customer Portal feature, not yet planned).
- Communication-channel ingestion that might create tickets
  automatically from email/WhatsApp/etc. (belongs to the separate
  Communication Channels feature).
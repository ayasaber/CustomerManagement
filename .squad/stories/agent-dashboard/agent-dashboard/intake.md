# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/agent-dashboard/agent-dashboard/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, dashboard, agent`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Agent Dashboard
```

---

## Description

```
Part of the Customer Support CRM. This feature gives an agent a single
landing page after login that surfaces their assigned tickets and
relevant customer information, plus three supporting capabilities:
ticket-linked tasks/reminders, a shared library of quick-reply canned
responses, and lightweight team collaboration on tickets (internal
notes and @mentions).

This feature lives in the same backend project as Customer Management,
Security & Administration, and Ticket Management (the team's current
architecture is one shared backend project behind the Ocelot gateway,
not a per-feature microservice split).

This feature is primarily an aggregation/consumption layer over
existing features, plus three new capabilities:
- Assigned tickets: reads from Ticket Management (tickets assigned to
  the logged-in agent).
- Customer information: reads from Customer Management (customer
  profile/contact/history for a ticket's linked customer).
- Tasks and reminders: NEW data, but always tied to a specific ticket
  (no standalone/personal to-do list in this story's scope).
- Quick replies: NEW data — a shared, admin-managed library of canned
  responses (not per-agent personal replies in this story's scope).
- Team collaboration: NEW data — internal notes/comments on a ticket
  (visible to agents/admins only, never to customers), plus the
  ability to @mention a teammate on a ticket to notify them.

This feature depends on:
- Security & Administration (agent identity for "assigned to me";
  permission checks; @mention target must be a real User with the
  Agent role).
- Ticket Management (tickets, ticket status/history; tasks/reminders
  and internal notes attach to an existing Ticket).
- Customer Management (customer profile data shown for a ticket's
  customer).

User stories:

1. As an agent, I want to see a list of tickets currently assigned to
   me when I land on my dashboard, so that I immediately know what to
   work on without navigating elsewhere first.

2. As an agent, when viewing a ticket, I want to see the relevant
   customer's information (profile, contact details, interaction
   history) alongside it, so that I have full context without
   switching screens.

3. As an agent, I want to add a task or reminder to a specific ticket
   (e.g. "follow up Thursday"), so that I don't lose track of
   follow-up work tied to that ticket.

4. As an agent, I want to see my open tasks/reminders across all my
   tickets in one place on the dashboard, so that nothing tied to my
   work slips through.

5. As an agent, I want to mark a task/reminder as done, so that my
   outstanding list stays accurate.

6. As an agent, I want to insert a quick reply from a shared,
   admin-managed library into my response to a customer, so that I
   can respond faster and consistently for common situations.

7. As an admin, I want to manage the shared quick-reply library (add,
   edit, deactivate entries), so that the available canned responses
   stay accurate and useful without needing a code change.

8. As an agent, I want to leave an internal note/comment on a ticket
   that only agents/admins can see (never the customer), so that I can
   share context or handoff notes with teammates.

9. As an agent, I want to @mention a specific teammate in an internal
   note, so that they're notified and know I need their attention on
   that ticket.

10. As an agent, after @mentioning a teammate on a ticket, I want the
    option to reassign that ticket to them as a follow-up step, so
    that raising their attention can lead directly into handing them
    the work, without a separate trip to a reassignment screen.
    Reassignment remains an explicit, separate confirmation from the
    mention itself — mentioning someone never reassigns the ticket
    automatically.
```

---

## Acceptance criteria

```
- The dashboard's default/landing view shows the tickets currently
  assigned to the logged-in agent, without requiring the agent to
  apply a filter manually first.
- From the dashboard or a ticket detail view, the agent can see the
  linked customer's profile, contact details, and interaction history
  by calling into the existing Customer Management endpoints (no
  duplication of customer data into this feature's own tables).
- A task/reminder is always linked to exactly one existing Ticket
  (foreign key). There is no standalone/personal task not tied to a
  ticket in this story's scope.
- A task/reminder has at minimum: description text, a due date/time,
  a completion state (open/done), who created it, and who it's for
  (defaults to the creating agent). Agents can mark their own
  tasks/reminders done.
- The dashboard shows an aggregated view of the logged-in agent's open
  tasks/reminders across all their tickets, not just per-ticket.
- Quick replies are a single shared library, manageable (add/edit/
  deactivate, not hard-delete) only by Admins. All agents can view and
  use active quick replies; agents cannot create personal/private
  quick replies in this story's scope.
- A deactivated quick reply is no longer selectable for new use but
  does not retroactively change messages where it was already used.
- Internal notes on a ticket are visible only to Users with the Agent
  or Admin role — never exposed through any customer-facing surface
  (this matters for the future Customer Portal feature: internal
  notes must be excluded from whatever ticket data that feature is
  ever allowed to read).
- An internal note can @mention one or more existing Users with the
  Agent role. Mentioning a user triggers a notification to them (the
  exact notification delivery mechanism — in-app only vs. email — is
  an explicit assumption the plan should state, since it is not yet
  decided).
- After posting an internal note that @mentions a teammate, the agent
  is offered an explicit follow-up action to reassign the ticket to
  the mentioned teammate (calling into Ticket Management's existing
  reassignment endpoint). This is a separate confirmation step, not an
  automatic side effect of mentioning someone — a mention alone never
  changes the ticket's assignee. This reassignment is still subject to
  Ticket Management's own reassignment permission rules.
- All Agent Dashboard endpoints require authentication and enforce
  permission checks from the Security & Administration feature (no
  hardcoded role-name checks).
- Where this feature's own new entities are mutable (e.g. a task's
  completion state, a quick reply's text), the same RowVersion-based
  optimistic concurrency pattern already established on Customer and
  Ticket is used.
- The dashboard UI (Angular) is clean, modern, and visually consistent
  with the Customer Management, Security & Administration, and Ticket
  Management UI already built — not a bare/unstyled scaffold.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** Should be planned/implemented after
  Ticket Management (tasks/notes attach to tickets) and Security &
  Administration (identity, roles, permissions) are in place.
- **Depends on code areas or other stories:**
  - Security & Administration (User entity, roles, permission model).
  - Ticket Management (Ticket entity — tasks/reminders and internal
    notes are foreign-keyed to it).
  - Customer Management (read-only consumption of customer data for
    display; no new customer data is introduced here).

## Extra notes (optional)

- Notification delivery for @mentions is not yet decided (in-app
  notification only, vs. also sending an email). The plan should state
  its assumption explicitly (e.g. "in-app only for this story, email
  deferred") rather than silently picking one without flagging it.
- This feature stays in the same backend project as Customer
  Management, Security & Administration, and Ticket Management
  (current team decision: one shared backend project behind the
  Ocelot gateway). Do not scaffold a new/separate project.
- Customer Management retains full management access for the Agent
  role (view and edit profile/contact/notes/attachments) — this was
  reconsidered and is confirmed as-is; no permission tightening is
  planned there.
- Ticket visibility is scoped: Agents see/act on tickets assigned to
  them plus unassigned tickets; Admins/leads see all tickets. This
  dashboard's "assigned tickets" view is consistent with that same
  scoping rule already defined in the Ticket Management story.

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
  the add task endpoint."
- Every entity/schema task must specify exact field names, types, and
  constraints (nullable/required, max length, foreign keys) — not just
  "create a Task entity."
- Follow the naming conventions already established in Customer
  Management and Ticket Management: PascalCase entities, plural
  controller names (e.g. TicketTasksController, QuickRepliesController,
  TicketNotesController), Guid primary keys, `*AtUtc` suffix for
  timestamp fields, and a `RowVersion` optimistic concurrency column on
  every mutable entity introduced here.
- The @mention notification mechanism must be spelled out explicitly
  (what triggers it, what it delivers, and via which mechanism) rather
  than left as "send a notification."
- Every UI task must specify concrete structure: which route/page it
  lives at (e.g. the dashboard landing page layout, the ticket detail
  page's added sections for tasks/notes/customer info), what
  components/sections it contains, and what data each list/table shows
  (exact columns, filters, sorting) — not a single vague "build the
  dashboard" line. Exact colors/CSS values remain an implementation-
  time decision.
- If the planner is uncertain about a specific value (an exact status
  code, an exact field name, an exact route, the notification
  mechanism), it should state its choice explicitly and note it as an
  assumption, rather than leaving the task vague.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`.
  Primary language: `csharp`.
- Backend: ASP.NET Core Web API, EF Core for persistence, in the same
  project as Customer Management, Security & Administration, and
  Ticket Management.
- API gateway: Ocelot — route under `/api/dashboard` for aggregation
  endpoints, `/api/ticket-tasks`, `/api/quick-replies`,
  `/api/ticket-notes` for the new entities, consistent with existing
  route prefix patterns.
- Frontend: Angular, consistent with the UI already built/planned for
  Customer Management, Security & Administration, and Ticket
  Management.
- Authorization: use Security & Administration's permission model
  (permission-name checks), not hardcoded role checks.

## Out of scope

- Standalone/personal tasks not tied to a ticket.
- Per-agent personal quick replies (shared admin-managed library only
  in this story's scope).
- Customer-visible comments/replies on a ticket (that is regular
  ticket communication, not this story's internal-notes concept).
- Real-time chat/messaging between agents (@mention notification only,
  not a live chat feature).
- Any Customer Portal-facing surface (internal notes must never be
  exposed there, but building that portal is a separate, not-yet-
  planned feature).
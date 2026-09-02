# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ticket-conversation/ticket-conversation/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Ticket Conversation
- **Feature slug (folder under `plans/`):** `ticket-conversation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, tickets, conversation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Ticket Conversation
```

---

## Description

```
Part of the Customer Support CRM. This feature is the actual
back-and-forth conversation on a ticket between the customer and the
agent — a message thread visible to both sides, similar to a support
chat/email thread (e.g. Zendesk, Intercom, Gmail thread view).

This is distinct from, and must never be confused with, Agent
Dashboard's "internal notes" feature, which is agent/admin-only and
never visible to the customer. This feature is the opposite: every
message here is visible to both the customer and the agent(s) working
the ticket.

This feature lives in the same backend project as Customer Management,
Security & Administration, and Ticket Management (current team
decision: one shared backend project behind the Ocelot gateway).

This feature depends on:
- Ticket Management (a conversation thread belongs to exactly one
  existing Ticket).
- Security & Administration (message sender identity/role; permission
  checks for who can post as an agent).
- Customer Management (the customer side of the conversation is tied
  to the ticket's linked Customer).

Note: how a customer actually gains access to view/reply to this
thread (i.e. via the not-yet-planned Customer Portal, since customers
currently have no logged-in area) is explicitly out of scope here —
this story defines the data model and agent-side API/UI for the
conversation thread. Customer-side access is deferred to the Customer
Portal story, which will consume the same underlying thread.

User stories:

1. As an agent, I want to send a message to the customer on a ticket,
   so that I can respond to their issue directly within the ticket's
   context.

2. As an agent, I want to see the full conversation thread on a ticket
   in chronological order, with each message clearly attributed to
   the customer or to a specific agent, so that I can follow the
   history of what's been said.

3. As an agent, I want to insert a quick reply (from the shared
   quick-reply library) into a conversation message, so that I can
   respond quickly and consistently to common situations.

4. As an agent, I want a message I send to be clearly distinguished
   from internal notes in the UI (e.g. different styling/section), so
   that I never confuse a customer-visible reply with an agent-only
   internal note, and never accidentally send an internal note to the
   customer.
```

---

## Acceptance criteria

```
- A conversation message is always linked to exactly one existing
  Ticket (foreign key). A message has at minimum: sender type
  (Customer or Agent), sender identity (User id when sent by an
  agent), body text, and a created timestamp (UTC).
- Messages on a ticket are retrievable in chronological order via the
  API, with pagination for tickets with long conversation histories.
- Only Users with the Agent or Admin role can post a message as the
  "Agent" sender type through this story's API surface. (Customer-side
  posting is deferred to the Customer Portal story, which will use
  this same data model but is not built here.)
- This feature's data model and internal-notes' data model
  (Agent Dashboard feature) are kept as clearly separate entities —
  a conversation message must never be retrievable through, or mixed
  into, the internal-notes endpoint or vice versa. This separation is
  the single most important acceptance criterion for this story:
  internal notes must never be exposed to a customer-facing surface,
  and conversation messages must always be visible to the customer
  once the Customer Portal exists.
- An agent can insert text from the shared quick-reply library
  (Agent Dashboard feature) into a conversation message before
  sending, reusing that feature's existing library rather than
  duplicating quick-reply data here.
- The agent-facing UI clearly visually distinguishes the customer-
  visible conversation thread from the internal-notes section on the
  same ticket detail page (e.g. separate labeled sections, distinct
  visual styling) so an agent cannot reasonably confuse the two.
- All ticket-conversation endpoints require authentication and enforce
  permission checks from the Security & Administration feature, and
  respect the same ticket-visibility scoping already defined in
  Ticket Management (an agent can only post/view messages on tickets
  assigned to them or unassigned tickets; Admins/leads can access all).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| `attachments/conversation-thread-reference.png` | Reference screenshot of the desired conversation thread UI layout (see description below). |

---

## Dependencies

- **Blocked by / related ids:** Should be planned/implemented after
  Ticket Management (messages attach to tickets) and after Agent
  Dashboard's quick-reply library exists, since this story reuses it.
- **Depends on code areas or other stories:**
  - Ticket Management (Ticket entity, ticket visibility scoping rules).
  - Security & Administration (User entity, roles, permission model).
  - Agent Dashboard (shared quick-reply library, and the internal-notes
    entity this feature must remain clearly separate from).

## Extra notes (optional)

- This story intentionally does NOT build customer-side access (login,
  portal UI) — that belongs to the future Customer Portal story. This
  story's job is to get the conversation data model and the agent-side
  experience right, and expose an API shape the future Customer Portal
  can plug into without rework.
- The separation between this feature (customer-visible) and Agent
  Dashboard's internal notes (agent-only) is a strict, non-negotiable
  boundary — worth double-checking in review that the plan actually
  keeps them as separate tables/endpoints, not a single "notes" entity
  with a visibility flag that could be toggled or defaulted wrong.

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
  the send message endpoint."
- Every entity/schema task must specify exact field names, types, and
  constraints (nullable/required, max length, foreign keys) — not just
  "create a Message entity." Explicitly confirm this entity is
  separate from (not a shared table with) Agent Dashboard's internal
  notes entity.
- Follow the naming conventions already established in Customer
  Management and Ticket Management: PascalCase entities, plural
  controller names (e.g. TicketMessagesController), Guid primary keys,
  `*AtUtc` suffix for timestamp fields, and a `RowVersion` optimistic
  concurrency column if messages are ever editable (state explicitly
  whether messages are editable/deletable after sending, or immutable
  once posted — this is not yet decided and should be a stated
  assumption).
- Every UI task must specify concrete structure: which section of the
  ticket detail page it occupies, how it's visually distinguished from
  the internal-notes section, what the compose/send UI looks like
  (including quick-reply insertion), and pagination/scroll behavior
  for long threads — not a single vague "build the conversation
  thread" line. Exact colors/CSS values remain an implementation-time
  decision.
- The conversation thread's visual layout must follow the reference
  screenshot in `attachments/conversation-thread-reference.png`:
  a vertical list of messages, one entry per message, each entry
  showing (left to right / top to bottom): a circular avatar image
  for the sender, the sender's display name next to a timestamp
  (date + time, e.g. "05-08-2026 10:38 am") on the same line, and the
  message body text below that line. Consecutive messages stack
  vertically with clear spacing between entries; there is no bubble/
  chat-bubble background — it reads as a clean list of attributed
  posts, similar to a comment thread. Apply this same visual pattern
  regardless of whether the sender is the customer or an agent;
  distinguish sender identity through the avatar and name, not through
  different background colors per side (unlike typical two-sided chat
  UIs).
- If the planner is uncertain about a specific value (an exact status
  code, an exact field name, an exact route, whether messages are
  editable), it should state its choice explicitly and note it as an
  assumption, rather than leaving the task vague.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`.
  Primary language: `csharp`.
- Backend: ASP.NET Core Web API, EF Core for persistence, in the same
  project as Customer Management, Security & Administration, and
  Ticket Management.
- API gateway: Ocelot — route under `/api/ticket-messages`, distinct
  from `/api/ticket-notes` (Agent Dashboard's internal notes).
- Frontend: Angular, as a section within the existing ticket detail
  page, visually distinct from the internal-notes section.
- Authorization: use Security & Administration's permission model
  (permission-name checks), not hardcoded role checks. Respect Ticket
  Management's ticket-visibility scoping.

## Out of scope

- Customer-side login, authentication, or portal UI (deferred to the
  future Customer Portal story).
- Real-time push/websocket delivery of new messages (a simple
  request/poll or reload-based read model is sufficient for this
  story; real-time delivery could be a future enhancement).
- Attachments within conversation messages (Customer Management's
  existing attachments feature is separate and unrelated to this
  story's message text).
- Automated/system-generated messages (e.g. auto-replies) — this story
  covers human-authored agent messages only.
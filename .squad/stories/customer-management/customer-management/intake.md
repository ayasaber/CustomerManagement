# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-management/customer-management/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, customer-management`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer Management
```

---

## Description

```
Part of a Customer Support CRM. This feature lets support agents manage
customer records: view and edit customer profiles, see contact details,
review a customer's interaction history, and attach notes and files to
a customer record.

User stories:

1. As a support agent, I want to view a customer's profile, so that I
   have their key details in one place before I respond.

2. As a support agent, I want to see a customer's contact details, so
   that I can reach them through their preferred channel.

3. As a support agent, I want to see a customer's interaction history,
   so that I understand past issues without asking them to repeat
   themselves.

4. As a support agent, I want to attach notes and files to a customer
   record, so that context from a call or email isn't lost for the
   next agent.
```

---

## Acceptance criteria

```
- Agents can create, view, and edit a customer profile (name, company,
  contact details, other core fields TBD).
- Agents can view all contact details associated with a customer.
- Agents can view a chronological interaction history for a customer,
  spanning the communication channels the CRM supports (email,
  WhatsApp, live chat, SMS, web forms).
- Agents can add free-text notes to a customer record.
- Agents can attach files to a customer record and later retrieve them.
- All customer data is exposed through the API gateway (Ocelot) under
  a dedicated customer-management route.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** None yet.
- **Depends on code areas or other stories:** Ticket Management and
  Communication Channels stories will reference customer records
  created here (e.g. tickets link to a customer, interaction history
  is populated by events from the communication channels).

## Extra notes (optional)

- Open questions to resolve before/while planning:
  - Audience: agent-only, or does this also need a customer-facing
    self-service view?
  - Attachments: where are files stored (blob storage, DB, file
    system)? Any size/type limits?
  - What counts as an "interaction" for history purposes — is this
    populated automatically from the Communication Channels feature,
    or can agents log interactions manually too?

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`.
  Primary language: `csharp`.
- Backend: ASP.NET Core Web API, EF Core for persistence.
- API gateway: Ocelot — this feature should be routed under
  `/api/customers`.
- Frontend: Angular.

## Out of scope

- Ticket creation/management itself (covered by the separate Ticket
  Management story).
- The mechanics of email/WhatsApp/live chat/SMS/web form integrations
  (covered by the separate Communication Channels story) — this story
  only covers displaying interaction history once it exists.
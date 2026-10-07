# Customer Support CRM

A small, full-stack customer support CRM: agents and admins manage customers,
tickets, and users; customers can self-serve through their own portal. Built
across three deployable parts behind a single API gateway.

## Architecture

```
CustomerManagement.Ui       → Angular app (agent/admin screens + customer portal)
        ↓
CustomerManagement.Gateway  → Ocelot API gateway: routing, JWT validation,
                               CORS, trusted-header forwarding
        ↓
CustomerManagement.Api      → ASP.NET Core Minimal API: all business logic,
                               its own independent JWT + permission validation,
                               EF Core persistence
```

The API never trusts the Gateway blindly — it re-validates every request's
token and permissions itself. The Gateway's job is routing, CORS, and a
first line of defense, not the sole security boundary.

## Features

| Feature | Status | Plan |
|---|---|---|
| Customer Management | Implemented | `.squad/plans/customer-management/` |
| Security & Administration | Implemented | `.squad/plans/security-administration/` |
| Ticket Management | Implemented | `.squad/plans/ticket-management/` |
| Agent Dashboard | Implemented | `.squad/plans/agent-dashboard/` |
| Ticket Conversation | Implemented | `.squad/plans/ticket-conversation/` |
| Customer Portal | Implemented | `.squad/plans/customer-portal/` |
| Knowledge Base (help articles, guides, unified search) | Implemented | `.squad/plans/knowledge-base/` |
| Communication Channels (email, WhatsApp, live chat, SMS, web forms) | Planned next | `.squad/stories/communication-channels/` |

## Running locally

- **API** — `dotnet run` from `src/CustomerManagement.Api` (default: `http://localhost:5215`)
- **Gateway** — `dotnet run` from `src/CustomerManagement.Gateway` (default: `http://localhost:5101`) — the UI talks to this, not the API directly
- **UI** — `npm install && npm start` from `src/CustomerManagement.Ui`
- **Tests** — `dotnet test` from `tests/CustomerManagement.Api.Tests` and `tests/CustomerManagement.Gateway.Tests`

## How this project is built: Spec-Driven Development (SDD) with squad-kit

Every feature in the table above was built through the same four-step loop,
managed by [squad-kit](https://github.com/AzmSquad/squad-kit). The tool's
own usage notes live in `.squad/README.md`; this section documents **how we
actually apply that process on this project**, with a real, current example
for each step.

### Step 1 — Intake: business requirements only, no technical decisions

A plain-language spec of *what* is needed, written before any code and
before any technical decision is made — no mention of frameworks, routes,
or entity names. This keeps "what the business needs" cleanly separate from
"how it gets built."

- Command: `squad new-story <feature-slug>`
- Produces: `.squad/stories/<feature>/<feature>/intake.md`
- Example: [`.squad/stories/customer-portal/customer-portal/intake.md`](.squad/stories/customer-portal/customer-portal/intake.md)

### Step 2 — Plan: the intake becomes a concrete, technical contract

The intake is turned into one or more numbered story files. This is where
technical decisions are made explicitly — exact routes, request/response
shapes, field names, status codes, and a **Test Plan** section listing the
exact test cases required. A plan is written so that two different
engineers (or two different AI models) given the same story file would
produce materially the same result.

- Command: `/squad-plan <intake-path>` (inside an agent's chat — Claude
  Code, Cursor, Copilot, Gemini) or `squad new-plan <intake-path> --api`
  from a plain terminal
- Produces: `.squad/plans/<feature>/NN-story-*.md`
- Example: [`.squad/plans/customer-portal/21-story-faq-management-api.md`](.squad/plans/customer-portal/21-story-faq-management-api.md)

### Step 3 — Implement: one scoped agent session per story

A **new, isolated** agent session is opened with **only** the single
`NN-story-*.md` file attached — no other context, no other stories. This
keeps execution narrow and reproducible: the agent implements exactly what
the story specifies, including the tests its Test Plan section lists.

### Step 4 — Review: check the implementation against the story, and record it

After implementation, the story's **Scope** and **Acceptance Criteria**
(and its **Test Plan**) are checked line-by-line against the actual code —
not a general "looks fine" pass. The result is recorded directly at the
bottom of the same story file, so the review is visible evidence, not
something that only happened in someone's head:

```markdown
## Review — completed 2026-10-06

- [x] TicketEndpointsTests.cs — priority default/validation cases: added, passing
- [x] TicketAttachmentEndpointsTests.cs — all listed scenarios: added, passing
- [x] TicketMessagesEndpointsTests.cs — customer reply cases: added, passing
```

This step is what turns "we used AI to help build this" into something
checkable: every story's plan, implementation, and review sign-off are all
in this repo, not just the final code.

## Project conventions

- Entities: PascalCase, `Guid` primary keys, `*AtUtc` suffix for timestamps,
  `RowVersion` optimistic concurrency column on mutable entities.
- Authorization: permission-based (`Permission:<name>` policies), not
  hardcoded role checks — see `Infrastructure/Auth/PermissionPolicyProvider.cs`.
- Tests: one `*EndpointsTests.cs` file per endpoint group under
  `tests/CustomerManagement.Api.Tests`, using `CustomerManagementApiFactory`
  and `TestAuthHandler` to simulate a logged-in role without a real token.
# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/security-administration/security-administration/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, security, identity, foundational`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Security & Administration
```

---

## Description

```
This is a foundational feature for the Customer Support CRM. It establishes
real user identity, roles, and permissions using ASP.NET Core Identity, and
provides administrators with the tools to manage users, audit system
activity, and configure system-wide settings.

This feature is a dependency for every other feature in the system:
- Customer Management currently has a placeholder "agent-only" auth check
  that must be retrofitted to use the real role/permission system built
  here once it exists.
- Ticket Management needs real Agent identities to assign tickets to.
- Agent Dashboard needs to know which agent is logged in to show "my
  assigned tickets."
- Customer Portal needs a separate Customer identity/role, distinct from
  Agent, with its own restricted permissions.

Known roles in the system (confirm/extend during planning):
- Admin — full system access, manages users/roles/permissions/config.
- Agent — support staff; accesses Customer Management, Ticket Management,
  Agent Dashboard.
- Customer — external user; accesses only Customer Portal (their own
  tickets, their own profile, FAQs).

User stories:

1. As an administrator, I want to create, view, edit, and deactivate user
   accounts, so that I can control who has access to the system.

2. As an administrator, I want to assign one or more roles to a user
   (Admin, Agent, Customer), so that each user's access matches their
   job function.

3. As an administrator, I want to define granular permissions within
   roles (e.g. "can escalate tickets", "can delete customer records"),
   so that access can be tuned beyond broad role membership alone.

4. As an administrator, I want every significant action in the system
   (login, permission change, record deletion, role assignment, etc.)
   recorded in an audit log, so that I can investigate incidents and
   meet compliance/accountability requirements.

5. As an administrator, I want to view and filter the audit log (by
   user, date range, action type), so that I can find relevant events
   quickly.

6. As an administrator, I want to configure system-wide settings (e.g.
   password policy, session timeout, feature toggles), so that the
   system's behavior can be adjusted without a code change.

7. As any user, I want to log in securely and have my session respected
   across the app (including through the Ocelot gateway), so that I
   don't have to re-authenticate on every request and my access is
   correctly scoped to my role.

8. As an administrator, I want a clean, modern admin console (users
   list, role editor, audit log viewer, settings panel), so that
   day-to-day administration doesn't require touching the database
   directly.

9. As a new user (Agent or Customer), I want a public registration page
   where I choose which type of account I'm creating — similar to
   Upwork's "join as a client / join as a freelancer" pattern — so that
   I land in the correct role-specific experience from the start.
   Admin accounts are never self-registered; they are created only by
   an existing Admin through the admin console.

10. As a newly registered user, I want to be redirected to the correct
    role-specific landing page immediately after registration/login
    (Agent → Agent Dashboard, Customer → Customer Portal), so that I
    don't have to navigate manually to find where I belong.
```

---

## Acceptance criteria

```
- The system uses ASP.NET Core Identity as the underlying user store
  (password hashing, lockout, email confirmation support available even
  if not all enabled initially).
- At minimum, three roles exist: Admin, Agent, Customer. Roles are
  stored so that additional roles can be added later without a schema
  change.
- Permissions are modeled as a distinct concept from roles (e.g. claims
  or a permission table linked to roles), so that permission checks in
  code reference a permission name, not a hardcoded role name.
- Admins can create, view, edit, deactivate (not hard-delete) user
  accounts through an API and an admin UI.
- Admins can assign/change a user's role(s) through the same UI.
- Every login attempt (success and failure), role change, permission
  change, and record deletion across the system is written to an audit
  log with: who, what, when (UTC), and affected entity/id.
- Admins can view the audit log with filtering by user, date range, and
  action type, with pagination for large result sets.
- Admins can view and update system configuration settings (starting
  with: password policy, session/token expiry) through an API and UI,
  without requiring a redeploy.
- Authentication issues a token (JWT or equivalent) that carries role
  and permission claims, and this identity is correctly forwarded
  through the Ocelot gateway to downstream services so they can
  authorize requests without re-authenticating the user.
- The admin console UI is clean and modern: consistent spacing,
  a clear visual hierarchy (page title, primary actions, data table/
  list), readable typography, and responsive layout that works on a
  standard laptop screen width. Avoid a bare/unstyled default look —
  this should feel like a finished product, not a scaffold.
- Existing "agent-only" auth checks in the already-built Customer
  Management stories are noted as needing a follow-up update to consume
  this real role/permission system (tracked as a dependency, not
  necessarily solved inside this story).
- A public registration page lets a new user choose their account type
  (Agent or Customer) — Upwork-style role selection at signup — and
  creates the account with the correct role assigned. Admin is not a
  selectable option at public registration; Admin accounts can only be
  created by an existing Admin via the admin console.
- After successful registration or login, the user is redirected to a
  role-appropriate landing page (Agent → Agent Dashboard route,
  Customer → Customer Portal route) rather than a generic page.
- Authentication uses JWT access tokens plus a refresh token flow
  (access tokens short-lived, refresh tokens longer-lived and
  revocable), suitable for an Angular SPA calling through the Ocelot
  gateway.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** None — this is foundational and should
  be treated as high priority since other features depend on it.
- **Depends on code areas or other stories:** None inbound. Outbound:
  Customer Management (Stories 01-04) has placeholder agent-only auth
  that will need retrofitting once this exists. Ticket Management,
  Agent Dashboard, and Customer Portal (not yet planned) will depend on
  the identities and roles established here.

## Extra notes (optional)

- UI expectation: the admin console should look and feel modern and
  clean — polished spacing, typography, and layout, not a rough
  scaffold. Where the plan reaches UI tasks, it should call out concrete
  layout/structure decisions (e.g. sidebar navigation + content panel,
  data table component for the users list, modal or side-panel for
  edit forms) rather than leaving "build the admin UI" as a single vague
  task — exact CSS/styling values belong at implementation time, but the
  plan should specify the UI's structure and key components clearly
  enough that implementation doesn't have to guess.
- Registration UX: the public registration page should present a clear
  role-selection step first (e.g. two large clickable cards: "I'm an
  Agent" / "I'm a Customer"), then show a role-appropriate registration
  form — mirroring how Upwork splits "client" vs "freelancer" signup at
  the very first step, not buried in a dropdown inside a single generic
  form.

## Plan specificity requirements (read carefully)

This is a spec-driven-development (SDD) workflow: the plan produced from
this intake is the contract that any coding agent (Copilot today, a
different agent or developer later) will implement. Two different
agents given the same plan must produce materially the same result. To
achieve that, every story/task in the generated plan must be concrete,
not descriptive. Specifically:

- Every API task must specify the exact route, HTTP verb, request DTO
  shape (field names and types), response DTO shape, and status codes
  per outcome (success and each failure case) — not just "implement
  the login endpoint."
- Every entity/schema task must specify exact field names, types, and
  constraints (nullable/required, max length, etc.) — not just "create
  a User entity."
- Follow the naming conventions already established in the
  Customer Management feature: PascalCase entities, plural controller
  names (e.g. UsersController, RolesController), Guid primary keys,
  `*AtUtc` suffix for timestamp fields, and a `RowVersion` optimistic
  concurrency column on any entity that can be edited (Users, Roles,
  SystemSettings) — same pattern used for Customer.
- Every UI task must specify concrete structure: which route/page it
  lives at, the page's layout (e.g. sidebar navigation + content panel
  for the admin console; two-card role selector + form for
  registration), what components/sections it contains, and what data
  each list/table shows (exact columns) and how it's sorted/paginated.
  Do not leave a UI task as a single vague "build the X page" line —
  break it down enough that implementation doesn't have to invent
  structure. Exact colors/CSS values are still an implementation-time
  decision, not a planning-time one.
- If the planner is uncertain about a specific value (an exact status
  code, an exact field name, an exact route), it should state its
  choice explicitly and note it as an assumption, rather than leaving
  the task vague — an explicit, wrong assumption is easy to correct in
  review; a vague task just gets reinterpreted differently each time
  it's implemented.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`.
  Primary language: `csharp`.
- Backend: ASP.NET Core Web API, ASP.NET Core Identity, EF Core for
  persistence.
- Authorization model: role-based plus granular permissions/claims
  (not roles alone), so downstream services can check specific
  permissions.
- API gateway: Ocelot — authentication/authorization must integrate
  with the gateway so claims/identity are forwarded to downstream
  services correctly (this feature should define the contract other
  gateway routes rely on).
- Frontend: Angular, for the admin console UI (users, roles, audit log,
  settings).
- This feature's role/permission model is a contract other stories
  (Customer Management retrofit, Ticket Management, Agent Dashboard,
  Customer Portal) will consume — design the permission-check
  interface to be simple to call from other features' authorization
  attributes/policies.

## Out of scope

- Retrofitting Customer Management's existing placeholder auth checks
  to use this system (tracked as a known follow-up, not built here).
- Ticket Management, Agent Dashboard, and Customer Portal feature logic
  themselves — only the identity/role/permission foundation they will
  depend on.
- Third-party SSO/OAuth login providers (Google, Microsoft login, etc.)
  unless explicitly added later — assume username/password via
  ASP.NET Core Identity for the initial version.
# Story 23 — Customer Portal UI And Gateway Routing

## Prerequisites

- Story 20 completed: [20-story-customer-ticket-self-service-api.md](20-story-customer-ticket-self-service-api.md).
- Story 21 completed: [21-story-faq-management-api.md](21-story-faq-management-api.md).
- Story 22 completed: [22-story-customer-feedback-api.md](22-story-customer-feedback-api.md).
- Story 19 completed: `../ticket-conversation/19-story-ticket-conversation.md` (this story reuses `TicketConversationPanelComponent`).

---

## Story Goal

Wire the three new backend capabilities (ticket attachments + customer replies from Story 20, FAQ from Story 21, feedback from Story 22) through the gateway and build out the real `CustomerPortalPageComponent` (today a placeholder) so a customer can, end to end:

1. Submit a new request (subject, description, category/topic, optional file attachment) without picking a priority.
2. See a list of only their own requests with current status.
3. Open one of their own requests and read the full conversation in order.
4. Reply to one of their own open requests.
5. Browse active FAQ entries grouped by topic.
6. Submit feedback (rating + optional comment) at any time.

It also makes the customer portal **its own clearly separated space**: replacing the generic `authenticatedGuard` on `/customer/portal` with a customer-only guard (mirroring `agentOnlyGuard`/`adminOnlyGuard`), and adding an admin screen for FAQ maintenance so admins never need the customer screens to do their job.

Out of scope for this story:

- Self-registration and login — already fully implemented (`POST /api/auth/register` already accepts `accountType: "customer"` and the registration UI already offers an "I'm a Customer" path; `AuthService.resolveLandingRoute` already sends the `customer` role to `/customer/portal`). No changes needed.
- Changing how agents/admins see tickets, conversations, or taxonomy in `/tickets` or `/admin/ticket-taxonomy`.

---

## Context — Read These Files First

1. `src/CustomerManagement.Gateway/ocelot.json` — route entry shape (`DownstreamPathTemplate`, `UpstreamPathTemplate`, `UpstreamHttpMethod`, `AuthenticationOptions`, `AddHeadersToRequest`); existing `/api/tickets/categories` and `/api/ticket-messages`-style entries as the closest precedent for the new routes.
2. `src/CustomerManagement.Gateway/ROUTES.md` — existing "Ticket Route Contract" table to extend.
3. `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` — `Assert.Equal(47, routes.Count);` (line 23) to update.
4. `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs` — `for (var i = 0; i < 37; i++)` override loop (line 29) to update.
5. `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` and `tests/CustomerManagement.Gateway.Tests/Infrastructure/DownstreamStubServer.cs` — existing routing-theory test cases and stub permission resolver, to extend for the new routes.
6. `src/CustomerManagement.Ui/src/app/app.routes.ts` — the `/customer/portal` route entry (`canActivate: [authenticatedGuard]`) to change to a new customer-only guard.
7. `src/CustomerManagement.Ui/src/app/core/guards/agent-only.guard.ts` (full file) — direct precedent for the new `customer-only.guard.ts`.
8. `src/CustomerManagement.Ui/src/app/features/landing/customer-portal-page.component.ts` (full file, currently a placeholder) — file to replace with the real implementation.
9. `src/CustomerManagement.Ui/src/app/features/tickets/components/ticket-conversation-panel.component.ts` (full file) — reusable `@Input() messages`, `@Input() quickReplies`, `@Input() canCompose`, `@Input() sending`, `@Input() loading`, `@Output() sendClicked`, `@Output() reloadClicked` component to import directly into the customer portal (no new conversation UI needed).
10. `src/CustomerManagement.Ui/src/app/features/tickets/ticket-management-page.component.ts` — `loadConversation` (~line 220) and the `createTicketMessage` call site (~lines 240–260) as the precedent for wiring the conversation panel's events to `TicketManagementApiService`.
11. `src/CustomerManagement.Ui/src/app/core/models/ticket-management.models.ts` — existing `CreateTicketRequest` (`customerUserId?`, `categoryId`, `priorityId`, `subject`, `description`) to update (`priorityId` becomes optional) and extend with ticket-attachment interfaces.
12. `src/CustomerManagement.Ui/src/app/core/services/ticket-management-api.service.ts` — `listTickets`, `createTicket`, `createTicketMessage` method patterns (`withAutoRefresh`, `mapError`, `buildHeaders`) to extend with attachment methods.
13. `src/CustomerManagement.Ui/src/app/core/services/customer-management-api.service.ts` — `uploadAttachment` (~lines 106–116) and `downloadAttachment` (~lines 118–126) as the exact `FormData`/blob precedent for the new ticket-attachment service methods.
14. `src/CustomerManagement.Ui/src/app/features/admin/ticket-taxonomy/admin-ticket-taxonomy-page.component.ts` (full file) — precedent for a simple admin CRUD page (create form + list + retire-by-toggle) to mirror for the new admin FAQ page.
15. `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts` (full file) — sidebar nav list to extend with an "FAQ" link.
16. `src/CustomerManagement.Ui/src/app/features/auth/register/register-page.component.ts` — confirms customer self-registration already exists (read-only reference, no changes).

---

## Gateway Tasks

### 1. Add routes

File: `src/CustomerManagement.Gateway/ocelot.json`

Add, following the exact JSON shape used by every existing route (same `DownstreamScheme`, `DownstreamHostAndPorts`, `AuthenticationProviderKey: "Bearer"`, and `AddHeadersToRequest` block):

| Upstream Path | Methods |
|---|---|
| `/api/tickets/{ticketId}/attachments` | `GET`, `POST` |
| `/api/tickets/{ticketId}/attachments/{attachmentId}/content` | `GET` |
| `/api/faq` | `GET`, `POST` |
| `/api/faq/{faqId}` | `PUT` |
| `/api/feedback` | `GET`, `POST` |

That is 5 new route entries.

### 2. Update route contract docs

File: `src/CustomerManagement.Gateway/ROUTES.md`

- Add the 5 new rows to the "Ticket Route Contract" table (attachments) and a new "FAQ Route Contract" / "Feedback Route Contract" section (or extend an existing table), with purpose text noting: ticket attachments are ownership-scoped the same as other ticket routes; FAQ read is customer/agent/admin, FAQ write is admin-only; feedback submit is customer-only, feedback read is agent/admin-only.

### 3. Update gateway tests

Files:

- `tests/CustomerManagement.Gateway.Tests/GatewayRouteContractTests.cs` — change `Assert.Equal(47, routes.Count);` to `Assert.Equal(52, routes.Count);` and add route-presence assertions for the 5 new upstream paths.
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/CustomerManagementGatewayFactory.cs` — change the override loop from `37` to `42`.
- `tests/CustomerManagement.Gateway.Tests/CustomerManagementGatewayRoutingTests.cs` — add routing-theory cases for `GET/POST /api/tickets/{ticketId}/attachments`, `GET /api/tickets/{ticketId}/attachments/{attachmentId}/content`, `GET/POST /api/faq`, `PUT /api/faq/{faqId}`, `GET/POST /api/feedback`.
- `tests/CustomerManagement.Gateway.Tests/Infrastructure/DownstreamStubServer.cs` — add stub handlers for the new downstream paths, matching the existing stub style for ticket/ticket-message routes.

---

## Frontend Tasks

### 1. Add a customer-only route guard

Create file: `src/CustomerManagement.Ui/src/app/core/guards/customer-only.guard.ts`, mirroring `agent-only.guard.ts` exactly:

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const customerOnlyGuard: CanActivateFn = () => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const user = authService.currentUser()();

  if (!user) {
    return router.createUrlTree(['/auth/login']);
  }

  const hasCustomerRole = user.roles.some((role) => role.toLowerCase() === 'customer');

  if (hasCustomerRole) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
```

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Replace the `/customer/portal` route's `canActivate: [authenticatedGuard]` with `canActivate: [customerOnlyGuard]` and import `customerOnlyGuard` instead of (or alongside, if `authenticatedGuard` is still used elsewhere) `authenticatedGuard`.

### 2. Update ticket models and API service

File: `src/CustomerManagement.Ui/src/app/core/models/ticket-management.models.ts`

- Change `CreateTicketRequest.priorityId` from `priorityId: string;` to `priorityId?: string;`.
- Add:

```typescript
export interface TicketAttachmentResponse {
  id: string;
  ticketId: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedByDisplayName: string;
  createdAtUtc: string;
}
```

File: `src/CustomerManagement.Ui/src/app/core/services/ticket-management-api.service.ts`

- Add methods modeled directly on `CustomerManagementApiService.uploadAttachment` / `listAttachments` / `downloadAttachment`:
  - `listTicketAttachments(ticketId: string): Observable<TicketAttachmentResponse[]>`
  - `uploadTicketAttachment(ticketId: string, file: File): Observable<TicketAttachmentResponse>` (builds a `FormData` with `file`, posts to `${this.baseUrl}/${ticketId}/attachments`)
  - `downloadTicketAttachment(ticketId: string, attachmentId: string): Observable<Blob>` (`responseType: 'blob'`, same pattern as `customer-management-api.service.ts`)
- Reuse the existing `withAutoRefresh` + `mapError` + `buildHeaders` pattern already used by every other method in this service.

### 3. Add FAQ models, service, and admin management page

Create file: `src/CustomerManagement.Ui/src/app/core/models/faq.models.ts`

```typescript
export interface FaqEntryResponse {
  id: string;
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateFaqEntryRequest {
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
}

export interface UpdateFaqEntryRequest {
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
  isActive: boolean;
  rowVersion: number[];
}
```

Create file: `src/CustomerManagement.Ui/src/app/core/services/faq-api.service.ts`, modeled on `ticket-management-api.service.ts`'s structure (`withAutoRefresh`, `mapError`, `buildHeaders`):

- `baseUrl = 'http://localhost:5101/api/faq'`
- `listFaqEntries(activeOnly?: boolean): Observable<FaqEntryResponse[]>`
- `createFaqEntry(request: CreateFaqEntryRequest): Observable<FaqEntryResponse>`
- `updateFaqEntry(faqId: string, request: UpdateFaqEntryRequest): Observable<FaqEntryResponse>`

Create file: `src/CustomerManagement.Ui/src/app/features/admin/faq/admin-faq-page.component.ts`, modeled directly on `admin-ticket-taxonomy-page.component.ts` (create form + list, with a "Retire" / "Reactivate" button per row that calls `updateFaqEntry` with `isActive` flipped and the row's current `rowVersion`).

File: `src/CustomerManagement.Ui/src/app/features/admin/admin-shell.component.ts`

- Add `<a routerLink="/admin/faq" routerLinkActive="active">FAQ</a>` to the sidebar, next to the existing `Ticket Taxonomy` link.

File: `src/CustomerManagement.Ui/src/app/app.routes.ts`

- Add a child route `{ path: 'faq', component: AdminFaqPageComponent }` under the existing `admin` route's `children` array, alongside `ticket-taxonomy`.

### 4. Add feedback models and service

Create file: `src/CustomerManagement.Ui/src/app/core/models/feedback.models.ts`

```typescript
export interface CreateFeedbackRequest {
  rating: number;
  comment?: string;
}

export interface FeedbackResponse {
  id: string;
  customerId: string;
  customerName: string;
  rating: number;
  comment: string | null;
  createdAtUtc: string;
}
```

Create file: `src/CustomerManagement.Ui/src/app/core/services/feedback-api.service.ts`, modeled on `ticket-management-api.service.ts`:

- `baseUrl = 'http://localhost:5101/api/feedback'`
- `submitFeedback(request: CreateFeedbackRequest): Observable<FeedbackResponse>`

(No list method is needed on the customer side — `GET /api/feedback` is agent/admin-only per Story 22 and is not surfaced in this story's UI.)

### 5. Build the real `CustomerPortalPageComponent`

File: `src/CustomerManagement.Ui/src/app/features/landing/customer-portal-page.component.ts` — replace the placeholder entirely.

Structure: a single page with a local section switcher (signal-based, no nested routes — same single-page-with-internal-state pattern as `ticket-management-page.component.ts`), with four sections:

- **My Requests** (default section):
  - On init, call `TicketManagementApiService.listTickets()` (already ownership-scoped server-side to the logged-in customer — no client-side filtering needed) and render subject/category/priority/status per row.
  - Selecting a row loads its messages via the existing `TicketManagementApiService` conversation methods (same calls `ticket-management-page.component.ts` already makes) and renders `<app-ticket-conversation-panel>` with `[messages]`, `[loading]="conversationLoading()"`, `[sending]="conversationSending()"`, `[canCompose]="true"` (always true here — Story 20 made customer replies allowed for any non-closed ticket; the panel itself has no closed/open awareness, so a reply attempt on a closed ticket surfaces the `400` from Story 20 as an inline form error, same as any other validation error elsewhere in this component), and `[quickReplies]="[]"` (customers never see the quick-reply helper, which the panel already hides automatically when `quickReplies` is empty).
  - Also show the ticket's existing attachments via `listTicketAttachments` and a simple upload control using `uploadTicketAttachment`.

- **New Request**:
  - Reactive form: `topic`/category (`<select>` populated from `TicketManagementApiService`'s existing categories load, filtered to active), `subject`, `description`, optional file input.
  - Submits via `TicketManagementApiService.createTicket({ categoryId, subject, description })` — **no `priorityId` field in this form at all** (the model now allows omitting it; Story 20's backend resolves the default for a customer actor).
  - If a file was selected, immediately follow the successful `createTicket` response with `uploadTicketAttachment(ticket.id, file)`.
  - On success, switch to **My Requests** and show the new ticket selected.

- **FAQ**:
  - On section activation, call `FaqApiService.listFaqEntries()` (server already forces `activeOnly=true` for the customer role regardless of what's passed — call it with no arguments for clarity).
  - Group entries client-side by `topic` (e.g. `Object.groupBy`-style reduce into a `Map<string, FaqEntryResponse[]>`) and render as a list of topic headings each followed by its question/answer entries, ordered by `sortOrder` within each topic.
  - Empty state when no entries exist yet.

- **Feedback**:
  - Simple form: a 1–5 rating control (radio group or star buttons) and an optional comment textarea.
  - Submits via `FeedbackApiService.submitFeedback(...)`.
  - On success, show a confirmation message and reset the form; feedback submission is not tied to any specific ticket and has no list/history view for the customer in this story.

Isolation requirement: this component must not import or link to anything under `features/admin/**` or `features/agent-dashboard/**`; it only imports `TicketConversationPanelComponent` from `features/tickets/components/` (an already-shared, role-agnostic component) plus its own new FAQ/feedback pieces.

---

## Edge Cases & Failure Modes

- Customer submits a new request without selecting a category: blocked client-side (required field) and server-side (existing `ValidateCreateTicketAsync` already requires a valid `categoryId`).
- Customer tries to reply to a ticket that became `Closed` between loading the page and clicking Send: the `201`/`400` response from `POST /api/ticket-messages` is surfaced as an inline error on the conversation panel's existing `errorText` binding (see `ticket-conversation-panel.component.ts`); no special-casing needed beyond passing the error message through.
- FAQ section loads while no entries exist: empty state message, not a blank screen or console error.
- Attachment upload fails validation (oversized/disallowed type): the `400` response is surfaced as an inline error in the New Request / ticket detail attachment control, consistent with how other validation errors are shown elsewhere in `ticket-management-page.component.ts`.
- A non-customer (agent/admin) directly navigates to `/customer/portal`: `customerOnlyGuard` redirects to `/forbidden`, same behavior pattern as `agentOnlyGuard`/`adminOnlyGuard` on their respective routes.
- A customer directly navigates to `/admin/**` or `/agent/dashboard`: already blocked today by `adminOnlyGuard`/`agentOnlyGuard` — unchanged by this story, confirmed by regression tests only.
- Feedback rating not selected: client-side required-field validation blocks submit before the `400` from the server would ever be hit.

---

## Test Plan

1. Create `src/CustomerManagement.Ui/src/app/core/guards/customer-only.guard.spec.ts` (new), mirroring the existing `agent-only.guard.spec.ts` if one exists, or following the existing guard test conventions in `core/guards/`.
2. Extend `ticket-management-api.service.spec.ts` for the new attachment methods and the now-optional `priorityId`.
3. Create `faq-api.service.spec.ts` and `feedback-api.service.spec.ts` following the same HTTP-mock test conventions as `ticket-management-api.service.spec.ts`.
4. Create `customer-portal-page.component.spec.ts`:
   - Renders "My Requests" by default with only the current customer's tickets (mock service returns a fixed list; assert rendering, not scoping — scoping is a backend concern already covered by Story 20's API tests).
   - "New Request" form requires category/subject/description; omits any priority control entirely (assert no priority `<select>`/input exists in the rendered template).
   - Selecting a ticket renders `<app-ticket-conversation-panel>` with `canCompose` truthy.
   - "FAQ" section groups entries by topic and shows an empty state when the mocked service returns `[]`.
   - "Feedback" section requires a rating before allowing submit.
5. Create `admin-faq-page.component.spec.ts` following `admin-ticket-taxonomy-page.component.ts`'s existing test conventions (if a spec file exists for that component) for create + retire-toggle behavior.
6. Extend gateway tests per the Gateway Tasks section above.

---

## Verification Steps

1. **Backend builds:** `dotnet build` in `src/CustomerManagement.Api`.
2. **Backend tests:** `dotnet test` in `tests/CustomerManagement.Api.Tests`.
3. **Gateway tests:** `dotnet test` in `tests/CustomerManagement.Gateway.Tests`.
4. **Frontend builds:** `ng build` in `src/CustomerManagement.Ui`.
5. **Frontend tests:** `ng test` in `src/CustomerManagement.Ui` (or the project's configured test runner for the UI project).
6. **Regression:** run the full existing suite for `tests/CustomerManagement.Api.Tests`, `tests/CustomerManagement.Gateway.Tests`, and the UI test suite to confirm no existing ticket/customer/admin flows broke.

---

## Done Criteria

- [ ] A customer can submit a new request with subject, description, category, and an optional attachment, and never sees a priority control.
- [ ] A submitted customer request appears in the same `/api/tickets` data the agent/admin `/tickets` screen already reads — no separate system.
- [ ] A customer sees only their own requests with current status on `/customer/portal`.
- [ ] A customer can open one of their own requests and read the full conversation in chronological order via the shared `TicketConversationPanelComponent`.
- [ ] A customer can reply to one of their own open requests; replying on a `Resolved`/`WaitingOnCustomer` ticket moves it to `InProgress`, and replying on a `Closed` ticket is blocked.
- [ ] A customer can never see, open, or reply to another customer's request, including by direct ticket id (`404`, not `403`).
- [ ] An admin can create, update, and retire FAQ entries from `/admin/faq`.
- [ ] A customer can browse only active FAQ entries, grouped by topic.
- [ ] A customer can submit feedback (rating + optional comment) at any time, independent of any ticket.
- [ ] Submitted feedback is visible to agents/admins via `GET /api/feedback`, with no customer-facing read access.
- [ ] `/customer/portal` is reachable only by the `customer` role; agents/admins are redirected to `/forbidden`.
- [ ] All new gateway routes are registered, documented in `ROUTES.md`, and covered by updated route-count/contract tests.

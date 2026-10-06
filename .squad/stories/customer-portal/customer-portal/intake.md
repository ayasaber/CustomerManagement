# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/customer-portal/customer-portal/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, customer-portal, self-service`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Customer Portal
```

---

## Description

```
Part of the Customer Support CRM. Today, only agents and admins can
raise a support ticket on a customer's behalf and work with it.
Customers have no way to help themselves — they cannot open a ticket
directly, check on one that's already open, look up an answer without
contacting anyone, or tell us how the support experience is going.

This feature gives a customer their own self-service area where they
can raise and follow their own requests, find answers to common
questions on their own, and leave feedback about their experience —
without needing an agent involved for any of that.

This depends on customers already being able to log in and on support
tickets already existing as a concept in the system (both are already
in place from earlier work). A customer must only ever be able to see
and act on their own information — never another customer's.
```

---

## User stories

```
1. As a customer, I want to submit a new support request describing
   my issue, so that I can get help without needing to contact an
   agent directly to have it logged.

2. As a customer, I want to see a list of all my requests and their
   current status, so that I know what's open, what's in progress,
   and what's done.

3. As a customer, I want to open one of my requests and see everything
   that's been said about it so far, so that I have full context
   before adding anything new.

4. As a customer, I want to add more information or a reply to one of
   my open requests, so that I can respond to a question or add
   detail without starting over somewhere else.

5. As a customer, I want to browse a list of frequently asked
   questions, organized by topic, so that I can often find an answer
   myself before raising a request at all.

6. As a customer, I want to share feedback about my support
   experience (a rating and an optional comment), even when I don't
   have an open request, so that the business can hear from me
   directly.

7. As a customer, I want to be confident that only I can see my own
   requests and history — never anyone else's — so that I trust the
   portal with my information.
```

---

## Acceptance criteria

```
- A customer can submit a new request with, at minimum, a subject,
  a description of the issue, a topic/category, and an optional file
  attachment.
- A request submitted by a customer shows up for the support team to
  work on, the same as any request raised by an agent — customers and
  agents are working with the same requests, not two separate systems.
- A customer can see a list of only their own requests, each showing
  its current status.
- A customer can open one of their own requests and see its full
  conversation/history in the order it happened.
- A customer can add a new reply to one of their own open requests.
  Whether a customer can still reply after a request has been marked
  resolved/closed is an open business question — see "Extra notes."
- A customer can never see, open, or reply to another customer's
  request, under any circumstance. This must hold even if a customer
  tries to access another request directly, not just when browsing
  normally.
- An admin can maintain a list of FAQ entries (a question, an answer,
  and a topic/category) and can retire an entry so it's no longer
  shown to customers.
- A customer can browse the current, active FAQ entries, grouped by
  topic.
- A customer can submit feedback at any time, consisting of a rating
  and an optional written comment. Feedback is not the same thing as
  a support request and does not create one.
- Submitted feedback is visible to the support team/admins so they
  can read it. Acting on feedback (responding to it, routing it
  somewhere) is not part of this feature.
- The customer-facing area is clearly its own space, separate from
  the screens agents and admins use — a customer should never be able
  to land on or navigate into an agent/admin screen, and an agent/
  admin should not need to use the customer's screens to do their job.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** Depends on customer accounts/login and
  support requests already existing as concepts in the system (both
  already in place from earlier work on this CRM).
- **Depends on other features:** Customer accounts & permissions,
  Customer records, and Support Requests/Tickets.

## Extra notes (optional)

- Open business questions to decide before/during planning, rather
  than silently assume:
  - Can a customer still add a reply to a request that's already
    marked resolved/closed? If yes, does that reopen it automatically,
    or does it stay closed with the reply just added as a note?
  - Can a customer choose how urgent/important their request is, or
    is that something only the support team decides after reviewing
    it? (Current assumption: the customer does not set this — the
    support team decides it after the request comes in.)
  - Is there already a way for a brand-new customer (nobody from the
    business has entered them into the system yet) to create their
    own account, or does every customer's account need to be created
    by the business first, with the customer just setting a password
    afterward?
  - Should feedback be tied to the customer who submitted it, or is
    anonymous feedback also something we want to support? (Current
    assumption: always tied to the logged-in customer, no anonymous
    option in this version.)
- This is a self-service companion to the existing request-handling
  work already built for agents — it should feel like the same system
  from the customer's side, not a separate product.

## Out of scope

- A customer choosing how urgent their own request is.
- Any process for responding to or acting on submitted feedback
  (collecting and showing it to the team is as far as this goes).
- Live chat, phone support, or any way of reaching the business other
  than the request conversation thread already covered above.
- Suggesting FAQ answers automatically based on what a customer types
  into a new request (a plain, browsable FAQ list only).
# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/knowledge-base/knowledge-base/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** ``
- **Work item type:** `feature`
- **Status:** `planning`
- **Assignee:** ``
- **Labels:** `crm, knowledge-base, self-service, search`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Knowledge Base
```

---

## Description

```
Part of the Customer Support CRM. FAQs (short question-and-answer
entries, managed by Admins, browsable by agents and customers) already
exist in the system today. This feature does NOT re-build FAQ
management — it is strictly additive to it.

This feature adds two new kinds of self-service content that FAQs
don't cover, plus a single way to search across all of it:

- Help articles: longer, structured explanations (e.g. "Getting
  started with your account") — more than a one-line answer, closer
  to a short document.
- Solutions and guides: step-by-step, procedural content for working
  through a problem (e.g. "Your order didn't arrive: try this, then
  this, then this") — not just information, a sequence of steps.
- Search: one search box where a customer or agent can type a
  question or keywords and get back the relevant results from FAQs,
  help articles, and guides together, ranked by relevance — not three
  separate places to look.

This follows the same access pattern already established for FAQs and
for other reference content in this system (ticket categories,
ticket priorities): Admins create, edit, and retire content; agents
and customers can only browse and search it. This feature does not
change who can manage FAQs or how — it only adds the two new content
types and the unified search, and makes existing FAQs part of that
same search.
```

---

## User stories

```
1. As an admin, I want to create a help article with a title and
   structured content (more than a single short answer), so that I
   can explain something that needs more than a one-line FAQ answer.

2. As an admin, I want to create a solution/guide made up of ordered
   steps, so that I can walk a customer through resolving a common
   problem on their own.

3. As an admin, I want to edit or retire a help article or guide, so
   that I can keep content accurate and remove anything outdated.

4. As an agent, I want to browse and search help articles and guides
   (in addition to FAQs), so that I can find a consistent, approved
   answer quickly while handling a request.

5. As a customer, I want to browse and search help articles and
   guides, so that I can solve my own problem without needing to
   contact anyone.

6. As a customer or agent, I want one search box that searches FAQs,
   help articles, and guides together, so that I don't have to guess
   which of three separate places has the answer.

7. As a customer, I want search results ranked by how relevant they
   are to what I typed, so that the most useful answer is easy to
   find, not buried in a long list.

8. As the business, I want retired/inactive content (FAQs, articles,
   or guides) to never appear in search results or browsing for
   agents or customers, so that outdated information never gets
   mistakenly followed.
```

---

## Acceptance criteria

```
- Admins can create, edit, and retire help articles (title +
  structured/longer body content) and solutions/guides (title +
  an ordered list of steps). Only admins can do this — same access
  rule already used for FAQs, ticket categories, and ticket
  priorities in this system.
- Agents and customers can browse help articles and guides. They
  cannot create, edit, or retire any of it.
- A single search accepts free-text input and returns matching
  results drawn from FAQs, help articles, and guides together, not
  as three separate result sets the person has to check one at a
  time.
- Search results only ever include active (non-retired) content —
  retiring an FAQ, article, or guide removes it from search
  immediately.
- A search result clearly indicates what kind of content it is (FAQ,
  help article, or guide) before the person opens it, so they know
  what to expect.
- This feature does not add, remove, or change who can manage FAQs,
  and does not duplicate FAQ's existing data — FAQs remain exactly as
  they are today, just now included in this feature's search.
- An admin can see a single place listing all help articles and
  guides (active and retired) to manage them, the same way FAQ
  management already works today.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** Depends on the existing FAQ feature
  (already implemented, part of Customer Portal) — this feature
  extends search to include it but does not modify it. Depends on the
  existing Admin/Agent/Customer role and permission model.
- **Depends on other features:** Customer Portal (existing FAQ),
  Security & Administration (roles/permissions).

## Extra notes (optional)

- Deliberately out of scope, decided rather than left open: any
  change to who manages FAQs or how FAQ management works. FAQ stays
  exactly as already built.
- Deliberately out of scope: agents or customers suggesting,
  drafting, or contributing content themselves. Content creation
  stays Admin-only, matching the existing pattern for all reference
  content in this system.
- Open business question for planning to confirm rather than assume:
  should search match only on title/exact wording, or also look
  inside the body/step content of articles and guides? Current
  assumption, to be confirmed: search should look at title and full
  body/step content for all three content types, not title only —
  otherwise a guide with the right answer but a generic title would
  never surface.

## Out of scope

- Any change to existing FAQ management, authoring, or its access
  rules.
- Agents or customers creating, editing, suggesting, or drafting any
  content (FAQs, articles, or guides) — admin-only, as today.
- Ranking or personalizing search results based on a specific
  customer's history or past requests (plain relevance-to-the-typed-
  text ranking only).
- Multi-language content or translated versions of articles/guides.
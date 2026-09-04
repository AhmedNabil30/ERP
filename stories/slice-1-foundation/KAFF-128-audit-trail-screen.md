# KAFF-128 · The audit trail screen

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 (**proposed**) · **Status:** **Ready** — cut 2026-09-05 by the Scrum Master **in the same act as KAFF-117 was pulled**, under `process/agile.md` §2a rule 6. **Sprint 5, Lane B. Not pulled into sprint 4.**
**Spec:** §7, §9 · **Decisions:** D-012, **D-049 (ruling 1)**, D-111, D-114, **D-117**
**UX:** `ux/navigation.md` · `ux/components.md`
**Depends on:** **KAFF-117** — and it must be **merged**, not "nearly done" (§2a rule 1)

## Why this story exists, and why it exists *today*

**It is `process/agile.md` §2a rule 6's first application, and the rule was written the same day.**

Every other UI criterion in this slice moved to a Frontend story *after* its backend story was
delivered. `AC-106-J` took **nineteen days**. `AC-119-L`, `AC-121-I` and `AC-124-I` took one day each,
and only because the pipeline was being written that week and somebody happened to be looking.

`AC-117-I` moved **before a line of KAFF-117 was written**. That is the whole difference rule 6 buys:

> *"A backend story carrying a UI criterion is not pullable until the Frontend story that will
> discharge it exists on the board. Not 'is scheduled' — exists, with an identifier."*

**This story is that identifier.** It is deliberately small — one read-only screen — and it is
deliberately *not* in sprint 4: rule 1 forbids starting it against an unmerged API, and KAFF-117 is
being built in sprint 4, not before it.

## The permission, and why this screen is the sharpest one in slice 1

Nabil, verbatim:

> *"The Audit Trail is strictly limited to the Owner (Global) … completely hidden from all other
> roles, even for their own projects."*

**"Even for their own projects" is the unusual half.** Every other permission in this system is
`role × assignment`; this one refuses a Technical Office lead reading the trail of a project they run.
So the screen must not render a filtered trail for a non-Owner — it must render nothing, and the
server must already have refused.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **The server decides, always.** The route guard is convenience; `AuditRead` is Owner-only server-side and answers `403` to everyone else | `CLAUDE.md` · spec.md §9 |
| 2 | **A refusal renders as S-016 at `/forbidden`, never as a silent redirect** — `ux/navigation.md`, and the rule that `clientManageGuard` broke once already (D-114 §3) | `ux/navigation.md` · D-114 |
| 3 | **Standalone, signals, signal forms, zoneless, `@if`/`@for`, `inject()`** | `CLAUDE.md` |
| 4 | **RTL is the primary direction.** Logical properties only | `CLAUDE.md` · `ux/rtl-and-i18n.md` |
| 5 | **No hardcoded strings**, both catalogues, from the first commit | `CLAUDE.md` |
| 6 | **Latin runs inside Arabic rows are bidi-isolated** — identifiers, timestamps, IP addresses. Without it a timestamp reorders visually and the trail reads wrong | `ux/rtl-and-i18n.md` |
| 7 | **The trail is read-only, and the screen offers no control that implies otherwise** — no edit, no delete, no "correct this". `AC-117-H`: no such endpoint exists and the database trigger refuses it | `CLAUDE.md` · `AC-117-H` |
| 8 | **The four `audit.grant.*` i18n keys are this story's to use or to delete.** They have been orphaned since slice 0 and were kept *"pending a KAFF-117 judgement"* — this is where that judgement is made, and leaving them orphaned a third time is not one of the options | `stories/backlog.md` — owed items |

## Acceptance criteria

**AC-128-A — the trail renders Arabic RTL at 390px** *(fails if the rule is broken)* — **inherits `AC-117-I`, moved 2026-09-05 before KAFF-117 was pulled**
Given the trail at 390px in Arabic
When it renders
Then direction is RTL, Latin identifiers and timestamps inside Arabic rows are bidi-isolated, and there is no horizontal overflow

**AC-128-B — nobody but the Owner reaches it, and the refusal is visible** *(fails if the rule is broken)*
Given Finance, Technical Office **on a project they are assigned to**, and a portal `Role.Client` user
When each navigates to the audit route by URL
Then each lands on `/forbidden` with S-016's surface in their language and the chrome intact
And the Technical Office case is the one that matters: an assignment does not admit them (D-049 ruling 1)

**AC-128-C — the guard awaits session resolution itself** *(fails if the rule is broken)*
Given the audit route typed, bookmarked or refreshed as a hard load
When the Owner arrives
Then the screen renders, and the guard does not depend on its position in the `canActivate` array (D-113 §2)

**AC-128-D — the screen offers no way to change a record** *(fails if the rule is broken)*
Given the trail rendered
When every control on it is enumerated
Then none edits, deletes or corrects a record — the trail is append-only and the screen must not suggest otherwise

**AC-128-E — every string is in both catalogues, and the orphans are resolved** *(fails if the rule is broken)*
Given the screen
When the catalogues are compared
Then every key it uses exists in `ar.json` and `en.json`
And the four `audit.grant.*` keys are either used by this screen or deleted from both catalogues — **not left orphaned a third time**

**AC-128-F — an E2E test exists for the guard and the RTL width** *(fails if the rule is broken)*
Given the screen is built
When `tests/E2E.Tests` runs
Then a non-Owner reaching the route lands on `/forbidden`, and the trail does not scroll sideways at 390px

> **`AC-128-F` is written down for the same reason `AC-127-I` is:** KAFF-126 shipped without an E2E
> test and it had to be paid back the next day (D-114 §4). Evidence a build session produces is not a
> check that runs tomorrow.

## Not in this story
Everything KAFF-117 owns: the endpoint, the filters, the permission itself, and `AC-117-A` … `AC-117-H`.
Exporting the trail. A global Finance/Audit role — D-049 ruling 1 anticipates one *"if added later"*
and does not create one.

## Questions for Karim
None that block. **What the trail shows for a deactivated actor** is settled by `AC-118-J` — the
records still name them — so the screen renders the name it was written with, not a lookup that
would come back empty.

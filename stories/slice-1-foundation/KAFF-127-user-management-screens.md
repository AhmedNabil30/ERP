# KAFF-127 · The user-management screens

**Slice:** 1 · **Epic:** Foundation · **Points:** 8 (**proposed**) · **Status:** **Ready** — cut 2026-09-05 by the Scrum Master at the sprint-3 close. **Not pulled: scope is Nabil's.**
**Spec:** §9 · **Decisions:** D-051, D-055, **D-111** (the two lanes), D-113
**UX:** `ux/slice-1-flows.md` · `ux/navigation.md` · `ux/components.md`
**Depends on:** KAFF-106, KAFF-108, KAFF-109, KAFF-110, KAFF-112, KAFF-125 — **all merged**, which is §2a rule 1's whole requirement

## Why this story exists

**The same reason `KAFF-126` existed, and it is the last instance of it in slice 1.**

`process/agile.md` §2a rule 5, adopted 2026-09-04 at Nabil's direction:

> *"A UI criterion sitting on a delivered backend story is a defect in the board. Move it to the
> Frontend story that will discharge it — moved, not copied."*

`AC-106-J` — *"Arabic, RTL, at mobile width"* — was marked **"deferred to Frontend"** on 2026-08-25.
**Frontend is a role, not a story, and a criterion deferred to a role is a criterion nobody is
holding.** It sat undischarged for nineteen days. `AC-119-L`, `AC-121-I` and `AC-124-I` were homeless
for one day each before KAFF-126 was cut for them; this one outlasted all three combined, because
nothing on the board was pointing at it.

**And the hole is wider than the one criterion.** Five identity endpoints are merged, tested, gated
and reachable by nobody:

| Endpoint | Story | Screen |
|---|---|---|
| `POST /api/users` | KAFF-106 | **none** — and it carries the only held criterion |
| `PUT /api/users/{userId}/department` | KAFF-108 | **none** |
| `PUT /api/users/{userId}/role` | KAFF-109 | **none** |
| `POST /api/users/{userId}/deactivate` | KAFF-110 | **none** |
| `POST /api/users/{userId}/reactivate` | KAFF-112 | **none** |

**Four of those five carry no UI criterion at all**, which is why only one of them shows up as a board
defect. That is not the same as them being fine: *"the Owner creates a user"* is not a delivered
capability while the only way to do it is a POST body. **The absent criteria are the quieter half of
the same defect**, and this story is where they are answered — a story cut against the endpoints, not
only against the one criterion that happened to be written down.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **The server decides, always.** Every screen here is already gated server-side; hiding a control is convenience | `CLAUDE.md` · spec.md §9 |
| 2 | **Standalone, signals, signal forms, zoneless, `@if`/`@for`, `inject()`.** No NgModule, no `BehaviorSubject` for component state, no Zone.js | `CLAUDE.md` |
| 3 | **RTL is the primary direction, not a mirror.** Logical properties only | `CLAUDE.md` · `ux/rtl-and-i18n.md` |
| 4 | **No hardcoded user-facing strings**, in both catalogues, from the first commit | `CLAUDE.md` |
| 5 | **A refusal renders as S-016, never as a silent redirect.** `ux/navigation.md`: *"It must not render as a crash, a blank page, or a redirect that hides what happened"* — and that rule was broken once already, by `clientManageGuard` (D-114 §3). There is a `/forbidden` route now; use it | `ux/navigation.md` · D-114 |
| 6 | **An HR user cannot be created or moved outside the HR department.** The form must not offer the combination the server refuses — the same shape as §6.7's pair on the client form (D-109 §1) | KAFF-106 rule · KAFF-107 |
| 7 | **Deactivation asks for a reason, and the reason is stored verbatim.** It is a required field, because `AC-118-G` asserts it lands on the audit record | KAFF-110 · `AC-118-G` |
| 8 | **A role change and a deactivation both revoke project assignments**, and the screen must say so **before** the act, not report it after. Four audit records, one act (D-049 ruling 5) | KAFF-109 · KAFF-111 · `AC-118-C`, `AC-118-D` |
| 9 | **Nobody edits their own role and nobody deactivates themselves.** If the server refuses it, the screen must not offer it | spec.md §9 |
| 10 | **No password is ever displayed after creation except the temporary one, once.** It is the one moment it exists in the clear, and `localStorage` is prohibited for it as for the token | D-050 · KAFF-106 |

## Acceptance criteria

**AC-127-A — the user list renders Arabic RTL at 390px** *(fails if the rule is broken)*
Given the user list at 390px in Arabic
When it renders
Then direction is RTL, user names and roles resolve from the catalogue, and there is no horizontal overflow

**AC-127-B — the create form renders Arabic RTL at 390px** — **inherits `AC-106-J`, moved 2026-09-05**
Given the user form at 390px in Arabic
When it renders
Then direction is RTL, every label resolves from the catalogue, and there is no horizontal overflow

**AC-127-C — the HR pair is kept legal on the way in, not submitted to be refused** *(fails if the rule is broken)*
Given the create form with `Role.Hr` selected
When a department other than HR is chosen
Then the form does not submit the combination, and if the server refuses anyway `errors.identity.hr_role_requires_hr_department` is shown against the field

**AC-127-D — deactivation states its consequence before it happens** *(fails if the rule is broken)*
Given a user with three active project assignments
When the Owner opens the deactivate confirmation
Then it names the number of assignments that will be revoked, before the act
And the reason field is required

**AC-127-E — a role change states the same consequence**
Given a Site Engineer with three active project assignments
When the Owner changes their role
Then the confirmation names the assignments that will be revoked (KAFF-109, D-051 Q27)

**AC-127-F — the temporary password is shown once and never stored** *(fails if the rule is broken)*
Given a newly created user
When the response is rendered
Then the temporary password is displayed once, and it is written to no storage of any kind — not `localStorage`, not `sessionStorage`, not a signal that survives navigation

**AC-127-G — a role without the permission reaches nothing** *(fails if the rule is broken)*
Given a Finance user, then a portal `Role.Client` user
When each navigates directly to the user-management routes by URL
Then each sees S-016's Forbidden surface at `/forbidden`, in their language, with the app chrome intact
And the route guard awaits session resolution itself rather than relying on its position in the `canActivate` array (D-113 §2)

**AC-127-H — every string is in both catalogues** *(fails if the rule is broken)*
Given the screens
When the catalogues are compared
Then every key these screens use exists in `ar.json` and `en.json`, and no key is added that no template uses

**AC-127-I — an E2E test exists for at least the guard and the RTL width** *(fails if the rule is broken)*
Given the screens are built
When `tests/E2E.Tests` runs
Then a bookmarked deep URL loads its screen, a role without the permission lands on `/forbidden`, and the list does not scroll sideways at 390px

> **`AC-127-I` is here because KAFF-126 shipped without one and it had to be paid back the next day
> (D-114 §4).** The evidence a build session produces is not a check that runs tomorrow. Writing the
> criterion down is the cheapest way to stop that being rediscovered a third time.

## Not in this story
The audit trail screen — **KAFF-117**, Owner-only, and it is Lane A's. The project team panel —
**KAFF-115**, which is its own story and its own 8 points. Password reset — **KAFF-104**.

## Questions for Karim
None that block. **Whether a deactivation reason is picked from a list or typed free-text** is the
same shape as Q35 and the duplicate-phone reason; free-text is assumed, because `AC-118-G` says
*verbatim*.

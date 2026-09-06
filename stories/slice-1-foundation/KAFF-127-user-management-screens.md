# KAFF-127 · The user-management screens

<!-- kaff id=KAFF-127 slice=1 points=8 state=VERIFIED verdict=CONDITIONAL at=116c08a on=2026-09-06 -->

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

### 📌 `AC-127-J` … `AC-127-N` — added 2026-09-07, closing `V-34-A`

**`GET /api/users` shipped against no acceptance criterion.** `qa/slice-1/verification-2026-09-06.md`
-> `V-34-A`: *"KAFF-127's criteria are `AC-127-A`…`I` and every one of them is a screen criterion; the
story's business-rule table names no read endpoint either … The endpoint's permission, its payload and
its refusals are asserted only by tests written in the same commit as the endpoint … there is no
statement of what should be true for a Verifier to execute against, only a statement of what is."*

The endpoint is real and reachable [Verified: 2026-09-07 @
`src/Api/Features/Users/ListUsers/Endpoint.cs` -> `Endpoint.Route`]. **The five below are written from
`spec.md` §9, `ux/screen-inventory.md` -> `S-006`, `ux/slice-1-flows.md` -> `S-006`, and the D-numbers
each cites — not from the handler.** Where the handler does something no rule asks for, it is in
*Findings* at the foot of this story, unasserted, rather than turned into a criterion that could never
fail.

**AC-127-J — the user list is the Owner's, and every other role that can sign in is refused** *(fails if the rule is broken)*
Given each role that may hold a staff session and is not the Owner — Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales and Hr — and a portal `Role.Client`
When each calls `GET /api/users` directly, bypassing the SPA entirely (`CLAUDE.md`: hiding a menu item is presentation, not security)
Then every one of them is refused
And **the refused set is derived from `Role` rather than written out by hand**, so a role added to the enum is either refused or reddens the assertion — `decisions.md` **D-122 §4** / `V-34-E`, where deleting the `Role.HeadOfDesign` row left `ListUsersTests` **6/6 green**
*Rule:* `UserManage` is CompanyWide and granted to the Owner alone [Verified: 2026-09-07 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserManage` — `PermissionScope.CompanyWide, [owner]`]; `ux/screen-inventory.md` -> `S-006`: *"Owner only — `UserManage` is `CompanyWide`, Owner alone (D-044 §1)."*

**AC-127-K — HR holds `UserRead` and it does not reach this list, and that absence is the ruling** *(fails if the rule is broken)*
Given an HR user, who does hold `Permission.UserRead` [Verified: 2026-09-07 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead` — `PermissionScope.CompanyWide, [owner, hr]`]
When they call `GET /api/users`
Then they are refused, on the same grounds as every other non-Owner role
And no route is added that projects this payload behind `UserRead`
*Rule:* `spec.md` §9's amendment of 2026-08-22 (`decisions.md` **D-055 §2**), verbatim: *"names and roles only, no editing … This does **not** hand HR the Owner's user administration surface — usernames, departments and active state for every account."* This payload **is** that surface. **`Permission.UserRead` therefore has no endpoint at all today, and that is a decision, not a gap to be closed** — `V-34-F` reports the grant as reaching nothing and is right about the fact and wrong about the remedy: HR's names-and-roles list, if the business ever asks for one, is a **different response type on a different route**, because D-055 §2's own words are *"the permission is not the whole control — the endpoint's projection is."* **Do not open this endpoint to HR.**

**AC-127-L — a deactivated user is on the list, because nothing else can reach them** *(fails if the rule is broken)*
Given a user who has been deactivated
When the Owner opens the user list
Then that user is present and shown as inactive, in a neutral treatment rather than an error one
*Rule:* `decisions.md` D-049 ruling 5 — *leavers are deactivated, never deleted*; `ux/screen-inventory.md` -> `S-006` names **active state** as one of the four things the screen carries, and `ux/slice-1-flows.md` -> `S-006` draws the `[ Inactive ]` chip as *"neutral chip, not an error colour"*. **A list of active accounts only would leave `POST /api/users/{userId}/reactivate` (KAFF-112) with no subject any screen can name**, which is the whole defect this story was cut against.

**AC-127-M — the number the confirmation states is the number the act revokes** *(fails if the rule is broken)*
Given a user holding three active project assignments and one already revoked
When the Owner opens the deactivate confirmation of `AC-127-D`, reads the figure and the project names, and completes the act
Then the figure read **three**, the three projects named are the three that were assigned, and exactly those three assignments are revoked with three assignment audit records written
And the already-revoked assignment is neither counted nor named
And a user holding **no** assignment shows no project list and no revocation line at all, rather than a zero
*Rule:* rule 8 above, KAFF-109, KAFF-111, `AC-118-C` / `AC-118-D`, and `ux/slice-1-flows.md` -> `S-008`, verbatim: *"The count and the names come from the server, in the same response that describes the user. Do not compute them in the client from an assignment list and do not guess the number"* — and, for the zero case, *"If the user holds **no** assignments, the project list and the revocation line are omitted rather than rendered as '0 projects'."* `AC-127-D` asserts the figure is **shown before the act**; this asserts it is **true**. A screen that states a number the act then contradicts is worse than one that states nothing, and the two can only agree if the figure comes from the same active-assignment set the handlers revoke — a second, independently written count is the defect this criterion exists to catch.

**AC-127-N — the payload is a whitelist, and a new column on `User` does not reach the wire** *(fails if the rule is broken)*
Given a field is added to the `User` entity
When `GET /api/users` is called
Then the response carries only the fields `S-006` names, plus what `AC-127-M` requires, and the new field does not appear
And the check is a pin on the response type itself, not a reading of the projection
*Rule:* `decisions.md` **D-055 §2** — *"A `UserRead` endpoint returning the full user row satisfies the permission and breaks the ruling. Whoever builds it projects name and role, and stops"* — the same argument one level up, applied to the Owner's surface: **the permission gate does not bound the payload, only the response type does.** The precedent is `AC-105b-F`, where *"a reflection test fails the instant that changes"*. This is the surface every future field on `User` — a salary, a national id, a home address — arrives on by default unless something refuses it.

## Not in this story
The audit trail screen — **KAFF-117**, Owner-only, and it is Lane A's. The project team panel —
**KAFF-115**, which is its own story and its own 8 points. Password reset — **KAFF-104**.

## Findings on `GET /api/users`, recorded rather than blessed — BA, 2026-09-07

**`AC-127-J`…`N` were derived from the rules. These are the places the shipped endpoint and the rules
do not line up.** None is written as a criterion: a criterion transcribed from the implementation
asserts what the code does rather than what the business needs, and can never fail.

| # | What | Why it is here, not a criterion |
|---|---|---|
| **F-127-1** | **`ux/slice-1-flows.md` -> `S-006` draws a search box (*"Search name or phone"*) and `[ All ] [ Active ] [ Inactive ]` filter chips. The endpoint has neither, and no criterion on this story — old or new — asks for either** [Verified: 2026-09-07 @ `src/Api/Features/Users/ListUsers/Endpoint.cs` -> `Endpoint.Map` — a bare `MapGet` with no query parameter; @ `Handler.HandleAsync` — no predicate and no `Skip`/`Take`]. The endpoint's own remarks record this as *"owed, and recorded as owed rather than guessed."* **This is a ruled UX element with nothing behind it — a gap in the other direction from `V-34-A`.** Writing the criterion here would enlarge a story that is already shipped and carries a verdict; leaving it silent lets `S-006` read as discharged. **Whether it belongs to this story or to a follow-on is a scope call and Nabil's** | Ruled by UX, undischarged, and not this agent's to schedule |
| **F-127-2** | **The list is unbounded and computes one correlated assignment sub-query per user row** [Verified: 2026-09-07 @ `src/Api/Features/Users/ListUsers/Handler.cs` -> `HandleAsync`]. Correct today at Kaff's headcount, and `AC-127-M` needs those names in the same response. No rule anywhere states a page size, and `S-006`'s flow draws no pagination | A performance ceiling, not a business rule. **Routed to the Architect**, who owns whether a list endpoint gets a bound before slice 3 |
| **F-127-3** | **Rows are ordered by `FullName`** [Verified: 2026-09-07 @ `src/Api/Features/Users/ListUsers/Handler.cs` -> `HandleAsync`]. Nothing rules an order for `S-006`; the Arabic collation this sorts under is not stated anywhere either | Sensible, unruled, and harmless. Named so the next reader knows it is a choice rather than a requirement. **If the order matters to Kaff, it is a question for Karim and nobody has asked it** |
| **F-127-4** | **`Permission.UserRead` has no endpoint at all** [Verified: 2026-09-07 — no `RequirePermission(Permission.UserRead)` under `src/Api/Features/`]. `V-34-F` reports this as a defect | **It is a decision, and `AC-127-K` now records it as one.** D-055 §2 gives HR *"names and roles only"* and refuses HR the Owner's administration surface; this payload **is** that surface. The remedy is a separate route with a two-field projection **if the business asks for one** — not opening this one |

## Questions for Karim
None that block. **Whether a deactivation reason is picked from a list or typed free-text** is the
same shape as Q35 and the duplicate-phone reason; free-text is assumed, because `AC-118-G` says
*verbatim*.

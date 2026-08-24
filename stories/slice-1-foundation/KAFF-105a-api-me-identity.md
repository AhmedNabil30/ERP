# KAFF-105a · `GET /api/auth/me` returns who I am and what I may do

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** BLOCKED
**Spec:** §9, §12 · **Decisions:** D-012, D-035, D-044, **D-050**, **D-051 (KAFF-105 split)**
**Depends on:** KAFF-101a

> **Split from KAFF-105** on 2026-08-21. The identity-and-permissions half was answerable and is this
> story. **The project list is KAFF-105b**, which was blocked on Q32 and is now `Ready` too (D-051
> Q32). `stories/README.md`: a split story keeps its number and gains `a` / `b`. **KAFF-105 as a
> single ID is retired; test cases written against it map to 105a unless they concern the project
> list.** The split was recommended by the sprint report and approved by the Architect — recorded in
> D-051, with the reason it was recorded at all: *"block the smallest thing that is actually
> unanswerable."*

## Story
As the frontend, I ask the server once who the signed-in user is — id, name, role, department and the
permissions actually evaluated for them — because after D-050 I have no other way of knowing that
anybody is signed in at all.

## D-050 made this endpoint structural, not convenient
It was requested at the kickoff as a way of stopping the frontend re-implementing
`PermissionCatalogue` in TypeScript (`meetings/2026-08-18-slice-1-kickoff.md` §4, UX → Architect).
That reason still holds. A second and harder one now sits under it:

> `AuthService` no longer stores anything. It holds profile facts fetched from `/api/auth/me`, and
> the `Session` type **has no token field** — the shape itself refuses the mistake. — D-050

The session lives in an `HttpOnly` cookie the page cannot read. **So this endpoint is the only thing
that can tell the UI whether anyone is signed in.** Without it the frontend has a session it cannot
see and a user it cannot name.

**The route is `GET /api/auth/me`** — not `/api/me`. `AuthService` already names it, and a mismatch
here would be found by a 404 in the browser rather than by a test.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The response carries user id, display name, role, department and Operations sub-department | §9 |
| 2 | **The response carries no token, and there is no field it could be put in** | D-050 |
| 3 | ⛔ **CONTRADICTS `AC-105a-C` — DO NOT BUILD EITHER SHAPE. See "The `password_change_required` shape is undecided" below.** The rule as drafted: it *reports* whether the signed-in user must still change a temporary password, because the shell has to send them to that screen and nowhere else. The fact is on the entity as `User.MustChangePassword` — read it, do not re-derive it [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `MustChangePassword`] | D-049 ruling 4 · KAFF-103 — **but neither source decides field-or-refusal; that is N-04 / Q-UX-18, action SM-16, open** |
| 4 | **Only `CompanyWide` permissions are returned, as a flat set — and for `Role.Client` that set is empty.** `PortalRead` and `PortalApprove` are `ProjectScoped`, so they are not in it [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PortalRead`, `Permission.PortalApprove`]. A project-scoped row placed in a company-wide list is read as holding everywhere: D-035's second path is a permission *"company-wide, so evaluated with **no project check and no client check at all**"*, and §12's boundary is the one that must not be crossed that way. The per-project list — where a portal client's two rows do belong — is **KAFF-105b**, deferred | §9 · D-035 · D-051 (the 105a/105b split) · `PermissionScope` |
| 5 | The response is computed from `PermissionCatalogue`, never from a hand-written list. A permission added to the catalogue appears here without this endpoint being edited | D-012 |
| 6 | A `Role.Client` **holds** `PortalRead` / `PortalApprove` and nothing else. That is a statement about the catalogue, not about this payload — both rows are `ProjectScoped`, so under rule 4 this endpoint returns the client an empty company-wide set and the rows surface in KAFF-105b [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PortalRead`] | §12 · D-035 |
| 7 | What this endpoint returns **decides nothing**. Every request is authorised again server-side | §9 · CLAUDE.md |
| 8 | An unauthenticated call is refused rather than answered with an empty profile. The frontend distinguishes "signed out" from "signed in as nobody", and only one of those is a real state | §9 |
| 9 | The response carries no security stamp and no password field, redacted or otherwise. Both are `[AuditRedacted]` on the entity, which governs the audit trail and **not** an API projection — this rule is the projection's own job [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp`] | CLAUDE.md audit · slice 0 `User` |

## Permissions, money, audit, i18n
- **Permissions:** authenticated, any role, no assignment. It returns only the caller's own facts.
- **Money:** moves no money and carries no money field. **No balance, no cost, no margin** — a
  convenient "project value" here would reach the portal (§12) and HR (D-044 ruling 2) in one step.
- **Audit:** none. This is a read, and CLAUDE.md requires an audit record on a *state change*. A
  record per navigation refresh would bury the records that matter.
- **i18n:** none in the payload. It returns identifiers — role names, permission names — and the
  client resolves them through the catalogue. **The server never sends prose**
  (`problem-details.ts`, slice 0).

## Acceptance criteria
**AC-105a-A — the caller learns who they are**
Given I am an active Finance user in `Department.Finance`
When I call `GET /api/auth/me`
Then the response names my id, display name, role and department, and lists the company-wide permissions Finance holds

**AC-105a-B — no token, anywhere** *(fails if the rule is broken)*
Given any successful call
When the response body is inspected field by field
Then no field contains the session token, and `localStorage` and `sessionStorage` remain empty

**AC-105a-C — a forced password change is announced** — ⛔ **BLOCKED, DO NOT BUILD. It contradicts
rule 3, and which of the two is right has never been decided.** See *"The `password_change_required`
shape is undecided"* below. The text stands unedited so the disagreement stays visible; **an
acceptance criterion nobody may execute is the honest state here, and picking a side to remove the
mark would be the BA inventing the answer.**
Given a user who has signed in with a temporary password and not yet changed it
When the shell calls this endpoint
Then it is refused with `errors.auth.password_change_required` (AC-103-B), which is the signal to route them to the change screen

**AC-105a-D — signed out is not "signed in as nobody"** *(fails if the rule is broken)*
Given no session cookie and no `Authorization` header
When `GET /api/auth/me` is called
Then it is refused with 401 — not answered with a profile whose fields are null

**AC-105a-E — the endpoint and the catalogue cannot drift** *(fails if the rule is broken)*
Given a permission is added to `PermissionCatalogue` with a grant for Finance
When a Finance user calls `GET /api/auth/me`
Then it appears in the response with no change to this endpoint's code

**~~AC-105a-F~~ — RETIRED 2026-08-22** · superseded by `AC-105a-H` · *it asserted that a portal client
receives `PortalRead` and `PortalApprove` from this endpoint. Both rows are `ProjectScoped`
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PortalRead`],
so under rule 4 — company-wide only — the criterion asserted the opposite of the rule it sat beside,
and building it would have put project-scoped rows into a company-wide list for the one role §12 draws
a hard boundary around. Finding **V-04**. The ID is retired, not recycled: `qa/slice-1/test-cases.md`
-> `TC-1-042` cites it and must be relocked to `AC-105a-H`, whose assertion is the **inverse** — that
is deliberate, a dead case failing loudly beats a live case asserting the withdrawn rule
(`stories/README.md` rule 4).*

**AC-105a-H — a portal client's company-wide set is empty, and nothing project-scoped leaks into it** *(fails if the rule is broken)*
Given I am `Role.Client`
When I call this endpoint
Then the permission set is empty
And it contains neither `PortalRead` nor `PortalApprove`, because both are `ProjectScoped` and this payload carries `CompanyWide` rows only (rule 4)
And it contains no internal permission of any scope — that is D-035 reopening

*Appended 2026-08-22 as the replacement for the retired `AC-105a-F` (finding **V-04**). **What a
portal client may do per project is `KAFF-105b`, deferred** — so in slice 1 the portal learns its two
permissions from nowhere, which is correct rather than a gap: the portal is a separate host and lands
in slice 8 (D-051 Q33), and there is no portal shell in this sprint to read them.*

**AC-105a-G — nothing secret leaks**
Given any successful call
When the payload is inspected
Then it contains no password hash and no security stamp

## The `password_change_required` shape is undecided — rule 3 vs `AC-105a-C`

**Finding V-03, raised by the Verifier on 2026-08-22. Not resolved here, and deliberately not.**

Rule 3 says this endpoint **reports** a flag. `AC-105a-C` says the call is **refused**. One endpoint
cannot do both, and **`spec.md`, `decisions.md` and the code between them do not decide which**:

* **D-049 ruling 4** gives the business rule — *"a temporary password set by the Owner which the user
  must change on first sign-in"* — and says nothing about which endpoints the forced-change session
  may reach, still less about a payload shape.
* **D-050** makes this endpoint the only thing that can tell the shell anyone is signed in, which
  argues for the flag. It does not rule on it, and a 403 carrying `errors.auth.password_change_required`
  is still distinguishable from the 401 of `AC-105a-D`, so the refusal shape is workable too.
* **The frontend already carries the flag** — `Session.mustChangePassword`, and `AuthService` holds
  nothing else that could [Verified: 2026-08-22 @ `src/Web/src/app/core/auth/auth.service.ts` ->
  `mustChangePassword`]. **That is not a source.** `stories/README.md`: *"A story never resolves an
  ambiguity by picking the reading the code already implements."*
* **UX wrote its dispatcher against the refusal** and said why — *"the stricter of the two and the one
  an acceptance criterion asserts"* [Verified: 2026-08-22 @ `ux/questions.md` -> `Q-UX-18`]. The
  acceptance criterion it deferred to is `AC-105a-C`, so that reason is circular the moment
  `AC-105a-C` is the thing in question.

**It is already a registered, routed and unclosed action.** `Q-UX-18` → refinement finding **N-04**,
action **SM-16**, owner **BA + Architect**, due *"KAFF-105a build"*
[Verified: 2026-08-22 @ `stories/questions-for-karim.md` -> `Q-UX-18`]. It is **not Karim's** — the
register says so on the same row — so it does not become a numbered question there. It needs the
Architect, and it needs deciding before this endpoint is built.

**The collision is three-way and one story cannot close it.** Two other committed stories take
positions:

| Where | What it says | Which side |
|---|---|---|
| `KAFF-105a` rule 3 | the endpoint *reports* the flag | field |
| `KAFF-105a` `AC-105a-C` | the endpoint is *refused* | refusal |
| `KAFF-103` `AC-103-B` | `GET /api/auth/me` is among the endpoints refused until the change | refusal |
| `KAFF-100` `AC-100-F` | *"`GET /api/auth/me` **reports** that no password change is required"* | field |

**All four are marked and none is edited.** Whichever way SM-16 goes, three of the four move together
or the contradiction simply relocates.

**What this does not affect.** Rules 1, 2, 4–9 and every other criterion here are unaffected and
buildable. **This is the smallest thing that is actually unanswerable** (`stories/README.md`), and it
is one field on one response.

## Not in this story
**The project list and every assignment on it — KAFF-105b**, which is one response section of this
same endpoint and is built with it or immediately after it. **A portal client's `PortalRead` /
`PortalApprove` belong there too** — see rule 4 and `AC-105a-H`. The navigation itself — which menu
items each role sees is UX's screen inventory, and it is convenience, not security. Re-fetching the
payload when a role or department changes mid-session: KAFF-108 and KAFF-109 cover what happens to the
caller's authority, and the frontend simply calls this endpoint again.

## Questions for Karim
**None — and that is the point about V-03.** The one open item on this story, the
`password_change_required` shape (**N-04 / Q-UX-18 / SM-16**), is **not a business question**: the
register records it as *"not a register row … Not a business question and not Karim's"*
[Verified: 2026-08-22 @ `stories/questions-for-karim.md` -> `Q-UX-18`]. It goes to the **Architect**,
through the Scrum Master. Routing it to Karim would be asking the wrong person and would leave it open
for another sprint.

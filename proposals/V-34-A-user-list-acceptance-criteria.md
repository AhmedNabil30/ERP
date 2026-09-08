# V-34-A — the missing acceptance criteria for `GET /api/users`

> # ⛔ SUPERSEDED 2026-09-08 — reconciled into the story, and this file is now HISTORY
>
> **The criteria that count are in
> [`stories/slice-1-foundation/KAFF-127-user-management-screens.md`](../stories/slice-1-foundation/KAFF-127-user-management-screens.md),
> as `AC-127-J` … `AC-127-T`.** This file is a dated record and states no current position — read it
> for *how* the criteria were derived, never for what they now say. `STATUS.md`'s map: `proposals/**`
> is HISTORY.
>
> **What happened to this document.** `GET /api/users` was given acceptance criteria **twice,
> independently, and neither author read the other** — `AC-U-A` … `AC-U-L` here on 2026-09-06, and
> `AC-127-J` … `AC-127-N` in the story on 2026-09-07. They were reconciled on 2026-09-08 by a third
> BA session, sprint-6 item 7, and **the comparison is at `KAFF-127` → *Reconciliation of the two
> `V-34-A` passes***. In summary:
>
> * **Four criteria agreed almost word for word** — `AC-U-B`/`AC-127-J`, `AC-U-C`/`AC-127-K`,
>   `AC-U-J`/`AC-127-L`, `AC-U-L`/`AC-127-M`. Two BAs reading the same rulings a day apart, neither
>   having seen the other. **That agreement is the most useful thing this document produced.**
> * **Six criteria here had no counterpart in the story and were right** — `AC-U-A`, `AC-U-D`,
>   `AC-U-F`, `AC-U-G`, `AC-U-H`, `AC-U-I`. They are folded in as **`AC-127-O` … `AC-127-T`**.
> * **`AC-U-K`'s eight-member list was the half `AC-127-N` was missing** and is folded in as
>   `AC-127-P`.
> * **§6's recommendation (a separate story) was not taken**, and its proposed id `KAFF-129` **has
>   since been taken by another story** — *Partition `audit_records` by month*. The split, if there
>   is one, is Nabil's; §6 says so itself.
> * **§5's `Q58` and `N12` were never merged into the register** and `Q58`'s number has since been
>   taken by a different question. They are now **`Q64`** and **`N12`** in
>   `stories/questions-for-karim.md`.
> * **§7.3's `D-055 §3` citation defect is REPAIRED** in `Endpoint.cs` and `ListUsersTests.cs`.
>   **§7.1's false test name is NOT** — it is still there, and still Backend's.
> * **One claim here is judged wrong**: `AC-U-B`'s blanket `403` over every refused role. A portal
>   `Role.Client` cannot present a staff cookie at all (D-050's `__Host-` prefix, D-051 Q33's separate
>   host), so that row is an unauthenticated refusal. `AC-127-J`'s vaguer *"refused"* is kept.
>
> **Nothing below this box has been edited.** It stands as it was written, including the two sections
> its own author left duplicated at the foot of the file.

**Author:** BA agent, dispatched by the unattended scheduled standup of **2026-09-06 11h UTC**. Nabil asleep.
**Deliverable:** this file. It is written to be read on its own, without the repository open.

> ## ⛔ Read this box before you trust a number in this file
>
> **There is no .NET SDK in the container this was written in, no PowerShell, and no Docker daemon.**
> `dotnet build`, `dotnet test`, `dotnet format`, `tools/status.ps1` and `/run-kaff-erp` **could not be
> run and were not run.** Every statement here about the code is **static reading of the files as they
> stand at commit `03bf556`** — file-and-identifier citations, no line numbers (SM-31). Nothing in this
> file is a gate result. Where a claim would need a compiler, it is marked **[unverified — needs a
> build]**.
>
> **This session also cannot push.** The file exists only in a container that is reclaimed when the
> session ends; it is committed locally as hygiene, and the criteria are repeated verbatim in the
> agent's closing message so they can be carried out by hand.

| § | Section | State |
|---|---|---|
| 1 | What `V-34-A` is, and what this document is | ✅ |
| 2 | The sources — everything below is derived from these, and they all pre-date the endpoint | ✅ |
| 3 | **The derived acceptance criteria** — `AC-U-A` … `AC-U-I` | ✅ |
| 3a | **Three more criteria** — `AC-U-J` … `AC-U-L` — and a disclosure about when I found their source | ✅ |
| 4 | **Divergences — findings, NOT criteria** | ✅ |
| 5 | Questions drafted, not answered | ✅ |
| 6 | Where the criteria should live — a recommendation, not a decision | ✅ |
| 7 | `V-34-F` — the false test name. Reported, not fixed | ✅ |
| 8 | Corrections to the Scrum Master's brief | ✅ |
| 9 | What I did not do | ✅ |

**Status: COMPLETE.** Every section is filled in and nothing is owed. If you are reading a version of
this file where a row says `pending`, that pass died mid-way and the missing section is genuinely
missing (D-120 §1).

---

## 1. What `V-34-A` is, and what this document is

`GET /api/users` shipped in commit **`116c08a`** (2026-09-06) with **no acceptance criterion anywhere
in the project**. I re-read `stories/slice-1-foundation/KAFF-127-user-management-screens.md` today and
confirm the Verifier's account of it rather than repeating it:

* Its criteria are **`AC-127-A` … `AC-127-I`**, nine of them, and **every one is a screen criterion** —
  RTL at 390px, the HR role/department pair on the form, the deactivation confirmation, the temporary
  password shown once, the route guard, the two catalogues, an E2E test.
* Its **ten business rules** name no read endpoint. Rule 1 says *"the server decides, always"*; that is
  a statement about the screens' relationship to the server, not a specification of a payload.
* Its own **"Not in this story"** section names KAFF-117, KAFF-115 and KAFF-104. It does not name a
  user-list endpoint either way.

So the Verifier's verdict — **NOT VERIFIABLE, not defective** — is the correct one. Verification is
*"does it do what the story says"*, and on this endpoint the story says nothing. The endpoint's
permission, payload and refusals are asserted **only** by `tests/Api.Tests/ListUsersTests.cs`, written
in the endpoint's own commit.

**What this document is.** §3 is a statement of what *should* be true for `GET /api/users`, derived
only from sources that existed **before** `116c08a`. §4 is a separate, later reading of the shipped
code and its tests, reporting where the two differ. **The order was real, not presentational:** §1–§3
were written and saved before `src/Api/Features/Users/ListUsers/` or `ListUsersTests.cs` was opened in
this session.

**Why the order matters more than the criteria do.** If the criteria were paraphrased out of
`ListUsersTests.cs`, the endpoint would become verifiable by definition and the next Verifier's pass
would prove nothing — the criteria and the tests would share one origin, which is the *whole* of what
`V-34-A` names. A criterion derived from the endpoint is not a criterion; it is a description.

---

## 2. The sources — and all of them pre-date `116c08a`

Everything in §3 comes from these, and nothing else. Each was read in full today.

| Source | What it gives |
|---|---|
| **`spec.md` §9**, opening paragraph | *"Permission = role × assignment. A user MUST be assigned to a project to open it or act on it. Role alone is insufficient. Enforcement is server-side; hiding UI elements is presentation, not security."* |
| **`spec.md` §9**, roles list | Nine roles. **`Subcontractor` is "record only, no login"**; `Client` is portal-only. |
| **`spec.md` §9 amendment, 2026-08-20, point 1** (= `decisions.md` D-010 / D-044) | *"The Owner is globally scoped … This is **reach, not capability**."* |
| **`spec.md` §9 amendment, 2026-08-20, point 3** (+ its ⚠️ superseded-count note) | HR is **strictly administrative with zero financial visibility**. |
| **`spec.md` §9 amendment, 2026-08-20, point 5** | *"Only the Owner creates users, company-wide."* |
| **`spec.md` §9 amendment, 2026-08-22, point 3** | *"**HR may read the user list — names and roles, nothing else.** `UserRead` is company-wide and held by HR and the Owner … This does **not** hand HR the Owner's user administration surface — usernames, departments and active state for every account."* |
| **`spec.md` §9 amendment, 2026-08-21, point 2** (= D-049 ruling 2) | *"A password change or a deactivation must invalidate every active session, everywhere, immediately."* |
| **`spec.md` §9 amendment, 2026-08-21, point 5** (= D-049 ruling 5) | Leavers are **deactivated, never deleted**, and stay on historical project teams. |
| **`spec.md` §12** | The portal client's boundary is absolute — the client must never see any other client's data. |
| **`decisions.md` D-044 ruling 1** | **`Permission.UserManage`, `CompanyWide`, `Role.Owner` alone.** Karim: *"strictly Global and held exclusively by the Owner."* Rejects folding user administration into `ProjectAssignmentManage`, which HR holds. |
| **`decisions.md` D-055 §2** (Q42 · `UserRead`) | The ruling, and the sentence that governs this endpoint: *"**the permission is not the whole control — the endpoint's projection is.** A `UserRead` endpoint returning the full user row satisfies the permission and breaks the ruling."* |
| **`stories/questions-for-karim.md` → `Q42`** | The warning preserved verbatim: *"**Do not close it by handing HR the Owner's user list**"* — described there as *"usernames, roles, departments and active state for every account in Kaff."* |
| **`decisions.md` D-066, "Not done"** (KAFF-106's close) | *"**A read endpoint.** Q42's warning stands and is not this story's: `UserRead` projects **name and role, and stops**."* — the read endpoint was explicitly deferred, unbuilt and unspecified. |
| **`decisions.md` D-106** (`V-32-A`) and **D-114 §1** (`AC-120-F`) | A payload guarantee is a **whitelist over the exact allowed member set**, never a blocklist of bad words. D-106 watched a `decimal RetainedAmount` ship past a green 241/241 through a blocklist. |
| **`CLAUDE.md`** | Every endpoint checks **role and assignment**, server-side. Audit records are for **state changes**. No money as `float`/`double`; nothing near money. No hardcoded strings. |
| **`agents.md` §7** | The Verifier reads `spec.md`, not the implementation, and **reports rather than fixes**. |
| **`stories/…/KAFF-124-list-and-search-clients.md`** | The house form for a list endpoint's criteria on the other master entity — permission row, portal-client refusal, a no-money criterion, an explicit default-filter criterion, an empty-state criterion. Verified PASS 2026-09-04, so it pre-dates `116c08a` and is a legitimate template. |
| **`stories/…/KAFF-117-read-the-audit-trail.md`** rule 10 | *"Reading writes nothing. An audit record per audit read would bury the records that matter."* |
| **`stories/…/KAFF-112-reactivate-a-user.md`** (via `STATUS.md`, VERIFIED PASS) | A reactivation path exists, so **some** surface must let the Owner find a deactivated account. That is an argument, not a ruling — see `Q59` in §5. |

**One derivation that has to be stated rather than assumed, because it looks like a rule being
skipped.** `CLAUDE.md` says *"every endpoint checks two things: role and assignment."* `UserManage` is
`CompanyWide` by D-044 ruling 1, and `spec.md` §9's assignment requirement is written about **projects**
— *"assigned to a project to open it or act on it."* A user list is not a project and cannot name one,
which is the identical reasoning `decisions.md` D-055 §3 used for `ProjectCreate`. So the assignment
axis on this endpoint is discharged **by the permission's scope**, not skipped — and the criterion
below (`AC-U-D`) pins that scope explicitly so a later session cannot quietly re-scope the row. The
mismatch between `CLAUDE.md`'s absolute phrasing and §9's project-shaped rule is recorded as an
ambiguity in §8, not resolved here.

---

## 3. The derived acceptance criteria

**Written to drop into either home** (see §6) — the identifiers are placeholders of the form **`AC-U-x`**
because the id depends on which story they land in, and that is Nabil's call. §6 gives the exact
substitution for each of the two options. Nine criteria, Given/When/Then, the house form; *(fails if
the rule is broken)* marks the ones that are prohibitions rather than features, the convention KAFF-124
and KAFF-127 both use.

Every criterion below names the source it comes from. **A criterion with no source is the failure mode
this whole document exists to avoid**, so there are none.

---

**`AC-U-A` — the Owner reads the list, and reads all of it** *(fails if the rule is broken)*
*Source: D-044 ruling 1; `spec.md` §9 amendment 2026-08-20 point 1 (reach, not capability).*

Given I am the Owner, and users exist across several departments, including some I have no project
assignment with
When I request the user list
Then I receive every user account in Kaff, with no assignment row anywhere in the system
And no project id, filter or assignment is required to obtain it

---

**`AC-U-B` — every other role is refused, and the refusal is exhaustive over the roles that exist** *(fails if the rule is broken)*
*Source: D-044 ruling 1 (`Role.Owner` **alone**); `spec.md` §9 (server-side enforcement).*

Given each role that can sign in and does not hold `Permission.UserManage`
When that role requests the user list, with and without a project id
Then every one of them is refused with `403`
And the set of refused roles is **derived from the `Role` enum** rather than written out by hand, so a
role added to the domain is covered on the day it is added and not on the day somebody remembers this
test

> **Why the second half is a criterion and not a test-style note.** D-044 ruling 1's grant is
> *"exclusively the Owner"* — a statement about **every** other role, including the ones that do not
> exist yet. A criterion that can only be satisfied by a hand-written list asserts something weaker
> than the ruling it comes from. This is also the shape of `V-33-A`/`V-34-E`; that those findings exist
> is not why the criterion is worded this way — the ruling is.

---

**`AC-U-C` — HR is refused, and that refusal is the ruling rather than an oversight** *(fails if the rule is broken)*
*Source: D-055 §2 and its rejected alternative; `stories/questions-for-karim.md` → `Q42`'s verbatim warning; `spec.md` §9 amendment 2026-08-22 point 3.*

Given I am an HR user, who holds `Permission.UserRead` and `Permission.ProjectAssignmentManage`
When I request the Owner's user-administration list
Then I am refused with `403`
And no username, department or active-state value appears anywhere in the response body

> **This criterion exists to be argued with, so it states its own defence.** D-055 §2 grants HR
> `UserRead` — *names and roles only* — and the register's warning against closing Q42 *"by handing HR
> the Owner's user list"* is preserved verbatim precisely because someone reading only the permission
> name would conclude the opposite. **HR's grant is satisfied by a different, narrower surface that
> does not exist** (§7, and `Q42`'s own words: *"the permission is not the whole control — the
> endpoint's projection is"*). **Do not discharge `AC-U-C` by widening this endpoint.**

---

**`AC-U-D` — the permission is `UserManage`, company-wide, and a project id does not change the answer** *(fails if the rule is broken)*
*Source: D-044 ruling 1; D-055 §3's reasoning about scope, applied to a resource that is not a project.*

Given the endpoint's authorization
When it is inspected, and then exercised with a project id supplied and with none
Then the gate is `Permission.UserManage` at `PermissionScope.CompanyWide`
And a supplied project id neither grants nor withholds access — a non-Owner assigned to every project
in Kaff is still refused, and the Owner with no assignment anywhere is still served

---

**`AC-U-E` — the payload is pinned to an exact member set, as a whitelist** *(fails if the rule is broken)*
*Source: D-106; D-114 §1; D-055 §2's "the projection is the control".*

Given the list response contract
When its members are compared against the set the story permits
Then they are **equivalent to that exact set** — a property added to the response type fails this
criterion whatever it is named
And the criterion is not satisfied by asserting the absence of a list of forbidden words

> **The member set itself is deliberately left blank here and is not mine to fill.** As written, this
> criterion supplies the *mechanism* and nothing else — a whitelist with no list. Writing a member list
> from the shipped response would be the exact substitution `V-34-A` reports.
>
> **⚠️ Amended by `AC-U-K`, §3a — and the amendment is left visible rather than folded in.** When this
> paragraph was written I believed the member set was underivable and had to become a question for
> Karim. It is derivable: `ux/slice-1-flows.md` **S-006** specifies the eight fields, and `AC-U-K`
> carries them with that citation. **`AC-U-E` and `AC-U-K` are two halves of one criterion** — the
> mechanism here, the list there — and they can be merged when they land in a story. The original
> sentence stays because a reader who checks my work should be able to see where I was wrong.

---

**`AC-U-F` — no money, and nothing joined to money** *(fails if the rule is broken)*
*Source: `CLAUDE.md` (money); `spec.md` §9 amendment 2026-08-20 point 3 (HR's zero financial visibility, as the reason the class of leak matters on an identity surface); KAFF-124 rule 5's precedent.*

Given the list response contract
When it is inspected
Then it carries no balance, no salary or pay figure, no cost, no margin, and no other money-shaped
field
And no money-bearing entity is joined into the projection to produce one

> Kaff's `User` has no money field today, which is exactly why this is worth writing down: D-106's
> finding was that a payload with no money field to leak stayed green across two sprints, and the leak
> arrived with the first natural addition. Slice 2 adds the Employee register and slice 10's §10 raises
> pay.

---

**`AC-U-G` — credentials never leave the server** *(fails if the rule is broken)*
*Source: `CLAUDE.md`; KAFF-117 rule 7 (`PasswordHash` and `SecurityStamp` are `[AuditRedacted]` and surface in no reading); D-055 §2 (*"no visibility into salary if one is ever added"*, the same shape).*

Given the list response
When it is inspected, for every user in it
Then no password hash, no security stamp, no temporary-password value and no reset token appears, in
any form, including inside a nested object

---

**`AC-U-H` — a dead session does not read the list** *(fails if the rule is broken)*
*Source: `spec.md` §9 amendment 2026-08-21 point 2 — a password change or a deactivation invalidates every session, everywhere, immediately.*

Given an Owner whose account has since been deactivated, and separately an Owner whose password has
since been changed on another device
When either presents the token they still hold and requests the user list
Then each is refused, and no user data is returned to either

---

**`AC-U-I` — reading the list writes no audit record** *(fails if the rule is broken)*
*Source: `CLAUDE.md` (audit is for **state changes**); KAFF-117 rule 10 verbatim; KAFF-124's audit line.*

Given the audit trail before the request
When the Owner reads the user list, any number of times
Then no audit record is written
And the trail's contents are byte-for-byte what they were

---

### What is deliberately **not** a criterion above, and why

**⚠️ The first two rows of this table were wrong when I wrote them, and are corrected in §3a rather than
deleted.** Both are derivable from `ux/slice-1-flows.md`, which I had not yet read. The strike-throughs
are the honest record of a document whose whole subject is not overstating what a source says.

| Not written | Because |
|---|---|
| ~~A default filter on deactivated accounts~~ | ~~Not derivable.~~ **CORRECTED, §3a — `AC-U-J`.** S-006's `Inactive` chip and `Inactive` filter cannot render from a list that omits inactive accounts. Only *which chip is preselected* is open, and it is a screen question: `N12` ①. |
| ~~The exact projected member set~~ | ~~Not derivable.~~ **CORRECTED, §3a — `AC-U-K`.** S-006 specifies the eight fields and the desktop column order. |
| Paging, ordering, a search term | **Still not derivable, and not Karim's.** `N12`, §5 — routed to Nabil and the Architect, since it is a shape decision rather than a business rule. |
| An RTL/mobile-width criterion | It would be a **screen** criterion, and `AC-127-A` already holds the user list's rendering. `process/agile.md` §2a rule 5 — a UI criterion belongs to the story that discharges it, moved and not copied. |
| An i18n/catalogue criterion | Same reason: `AC-127-H` holds it. A JSON list carries no user-facing string of its own; if a refusal key is added it belongs to `AC-127-H`'s sweep. |
| An E2E criterion | `AC-127-I` holds the screen's. An API-only E2E case for a read the screen already exercises would be `qa/`'s `TC-` work (`V-34-J`), not a tenth criterion. |

---

## 3a. Addendum — three more criteria, from a source I found late, and the disclosure that goes with it

**Read this preamble; it is the reason to trust or distrust the three criteria under it.**

After writing §3 and reading the code for §4, I found that `ux/slice-1-flows.md` — cited in KAFF-127's
own header, and gated by Nabil under `agents.md` §3 — contains **`S-006 · User list — Owner only`** and
**`S-008 · User detail and edit`**. They are a pre-existing, approved specification of this exact
screen, they pre-date `116c08a`, and I should have read them before §3. **They answer two of the three
things I was told might be unanswerable.**

**The disclosure:** unlike §3, these three were written **after** I had read the endpoint. So each one
below quotes the **verbatim pre-existing sentence** it rests on, and names its file, so that a reader
can check it against `ux/slice-1-flows.md` instead of taking my word that it did not come from the
code. §4 also records, per criterion, which shipped behaviour each one happens to endorse — judge the
contamination risk yourself rather than on my assurance.

---

**`AC-U-J` — the payload carries every account, whatever its state** *(fails if the rule is broken)*
*Source: `ux/slice-1-flows.md` S-006 — the wireframe draws filter chips reading `[ All ] [ Active ] [ Inactive ]`, a row rendering an `Inactive` chip keyed `users.state.inactive`, and a desktop column order of "Name · Username · Role · Department · Phone · State". Also `spec.md` §9 amendment 2026-08-21 point 5 (leavers are deactivated, never deleted) and KAFF-112 (a reactivation path must be able to find its subject).*

Given accounts exist in both states
When the Owner requests the list
Then every account is present regardless of state, and each row carries the state itself, so all three
of S-006's chips are renderable and the reactivation path has a subject to act on
And the endpoint does not decide which state the screen shows — **which chip is selected by default is
S-006's silence and a screen question**, not a payload question (see `N12`, §5)

---

**`AC-U-K` — the row carries what S-006 draws, and each field is traceable to it** *(fails if the rule is broken)*
*Source: `ux/slice-1-flows.md` S-006 — "Desktop: the cards become a table — column order right to left is **Name · Username · Role · Department · Phone · State**"; the mobile card draws `Site Engineer · Operations` and a phone in a `<bdi>`; the search input reads "Search name or phone". `spec.md` §9 for `OperationsSubDepartment` (Operations subdivides into Technical / Financial / Administrative, so Department alone under-describes an Operations user). An addressable identifier because the five sibling endpoints are all `/api/users/{userId}/…`.*

Given one row of the list
When it is inspected
Then it carries an addressable user identifier, the person's name, the username, the role, the
department, the Operations sub-department where one applies, the phone, and the active state — the
eight things S-006 renders and addresses a row by
And `Phone` is on that list **because S-006 draws it and its search box says "name or phone"**, not
because it happens to be on the entity

> **This is the criterion `AC-U-E` was waiting for.** `AC-U-E` supplies the mechanism — an exact-member
> whitelist — and `AC-U-K` supplies the eight members, each with a source. It is what I said in §3 I
> could not derive; S-006 derives it. **The ninth shipped member is not on this list** — see `D-2` in §4.

---

**`AC-U-L` — the assignments a destructive act would revoke come from the server** *(fails if the rule is broken)*
*Source: `ux/slice-1-flows.md` S-008, verbatim: "**The count and the names come from the server, in the same response that describes the user.** Do not compute them in the client from an assignment list and do not guess the number." And: "If the user holds **no** assignments, the project list and the revocation line are omitted rather than rendered as '0 projects'." Discharges the server half of `AC-127-D` and `AC-127-E`.*

Given a user holding active assignments and separately one holding none, and separately one whose
assignment has been revoked
When the response that S-008's confirmation is built from is read
Then it carries the names of the assignments that would be revoked **now** — revoked rows excluded —
and an empty collection rather than an absent field for a user holding none
And the client neither counts nor infers them

> **Where this belongs is a live question, not a settled one.** S-008 says *"the same response that
> describes the user"* — S-008 is the **detail** screen, and **there is no `GET /api/users/{userId}`**
> in the application (I enumerated every `MapGet` under `src/Api/Features`: `ListClients`, `GetClient`,
> `WhoAmI`, `ListUsers`, `ReadAuditTrail`, `GetHealth`, `GetSetupAvailability`). So today the list row
> is the only response that describes a user at all. See `D-2` in §4 and `N12` in §5.

---

## 4. Divergences — these are FINDINGS, not criteria

**Read after §3, and never merged into it.** Everything below came from opening
`src/Api/Features/Users/ListUsers/` and `tests/Api.Tests/ListUsersTests.cs` **after** §3 was written and
committed (`6821815`, this session). **Nothing here changed a single word of §3.** A divergence is a
report about the shipped system; promoting one into a criterion is the certification-by-construction
that `V-34-A` names.

**[unverified — needs a build] applies to every claim in this section that concerns a test outcome.**
I read the tests; I did not run them. No count below is mine.

### 4.1 The shipped surface, as read today

`src/Api/Features/Users/ListUsers/` holds three files — `Endpoint.cs`, `Handler.cs`, `Response.cs`. No
`Request.cs` and no `Validator.cs`, consistent with a parameterless read.

* `Endpoint.Route` is `"/api/users"`, mapped `MapGet` with `.RequirePermission(Permission.UserManage)`
  and no scope argument.
* `Handler.HandleAsync` takes `KaffDbContext` and a `CancellationToken`, orders by `FullName`, projects
  `UserSummary`, and returns `Results.Ok(new Response(users))`. No paging, no parameters.
* `Response.cs` declares `UserSummary` with nine members and `Response` with one.

### 4.2 The endpoint does something no criterion asks for

| # | Divergence | Reading |
|---|---|---|
| **D-1** | **`FullName` ordering, and nothing specifies an order.** `Handler` calls `.OrderBy(user => user.FullName)`. S-006 draws no ordering and no criterion states one. | **Benign, and one open consequence.** Ordering Arabic names depends on the database collation, and nothing in the story or the schema states which. Routed as **`N12`**, §5 — an Architect question, not a defect. |
| **D-2** | **`ActiveProjectNames` is on every row of the *list*.** `AC-U-L` derives the field from S-008 — but S-008 is the **detail** screen and says *"the same response that describes the user"*. There is no `GET /api/users/{userId}`, so the list row is doing a detail row's job. The `Handler` computes it as a **correlated subquery per user** (`ProjectAssignments.Where(UserId == user.Id && RevokedAt == null).Join(Projects…)`). | **Defensible today, and it should be recorded as a decision rather than left as a shape.** No visibility problem exists — the only reader is the Owner, who reaches every project anyway (D-010). The costs are a per-row subquery on an unpaged list, and a payload that grows with assignments. `N12`, §5. **Not a criterion**: `AC-U-L` states the requirement without naming the response that satisfies it, which is the honest position while no detail endpoint exists. |
| **D-3** | **No search parameter and no status filter**, while S-006 draws both a search box and three chips. | **Correctly reported rather than guessed, and the endpoint's own `<remarks>` says so** — that a query parameter with no criterion behind it would be a second implementation of `ListClients`' matching rules on the assumption users are searched like clients. Because `AC-U-J` requires the whole population in the payload, S-006's chips and search are satisfiable client-side at Kaff's scale. **This is a divergence from S-006, not from a criterion, and my derived criteria do not close it** — `N12` carries whether it must ever move server-side. |
| **D-4** | **No paging at all** — the whole `users` table, every request. | Fine at Kaff's size; nothing states a bound. `N12`. |
| **D-5** | **The endpoint's own `<remarks>` and the test's `<remarks>` cite `D-055 §3` for HR's grant. That citation is wrong.** `decisions.md` **D-055 §2** is Q42 / `UserRead`; **D-055 §3** is N10 / `ProjectCreate`. The intended reference is `decisions.md` **D-055 §2**, or `spec.md` §9's 2026-08-22 amendment **point 3**. | **A citation defect, LOW, and it has propagated.** See §7.2 — the substance of the reasoning is right in every instance; only the pointer is wrong. |

### 4.3 A derived criterion asks for something nothing asserts

| Criterion | Asserted today? | What is actually there |
|---|---|---|
| `AC-U-A` Owner reads all | **Yes** | `ListUsersTests.The_owner_reads_every_account_active_and_inactive_alike` — asserts four seeded ids are present. Its fixture Owner holds no assignment, so the global-reach half is exercised incidentally. |
| `AC-U-B` exhaustive refusal | **Yes — and this is a correction to the Verifier's report.** | `ListUsersTests.The_refused_list_is_every_role_that_can_sign_in_and_is_not_the_owner` asserts `RefusedActors()` is `BeEquivalentTo(Enum.GetValues<Role>().Except([Role.Owner, Role.Subcontractor]))`. **`V-34-E` has been repaired since the 2026-09-06 pass was written** — the test's own `<remarks>` says "Written 2026-09-06 to close `V-34-E`", and `git log` puts it in `003fb8f`, one commit after `116c08a`. The verification report describes the pre-`003fb8f` state and is dated history, exactly as `STATUS.md`'s map says. **[unverified — needs a build]** that it passes. |
| `AC-U-C` HR refused | **Yes** | HR is row 6 of `RefusedActors()`, and the loop also asserts the body does not contain the staffed engineer's username. |
| `AC-U-D` `UserManage`, company-wide, project id irrelevant | **At the mechanism, not at this route** | `PermissionCatalogue` declares `new(Permission.UserManage, PermissionScope.CompanyWide, [owner], …)`. `PermissionMechanismTests.Only_the_owner_administers_users` and `A_company_wide_permission_needs_no_assignment` prove it — **against `ProbeEndpoint.UserAdminRoute` (`/probe/users`), not `/api/users`**. The chain closes only through `EndpointPermissionCoverageTests.Every_mapped_endpoint_carries_a_permission_requirement`. **Not asserted anywhere:** that an *assigned* non-Owner is still refused on this route. Low risk, sound composition — but the criterion should say it leans on the shared gate, and none of the tests do. |
| `AC-U-E` whitelist | **Yes, and it is the best thing about the suite** | `ListUsersTests.The_user_row_carries_exactly_these_members_and_no_credential` — `BeEquivalentTo` over the nine names, plus `Response` pinned to `["Users"]`. This is D-106's and D-114 §1's lesson applied correctly to the only payload in the system projected straight off `User`. I confirm the Verifier's reading of it. |
| `AC-U-F` no money | **Structurally, via `AC-U-E`** | No money-shaped member can exist while the whitelist holds. No standalone money assertion, and none is needed while the whitelist is the gate. |
| `AC-U-G` no credential | **Yes, twice** | The whitelist, plus `No_password_hash_or_security_stamp_reaches_the_wire`, which reads the real hash and stamp out of the database and asserts the response body contains neither — and asserts the fixture stamp is non-empty first, so the test cannot pass vacuously. |
| `AC-U-H` dead session | **At the mechanism, not at this route** | `PermissionMechanismTests.A_deactivated_owner_cannot_administer_users`, `A_deactivated_user_loses_company_wide_permissions_too` and `Rotating_the_security_stamp_kills_every_existing_session` — again on `/probe/users` and the other probe routes. Sound by composition, unasserted here. |
| `AC-U-I` a read writes no audit | **⛔ No. Not anywhere.** | `AuditCoverageTests.Ten_reads_write_no_audit_record` is the criterion's home (`AC-118-H`) and its loop names **three** routes — `/api/auth/me`, `/api/clients?status=all`, `/api/audit`. **`/api/users` is not in it.** The test's own comment records the convention that a new read joins the loop when it appears (*"the audit read … joined the loop on 2026-09-05"*). This one did not. **The fix is one line in that loop** — Backend's or QA's, not mine. |

### 4.4 One more, found while checking `AC-U-I`

**`AC-118-H` enumerates routes by hand, which is `V-33-A`'s shape on a criterion rather than on a test.**
Its text reads *"Given `GET /api/auth/me` and the project-assignment read are each called ten times"*,
and the loop has since grown to three routes by hand. A hand-written list of routes stays the length it
was written at — which is precisely how `GET /api/users` came to be outside it on the day it shipped.
**Severity LOW, and it is KAFF-118's, not this endpoint's.** The durable form is a criterion over
*every mapped `MapGet`*, the shape `EndpointPermissionCoverageTests` already uses for the permission
gate. **Reported, not fixed, and not folded into my criteria** — rewriting another story's criterion is
not this task.

### 4.5 One dependency worth flagging, since nobody has

**A shipped E2E test already depends on this endpoint as a fixture.**
`tests/E2E.Tests/UserScreenTests.A_portal_client_is_refused_the_staff_host_indistinguishably_from_a_wrong_password`
issues `GET /api/users` as the Owner to confirm the portal account exists before asserting the refusal.
So the endpoint is load-bearing for a test of a *different* criterion (`AC-127-G`). Not a defect — worth
knowing before anyone changes its shape. **[unverified — needs a build and a browser.]**

---

## 5. Questions — drafted, not answered

**⛔ I answered none of these, and the count is deliberately small.** Two of the three the brief
anticipated turned out to be **derivable** from `ux/slice-1-flows.md` S-006 and S-008 (§3a) — which is
the better outcome, because a question asked of Karim that an approved artefact already answers spends
the one scarce thing in this project. What remains genuinely unanswered is **one** row for Karim and
**one** for Nabil and the Architect.

Numbering, from the register as it stands today: the highest `Q` in use is **`Q57`**, so the new
business question is **`Q58`**. The highest `N` is **`N11`**, so the non-business row is **`N12`**.

### For `stories/questions-for-karim.md` → *The open list* (Karim's)

Insert in the table, positioned by what it blocks — below `Q55` and above `Q45`, since like `Q55` it is
about whether a capability is wanted rather than how it behaves.

| # | Question, as Nabil should ask it | Blocks | Origin |
|---|---|---|---|
| **Q58** | **"Your own user list shows you everyone in Kaff — their name, their username, their role, their department, their phone and whether their account is switched on. HR has to put people onto projects, so HR needs to see a list of people too. We were told HR may see **names and roles and nothing else**. Is that still what you want — or now that you have seen the screen, do you want HR to see anything more, or anything less?"** Say why it is being asked: **HR's list does not exist.** The permission was created for it on 2026-08-22 and no screen and no endpoint was ever built, so today HR still cannot name a single person to staff a project — which is the whole reason the HR role exists. Before one is built, the field list should be confirmed rather than inherited from a ruling made before anybody had seen either screen. | **`S-010`'s user picker, and therefore HR's half of `KAFF-115`** (READY, 8 points). Blocks nothing that is built. | New, 2026-09-06. Raised by Verifier finding **`V-34-F`** and by `ux/slice-1-flows.md`'s own standing note under S-009: *"S-010's user picker needs a list of users, and HR holds no `UserManage`… HR cannot assign somebody it cannot name."* **The ruling exists (D-055 §2) and is not being reopened by an agent** — this asks Karim to confirm its field list now that the surface it withholds is drawn and built, which D-055 §2's own *"Revisit if"* clause invites: *"Revisit if HR needs to see anything about a user beyond name and role — which is a question for Karim."* |

### For `stories/questions-for-karim.md` → *Not for Karim — decisions Nabil and the Architect owe*

| # | Decision | Blocks | Where it came from |
|---|---|---|---|
| **N12** | **The user list's shape — four things nobody has decided, gathered into one row because they are one conversation.** ① **Which state chip S-006 selects by default.** S-006 draws `[ All ] [ Active ] [ Inactive ]` and does not say which is on; the payload carries every account either way (`AC-U-J`), so this is a screen decision and it belongs in KAFF-127, not in an endpoint criterion. ② **Ordering.** `Handler` orders by `FullName`; nothing specifies an order, and **ordering Arabic names depends on a database collation the schema does not state** — the Architect's, and cheap now. ③ **Paging.** There is none: the whole `users` table on every request. Fine at Kaff's size and nothing states a bound. ④ **Where the revocation names live.** S-008 requires them *"in the same response that describes the user"*; there is **no `GET /api/users/{userId}`**, so they are on every list row as a correlated subquery. Either that placement is ratified, or a detail endpoint owns it. **None of the four is a business rule and none should go to Karim.** | Nothing built. ② is the only one that is cheaper before real data exists than after. | New, 2026-09-06, raised by the BA closing `V-34-A`. ①③④ from `ux/slice-1-flows.md` S-006/S-008 read against `src/Api/Features/Users/ListUsers/Handler.cs`; ② from the `OrderBy` in that handler. |

**What I did not turn into a question, and why** — because a question nobody needs is its own kind of
noise:

* **Whether `Phone` belongs on the list.** The brief flagged it as probably-unanswerable. **S-006
  answers it**: the phone is drawn on the card in a `<bdi>`, it is a desktop column, and the search
  placeholder reads "Search name or phone". `AC-U-K` carries it with that citation.
* **Whether deactivated accounts appear by default.** **S-006 answers the payload half** — an
  `Inactive` chip keyed `users.state.inactive` and an `Inactive` filter cannot render from a list that
  omits inactive accounts — and `spec.md` §9's 2026-08-21 point 5 plus KAFF-112 make it necessary.
  `AC-U-J` carries it. Only *which chip is preselected* is open, and that is `N12` ①, not Karim's.
* **Whether `ActiveProjectNames` belongs on an administration list.** It is not a visibility question,
  because the only reader is the Owner, who reaches every project without an assignment row anyway
  (D-010). It is a placement question: `N12` ④.

---

## 6. Where the criteria should live — a recommendation, not a decision

**⛔ This is a board and scope act and it is Nabil's** (`agents.md` §3b — the Scrum Master returns to
Nabil for scope, and the BA certainly does). The criteria in §3 and §3a are written so they drop into
either option without a word changing except the identifier.

### Recommendation: **a new story, `KAFF-129`.** Three reasons, in the order that matters.

1. **The board carries one trailer per story, and this story needs two verdicts.** KAFF-127's trailer
   reads `state=VERIFIED verdict=CONDITIONAL`. The Verifier reasoned about it as two halves and reached
   two different verdicts — **CONDITIONAL** for the screens, **NOT VERIFIABLE** for the endpoint. One
   trailer cannot carry both, and `STATUS.md` is generated from the trailer. Adding twelve API criteria
   to KAFF-127 makes a single verdict cover a screen suite and an endpoint suite forever, and the next
   pass has to re-derive the split by hand every time.
2. **This is the shape the project has already chosen twice, deliberately.** `KAFF-101a` (sign-in API)
   and `KAFF-101b` (sign-in screen) are separate stories with separate trailers and separate states —
   `101a` VERIFIED PASS, `101b` BUILT. `KAFF-105a` and `KAFF-105b` split the same way. **The API/screen
   split is an established convention here, not an invention.**
3. **`process/agile.md` §2a rule 5 points this way, from the other side.** The rule is *"a UI criterion
   sitting on a delivered backend story is a defect in the board — move it to the Frontend story that
   will discharge it."* KAFF-127 exists **because** of that rule. Putting API criteria onto the story
   that rule created is the same mistake inverted: a backend criterion sitting on a Frontend story is
   discharged by a suite nobody looks at when the screens are reviewed.

**Proposed id: `KAFF-129`.** Slice 1's ids run `KAFF-100` … `KAFF-128` with no gap, `128` is the highest
in use, and slice 3 starts at `KAFF-300`, so `129` is free and in sequence. Suffixed forms exist as a
precedent (`101a`/`101b`, `105a`/`105b`) — **`KAFF-127a` would be the alternative** if Nabil prefers the
kinship visible in the id; it costs nothing either way and the criteria are identical.

**Points: I propose 0.** The endpoint is built. Estimating work already done inflates the slice, and
`STATUS.md`'s point table exists to be read by Nabil. `KAFF-122` already sits at 0 points as
`SUPERSEDED`, so a 0 in that column is not novel. **This is a proposal, not a figure to quote.**

### The two substitutions

| If the criteria join… | Then the ids become | And |
|---|---|---|
| **`KAFF-129`** (recommended) | `AC-129-A` … `AC-129-I` for §3 in order, then `AC-129-J`, `AC-129-K`, `AC-129-L` for §3a | KAFF-127's trailer stays as it is; its *"Not in this story"* section gains one line naming KAFF-129 as the endpoint's home, per §2a rule 5's own "named, not implied" |
| **`KAFF-127`** | `AC-127-J` … `AC-127-U`, continuing after the existing `AC-127-I` | KAFF-127's business-rule table needs the read endpoint added to it — the story would then assert a rule it does not currently name, which is the gap `V-34-A` reports |

**Either way, three things must happen that are not mine.** The trailer edit and
`powershell -NoProfile -File tools/status.ps1` are the Scrum Master's (`STATUS.md` is generated; hand
edits are overwritten, and **there is no PowerShell in this container**). The QA cases are QA's
(`V-34-J`). And **whoever verifies these criteria must not be whoever wrote the endpoint** — `CLAUDE.md`:
*"if you wrote the code, you do not certify it."*

---

## 7. `V-34-F` — reported, with the fix named. **Not fixed.**

**Two separate things, and only one of them is what `V-34-F` reports.**

### 7.1 The false test name — `V-34-F` proper

I re-read the file rather than repeating the finding. `tests/Domain.Tests/PermissionEvaluatorTests.cs`
contains a test named **`Hr_may_read_the_user_list_and_still_reaches_nothing_financial`**.

**HR may not read the user list.** There is one user list — `GET /api/users` — and HR is refused it, by
design, correctly, and that refusal is now itself asserted by `ListUsersTests` (HR is row 6 of
`RefusedActors()`). **The test's name asserts a capability the shipped system refuses.**

* **The rule this breaks is already law here:** `decisions.md` **D-097 §2** / **SM-33**, the Test Naming
  Law — *"A name that is merely **narrow** stays. A name the change makes **false** is renamed in that
  same change, and its citations move with it in the same commit."* This name is not narrow. It is
  false.
* **The distinction D-097 §2 draws matters for the fix:** what HR actually holds is
  `Permission.UserRead`, a catalogue grant that no endpoint declares. A name about the **grant** would
  be true; a name about the **capability** is not.
* **The fix, named and not applied:** rename to a claim about the permission rather than the act —
  e.g. `Hr_holds_the_user_read_grant_and_still_reaches_nothing_financial` — and move its citations in
  the same commit. **Whose:** the rename is Backend's or the Architect's (`tests/Domain.Tests` is
  theirs); moving citations in `meetings/`, `qa/` and `proposals/` is explicitly the **Scrum Master's**
  under D-097 §2, *"the files the implementing agent may not edit"*.
* **Why I did not do it:** it is not this task, and `CLAUDE.md` plus the brief both forbid it. A BA
  closing `V-34-A` by editing a Domain test would be a second, quieter version of the boundary problem
  `V-34-A` is about.

**⛔ And the one thing not to do about it:** do **not** make the name true by granting HR this endpoint.
`AC-U-C` exists to hold that line, and its box in §3 states the defence. `V-34-F` itself says so —
*"The gating of `GET /api/users` itself is correct; do not open it to HR."* I confirm that, from the
sources rather than from the report: D-044 ruling 1, D-055 §2, `Q42`'s verbatim warning, and
`spec.md` §9's 2026-08-22 amendment point 3 all say the same thing, and the shipped `Endpoint.cs`
reasoned its way to the same place unprompted.

### 7.2 The under-delivery underneath it, which outlives the rename

**`Permission.UserRead` reaches nothing.** No endpoint declares it — I re-enumerated the
`RequirePermission` calls under `src/Api/Features` and the declared set is `AuditRead`, `ClientManage`,
`ProjectAssignmentManage`, `UserManage`, and nothing else. So:

* The operational problem D-055 §2 was written to solve — *"HR held `ProjectAssignmentManage` and could
  not name a single person to put on a project"* — **is unsolved as slice 1 closes.**
* `ux/slice-1-flows.md` carries the standing note under S-009: *"S-010's user picker needs a list of
  users, and HR holds no `UserManage`… HR cannot assign somebody it cannot name."* So this is not only
  a permission with no endpoint; it is a **screen that cannot be built**, and it sits under `KAFF-115`,
  which `STATUS.md` shows as READY at 8 points.
* **Renaming the test does not close this.** The rename makes the suite honest; HR still cannot staff a
  project. That is why **`Q58`** exists in §5 — and why it is a question rather than a story I wrote:
  D-055 §2's own *"Revisit if"* clause makes the field list Karim's the moment somebody needs to build
  the surface.

### 7.3 A third thing, found while checking the second — the citation, and it has spread

**Every artefact that reasons about HR and this endpoint cites `D-055 §3`. The correct citation is
`D-055 §2`.** `decisions.md` D-055 §2 is *"Q42 · `UserRead` — HR may see who exists, and nothing more"*;
D-055 §3 is *"N10 approved · `ProjectCreate` splits from `ProjectManage`"*. The confusion is
understandable — `spec.md` §9's 2026-08-22 amendment numbers its **points** differently from
`decisions.md` D-055's **sections**, and `UserRead` is point **3** in `spec.md` and section **2** in
`decisions.md`.

Sites, from a repository-wide search today (49 occurrences of `D-055 §3`; the great majority are correct
references to N10 and must be left alone):

| File | Note |
|---|---|
| `src/Api/Features/Users/ListUsers/Endpoint.cs` | in the `<remarks>` — **correctable** |
| `tests/Api.Tests/ListUsersTests.cs` | twice — a `<remarks>` and an assertion message — **correctable** |
| `qa/slice-1/verification-2026-09-06.md` · `meetings/BRIEF-2026-09-06-verifier.md` | dated history; **the Scrum Master moves these**, D-097 §2 |
| `decisions.md` (the entry recording the endpoint) | **append-only and never rewritten** — it can only be superseded by a note in a later entry |

**Severity LOW and it is a pointer, not a claim** — the *reasoning* at every one of those sites is
correct and matches D-055 §2's actual text. **Reported, not fixed:** `src/` is out of bounds for this
session by the brief, and `decisions.md` is append-only. **The whole of the fix is: read `§2` for `§3`
wherever the subject is HR.**

---

## 8. Corrections to the Scrum Master's brief

The brief invited this in its last line. Four items, in order of how much they matter.

1. **⛔ `D-055 §3` is the wrong citation, and the brief passed the error on.** The brief instructed me to
   read *"`D-055 §3`"* for HR's *"names and roles only"* grant. That is `decisions.md` **D-055 §2**
   (Q42 / `UserRead`); **D-055 §3** is N10 / `ProjectCreate` and has nothing to do with HR. The brief
   inherited this from `Endpoint.cs` and `ListUsersTests.cs`, which is itself the finding — see §7.3.
   `spec.md` §9's 2026-08-22 amendment **point 3** is the other correct pointer, and the two numbering
   systems are almost certainly the origin of the mistake.
2. **`V-34-E` is already repaired, and the brief presents it as live.** The brief describes
   `ListUsersTests`' refused-role array as *"a hand-written list with no exhaustiveness assertion"* and
   as `V-34-A`'s companion defect. The file today contains
   `The_refused_list_is_every_role_that_can_sign_in_and_is_not_the_owner`, asserting `RefusedActors()`
   equals `Enum.GetValues<Role>().Except([Role.Owner, Role.Subcontractor])`. Its `<remarks>` say
   "Written 2026-09-06 to close `V-34-E`", and `git log` puts it in `003fb8f`, the commit after
   `116c08a`. **This is exactly the failure mode both `CLAUDE.md` and the brief's own evidence rule
   warn about** — repeating a finding out of `qa/`, which `STATUS.md`'s map calls dated history, without
   re-reading the file it names. **[unverified — needs a build]** that it passes; I only read it.
3. **The brief's premise — "can these be derived at all?" — holds, and its list of the underivable was
   pessimistic.** It named `Phone`, `ActiveProjectNames` and the deactivated-by-default question as
   likely refusals. **`ux/slice-1-flows.md` S-006 and S-008 answer the first two and the payload half of
   the third** (§3a). They are cited in KAFF-127's own header and were approved by Nabil under
   `agents.md` §3, so they are legitimate pre-existing sources. **One genuine business question
   remains** (`Q58`), and it is not one the brief predicted: not what the *Owner's* list carries, but
   that **HR's list — ruled into existence on 2026-08-22 — was never built at all.** I record my own
   process error plainly: I should have read the UX flows before writing §3, and §3a discloses that I
   did not.
4. **The model choice was right, and not for the reason given.** The brief overrode `agents.md` §M's
   *mid* for "BA writing stories from an answered ruling" on the grounds that refusal was the likely
   outcome. **Refusal turned out to be the *smallest* part of this task** — one question for Karim and
   one for Nabil. What actually needed the stronger model was: noticing that a pre-existing UX artefact
   already answered questions I was told to escalate; distinguishing a wrong `§`-pointer from a wrong
   claim across 49 citation sites; and catching that a finding the brief stated as current had been
   fixed a commit later. **The right general rule is narrower than §M's clause and worth recording: the
   never-downgrade case is a task whose deliverable is a *boundary* — between what a source licenses
   and what shipped code does — and not merely one where a refusal is likely.** Refusing is easy; the
   hard part is knowing which things are not refusals.

**One thing in the brief I want to endorse rather than correct.** Its instruction to write §3 before
opening the code, and to keep the two apart in the output, is what makes this document worth anything.
I committed §1–§3 as `6821815` before reading a line of `ListUsers`, so the ordering is a fact about the
history rather than a claim in my prose. That was the brief's idea, not mine.

**And one ambiguity in `spec.md`, as the brief asked for.** `CLAUDE.md` states flatly that *"every
endpoint checks two things: role and assignment"*, while `spec.md` §9's assignment rule is written
about **projects** — *"a user MUST be assigned to a project to open it or act on it"* — and D-044 ruling
1 makes `UserManage` `CompanyWide`. A user list is not a project and cannot name one, so the assignment
axis is discharged **by the permission's scope** rather than checked per request; `PermissionEvaluator`
returns `ProjectNotSpecified` for a `ProjectScoped` row with no project, which is why the scope choice
*is* the check. `AC-U-D` pins the scope for that reason. **The two documents do not conflict in
substance, but `CLAUDE.md`'s phrasing is stricter than the rule it summarises**, and a future session
reading only `CLAUDE.md` would call this endpoint defective. Worth one clarifying clause in `CLAUDE.md`
— **the Architect's, not mine.**

---

## 9. What I did **not** do

**Read this section as carefully as §3. Everything in it is something a later session might otherwise
assume exists.**

**Not run — because it is impossible in this container, not because it was skipped:**

* `dotnet build`, `dotnet test`, `dotnet format` — **no .NET SDK**, and the network policy returns 403
  for `builds.dotnet.microsoft.com`.
* `/run-kaff-erp` — needs the SDK and a Docker daemon; neither exists here.
* `tools/status.ps1` — **no PowerShell.** `STATUS.md`'s generated block is therefore untouched and
  still shows `HEAD e862f29` and a dirty tree.
* `ci/check-citations.ps1` — same reason. **The citations I added in this file are unchecked by the
  tool**, though every one names a file and an identifier that I opened today.
* **No test count, build result, or gate figure in this document is mine.** Where I describe a test I
  describe the source I read. `[unverified — needs a build]` marks every claim that would need a
  compiler.

**Not changed, deliberately:**

* **`src/` — untouched.** Including the `D-055 §3` citation in `Endpoint.cs`, which I found and reported
  (§7.3) and did not correct.
* **No story trailer, and no `STATUS.md` edit.** The trailer is the board's fact and the Scrum Master
  moves it; `STATUS.md`'s block is generated.
* **`tests/Domain.Tests/PermissionEvaluatorTests.cs` — the false test name of `V-34-F` is NOT renamed.**
  §7.1 names the fix and its owner.
* **`AC-118-H` — not rewritten**, though §4.4 reports its hand-enumerated route list as the weakness
  that let this endpoint fall outside it.
* **`/api/users` was not added to `AuditCoverageTests.Ten_reads_write_no_audit_record`'s loop**, which
  is the one-line fix for the only derived criterion nothing asserts at all (§4.3, `AC-U-I`).
* **`stories/questions-for-karim.md` — not edited.** `Q58` and `N12` are **drafted here in the
  register's own row format** and must be pasted into it by whoever owns that file. Until then they are
  a proposal, not open questions.
* **No business question answered.** Not `Q58`, not `N12`, not by consensus, not to unblock anything.

**Not done because it is somebody else's:**

* **QA cases.** `V-34-J` — no frontend story has a `TC-` case. `agents.md` §175: QA writes the cases,
  the Verifier executes them. These criteria will need them and I wrote none.
* **The E2E case for the endpoint.** Not written, and §4.5 notes an existing E2E test already leans on
  the endpoint as a fixture.
* **`decisions.md` — no entry appended.** Nothing structural was decided here; everything is a proposal
  or a finding. If Nabil takes §6's recommendation, **that** is the decision and it wants an entry.
* **`V-34-E`, `V-34-J`, `F-1`, `N11`, `KAFF-300`, `V-31-A`/`V-33-F`** — every other open item in
  `STATUS.md`. Untouched. `V-34-E` I re-read and report as **already repaired** (§8 item 2), which is a
  reading, not a verification.

**And the constraint that governs everything above:** **this session cannot push.** `git push` and the
GitHub API both return 403. This file is committed locally in a container that is reclaimed when the
session ends, so **the file is the deliverable only if somebody carries it out** — which is why the
criteria are repeated verbatim in the closing message rather than summarised.

---

## 5. Questions — drafted, not answered

*(filled in below)*

---

## 6. Where the criteria should live — a recommendation, not a decision

*(filled in below)*

---

## 7. `V-34-F` — a false test name. Reported, not fixed

*(filled in below)*

---

## 8. Corrections to the Scrum Master's brief

*(filled in below)*

---

## 9. What I did not do

*(filled in below)*

# V-34-A — the missing acceptance criteria for `GET /api/users`

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
| 3 | **The derived acceptance criteria** | ✅ |
| 4 | **Divergences — findings, NOT criteria** | ✅ |
| 5 | Questions drafted, not answered | ✅ |
| 6 | Where the criteria should live — a recommendation, not a decision | ✅ |
| 7 | `V-34-F` — the false test name. Reported, not fixed | ✅ |
| 8 | Corrections to the Scrum Master's brief | ✅ |
| 9 | What I did not do | ✅ |

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

> **The member set itself is deliberately left blank here and is not mine to fill.** Two of the fields
> the shipped endpoint returns cannot be derived from `spec.md` or any ruling — see `Q58` in §5.
> **This criterion is complete and buildable as a mechanism**, and its content lands the moment `Q58` is
> answered. Writing a member list from the shipped response would be the exact substitution `V-34-A`
> reports.

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

| Not written | Because |
|---|---|
| A default filter on deactivated accounts | **Not derivable.** `Q59`, §5. KAFF-124's archived-clients equivalent is derived from `spec.md` §2 and §3 *for clients*; §9 contains no such sentence for users. |
| The exact projected member set | **Not derivable.** `Q58`, §5. `AC-U-E` carries the mechanism; the list is Karim's or Nabil's. |
| Paging, ordering, a search term | **Not derivable, and not Karim's.** `N12`, §5 — routed to Nabil and the Architect, since it is a shape decision rather than a business rule. |
| An RTL/mobile-width criterion | It would be a **screen** criterion, and `AC-127-A` already holds the user list's rendering. `process/agile.md` §2a rule 5 — a UI criterion belongs to the story that discharges it, moved and not copied. |
| An i18n/catalogue criterion | Same reason: `AC-127-H` holds it. A JSON list carries no user-facing string of its own; if a refusal key is added it belongs to `AC-127-H`'s sweep. |
| An E2E criterion | `AC-127-I` holds the screen's. An API-only E2E case for a read the screen already exercises would be `qa/`'s `TC-` work (`V-34-J`), not a tenth criterion. |

---

## 4. Divergences — these are FINDINGS, not criteria

**Read after §3, and never merged into it.** Everything below came from opening
`src/Api/Features/Users/ListUsers/` and `tests/Api.Tests/ListUsersTests.cs` **after** §3 was written and
saved. **Nothing here changed a single word of §3.** A divergence is a report about the shipped system;
promoting one into a criterion is the certification-by-construction that `V-34-A` names.

*(filled in below)*

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

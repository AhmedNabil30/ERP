# KAFF-106 · The Owner creates a user with a role and a department

**Slice:** 1 · **Epic:** Foundation · **Points:** 5 · **Status:** BUILT — V-A cleared 2026-08-25, awaiting an independent Verifier pass
**Spec:** §9, §2 · **Decisions:** D-044 (rulings 1, 2), D-035, **D-049 (rulings 3, 4)**
**Depends on:** KAFF-100 *(soft — the Api harness issues identities directly, so this endpoint can be
built and tested before the bootstrap shape is decided; only the demo waits on it)*

## Story
As the Owner, I create a user, choose their role and department, and nobody else in Kaff can do it —
because whoever sets a user's department can hand out project-assignment power, which makes this the
most privileged operation in the system.

That sentence is the kickoff's finding, not a flourish: *"because grants can be written against a
department, whoever can set a user's department can grant project-assignment power. That permission
must be designed deliberately, not added by whoever writes the screen"*
(`meetings/2026-08-18-slice-1-kickoff.md` §2.1). Karim ruled on it on 2026-08-20.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` is `CompanyWide` and granted to `Role.Owner` alone — *"strictly Global and held exclusively by the Owner"* | D-044 ruling 1 |
| 2 | HR holds `ProjectAssignmentManage`, **not** `UserManage`. HR staffs projects with users that already exist; it does not mint logins or hand out roles | D-044 ruling 1 |
| 3 | Nine roles: Owner · Finance · TechnicalOffice · SiteEngineer · HeadOfDesign · MarketingSales · Client · Subcontractor · **Hr** | §9 · D-044 ruling 2 |
| 4 | Four departments: Finance · HR · Marketing · Operations. Only Operations subdivides — Technical, Financial, Administrative | §9 |
| 5 | A user in Operations must carry a sub-department; a user in any other department must not [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.OperationsRequiresSubDepartment` inside `ValidateDepartment`, reached from `Create` and from `MoveToDepartment`] | §9 · slice 0 `User.ValidateDepartment` |
| 6 | `Role.Client` and `Role.Subcontractor` cannot hold a department at all — **a grant that names a department and no role is satisfied by *any* role carrying that department**, which is the mechanism behind D-035, D-044 ruling 2 and F-04, so an outsider with a department would inherit company-wide permissions that skip both the project check and the client check [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.ExternalRoleCannotHoldDepartment`, and the matcher that makes it so at @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Matches`] | §12 · D-035 |
| 7 | A `Role.Client` user must name the client they belong to; no other role may [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.ClientUserRequiresClient`] | §12 · slice 0 `User.Create` |
| 8 | **The Owner sets a temporary password at creation.** The user MUST change it on first sign-in and can reach nothing else until they do. **The method is `User.SetTemporaryPassword`, which sets `MustChangePassword`; `SetOwnPassword` is the other one and is wrong here. `SetPasswordHash` no longer exists** [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetTemporaryPassword`, `MustChangePassword`, `SetOwnPassword`] | D-049 ruling 4 · KAFF-103 |
| 9 | The temporary password obeys the same rule as any other: at least 8 characters, no forced complexity | D-049 ruling 3 |
| 10 | Until a password exists the account cannot sign in — `PasswordHash` null. **Note `User.Create` sets `IsActive` true**, so "cannot sign in" is the absence of a credential, not an inactive flag [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Create`, `PasswordHash`] | slice 0 `User` · KAFF-101a |
| 11 | Username is unique, case-insensitive — `Create` lower-cases and trims it before storing; phone is stored entered-and-normalised [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `Create`]. ⚠️ **UNCITED — WAIVED, Q51. See "Readiness waiver" below** | slice 0 `User`, `PhoneNumber` |
| 12 | **Unresolved:** whether two users may share a phone number. `Client`, `Worker` and `Employee` are deduplicated by phone (§2); `User` is not in that list, and `PhoneNumber`'s own doc comment notices the gap | **Q36** — does not block; see below |
| 13 | **`Role.Hr` must carry `Department.Hr` and no other, and the create path refuses otherwise.** This is the same bare-department mechanism as rule 6, from the other direction: a grant written against a department with **no role named** matches any role carrying that department, so an HR user parked in Operations / Administrative would inherit whatever that department holds. `PhotoPublish` is a bare-department grant today [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PhotoPublish` — granted to `operationsAdmin` with no role named; it is **Q52**, open], and `SiteExpenseConfirm` was one until D-052 §1 named its roles [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. **The rule is written against the mechanism, not against today's row list** — that is D-044 ruling 2's point — spec.md §9 carries it as an amendment, *"HR is strictly administrative and has zero financial visibility"* — and the guard has to hold for grants not yet written. The catalogue cannot close it; `Create` is where it is refused [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.HrRoleRequiresHrDepartment` inside `ValidateDepartment`, reached from `Create`; the matcher that makes a bare-department grant match any role is @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Matches`] | §9 · D-044 ruling 2 · D-035 |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage`, `CompanyWide`, Owner only. No project, no assignment.
- **Money:** moves no money. It decides who may *later* move money, which is why the audit record
  below is not optional.
- **Audit:** `Created` on `User`, actor = the Owner, before null, after the full record with
  `PasswordHash` and `SecurityStamp` redacted. **The role and department must appear in the after
  state** — the record has to answer "who gave this person the treasury". The temporary password is
  a password like any other: it is redacted, never stored in the record, and never logged
  (D-049 ruling 4 exists to make the trail trustworthy, not to put a credential in it).
- **i18n:** `users.create.title`, `users.field.full_name`, `users.field.username`,
  `users.field.phone`, `users.field.role`, `users.field.department`, `users.field.sub_department`,
  `users.field.temporary_password`, `users.hint.hr_department_fixed`, `action.create` — and
  `enum.Role.*` / `enum.Department.*` / `enum.OperationsSubDepartment.*` for every enum member.
  **`enum.<Type>.<Member>` is the shape for a server enum rendered as text**
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `enum.<Type>.<Member>`; the screens use it at
  @ `ux/slice-1-flows.md` -> `enum.Role.SiteEngineer`], and the field and action keys are S-007's
  [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `users.hint.hr_department_fixed`]. *(This bullet said `users.role.*` and
  `users.department.*` until 2026-08-22 — corrected under **SM-15**, finding **V-07** / **N-05**, and
  noted rather than changed silently.)* Plus the existing `errors.identity.*` keys, and now
  **`errors.identity.hr_role_requires_hr_department`** — rule 13 and `AC-106-K`. That key is already
  in both catalogues [Verified: 2026-08-22 @ `src/Web/public/locales/ar.json` ->
  `errors.identity.hr_role_requires_hr_department`, and @ `src/Web/public/locales/en.json` ->
  `errors.identity.hr_role_requires_hr_department`], so nothing is added — it is *asserted*. Any new
  refusal needs a key in **both** `ar.json` and `en.json`.

## Acceptance criteria
**AC-106-A — the Owner creates a Finance user**
Given I am signed in as the Owner
When I create a user with role Finance and department Finance and a temporary password
Then the user exists, is active, and can sign in **only** to change that password (AC-103-B)
And an audit record of `Created` names me as the actor and carries the role and department, with the password redacted

**AC-106-B — nobody else can, whatever their role** *(fails if the rule is broken)*
Given I am signed in as Finance, then Technical Office, then Site Engineer, then Marketing, then HR, then a portal Client
When each attempts to create a user
Then every attempt is refused with 403 and `errors.auth.forbidden`, and every refusal is logged

**AC-106-C — HR cannot mint a login** *(fails if the rule is broken)*
Given I am signed in as `Role.Hr`, which holds `ProjectAssignmentManage`
When I attempt to create a user
Then the request is refused with 403
And HR's ability to assign existing users to projects is unaffected

**AC-106-D — an Operations user must carry a sub-department**
Given I am the Owner
When I create a user in Operations with no sub-department
Then it is refused with `errors.identity.operations_requires_sub_department`

**AC-106-E — a portal client cannot be given a department** *(fails if the rule is broken)*
Given I am the Owner
When I create a `Role.Client` user and give them `Department.Hr`
Then it is refused with `errors.identity.external_role_cannot_hold_department`
And the user is not created

**AC-106-F — a client user must name a client**
Given I am the Owner
When I create a `Role.Client` user with no client id
Then it is refused with `errors.identity.client_user_requires_client`
And when I give a client id to a Finance user, that is refused with `errors.identity.non_client_user_cannot_carry_client`

**AC-106-G — usernames do not collide**
Given a user `nabil` exists
When I create `NABIL`
Then it is refused, and the existing user is untouched

**AC-106-H — the temporary password is not a permanent one** *(fails if the rule is broken)*
Given the Owner creates a user with a temporary password
When that user signs in and calls any endpoint other than the change-password endpoint
Then it is refused with `errors.auth.password_change_required`

**AC-106-I — eight characters is enough for the temporary one too**
Given the Owner creates a user with an 8-character all-lower-case temporary password
When the request is submitted
Then it is accepted — no complexity rule refuses it (D-049 ruling 3)

**AC-106-J — Arabic, RTL, at mobile width**
Given the user form at 390px in Arabic
When it renders
Then direction is RTL, every label resolves from the catalogue, and there is no horizontal overflow

**AC-106-K — an HR user cannot be created outside the HR department** *(fails if the rule is broken)*
Given I am signed in as the Owner
When I create a `Role.Hr` user in `Department.Finance`, then in `Department.Marketing`, then in Operations / Administrative, then with no department at all
Then all four are refused with `errors.identity.hr_role_requires_hr_department`, and no user is created
And a `Role.Hr` user in `Department.Hr` is created normally

*Appended 2026-08-22 under **SM-21**, which made the KAFF-107 fold conditional on KAFF-106 and
KAFF-108 each carrying the constraint — KAFF-108 held up its half in `AC-108-D` and this half had been
lost. It is the create-path half of KAFF-107's `AC-107-B`. KAFF-107 was folded out of the sprint by
`meetings/2026-08-21-sprint-1-refinement.md` §A4 — its own file still reads `Ready`, which is finding
**V-14** and is the Scrum Master's to close, not this criterion's. **The refusal itself already exists in the domain and is pinned there**
[Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ValidateDepartment`; the domain assertion is
@ `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
`An_hr_user_cannot_be_placed_in_another_department`] — **what this criterion adds is the endpoint-level
refusal**, which is the thing the fold was made conditional on and the level at which the domain guard
can be bypassed by a handler that never calls `Create`.*

## Not in this story
**The HR-role/HR-department constraint is now IN this story** — rule 13 and `AC-106-K`, on the create
path. *(This paragraph said it "has its own story because it closes a specific hole (KAFF-107)" until
2026-08-22. That was false and it was a coverage hole, not a cross-reference: KAFF-107 was folded into
KAFF-106 and KAFF-108 on 2026-08-21 and is not in the build order, so the sentence sent Backend to a
story nobody is building. Finding **V-05**; the fold's condition is **SM-21**. Corrected, and noted
rather than fixed silently.)* The **move**-path half of the same constraint is KAFF-108, `AC-108-D`.

The rest is genuinely elsewhere. Moving a user between departments (KAFF-108). Changing an existing
user's role (KAFF-109, now `Ready` — and note that a role change **revokes every project assignment
the user holds**, D-051 Q27, reversing D-049 ruling 6). Deactivating (KAFF-110). Assigning to a
project (KAFF-113). Changing the temporary password (KAFF-103).

*(The three story references in this paragraph were wrong until 2026-08-21 — they named KAFF-108,
109 and 112 for deactivation and assignment. Corrected, and noted rather than fixed silently.)*

## Readiness waiver — signed, and it does not answer the question
`process/agile.md`'s Definition of Ready says an uncited rule is a question, not a story. **Rule 11
is uncited and is built anyway, under a named waiver** (`decisions.md` D-055 §4, **superseded by D-062 §1 — see below**):

> *"I accept the six stories containing uncited rules to pass them through the Definition of Ready so
> the sprint does not stall. I take this on my own responsibility as the Architect."*

> **✅ COUNTERSIGNED FOR SEVEN — Nabil, 2026-08-22. `decisions.md` D-062 §1.**
>
> *"Signed and approved. I officially approve adding KAFF-106 as the seventh story under the waiver.
> The numerical discrepancy flagged by the Scrum Master is accurate, and the story is essential to
> complete the creation flow for the first user (Owner). This closes the discrepancy and allows the
> build to proceed."*
>
> **Both halves changed.** The count is **seven**, not six — KAFF-100, 101a, 103, 106, 110, 112, 114.
> The signatory is **Nabil**, not the Architect. An Architect's waiver is one agent accepting risk on
> rules Karim has not answered; Nabil is the decision owner's proxy, which is the right weight for
> seven committed stories to be built on.
>
> **It still answers nothing.** Q45–Q51 stay open. The waiver permits the build; the questions remain
> Karim's.
> — **the Architect, signed, 2026-08-22**

| Waived rule | Open question |
|---|---|
| **Rule 11** — usernames collide case-insensitively, so `NABIL` cannot be taken while `nabil` exists. Sourced to the slice-0 index and to **nothing Karim said**. One of four refusals of the same shape asked as one question | **Q51**, open, for Karim |

**The waiver lets the story be built. It does not answer Q51**, which stays open in
`stories/questions-for-karim.md` alongside its three siblings (KAFF-110 rule 5, KAFF-112 rule 7,
KAFF-114 rule 4). It is built that way today and it is probably right — and *probably right* is a
rule nobody gave.

*(Note the waiver's count: `decisions.md` D-055 §4 says **six** stories carry an uncited rule.
Working it out from the files gives **seven**, and this is the seventh — every other story carries a
question numbered to itself, while KAFF-106 appears only as Q51's fourth sibling. Raised with the
Scrum Master rather than resolved here.)*

> **✅ CLOSED — Nabil, 2026-08-22, `decisions.md` D-062 §1.** *"The numerical discrepancy flagged by the
> Scrum Master is accurate ... This closes the discrepancy and allows the build to proceed."* **The
> waiver is now for seven, countersigned by Nabil.** This note is kept rather than deleted because a
> story that flagged its own uncited state, against itself, is the behaviour SM-29 exists to produce —
> and deleting the flag would erase the evidence that it worked.

## Questions for Karim
- **Q36** — can two people who use the system share a phone number? *(Merged from `ux/questions.md`
  Q-UX-7.)* **Does not block this story.** `User` carries no uniqueness rule on the phone today, and
  building none is building what exists; a `no` answer would add a duplicate interaction like the
  client one, which is a change rather than a gap.
- **Q51** — rule 11 makes username uniqueness case-insensitive, sourced to the slice-0 index and to
  nothing Karim said. Asked as one question with three siblings of the same shape (KAFF-110 rule 5,
  KAFF-112 rule 7, KAFF-114 rule 4). **Does not block** — it is built that way and probably right.
  **Waived through the Definition of Ready — see "Readiness waiver" below.**

**Q42 is CLOSED — D-055 §2** *(this paragraph said HR had no way to see a list of users; that was
true when written and is now false)*. HR holds **`UserRead`**, `CompanyWide`, granted to `Role.Hr`
and `Role.Owner` [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`;
the enum member is @ `src/Domain/Authorization/Permission.cs` -> `UserRead`]. **The ruling is names and roles
only** — no editing, and no visibility into salary if one is ever added. This story is the Owner's
create path and is unaffected either way, but two things must not be lost when the read endpoint is
built elsewhere:

- **The permission is not the whole control — the endpoint's projection is.** A `UserRead` endpoint
  returning the full user row satisfies the permission and **breaks the ruling**: the row carries
  usernames, departments and active state as well as name and role. `questions-for-karim.md` -> `Q42`
  warned in terms not to close Q42 *"by handing HR the Owner's user list"*. Nothing in the catalogue
  can stop that; whoever builds it projects **name and role, and stops**.
- **The old trap still holds:** `EmployeeManage` looks like the answer and is not — `User` and
  `Employee` are different entities and the Employee register is slice 2.

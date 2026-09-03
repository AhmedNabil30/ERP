# KAFF-107 · An HR user cannot be created or moved outside the HR department

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** FOLDED — not in sprint 1
**Spec:** §9, §8 · **Decisions:** D-044 (ruling 2), D-035
**Depends on:** KAFF-106

> **⚠️ FOLDED OUT OF SPRINT 1, 2026-08-21** — `meetings/2026-08-21-sprint-1-refinement.md` §A4.
> Its constraint is carried by **KAFF-106 `AC-106-K`** (create path) and **KAFF-108 `AC-108-D`** (move
> path), which is what refinement action **SM-21** made the fold conditional on. **This file read
> `Ready` until 2026-08-22** — finding **V-14**, raised by the BA against its own work and closed by
> the Scrum Master, who owns sprint status. A folded story left `Ready` is a story somebody builds.

## Story
As Kaff, I need the HR role pinned to the HR department, because a permission granted to a
*department* matches any role carrying it — so an HR user parked in Operations / Administrative
would be confirming site expenses, which is exactly the financial visibility Karim's ruling denies
them.

This is one hole closed from two directions, and both directions are needed. The catalogue alone
cannot do it: a grant written against a department with **no role named** is satisfied by any role
carrying that department.

**One of the two examples this story used has since been fixed.** `SiteExpenseConfirm` now names
`Role.Finance` and `Role.TechnicalOffice` explicitly
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm` — **D-052 §1**,
finding F-04] — and a test, `No_financial_permission_is_granted_to_a_bare_department`
[Verified: 2026-08-22 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`], holds the class across
the money-touching permissions. **There is now a third layer the story predates:** the evaluator
itself discards any role-less grant on a `TouchesMoney` permission, at the point of decision rather
than only in a test [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `TouchesMoney`
— D-053 §2]. **`PhotoPublish` is still a bare-department grant**
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PhotoPublish`] and is deliberately
left, because the Architect's ruling is scoped to *financial* permissions and a photo moves no money;
it is **Q52**, open. So the mechanism this story guards against is real, currently reachable through
exactly one permission, and the guard here is what covers it from the other side. See D-044 ruling 2,
D-052 §1, D-053 §2.

> **Every line number in the paragraph above was wrong until 2026-08-22, and one claim with it.** It
> cited `PermissionCatalogue.cs` at lines 238-248 and 258. The rows are `Permission.SiteExpenseConfirm`
> and `Permission.PhotoPublish`, and under SM-31 they are cited by those names and not by position. The file
> moved when three permission rows were added on 2026-08-22 (D-055 §§1–3). Corrected rather than
> deleted, because the pattern is the point: **a story's `file:line` is a claim about the code and
> goes stale the moment the file does** — SM-29.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | HR is *"strictly administrative"* with *"zero financial visibility — cannot see project costs, margins, or the safe"* | D-044 ruling 2 |
| 2 | `Role.Hr` must carry `Department.Hr` and no other. Creation and department moves both refuse otherwise [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.HrRoleRequiresHrDepartment` inside `ValidateDepartment`, reached from `Create` and from `MoveToDepartment`] | D-044 ruling 2 · slice 0 `User.ValidateDepartment` |
| 3 | **HR holds exactly THREE permissions: `EmployeeManage`, `ProjectAssignmentManage` and `UserRead`** — and none of the three touches money [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`, `Permission.ProjectAssignmentManage`, `Permission.EmployeeManage`, and the count is pinned by `Hr_holds_no_permission_that_touches_money` @ `tests/Domain.Tests/CatalogueCompletenessTests.cs` -> `Hr_holds_no_permission_that_touches_money`] | D-044 ruling 2 · **D-055 §2** · `PermissionCatalogue` |
| 4 | HR is absent from `ProjectRead`, from every treasury permission, and from every gate [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectRead` — `ProjectRead`'s grants are owner, finance, technicalOffice, marketing, SiteEngineer, HeadOfDesign; HR appears on no treasury or gate row] | D-044 ruling 2 |
| 5 | A grant written against a department alone matches any role carrying that department — this is the mechanism the rule exists to contain, and it has leaked once already. The matcher skips the role comparison when a grant's `Role` is null, which is exactly why [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Matches`] | D-035 · D-044 ruling 2 |

> **Rule 3 said "exactly two" and that became false on 2026-08-22.** `UserRead` was granted to HR by
> D-055 §2, answering Q42 — HR held `ProjectAssignmentManage` and could not name a single person to
> put on a project. **It does not weaken this story:** `UserRead` is `CompanyWide`, so it gives HR
> nothing on a project, it carries `TouchesMoney: false`, and the test that pins HR's set was renamed
> and **gained the money assertion its old name had only claimed** (D-056 §2). The number moved; rule
> 4 did not.
>
> **But the ruling is names and roles only, and the permission is not the whole control — the
> endpoint's projection is.** A `UserRead` endpoint returning the full user row satisfies the
> permission and breaks the ruling. Whoever builds it projects name and role and stops. There is no
> such endpoint in slice 1.

## Permissions, money, audit, i18n
- **Permissions:** `UserManage` (Owner only) is what reaches the code paths this story constrains.
- **Money:** moves no money — and the whole story is about making sure HR never sees any.
- **Audit:** covered by KAFF-106 and KAFF-108; this story adds no new record, it adds a refusal.
- **i18n:** `errors.identity.hr_role_requires_hr_department` — **present in both catalogues**
  [Verified: 2026-08-22 @ `src/Web/public/locales/en.json` -> `errors.identity.hr_role_requires_hr_department` and @
  `src/Web/public/locales/ar.json` -> `errors.identity.hr_role_requires_hr_department`]. Nothing to add.

  *This story said the key was **"missing from `ar.json` and `en.json` today"** and that adding both
  was part of it. **That was false, and it was the story's only remaining deliverable.** D-047 added
  it and made the catalogue structural — `tests/Domain.Tests/TranslationCatalogueTests.cs` fails if a
  domain error has no entry in either file, so the gap this story described cannot reopen without a
  red test. Corrected by refinement action **SM-11**.*

## Acceptance criteria
**AC-107-A — HR in HR is fine**
Given I am the Owner
When I create a user with `Role.Hr` and `Department.Hr`
Then the user is created

**AC-107-B — HR anywhere else is refused** *(fails if the rule is broken)*
Given I am the Owner
When I create a `Role.Hr` user in Finance, then in Marketing, then in Operations / Administrative, then with no department
Then all four are refused with `errors.identity.hr_role_requires_hr_department`

**AC-107-C — an existing HR user cannot be moved out** *(fails if the rule is broken)*
Given an HR user in the HR department
When I move them to Operations / Administrative
Then the move is refused, and the user's department is unchanged

**AC-107-D — HR reaches nothing financial** *(fails if the rule is broken)*
Given I am signed in as `Role.Hr`
When I call, in turn, an endpoint requiring `ProjectRead`, `SiteExpenseConfirm`, `TreasuryPostProject`, `FinancialMovementApprove`, `AccountManage` and `PhotoPublish`
Then every one is refused with 403

**AC-107-E — a Marketing user moved to HR gains nothing** *(fails if the rule is broken)*
Given a `Role.MarketingSales` user
When the Owner moves them to `Department.Hr`
Then they do **not** hold `EmployeeManage` — the grant is by role, not department

## Not in this story
Whether HR should be able to read the audit trail — **answered: no.** Karim ruled the trail is the
Owner's alone, company-wide, and *"completely hidden from all other roles, even for their own
projects"* (D-049 ruling 1). That is KAFF-117, and it now reinforces rule 4 rather than qualifying
it. Employee master data itself, which HR owns (§2, §10): slice 2.

**What HR may see of a *project* is answered — Q32, D-051.** HR gets a new narrow `ProjectTeamRead`
and a separate "Project Team" screen carrying a project's name and its assigned people, and
**zero financial detail** (KAFF-105b, KAFF-115 — **both deferred out of sprint 1**; the permission
still has no member in `Permission.cs`
[Verified: 2026-08-22 @ `src/Domain/Authorization/Permission.cs` — no `ProjectTeamRead` member, and
no such row in `PermissionCatalogue.cs`]). Whether the project's **code** may appear there is
**Q43**, open — this file previously asserted that it may, which D-051 does not say. It does not
affect this story, and it confirms what rule 4 already assumed: HR's visibility is delivered by a
permission, never by moving HR into another department.

**What HR may see of a *user* is also answered now — Q42, D-055 §2**, and it went the other way from
`ProjectTeamRead`: the permission **exists** and is `UserRead`, `CompanyWide`, Owner and HR
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`]. Same principle as
rule 4 — a permission, not a department move.

## What is left in this story — BA recommendation, for the Scrum Master to price

**Not re-pointed here.** `KAFF-107` is still **2 points, `Ready`** in this file; the recommendation
below is the BA's and the estimate is not the BA's to change (action **SM-11**).

**Everything this story described as work already exists** [all rows re-verified 2026-08-22 — **every
line number in this table was wrong before that**, see the note below]:

| What the story called a deliverable | Where it already is |
|---|---|
| The two i18n entries | [Verified: 2026-08-22 @ `src/Web/public/locales/en.json` -> `errors.identity.hr_role_requires_hr_department`, @ `src/Web/public/locales/ar.json` -> `errors.identity.hr_role_requires_hr_department`] — and `TranslationCatalogueTests` makes the gap unreopenable |
| `IdentityErrors.HrRoleRequiresHrDepartment` | [Verified: 2026-08-22 @ `src/Domain/Identity/IdentityErrors.cs`] |
| The refusal itself, on **both** paths | [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.HrRoleRequiresHrDepartment` inside `ValidateDepartment`, reached from `Create` and from `MoveToDepartment`] |
| AC-107-D — HR reaches nothing financial | [Verified: 2026-08-22 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `Hr_reaches_a_project_without_an_assignment_but_sees_nothing_financial`, `Hr_reaches_a_project_without_an_assignment_but_sees_nothing_financial`, at the evaluator] |
| AC-107-E — a Marketing user moved to HR gains nothing | [Verified: 2026-08-22 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `Hr_owns_employee_records_through_its_own_role`, `Hr_owns_employee_records_through_its_own_role` — grants are matched by role] |

> **Corrected 2026-08-22 under SM-29.** This table cited `User.cs` at lines 232-235, 119 and 197, and
> two test ranges at `:339-347` and `:304-311`. **All five were wrong.** `User.cs` grew the lockout
> and forced-password-change members (migration `20260821221842_UserLockoutAndForcedPasswordChange`),
> which moved every line below them; and `:339-347` in the test file is
> `A_site_engineer_cannot_approve_anything_financial`, **a different test about a different role** —
> the citation named a real test that does not assert what the row claimed it did. That is the SM-30
> failure mode reached through an SM-29 defect: a citation nobody checks decays into a claim nobody
> can check.

**What actually remains is API-level refusal tests on two endpoints, and both endpoints belong to
other committed stories:** AC-107-B is the create path (**KAFF-106**) and AC-107-C is the move path
(**KAFF-108**). AC-107-A is KAFF-106's happy path already.

**Recommendation: fold it — AC-107-B into KAFF-106, AC-107-C into KAFF-108 — and return the 2 points to the
sprint.** Two reasons beyond the arithmetic. First, a story whose criteria are all executed through
another story's endpoint is a *test plan*, not a slice, and it will be "done" only when someone else's
work is done. Second, and this is the one that decides it: **the refusal has to be asserted at the
endpoint that can produce it**, and putting the assertion in a third story is how it gets skipped when
that story is deferred — which is finding **F-21** and precisely what SM-10 spent this refinement
undoing elsewhere.

**Against folding, honestly:** this file is the only place that states *why* the rule exists as prose.
That reasoning now sits in a comment [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `IdentityErrors.HrRoleRequiresHrDepartment`]
and in D-044 ruling 2 and D-052 §1, so the
loss is small — but if it folds, KAFF-106 and KAFF-108 should each carry one line naming the
bare-department mechanism, or the next session reads the refusal as arbitrary.

## Questions for Karim
None.

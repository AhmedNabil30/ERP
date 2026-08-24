# KAFF-108 · Move a user between departments

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** BUILT — awaiting verification (D-067 gate defect fixed)
**Spec:** §9, §8 · **Decisions:** D-044 (ruling 2), D-035
**Depends on:** KAFF-106

## Story
As the Owner, I move someone from one department to another when their job changes, so that their
permissions follow the job rather than staying with whoever set up the account.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `UserManage` covers setting a user's department, and it is the Owner's alone | D-044 ruling 1 |
| 2 | The move re-applies every department rule: Operations needs a sub-department, nothing else may carry one. `MoveToDepartment` calls the same `ValidateDepartment` `Create` does, so there is one rule and not two [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `MoveToDepartment`, calling `ValidateDepartment`] | §9 · slice 0 `User.MoveToDepartment` |
| 3 | `Role.Hr` cannot leave `Department.Hr` | D-044 ruling 2 · KAFF-107 |
| 4 | `Role.Client` and `Role.Subcontractor` cannot be given a department by a move any more than by creation | §12 · D-035 |
| 5 | A department move changes permissions immediately, on the next request — not at token expiry | §9 (*"Enforcement is server-side"*) · `meetings/2026-08-18-slice-1-kickoff.md` §3, where the access policy was made to re-read the user on every request |
| 6 | A department move does not touch project assignments. `ProjectAssignment` constrains the **role**, not the department, and `MoveToDepartment` writes only `Department` and `OperationsSubDepartment` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `MoveToDepartment`; @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`] | slice 0 `ProjectAssignment.Create` |
| 7 | The department is one of the two axes a permission can be granted against, so this endpoint can grant capability without touching the role. It is `UserManage`-privileged for that reason | §9 · D-035 |

## Permissions, money, audit, i18n
- **Permissions:** `UserManage`, `CompanyWide`, Owner only.
- **Money:** moves no money. It can move somebody *into* Operations / Administrative, which is **half**
  of what `SiteExpenseConfirm` requires — the other half is `Role.TechnicalOffice`
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`, since D-052 §1;
  and the evaluator now discards any role-less grant on a `TouchesMoney` permission outright,
  `src/Domain/Authorization/PermissionEvaluator.cs` -> `TouchesMoney`, D-053 §2]. So a move changes who touches
  money only for a user who already holds the right role, and the audit record is the control. (This
  bullet said the sub-department *"holds `SiteExpenseConfirm`"* until 2026-08-22, which was true when
  written and became false with the F-04 fix.)
- **Audit:** `Modified` on `User`, actor = the Owner, before and after both carrying department and
  sub-department, `ChangedProperties` naming them.
- **i18n:** reuses `enum.Department.*`, `users.field.sub_department`,
  `errors.identity.operations_requires_sub_department`,
  `errors.identity.sub_department_only_for_operations`,
  `errors.identity.external_role_cannot_hold_department`, and
  `errors.identity.hr_role_requires_hr_department` — **which already exists in both catalogues**
  [Verified: 2026-08-22 @ `src/Web/public/locales/ar.json` ->
  `errors.identity.hr_role_requires_hr_department`, and @ `src/Web/public/locales/en.json` ->
  `errors.identity.hr_role_requires_hr_department`]. Nothing is added; it is reused.

  *(Two corrections, 2026-08-22. This bullet called that key **"the key KAFF-107 adds"**: KAFF-107 adds
  nothing — it was folded into KAFF-106 and KAFF-108 on 2026-08-21 and is not in the build order — and
  the key was already in both catalogues, put there by **D-047** (its closing subsection, *"And one defect this session introduced and closed"*), which also made the gap unreopenable — `TranslationCatalogueTests` fails if a domain error has no entry in either file. Finding **V-05**. And `users.department.*`
  became `enum.Department.*`: a server enum rendered as text is `enum.<Type>.<Member>`
  [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `enum.<Type>.<Member>`], finding **V-07** under
  **SM-15**. Noted rather than changed silently.)*

## Acceptance criteria
**AC-108-A — a move takes effect on the next request** *(fails if the rule is broken)*
Given a **`Role.TechnicalOffice`** user in Operations / Technical, holding a token issued before the move
When the Owner moves them to Operations / Administrative
And they call an endpoint requiring `SiteExpenseConfirm` with that same token
Then the request succeeds — permissions came from the database, not from the token

> **Corrected 2026-08-22 — the role is load-bearing and was missing.** This criterion named no role
> until now, so it asserted that *any* user moved into Operations / Administrative gains
> `SiteExpenseConfirm`. That was true when it was written and is **exactly the F-04 leak** closed by
> D-052 §1 and D-053 §2: the grant now names `Role.Finance` **or** `Role.TechnicalOffice` + that
> sub-department [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`],
> and the evaluator refuses any role-less grant on a money-touching permission
> [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `TouchesMoney`]. Built as
> written, this criterion would have reinstated the hole. The point it is really testing — that a
> department change takes effect on the next request rather than at token expiry (D-048) — is
> unchanged and still worth testing; only the vehicle needed a role.

**AC-108-B — and the reverse takes effect just as fast** *(fails if the rule is broken)*
Given a **`Role.TechnicalOffice`** user in Operations / Administrative holding `SiteExpenseConfirm`
When the Owner moves them to Marketing
Then their next request to that endpoint is refused with 403, with the same token

**~~AC-108-B2~~ — RETIRED 2026-08-22** · reissued as **AC-108-G** · *a suffixed insertion between `B`
and `C`, contrary to the AC-ID scheme it was created under (`stories/README.md` rule 2:
"a new criterion is appended and takes the next unused letter — never a letter inserted into the
sequence"). Nothing cited it. **Retired, not recycled.***

**AC-108-G — the department alone is never enough on money** *(fails if the rule is broken)*
Given a **`Role.SiteEngineer`** in Operations / Technical
When the Owner moves them to Operations / Administrative
Then they still **cannot** reach `SiteExpenseConfirm`
And spec.md §8 is the reason: site expenses are entered *"by Finance or Admin, **not the engineer**"*
— see D-052 §1, D-053 §2. This is the criterion AC-108-A used to contradict.

*It sits here, between `B` and `C`, on purpose. `stories/README.md` rule 3: the ID is an identity,
not a position — a story whose criteria run `A B G C D` is correct, not untidy. **Do not "tidy" it.***

The domain half is
already pinned by `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`
[Verified: 2026-08-22 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]; **what this criterion
adds is the same assertion at the endpoint, after a real move.**

**AC-108-C — the department rules are re-applied on a move**
Given a Finance user
When the Owner moves them to Operations with no sub-department
Then it is refused with `errors.identity.operations_requires_sub_department`
And when they are moved to Marketing with a sub-department, it is refused with `errors.identity.sub_department_only_for_operations`

**AC-108-D — HR stays in HR**
Given an HR user
When the Owner moves them to Finance
Then it is refused, and the department is unchanged

**AC-108-E — nobody but the Owner can move anyone**
Given I am HR, then Finance, then Technical Office
When each attempts a department move
Then each is refused with 403

**AC-108-F — assignments survive the move**
Given a Technical Office user assigned to two projects
When the Owner moves them between departments
Then both assignments are still active and unchanged

## Not in this story
Changing a user's **role** — that has a consequence departments do not, and it is KAFF-109, now
`Ready`. **Note the asymmetry, and that it is deliberate:** a department move leaves every assignment
alone (AC-108-F above), and a role change now revokes every one of them (**D-051 Q27**, reversing D-049
ruling 6). One screen, two very different acts — do not merge them. Renaming a user, changing their phone: routine edits, folded into KAFF-106's endpoint.

## Questions for Karim
None.

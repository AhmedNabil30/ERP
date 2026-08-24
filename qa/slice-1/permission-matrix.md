# Slice 1 — the permission matrix

**Nine roles × thirty-one permissions.** This is slice 1's gate: `agents.md` sets it at *permission
tests pass*, so this file must be complete and it must be right.

**Expected values are derived from `spec.md` and Karim's rulings in `decisions.md` — not from
`PermissionCatalogue.cs`.** The catalogue was read afterwards, to compare. Where the two disagree, the
cell is marked **⚠ F-nn** and the finding is at the bottom of this file and in `qa/questions.md`.
A cell justified by "that is what the catalogue says" would certify the catalogue against itself.

Executed by `TC-1-202` … `TC-1-215`. Every case hits **endpoints directly**, never the UI.

**Revised 2026-08-21.** All 31 rows re-read against
`src/Domain/Authorization/PermissionCatalogue.cs` and re-derived from `spec.md`. **Every cell in
sections 1–5 agrees with the catalogue** except the ones already flagged below.

| What changed | Effect on this file |
|---|---|
| **`AuditRead` is ruled, not assumed** (D-049 §1) — company-wide, `Role.Owner` alone, *"completely hidden from all other roles, **even for their own projects**"* | **F-18 closed.** The ⚠ comes off the cell, the row leaves the catalogue's `Unresolved` set. `TC-1-207` expected **two** rows after this ruling and expects **one** after D-052 §2 — `PeriodClose` |
| **HR sees a project's name and team through a separate screen** (D-051 Q32) | **F-03 and F-13 closed** — and a **new gap opened: the permission that screen needs does not exist. F-24.** **Still true on 2026-08-22:** `ProjectTeamRead` is named in four story files and in **no file under `src/`** [Verified: 2026-08-22]. The *story* half closed (`KAFF-115` `AC-115-H`/`AC-115-I`); the *permission* half did not. Tracked as **F-30** |
| **The withholding rate moved to the contract and belongs to Finance** (D-049 §9, §10) | **New gap: no permission expresses it. F-25** |
| **The session moved into an `HttpOnly` cookie** (D-050) | §6 now speaks of a session rather than a token, and gains four rows |
| **A role change revokes every assignment** (D-051 Q27, reversing D-049 §6) | New §6 row: a project-scoped permission is refused on the **next** request after a role change |
| **The global kill is `SecurityStamp` rotation** (D-051 N5) | ~~Declared and not implemented~~ **BUILT 2026-08-22, D-053 §1 — F-26 closed.** The stored stamp is compared on every authorized request and a mismatch or an absence is refused [Verified: 2026-08-22 @ `PermissionSubjectReader.cs` -> `ReadAsync`] |
| **`SiteExpenseConfirm` now names its roles** (D-052 §1) — `Role.Finance`, and `Role.TechnicalOffice` **conditional on** Operations / Administrative | **F-04 closed, and it was a real defect, not a paper one.** The ⚠F-04 cells come off that row. `PhotoPublish` keeps them — it is the **last** bare-department grant and is deliberately left |
| **The Owner and the Technical Office may open a project** (D-052 §2, answering Q17) | `ProjectManage` is granted to somebody at last and leaves the catalogue's `Unresolved` set. ~~But the row is still `ProjectScoped`, so it cannot authorise a create~~ — **resolved by the row below** |
| **`ProjectCreate` splits from `ProjectManage`** (D-055 §3, closing F-27 and N10) | **A new CompanyWide row, Owner + Technical Office.** `ProjectManage` keeps its name, its grants and its `ProjectScoped` scope for **editing**, so §9's assignment requirement still applies to every edit. Company-wide is not a weakening: a create request cannot name the project it is about to create |
| **`ProjectFinancialsEdit` is a third row** (D-055 §1, closing Q-N10-2 and F-25's permission half) | **`ProjectScoped`, `TouchesMoney`, `Role.Finance` and `Role.Owner` alone.** Finance was deliberately *not* added to `ProjectManage` — an accountant must not alter the engineering scope of a project. **It raises Q-N10-2b rather than closing it:** Finance has no global reach, so Finance cannot set a new contract's withholding until somebody assigns Finance to that project |
| **`UserRead` — HR may see who exists** (D-055 §2, closing Q42) | **CompanyWide, `Role.Hr` and `Role.Owner`. Names and roles only.** HR held `ProjectAssignmentManage` and could not name a single person to put on a project. **The permission is not the whole control — the endpoint's projection is:** a screen returning usernames, departments and active state satisfies the permission and breaks the ruling |

**There is no live hole left in this file.** F-04 was fixed on 2026-08-21 (**D-052 §1**) and **QA-1
is answered by the ruling that fixed it**. ~~What remains is one **scope** defect~~ — **F-27 is closed
too, 2026-08-22, D-055 §3.**

**⚠️ What this file now owes, and it is QA's not the Architect's.** The three new rows shipped
**reachable and untested** (**D-056 §3**), and one existing test —
`Only_the_owner_and_the_technical_office_may_open_a_project` — was left asserting against
`ProjectManage`, which after the split is the permission that *cannot* open a project. It would have
stayed green forever while testing something its own name disclaimed. ~~Three tests now cover them:
`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
`Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`,
`Hr_may_read_the_user_list_and_still_reaches_nothing_financial`.~~ **That list was wrong — F-31,
2026-08-22.** The first of the three asserts `ProjectManage`, which is not one of the three new
rows. **The four tests actually in play, each verified by opening the method:**
`Only_the_owner_and_the_technical_office_may_open_a_project` covers **`ProjectCreate`** — repointed
from `ProjectManage` on 2026-08-22, which is the correction the paragraph above is about
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`];
`Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope` covers
**`ProjectFinancialsEdit`**
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`];
`Hr_may_read_the_user_list_and_still_reaches_nothing_financial` covers **`UserRead`**
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Hr_may_read_the_user_list_and_still_reaches_nothing_financial`];
and `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` covers **`ProjectManage`**, the
row that was **not** new
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`].
*(The "Domain 74/74 green" count carried here was not re-run in this pass and is not re-asserted.)*
**Their
endpoints are slice 4**, so every `TC-` case against these three rows is a **catalogue/evaluator** case
now and an **endpoint** case later. The matrix must not carry a cell reading *"Finance may set the
withholding"* as though a route existed.

---

## ✅ `spec.md` now describes this model — F-12 is closed

**Was:** §9 named eight roles and said nothing about `Role.Hr` or global reach, so a Verifier reading
only `spec.md` would have been right to fail this whole matrix. That was the highest-priority
documentation defect in the project, raised 2026-08-18 as BA action A1.

**Closed 2026-08-20, `decisions.md` D-047.** §9 carries a 📌 AMENDMENT block covering all seven of
Karim's role rulings, and further blocks sit in §2, §6.1, §6.4 and §13. §0 states the convention:
an amendment has the same force as the paragraph above it and wins where the two disagree. The
2026-08-21 rulings were added the same way, including the ⚠️ SUPERSEDED block where Q27 reversed
D-049 §6.

Every cell below still cites its D-number rather than only `spec.md`, which costs nothing and keeps
from `spec.md` cites its D-number explicitly.

---

## Legend

| Symbol | Meaning |
|---|---|
| **G** | Granted. Company-wide — no project, no assignment. |
| **A** | Granted **only with an active `ProjectAssignment`** on that project. Unassigned ⇒ refused. |
| **A≥J** | Granted with an assignment at `Junior` or above. |
| **A≥S** | Granted only at `Supervisor`. |
| **RG** | Granted by **global reach** — no assignment row needed, but the project must exist. |
| **RC** | Granted because the project's client is this user's client. Never an assignment. |
| **D** | Granted **only** if the user sits in Operations / Administrative. The grant names a department and **no role**, so *any* role carrying that department matches. ⚠ **`PhotoPublish` is the only row left with this shape** (D-052 §1). |
| **A·D** | Granted with an active assignment **and** only from Operations / Administrative. The grant names a **role and** a department, and every criterion on a grant must match. Introduced 2026-08-21 for `TechOffice × SiteExpenseConfirm` (D-052 §1). |
| **R** | Refused — the role holds no grant. |
| **X** | Refused **before** the catalogue is consulted. `Role.Subcontractor`, spec.md §9. |
| **—** | Nobody holds it. |

**Every `A`, `A≥J`, `A≥S`, `RG` and `RC` cell also means: without that access, refused.** The negative
half is the half that matters and is exercised by `TC-1-204`.

---

## 1 · Project access and identity

| Permission | Scope | Owner | Finance | TechOffice | SiteEng | HeadOfDesign | Marketing | Client | Subcon | Hr |
|---|---|---|---|---|---|---|---|---|---|---|
| `ProjectRead` | Project | **RG** | A | A | A | A ⚠F-05 | A ⚠F-14 | **R** | X | **R** |
| `ProjectManage` | Project | **RG** 🟡 | — | **A** 🟡 | — | — | — | — | X | — |
| `ProjectAssignmentManage` | Project | **RG** | R | R | R | R | R | R | X | **RG** |
| `UserManage` | Company | **G** | R | R | R | R | R | R | X | **R** |

**Citations**

| Cell | Expected because |
|---|---|
| `Owner × ProjectRead = RG` | D-010 — Karim, 2026-08-17: *"owner role is like the admin so yes global."* Reach only; capability still comes from the catalogue. Bounded by the project existing (`TC-1-212`). |
| `Client × ProjectRead = R` | **D-035.** A portal user holding the same read permission as internal staff reaches any internal endpoint requiring only `ProjectRead`. §12: the client must never see costs, margins, catalogue, subcontractors or internal notes. The portal goes through `PortalRead` and nothing else. |
| `Hr × ProjectRead = R` | D-044 ruling 2 — HR is *"strictly administrative"* with *"zero financial visibility."* **This is the sharpest cell in the matrix:** HR's reach waves it onto every project, so the absence of this one grant is the only thing between HR and every project's financial data (`TC-1-100`). |
| `ProjectManage = Owner + TechOffice` 🟡 | **D-052 §2, answering Q17 — the oldest open question in the catalogue (raised at slice 0, D-012).** Karim: opening a project *"triggers engineering items, accounting ledgers, and cost tracking. It is strictly a technical and administrative responsibility. Site Engineers and Marketing have no business creating projects."* The grant is `[owner, technicalOffice]` and the row is **no longer `Unresolved`** — `PeriodClose` is the last one. ~~**Read the 🟡 below before treating this cell as "they may create a project."**~~ **Superseded 2026-08-22, D-055 §3: `ProjectManage` no longer governs creating at all.** Opening a project is **`ProjectCreate`** — a separate CompanyWide row with the same two holders. `ProjectManage` governs **editing**, stays `ProjectScoped`, and §9's assignment requirement applies to it [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate` and @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`] |
| `ProjectCreate = Owner + TechOffice`, CompanyWide | **D-055 §3**, approved from `proposals/N10-project-creation.md` design A. Same holders as `ProjectManage`, different act. **CompanyWide is forced, not chosen:** a create request cannot name the project it is about to create, and a `ProjectScoped` row with no project returns `ProjectNotSpecified` [Verified: 2026-08-22 @ `PermissionEvaluator.cs` -> `ProjectNotSpecified`]. **Do not merge this back into `ProjectManage`** — that is the smaller diff and it removes the assignment requirement from every project edit. |
| `ProjectFinancialsEdit = Owner + Finance`, ProjectScoped, TouchesMoney | **D-055 §1**, closing Q-N10-2. Two of Karim's own rulings met: D-052 §2 gave `ProjectManage` to Owner + Technical Office, D-049 rulings 9–10 gave **Finance** the contract's withholding category. **The Finance department will never hold `ProjectManage`** — an accountant must not alter the engineering scope of a project. `TouchesMoney` because the rate *"directly dictates ledger entries and money reconciliation"*, which makes the written-out money list **twelve**. |
| `UserRead = Owner + Hr`, CompanyWide | **D-055 §2**, closing Q42, answered by Nabil. **Names and roles only.** HR held `ProjectAssignmentManage` and could not name one person to staff a project. **The permission is not the whole control — the endpoint's projection is.** Not `ProjectRead`, which D-044 ruling 2's *"zero financial visibility"* forbids and a test pins. |
| `Hr × ProjectAssignmentManage = RG` | D-044 ruling 3 — *"HR does not need to be assigned to a project first in order to staff it."* Requiring an assignment in order to create assignments is circular. The permission stays **project-scoped**, so the route must still name a real project. |
| `UserManage = Owner only` | D-044 ruling 1 — *"strictly Global and held exclusively by the Owner."* Deliberately **not** folded into `ProjectAssignmentManage`: HR staffing a project is a different act from HR minting a login and choosing its role, and the second would let HR grant itself what ruling 2 denies it. |

### 🟡 `ProjectManage` authorises *editing* a project and cannot authorise *opening* one

**Do not read the two `ProjectManage` cells as "the Owner and the Technical Office may create a
project."** They cannot, and the cell would be reporting a capability that does not work.

The row is `PermissionScope.ProjectScoped` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`], so the evaluator refuses
when the request names no project — and **a create request cannot name one, because the project does
not exist yet.** Karim's ruling (D-052 §2) is about **opening** a project; the permission as written
covers only the half he was not asked about.

**QA does not fix this and has not chosen between the two fixes.** D-052 names them and declines both
deliberately: making the row company-wide would also drop the assignment requirement from *editing*,
weakening §9, and splitting create from edit means two permissions and two cells here. *"That is an
architecture decision with a §9 consequence, not a drafting choice, so it is raised rather than
taken. Lands in slice 4 with KAFF-407."*

**What this meant for the cases, now done.** `TC-1-206` asserted *"nobody holds `ProjectManage`"* and
`TC-1-207` expected an `Unresolved` set of **two**; both would have failed against a correctly-applied
ruling. Both were rewritten on 2026-08-21 — `TC-1-206` against `[owner, technicalOffice]`, `TC-1-207`
against `{ PeriodClose }`, which is the only `Unresolved: true` row left [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PeriodClose`].
Neither now asserts the scope, because the scope is unsettled. **F-25's *"it would sit under
`ProjectManage`, granted to nobody"* is superseded by this cell** — the holder exists; what is missing
is a permission that can authorise a create.

---

## 2 · Master records — `spec.md` §2's ownership table

| Permission | Scope | Owner | Finance | TechOffice | SiteEng | HeadOfDesign | Marketing | Client | Subcon | Hr |
|---|---|---|---|---|---|---|---|---|---|---|
| `ClientManage` | Company | **G** | R | R | R | R | **G** | **R** | X | R |
| `CatalogueManage` | Company | G ⚠F-15 | R | **G** | R | R | R | R | X | R |
| `BabManage` | Company | G ⚠F-15 | R | **G** | R | R | R | R | X | R |
| `EmployeeManage` | Company | G ⚠F-15 | R | R | R | R | **R** | R | X | **G** |
| `SubcontractorManage` | Company | G ⚠F-15 | R | **G** | R | R | R | R | X | R |
| `SupplierManage` | Company | **G** | **G** | R | R | R | R | R | X | R |
| `OpportunityManage` | Company | G ⚠F-15 | R | R | R | R | **G** | R | X | R |

**Citations**

| Cell | Expected because |
|---|---|
| Department owners | §2's ownership table, verbatim: Client → Marketing; CatalogueItem, Bab, Subcontractor → Technical Office; Employee/Worker → HR; Supplier → Finance; Opportunity → Sales. |
| `Owner` on every row | D-044 ruling 4 — *"The Owner has Global Reach for all master data … without departmental restrictions."* Consistent with D-010. **⚠ F-15:** the ruling's *rule* line says "all master data"; its *action* line names three (Clients, Suppliers, Banks). The rule line is applied. If the list was literal, five Owner grants come back out — Q12 / Q-UX-14, D-045 #2. |
| `Marketing × EmployeeManage = R` | D-044 ruling 2. Until 2026-08-20 the grant was written against `Department.Hr`, which matches **any** role carrying it — so a Marketing user moved to HR held it. Karim created `Role.Hr` *"rather than dangerously piggybacking"*, and the grant moved to the role (`TC-1-064`). |
| `Client × ClientManage = R` | §12, absolutely. This permission reaches **every client Kaff has**. |

---

## 3 · Site execution — `spec.md` §7, §8, §9

| Permission | Scope | Owner | Finance | TechOffice | SiteEng | HeadOfDesign | Marketing | Client | Subcon | Hr |
|---|---|---|---|---|---|---|---|---|---|---|
| `ExtractPrepare` | Project | **R** | R | R | **A≥J** | R | R | R | X | R |
| `DailyLogWrite` | Project | **R** | R | R | **A≥J** | R | R | R | X | R |
| `DraftCreate` | Project | R | R | R | **A≥J** | R | R | R | X | R |
| `DraftSubmit` | Project | **R** | R | R | **A≥S** | R | R | R | X | R |
| `SiteExpenseDraft` | Project | R | R | R | **A≥J** | R | R | R | X | R |
| `SiteExpenseConfirm` | Project | **R** | **A** | **A·D** | **R** | **R** | **R** | R | X | **R** |
| `PhotoPublish` | Project | D 🟡 | D 🟡 | D 🟡 | D 🟡 | D 🟡 | D 🟡 | R | X | **R** |

**Citations**

| Cell | Expected because |
|---|---|
| `SiteEng × DraftCreate = A≥J`, `× DraftSubmit = A≥S` | §9: *"a junior engineer raises requests as drafts; the supervisor submits them."* Seniority is on the **assignment**, not the user (D-044 ruling 5), so the same person is `A≥S` on one project and only `A≥J` on another (`TC-1-102`, `TC-1-103`). |
| `Owner × DraftSubmit = R` | Global reach is not global capability. The Owner reaches the project and still holds no grant. `TC-1-203`. Note the Owner's reach is granted at `Supervisor` level deliberately (D-044 ruling 3), so if a levelled Owner grant is ever written it will silently succeed — a trade Karim's ruling records and accepts. |
| `Owner × ExtractPrepare = R` | §7: the site engineer prepares. §9: *"Nobody creates and approves the same movement."* The Owner approves every extract, so he must not prepare one. |
| `Finance × SiteExpenseConfirm = A` | §8: *"Site financial expenses are entered by **Finance or Admin**, not the engineer."* Granted by role, so it does not depend on where Finance sits. |
| `SiteEng × SiteExpenseConfirm = R` | §8, same sentence — **"not the engineer"**, named explicitly. **The catalogue now delivers this** (D-052 §1) [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]: the grant names `Role.Finance` and `Role.TechnicalOffice`, so a site engineer parked in Operations/Administrative matches neither. F-04 closed. |
| `TechOffice × SiteExpenseConfirm = A·D` | §8's *"Finance or **Admin**"*, read as the Operations / Administrative sub-department §9 gives the office. The grant carries a role **and** a department, and `PermissionEvaluator.Matches` requires every criterion set on a grant to match — so the Technical Office holds it from Administrative and **not** from Technical — `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` asserts both directions [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]. |
| `Owner × SiteExpenseConfirm = R` | Global reach is not global capability, and the Owner holds no grant on this row. It read `D ⚠F-04` while the grant was department-only; the Owner has no department at all (`TC-1-002`), so the old cell was describing a path that could not be walked. |
| `PhotoPublish` = **D 🟡** on every role that can hold a department | The 🟡 replaced ⚠F-04 on 2026-08-21: F-04 is closed and this row was **not** part of the fix. The grant is still `[operationsAdmin]` with no role [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`], so any role placed in Operations / Administrative holds it — including a site engineer moved there. §9 gives that sub-department *"reports, photos and tasks"* with no role named, so a department grant may well be the correct shape. **Nobody has ruled, and QA is not deciding.** See the section below. |
| `Hr × SiteExpenseConfirm = R`, `× PhotoPublish = R` | D-044 ruling 2. Held by a **second** mechanism, not by the catalogue: `User.Create` refuses an HR user in any department but HR, so HR can never sit in Operations/Administrative and can never match either department grant (`TC-1-061`, `TC-1-063`, `TC-1-066`). |
| `Client`, `Subcon` on every row | Neither may hold a department at all (`User.ValidateDepartment`, D-035), so neither can match a department grant. |

### ~~⚠ F-04~~ — fixed 2026-08-21, and `PhotoPublish` is the last department-only grant

**Was:** `SiteExpenseConfirm` and `PhotoPublish` were the only two permissions granted against a
**department with no role named**, so any role placed in Operations / Administrative held them — and
`User.Create` places a `Role.SiteEngineer` there without complaint, which is the one role §8 names:

> *"Site financial expenses are entered by Finance or Admin, **not the engineer**. An engineer's
> expense entry is a draft that Accounts confirms and posts."*

**Fixed by D-052 §1** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. The Architect's
ruling is the mechanism rather than the row: *"Financial permissions like `SiteExpenseConfirm` must
never be granted to a bare department without specifying a role."* `SiteExpenseConfirm` now grants to
`Role.Finance`, and to a second grant naming `Role.TechnicalOffice` **plus** Operations /
Administrative. Every criterion set on a grant must match, so the office holds it only from the
Administrative sub-department and a site engineer parked there holds nothing.

**Held by two tests, both green.** `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]
pins the four outcomes on this row, and `No_financial_permission_is_granted_to_a_bare_department`
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]
pins the **class** across **twelve** money-touching permissions (eleven until `ProjectFinancialsEdit`
joined the list on 2026-08-22 — **F-34**), so the shape cannot reappear on a different row. D-052 verified the fix
by removing the role line and watching both go red first.

**🟡 `PhotoPublish` is still a bare-department grant** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] and is
**deliberately** left: the ruling is scoped to *financial* permissions and publishing a photo moves
no money, so extending it there would be applying a rule nobody gave. Its row above therefore keeps
the **D** symbol. **It is the last one, and it needs its own ruling** — if §9's *"Operations /
Administrative owns reports, photos and tasks"* is not meant to include, say, a site engineer moved
into that sub-department, somebody has to say so. Not QA's to decide and not invented here.

**Executed by `TC-1-215`** — whose **Domain** half is now regression cover and green, and whose
**Api** half has no endpoint to call until slice 6, **KAFF-608**. The two are not the same state; do
not report the case as passing.

---

## 4 · Gates and treasury — `spec.md` §7, §9, §6

| Permission | Scope | Owner | Finance | TechOffice | SiteEng | HeadOfDesign | Marketing | Client | Subcon | Hr |
|---|---|---|---|---|---|---|---|---|---|---|
| `QuantityGateApprove` | Project | **R** | R | **A** | R | R | R | R | X | R |
| `FinancialMovementPrepare` | Project | **R** | **A** | R | R | R | R | R | X | R |
| `FinancialMovementDisburse` | Project | **R** | **A** | R | R | R | R | R | X | R |
| `FinancialMovementApprove` | Project | **RG** | **R** | **R** | **R** | R | R | R | X | **R** |
| `ChangeOrderApprove` | Project | **RG** | **R** | R | R | R | R | R | X | R |
| `FirmAdvanceApprove` | Project | **RG** | R | R | R | R | R | R | X | R |
| `TreasuryPostProject` | Project | **R** | **A** | R | R | R | R | R | X | **R** |
| `TreasuryPostCompany` | Company | **R** | **G** | R | R | R | R | R | X | R |
| `AccountManage` | Company | G ⚠F-16 | **G** | R | R | R | R | R | X | **R** |
| `PeriodClose` | Company | R ⚠F-17 | G ⚠F-17 | R | R | R | R | R | X | R |

**Citations — these are the separation-of-duties cells and `spec.md` §9 states each one**

| Cell | Expected because |
|---|---|
| `Owner × QuantityGateApprove = R` | §9: *"Technical Office gates quantities, never money."* The gate is the Technical Office's, and the Owner does not gate quantities either. `TC-1-203`. |
| `TechOffice × FinancialMovementApprove = R` | §9, same sentence — **never money**. |
| `Finance × ChangeOrderApprove = R` | §9: *"Finance prepares and disburses but **does not approve change orders**."* Finance is deliberately absent, and this cell is the whole reason `ChangeOrderApprove` is a separate permission. |
| `Finance × FinancialMovementApprove = R` | §7: *"Owner approval [EVERY extract, no threshold]."* §9: *"Owner approves all financial movements."* |
| `Owner × FinancialMovementPrepare / Disburse / TreasuryPost* = R` | §9: **"Nobody creates and approves the same movement."** If the Owner could post, he could approve his own posting. This is the cell that turns the rule into a mechanism. |
| `SiteEng × FinancialMovementApprove = R` | §9: *"Site engineers approve nothing financial."* Tested at `Supervisor` level, because a supervisor is the strongest engineer and must still be refused. |
| `Hr × everything in this table = R` | D-044 ruling 2 — *"cannot see project costs, margins, or the safe."* `TC-1-063`. |
| `Owner × FirmAdvanceApprove = RG` | §6.4.3: a firm advance needs owner approval **and a hard cap the system enforces**. The cap is slice 3's and does not exist yet — `qa/risk-register.md` RSK-16. |
| `Owner × AccountManage = G` | ⚠ **F-16.** Ruling 4 names *"Banks (BankManage)"* as master data the Owner may create and edit. **There is no Bank master record in `spec.md`** — a bank is an account of `AccountType.Bank` in the §6.3 tree — so it has been mapped onto `AccountManage`, which also opens **every other account type**. Q13 / Q-UX-14, D-045 #1. Safe meanwhile because `Account.Create` can only turn a floor **on**, never off, and guard 3c freezes configuration after creation. |
| `PeriodClose = Finance` | ⚠ **F-17.** §6.6 requires a month-end close and does **not** say who performs it, nor whether the Owner must approve it as a financial movement. Finance is an **assumption**, marked `Unresolved`. Q23. |

---

## 5 · Portal and oversight — `spec.md` §12

| Permission | Scope | Owner | Finance | TechOffice | SiteEng | HeadOfDesign | Marketing | Client | Subcon | Hr |
|---|---|---|---|---|---|---|---|---|---|---|
| `PortalRead` | Project | **R** | **R** | **R** | **R** | R | **R** | **RC** | X | **R** |
| `PortalApprove` | Project | **R** | R | R | R | R | R | **RC** | X | R |
| `AuditRead` | Company | **G** | **R** | **R** | **R** | **R** | **R** | **R** | X | **R** |

**Citations**

| Cell | Expected because |
|---|---|
| `Client × PortalRead / PortalApprove = RC` | §12: read and approve only. Access comes from `Project.ClientId` matching `User.ClientId`, **compared against the database, never against anything the request carried**, because §12 is absolute that a client must never see *"any other client's data"* (`TC-1-043`). Assignment does not apply to clients — a client is never assignable (`TC-1-106`). |
| Everybody else × `PortalRead` = R | The portal is a separate surface, not a softer view of the internal one. Slice 8's `KAFF-810` makes it `/api/portal/*` with unshared response types; keeping internal roles off it now is what makes that possible later. |
| `AuditRead = Owner` | **D-049 ruling 1 — a ruling now, not an assumption.** Karim, 2026-08-21: the trail is *"strictly limited to the Owner (Global) … completely hidden from all other roles, **even for their own projects**."* **What he rejected is the load-bearing half:** a project-scoped audit read for the people working on that project — because from slice 3 the trail records every movement of money, so scoping it by project would reopen the zero-financial-visibility rule from a direction nobody was watching. The uncomfortable shape was put to him in those words — *the only globally-reaching actor is also the only reader of the trail that watches him* — and he accepted it. `TC-1-142`, `TC-1-207`. |
| `TechOffice × AuditRead = R`, `SiteEng × AuditRead = R` | Same ruling, stated separately because this is the cell people assume is softer: **an assigned user cannot read their own project's trail, and cannot read their own actions.** There is no project-scoped view and no "my changes" view. `TC-1-142`. |
| **No Global Finance/Audit role exists** | D-049 ruling 1 anticipates one *"if added later"* and does not create one. Nobody at Kaff has asked for it, and a role that exists before anybody needs it is a member of the permission model that means nothing — it is on the backlog's do-not-add list. **A tenth column appearing in this matrix is a defect, not a feature.** |
| `Client × AuditRead = R` | §12 lists what the client sees; the trail is not on the list (`TC-1-136`). |

---

## 6 · Cells that are not about a role — the ones that catch the real bugs

These hold for **every** row above and are executed by `TC-1-204`, `TC-1-210` … `TC-1-215`.

| Rule | Expected | Source |
|---|---|---|
| The right role, no assignment, project-scoped permission | **Refused** — `NotAssignedToProject` | §9 *"Role alone is insufficient"* |
| An assignment to project A, request for project B | **Refused** | §9 |
| Global reach against a project id that names nothing (Owner, HR) | **Refused**, not a 500 | D-010, D-044 ruling 3 |
| A deactivated account, project-scoped endpoint | **Refused on the next request** | §9, kickoff §3 |
| A deactivated account, **company-wide** endpoint | **Refused on the next request** | §9 — was F-11, **FIXED D-048** |
| A token claiming a role the database disagrees with, project-scoped | **Refused** | kickoff §3 |
| A token claiming a role the database disagrees with, **company-wide** | **Refused** | §9 — was F-11, **FIXED D-048** |
| A token claiming a department the database disagrees with | **Refused** | §9 *"Enforcement is server-side"* — was F-10, **FIXED D-048** |
| `Role.Subcontractor`, any permission, even one wrongly granted | **Refused before the catalogue is read** | §9 *"record only, no login"* |
| Any permission with no catalogue row | **Refused** — deny by default | D-012 |
| Any catalogue row with an empty `SpecReference` | **Must not exist** | D-012 |
| A request with **no session cookie and no `Authorization` header** | **Refused** — never a 200 with a null user | D-050 · `TC-1-236` |
| A session cookie issued **without** `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/` and no `Domain` | **Must not happen** | D-050 · `TC-1-220`, `TC-1-221` |
| A session whose **security stamp claim** no longer matches the stored one | **Refused** — ⚠ **F-26**, declared and not implemented | D-049 §2 · D-051 N5 · `TC-1-225` |
| A session presented **after a role change**, either scope | **Refused** — the change also revoked every assignment | **D-051 Q27** · `TC-1-075`, `TC-1-080` |
| An `Role.Hr` user against **any** endpoint returning a money-shaped field | **Refused, or the field is absent** | D-044 ruling 2 · D-051 Q32 · `TC-1-245` |

---

## 7 · Findings — where the catalogue and `spec.md` disagree

Numbered to match `qa/questions.md`, which is the master list.

| # | Cell or rule | The disagreement | Severity |
|---|---|---|---|
| **F-04** | `SiteEngineer × SiteExpenseConfirm` | ~~§8 says site expenses are entered *"by Finance or Admin, **not the engineer**"*, and the grant named a department with no role, so a Site Engineer placed in Operations/Administrative held it.~~ **FIXED 2026-08-21, D-052 §1** — the grant now names `Role.Finance` and `Role.TechnicalOffice` + Operations/Administrative [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. **QA-1 is answered by the ruling.** | **CLOSED.** Regression cover: `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`] and `No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`], 70/70 Domain green. `TC-1-215`'s **Api** half is still unrunnable until slice 6 (KAFF-608). |
| ~~F-27~~ | `ProjectManage`'s **scope** | ~~D-052 §2 answered *who* and the row is still `ProjectScoped`, so a create request that cannot name a project is refused.~~ **CLOSED 2026-08-22, D-055 §3 — split, not widened.** `ProjectCreate` is CompanyWide (Owner + Technical Office); `ProjectManage` keeps its grants and its `ProjectScoped` scope for editing [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate` and @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]. **The rejected alternative is the substance:** widening `ProjectManage` fixes creation by removing the assignment requirement from editing. A later session that merges the two rows back reopens that §9 hole. | **CLOSED.** Cover: `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` — **watched to fail:** widening `ProjectManage` to `CompanyWide` turns it red and nothing else. `TC-1-206`, `TC-1-207` become **catalogue-level** cases now; endpoint cases land in slice 4. |
| **F-05** | `HeadOfDesign × ProjectRead` | `PermissionCatalogue`'s own XML doc says *"`Role.HeadOfDesign` holds nothing yet. spec.md §9 marks it phase 2."* The `ProjectRead` row grants it anyway. The data and its documentation contradict each other, and §9 gives no basis for either. | **P2 — question.** Decide whether phase 2 means "no grants" or "no features". |
| **F-10** | Every department-granted cell, and `KAFF-108` AC1/AC2 | ~~The department is read from the token claim and never revalidated.~~ **FIXED 2026-08-20, D-048.** `IPermissionSubjectReader` now reads role, department, sub-department, client scope and liveness from the users table on every authorized request. The token supplies only the user id. | **CLOSED.** Regression cover: `A_stale_department_claim_grants_nothing`. |
| **F-11** | Every `CompanyWide` cell | ~~`ProjectAccessPolicy` is only invoked for project-scoped permissions, so a deactivated Owner keeps `UserManage`.~~ **FIXED 2026-08-20, D-048** — your finding, and the most valuable one so far. | **CLOSED.** Regression cover: `A_deactivated_user_loses_company_wide_permissions_too`, `A_deactivated_owner_cannot_administer_users`. Verified by reverting the fix and watching five tests go red. |
| **F-12** | The whole matrix | ~~`spec.md` §9 names eight roles and describes neither `Role.Hr` nor any global reach.~~ **CLOSED 2026-08-20, D-047** — §9 carries an amendment block for every role ruling, and §0 gives amendments the same force as the text they annotate. | **CLOSED.** Was BA action A1 / N3. |
| **F-14** | `Marketing × ProjectRead` | Granted, cited only as "§9". §9 does not give Marketing project access, and §2 gives Marketing Client and Opportunity. Plausible — Marketing may need to see the job they sold — but uncited. | **P3 — question.** |
| **F-15** | `Owner ×` CatalogueManage, BabManage, EmployeeManage, SubcontractorManage, OpportunityManage | D-044 ruling 4's *rule* line says "all master data"; its *action* line names three. The rule line was applied. If the list was literal, five grants come out. | **P2 — open, D-045 #2, Q12.** Settle before slice 2. |
| **F-16** | `Owner × AccountManage` | "BankManage" has no Bank master record in `spec.md` and was mapped onto `AccountManage`, which opens every account type. | **P2 — open, D-045 #1, Q13.** Settle before slices 2 and 3. |
| **F-17** | `Finance × PeriodClose` | §6.6 does not say who closes the month, nor whether the Owner must approve it. Finance is an assumption. | **P2 — open, Q23.** Slice 7. |
| **F-18** | `Owner × AuditRead` | ~~`spec.md` does not say who reads the trail; Owner is an assumption.~~ **ANSWERED by Karim 2026-08-21, D-049 §1** — Owner only, company-wide, hidden from every other role *even on their own projects*. No longer `Unresolved` in the catalogue. | **CLOSED.** KAFF-117 is `Ready`. |
| **F-24** | HR's project team screen | **D-051 Q32 rules that HR may see a project's name and its assigned engineers, through a separate screen.** ~~and there is no story~~ — **the story half is CLOSED 2026-08-22: `KAFF-115` now carries HR's surface as `AC-115-H` / `AC-115-I` and names `ProjectTeamRead`.** **The permission half is still open and is now the sharper of the two:** `ProjectTeamRead` is named in **four story files and in no file under `src/`** [Verified: 2026-08-22 — `grep -rn ProjectTeamRead src/` returns nothing; present in `KAFF-105b`, `KAFF-107`, `KAFF-113`, `KAFF-115`]. **A permission that exists only in prose is the shape SM-30 was adopted for, seen from the other side** — SM-30 catches a row with no test; this is a name with no row. The risk is unchanged and is the shortcut: granting HR `ProjectRead` closes the gap in one line and hands HR the project surface D-044 ruling 2 was written to remove. | **P1 — ruled, story written, permission unbuilt.** Slice 2 with `KAFF-115`. `TC-1-243`…`TC-1-245`, which cannot pass until the row exists. Split out as **F-30** by QA. |
| **F-25** | The contract's withholding category | ~~No permission expresses that.~~ **The permission half is CLOSED 2026-08-22, D-055 §1: `ProjectFinancialsEdit`**, `ProjectScoped`, `TouchesMoney`, `Role.Finance` and `Role.Owner` alone [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`]. **F-25's other half is untouched and is the one that was always the risk:** `SetWithholding` trusts a `ClientKind` the caller supplies, and no Domain test can catch a lie — see `qa/questions.md` F-25. **And the ruling raised Q-N10-2b:** Finance has no global reach, so on a newly-opened project Finance cannot set the withholding until HR or the Owner assigns Finance to it. | **P2, half closed — slice 4.** Cover for the permission half: `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`. `TC-1-160`, `TC-1-242` — catalogue-level now, endpoint in slice 4. |
| **F-26** | Every session, every scope | **D-051 N5 makes `SecurityStamp` rotation the global kill** — and records that **nothing compares the token's stamp claim to the stored one.** `KaffClaimTypes.SecurityStamp` exists, `User.SecurityStamp` rotates on `SetPasswordHash` and `Deactivate`, and no code reads both. **Declared, not implemented**, and D-051 assigns it to **KAFF-101a**. Narrower than it looks: **D-048 already covers liveness, role and department** by re-reading the user row, and explicitly rejected a stamp as the fix for those. What is left uncovered is the case the row re-read cannot see — **a password change**, where the user is still active with the same role. Note the trap D-051 names: a check with a *"skip when the claim is absent"* fallback is **worse than no check**, because it looks implemented. `Reactivate` does not rotate at all. | ~~P1 — declared, not implemented.~~ **CLOSED — BUILT 2026-08-22, D-053 §1.** The comparison runs on every authorized request [Verified: 2026-08-22 @ `PermissionSubjectReader.cs` -> `ReadAsync`], and an **absent** claim is refused rather than skipped — which is the trap D-051 named. **`Reactivate` still does not rotate; that is KAFF-112 rule 9a, not this finding.** `TC-1-019`, `TC-1-097`, `TC-1-225`, `TC-1-230`, `TC-1-233`. RSK-06. |
| *(note, not a finding)* | ~~`ProjectManage = nobody`~~ | ~~`spec.md` §2 names a module that is not a role, so *nobody* was the correct deny-by-default outcome, recorded so it was not "fixed" by granting it.~~ **Superseded 2026-08-21: Karim named the holders (D-052 §2).** The deny-by-default was right to hold for as long as it did — it held from slice 0 until the question was actually answered, which is what it was for. What replaced it is **F-27**, a scope defect, not a holder gap. | **Superseded → F-27.** |

### Closed by the two rounds of rulings

| # | Was | Closed by |
|---|---|---|
| **F-03** | HR must pick a project and holds no permission that shows one | **D-051 Q32** — a separate team screen, not `ProjectRead`. Replaced by **F-24**, which is a missing permission rather than a missing answer. |
| **F-08** | Two error keys for one refusal | D-049 §9 and the story correction: `errors.master.individual_does_not_withhold`, one key, both catalogues. `TC-1-166`. |
| **F-13** | KAFF-105 AC3 gave HR a project list it may not see | **D-051 Q32** — HR's `/api/auth/me` names no project. `TC-1-040` reverses. |
| **F-20** | KAFF-118 AC1 required a role change KAFF-109 could not deliver | **D-051 Q27** unblocked KAFF-109. `TC-1-143`. |
| **F-22** | Where portal clients sign in was in one register only | **D-051 Q33** — a different URL, a *"completely isolated interface"*. The residue — separate deployment or second origin — is `TC-1-017`, still PENDING, and it decides whether the `__Host-` cookie can be shared at all. |

**F-05, F-09, F-14, F-15, F-16 and F-17 are untouched by both rounds and remain open.**

**F-12 is closed** — `spec.md` §9 was amended on 2026-08-20 (D-047), which is what stops a Verifier
reading only `spec.md` from correctly failing this whole model. It appears struck through in the table
above rather than deleted, because it was reported as open twice after it had been fixed.

**None of these is a live hole any more.** F-10 and F-11 were fixed on 2026-08-20 (D-048), F-18 was
answered on 2026-08-21 (D-049), and **F-04 — the last one — was fixed on 2026-08-21 (D-052 §1)**.
Their rows above record that rather than being deleted, so nobody re-reports them; F-04 in particular
had already been re-reported once as *"not reachable until slice 6"*, which D-052 corrects: that was
true of the **Api** half only, and the rule lives in the **Domain** half, which needed nothing but a
call to the evaluator. ***"No endpoint calls it"* is a statement about reach, not about whether a rule
is wrong** — worth keeping, because it is the reasoning error and not the row that generalises.

~~**What is open here is F-27**~~ — **closed 2026-08-22 (D-055 §3), along with F-26 (D-053 §1) and
F-25's permission half (D-055 §1).**

**What is open here now is a QA debt, not a ruling.** Three new catalogue rows —
`ProjectCreate`, `ProjectFinancialsEdit`, `UserRead` — exist and are covered by three Domain tests, and
**no `TC-` case in `qa/slice-1/test-cases.md` names any of them yet.** They must be added as
**catalogue and evaluator** cases in slice 1 and as **endpoint** cases in slice 4, and the two kinds
must be labelled, because a case that reads like an endpoint case and cannot run is how a suite grows
`PENDING` rows nobody can date. **F-05, F-07, F-09, F-14, F-15, F-16, F-17, F-23 and F-24 remain
open**, none of them blocking a committed sprint-1 story.

<!-- Superseded paragraph kept for the record: F-10 is a hole in the other -->
<!--
direction: a permission that should be granted after a department move is not. All three have cases
written against them and all three are expected to fail on first run, which is what makes them worth
writing.
-->

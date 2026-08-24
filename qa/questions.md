# qa/questions.md — findings, contradictions and questions

> ## ⚠️ Corrections applied 2026-08-21 — read before trusting a row below
>
> Parts of this file predate the fixes and rulings that closed them, and were re-reported as open
> after they had been resolved. **`slice-1/permission-matrix.md` and `slice-1/test-cases.md` are
> current; this file is not, except for the corrections here.**
>
> | Row below says | Actually |
> |---|---|
> | **F-10** department claim never revalidated | **CLOSED — D-048.** `IPermissionSubjectReader` re-reads role, department, sub-department, client scope and liveness from the users table on every authorized request. The token supplies only the user id. |
> | **F-11** company-wide permissions never revalidated | **CLOSED — D-048.** Verified by reverting the fix and watching five tests go red. |
> | **F-12** `spec.md` §9 never updated — "highest priority" | **CLOSED — D-047.** §9 carries an amendment block for every role ruling; §0 gives amendments the same force as the text they annotate. Further blocks in §2, §6.1, §6.4, §13. |
> | **F-18** `AuditRead` is an assumption | **CLOSED — D-049 §1.** Owner only, company-wide, hidden from every other role even on their own projects. No longer `Unresolved`. |
> | **KAFF-109 is BLOCKED** | **Ready.** D-051 Q27 reversed D-049 §6 — a role change now revokes every assignment rather than being refused. |
> | **KAFF-120 / 121 / 123 / 124 BLOCKED behind KAFF-119** | All **Ready**. D-049 rulings 7 and 8 unblocked KAFF-119. |
> | **F-04** a Site Engineer can confirm site expenses | **CLOSED — D-052 §1, 2026-08-21.** `SiteExpenseConfirm` now grants to `Role.Finance` and to `Role.TechnicalOffice` **+** Operations/Administrative [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. A `Role.SiteEngineer` parked there holds nothing. Held by `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`] and `No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]; 70/70 Domain green on a clean rebuild. |
> | **QA-1** is the question F-04's fix depends on | **ANSWERED by the ruling that fixed it.** The Architect: *"Financial permissions … must never be granted to a bare department without specifying a role."* Nothing on site confirms a site expense. |
> | **QA-4** must the first Owner change his own password | **ANSWERED — D-052 §3.** Nabil: **no.** `TC-1-006` and `KAFF-100` AC6 have a definite expected result. |
>
> **⚠️ F-04 was the fifth finding on this project to be re-reported after it had been closed** — the
> others are F-10, F-11 (D-048), F-12 (D-047) and F-18 (D-049 §1). **Do not re-report it.** The row
> below is struck through and kept for exactly that reason. It also carries a second correction worth
> keeping: this file once recorded F-04 as *"not reachable today (no endpoint requires it); live in
> slice 6."* **That was wrong about the half that mattered.** It was true of the **Api** half only —
> the **Domain** half needed nothing but a call to the evaluator, which is where the rule lives.
> D-052: ***"no endpoint calls it" is a statement about reach, not about whether a rule is wrong.***
>
> **~~What is genuinely open in this file is F-27~~ — CLOSED 2026-08-22 by D-055 §3.** `ProjectCreate`
> splits from `ProjectManage`: creating is a new **CompanyWide** row (Owner + Technical Office),
> editing stays **`ProjectScoped`** so §9's assignment requirement keeps applying
> [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate` and @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`].
>
> **⚠️ Read this next paragraph before writing a new finding, because it is the sixth instance of the
> same failure.** F-04 was re-reported four times. On 2026-08-22 the pattern repeated inside
> `decisions.md` itself: D-055 §7 stated four `User` fields did not exist, Backend built them within
> the hour, and the entry read as current for the rest of the day (**D-056**). **F-26 in the table
> below was still marked open in this file today and has been built since 2026-08-22 (D-053 §1)** —
> found only because this sweep re-read the source rather than the finding. The rule is now law:
> **SM-29 — no finding is repeated from a document without re-reading the file that document names,
> today.** A dated `file:line` is checkable in seconds; an undated claim is re-litigated by whoever
> reads it next.

Two kinds of thing, kept apart on purpose:

- **Findings (`F-nn`)** — a defect, a contradiction between two documents, or a rule that cannot be
  built as written. **A contradiction between a story and `spec.md` is a defect in the story, not a
  question** (`process/agile.md`: *"If a story and `spec.md` disagree, `spec.md` wins and the story is
  a bug"*). These go to the BA, the Architect or Nabil — not to Karim.
- **Questions (`QA-n`)** — a business rule nobody has stated, that QA needs in order to write a case
  that can fail. These go to Karim, through Nabil, and become D-numbers.

**Nothing in this file was resolved by QA.** Where a case could not be written, `test-cases.md` says
`PENDING` rather than encoding a plausible answer.

**Revised 2026-08-21.** Thirteen findings closed — three by code (**D-048**, **D-052 §1**), ten by rulings
(**D-049**, **D-051**). **Closed rows are struck through and kept, never deleted**, so nobody
re-reports them: a finding that vanishes comes back next session as a fresh discovery, and a finding
that reports a fixed defect costs an hour proving it was never broken. Four new ones — **F-24**,
**F-25**, **F-26**, **F-27** — and all four are *"ruled but not built"* rather than *"nobody
decided"*, which is a different and more tractable kind of gap.

**Revised again 2026-08-22 (refinement action SM-31).** **F-26 and F-27 are closed** — F-26 built
(D-053 §1), F-27 ruled and the catalogue row built (D-055 §3). **QA-1, QA-2 and QA-4 were already
answered; QA-3 is the only question left in this file.** Of the fifteen findings still open, none
blocks a committed sprint-1 story.

**Three new findings, 2026-08-22, all from the SM-23 relock of `slice-1/test-cases.md`** — **F-28**
(the `ProjectManage` row cites a test that does not exist), **F-29** (`TC-1-040` asserts the opposite
of `AC-105b-C`) and **F-30** (`ProjectTeamRead` is specified in four stories and exists in no source
file). **Each was verified against the file it names, today, before being written** — F-28 is
described in `decisions.md` D-057 §1 and `process/agile.md`, and was re-checked at the source rather
than repeated from either. **None was found by reading a test result; all three were found by reading
a citation and following it.** That is the relock's argument for itself: a stable ID can be checked
against the rule it names, and a positional label cannot.

**F-28 is closed, 2026-08-22** — Backend fixed the row and QA verified the fix by re-reading the
catalogue, not a report of it. See the F-28 section below for the evidence and for the other three
rows of that batch, all of which now cite a test that exists.

**Three further findings, 2026-08-22, from the SM-31 citation migration of this directory** —
**F-31** (the coverage list for the three new rows credits `ProjectCreate` to a test that asserts
`ProjectManage`), **F-32** (`TC-1-254`'s *"expected to fail on first run"* note outlived the defect
it predicted) and **F-33** (`TC-1-255` quotes the `UserRead` catalogue comment in quotation marks
and the words are not the ones in the file). **All three were found the same way and it is the way
SM-31 predicts:** by opening the file each citation named instead of converting the citation
mechanically. A line number would have resolved for every one of them.

**Corrected 2026-08-21 for D-052.** F-04 is **fixed** and QA-1 and QA-4 are **answered** — see the
⚠️ block above, which is the part to read first. **What is left for Karim is one question: QA-3**, the
reason on a deactivation, which is why `TC-1-086` is now `PENDING Q35` rather than asserting a guess.
Everything else in this file is work, not waiting — ~~and the newest entry, **F-27**, is *ruled but
unbuildable as written*~~ **F-27 is closed, D-055 §3, 2026-08-22.**

---

## Summary

| # | Finding | Kind | Owner | Blocks |
|---|---|---|---|---|
| ~~F-01~~ | ~~Two question registers, same numbers~~ | doc defect | BA + UX | **CLOSED — merged, backlog SM-4** |
| ~~F-02~~ | ~~KAFF-100 treats the bootstrap shape as decided~~ | story defect | BA | **CLOSED — D-051 Q31** |
| ~~F-03~~ | ~~HR must pick a project and holds no permission that shows one~~ | gap | BA | **CLOSED — D-051 Q32.** Replaced by **F-24** |
| ~~F-04~~ | ~~`SiteExpenseConfirm` is granted by department with no role, so a Site Engineer holds it~~ | code defect | Backend | **CLOSED — D-052 §1, fixed and pinned by two tests** |
| F-05 | `HeadOfDesign` holds `ProjectRead` while the catalogue's own doc says it holds nothing | doc/code mismatch | Architect | slice 1 gate |
| ~~F-06~~ | ~~`User` has no `ChangeRole`~~ | story/code mismatch | BA + Architect | **CLOSED — D-049 §6: the guard is handler-level by design** |
| F-07 | `AuditRecord` has no grant-path column; the window to add one closes with slice 1 | scope risk | Backend | KAFF-116 |
| ~~F-08~~ | ~~Two error keys for one refusal~~ | doc defect | BA + UX | **CLOSED — one key, both catalogues.** `TC-1-166` |
| F-09 | `Client` has no way to change the primary phone or the name; KAFF-121 requires both | story/code mismatch | Backend | KAFF-121 |
| ~~F-10~~ | ~~The department claim is never revalidated~~ | code defect | Backend | **CLOSED — D-048, 2026-08-20** |
| ~~F-11~~ | ~~Company-wide permissions never revalidate liveness or role~~ | code defect | Backend | **CLOSED — D-048, 2026-08-20** |
| ~~F-12~~ | ~~`spec.md` §9 has never been updated for `Role.Hr` or for global reach~~ | doc defect | BA | **CLOSED — D-047, 2026-08-20.** §9 carries an amendment block for every role ruling; §0 gives amendments the same force as the text above them |
| ~~F-13~~ | ~~KAFF-105 AC3 gives HR a project list it may not see~~ | story defect | BA | **CLOSED — D-051 Q32.** `TC-1-040` reverses |
| F-14 | `Marketing` holds `ProjectRead` with no citation beyond "§9" | uncited grant | Architect | slice 2 |
| F-15 | Owner grants on five master records rest on one reading of D-044 ruling 4 | open ruling | Nabil → Karim | slice 2 |
| F-16 | `BankManage` mapped onto `AccountManage`, which opens every account type | open ruling | Nabil → Karim | slices 2, 3 |
| F-17 | `PeriodClose` granted to Finance as an assumption | open assumption | Nabil → Karim | slice 7 |
| ~~F-18~~ | ~~`AuditRead` granted to the Owner as an assumption~~ | open assumption | Nabil → Karim | **CLOSED — D-049 §1** |
| ~~F-19~~ | ~~KAFF-121 AC2 asserts a refusal UX lists as unanswered~~ | story/UX mismatch | BA + UX | **CLOSED — D-049 §8: it warns, it does not refuse** |
| ~~F-20~~ | ~~KAFF-118 AC1 requires a role change KAFF-109 owns~~ | story defect | BA | **CLOSED — D-051 Q27 unblocked KAFF-109** |
| ~~F-21~~ | ~~Six `Ready` stories depend on a `BLOCKED` story~~ | planning defect | Scrum Master | **CLOSED — backlog recomputed transitively, SM-1** |
| ~~F-22~~ | ~~"Do portal clients sign in on the same host as staff"~~ | gap | BA | **CLOSED — D-051 Q33.** Residue: `TC-1-017` |
| F-23 | Slice 1 assigns against projects that exist only as seed data | accepted, must not become habit | Scrum Master | KAFF-113 |
| **F-24** | **HR's project team screen is ruled and has no permission and no story** | **gap** | **BA + Architect** | **`TC-1-243`…`TC-1-245`** |
| **F-25** | **`SetWithholding` trusts a client kind the caller supplies; no Domain test can catch a lie** | **design risk** | **Architect** | **KAFF-416** |
| ~~F-26~~ | ~~The global kill (`SecurityStamp`) is declared and not implemented~~ | code gap | Backend | **CLOSED — D-053 §1, built 2026-08-22.** The stamp is compared on every authorized request and a mismatch or an absence is refused [Verified: 2026-08-22 @ `PermissionSubjectReader.cs` -> `ReadAsync`]. **This row still said "not implemented" on 2026-08-22 and was found only by re-reading the source** — SM-29 |
| ~~F-27~~ | ~~`ProjectManage` is `ProjectScoped`, so it cannot authorise creating a project — the act D-052 §2 just ruled on~~ | scope defect | Architect | **CLOSED — D-055 §3, 2026-08-22.** Split, not widened: `ProjectCreate` is CompanyWide for Owner + Technical Office, `ProjectManage` keeps its grants and its `ProjectScoped` scope for editing [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate` and @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]. **Endpoint is slice 4; the catalogue row exists now** |
| ~~F-28~~ | ~~The `ProjectManage` row cites a test that does not exist~~ | code defect | Backend | **CLOSED — fixed by Backend, verified by QA 2026-08-22.** The row now cites `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` and `Only_the_owner_and_the_technical_office_may_open_a_project`, and both exist [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`, @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`]. `TC-1-254` |
| **F-29** | **`TC-1-040` asserts the strict opposite of `AC-105b-C`, and both cite D-051 Q32** | **story/case contradiction** | **BA + Nabil** | **`TC-1-040`, marked DISPUTED** |
| **F-30** | **`ProjectTeamRead` is named in four stories and exists in no source file** | **gap** | **Architect + Backend** | **`TC-1-127`, `TC-1-243`…`TC-1-245`** |
| **F-31** | **The three new rows' coverage list names the wrong test** — it credits `ProjectCreate` to a test that asserts `ProjectManage` | **QA register defect** | **QA** | **`TC-1-249`, `TC-1-254`** |
| **F-32** | **`TC-1-254`'s *"expected to fail on first run"* note is stale** — the defect it predicted (F-28) has been fixed | **QA register defect** | **QA** | **`TC-1-254`** |
| **F-33** | **`TC-1-255` quotes the `UserRead` catalogue comment in quotation marks and the words are not the ones in the file** | **QA register defect** | **QA** | **`TC-1-255`** |
| **F-34** | **Four places in `qa/` say the money-touching list holds *eleven* permissions; it holds twelve** | **QA register defect** | **QA** | **`TC-1-215`, `TC-1-250`** |

| # | Question for Karim | Blocks |
|---|---|---|
| ~~QA-1~~ | ~~Is a Site Engineer ever allowed to confirm a site expense?~~ | **ANSWERED — D-052 §1. No. F-04 fixed the same day** |
| ~~QA-2~~ | ~~What may HR see of a project?~~ | **ANSWERED — D-051 Q32** |
| QA-3 | Must the reason on a deactivation be typed, or may it be blank? | `TC-1-086`, now **PENDING Q35** — KAFF-110 AC6 — **still unasked** |
| ~~QA-4~~ | ~~Must the first Owner change the password he typed himself on the setup screen?~~ | **ANSWERED — D-052 §3. No.** `TC-1-006` and KAFF-100 AC6 now have a definite expected result |

---

## Findings

### F-01 · Two question registers, same numbers, different questions — **doc defect**

`stories/questions-for-karim.md` numbers Q1–Q26. `ux/questions.md` numbers Q1–Q15. They are different
questions:

| | BA register | UX register |
|---|---|---|
| **Q1** | Who may read the audit trail | How the first Owner comes to exist |
| **Q3** | How long a session lasts | What HR may see of a project |
| **Q4** | Does signing out on one device sign out the others | What happens on a duplicate client phone |
| **Q7** | What happens to assignments when a role changes | Are users deduplicated by phone |
| **Q8** | Do clients have a reference number | Can a user's role be changed |

The story files write bare `Q2`, `Q5`, `Q7` and mean the BA register. `ux/slice-1-flows.md` writes
bare `Q1`, `Q3`, `Q4` and means the UX one — and its S-002 section writes *"this is `questions.md`
**Q1**"* meaning the UX Q1, while a reader coming from the stories will read it as the audit-trail
question.

**Why it matters here.** A `PENDING Q3` in a test case is unexecutable: the Verifier cannot tell which
answer would unblock it. `qa/` therefore always writes `Q-BA-n` or `Q-UX-n`.

**Ask:** merge into one register with one numbering, or give each a distinct prefix at source.
**Owner:** BA and UX. **Not Karim's.**

---

### F-02 · KAFF-100 treats the bootstrap shape as decided — **story defect**

`stories/KAFF-100` rule 3: *"The seed runs once and is idempotent"*, cited to "D-038 pattern,
`architecture.md` seeding strategy". The story's title and its AC1 (*"When the application starts"*)
both assume a **seeded** bootstrap.

`ux/slice-1-flows.md` S-002 says the opposite in as many words: *"Two shapes exist. **Neither is
chosen here.** … Do not build either until Nabil answers."* And `decisions.md` D-043's note that *"the
bootstrap has to be a database seed"* is, as UX correctly says, **a description of the current state,
not a ruling.**

So the story has resolved a bucket-three item by citing a description. The citation is real; what it
cites is not a decision.

**This is exactly the failure `stories/README.md` reason 4 names:** *"A story never resolves an
ambiguity by picking the reading the code already implements."*

**Fix:** either add the bootstrap shape to the BA register as a question, or get a D-number for the
seed. `TC-1-001` … `TC-1-005` are written to hold under **either** shape, so they are not lost either
way. **Owner:** BA and Nabil.

---

### F-03 · HR must pick a project and can see no project — **gap, and it is missing from the BA register**

`Role.Hr` holds exactly `EmployeeManage` and `ProjectAssignmentManage`. It does **not** hold
`ProjectRead`. Nothing in the catalogue lets HR obtain a list of projects, or even one project's name.

`ux/questions.md` **Q-UX-3** raises this and calls it a slice-1 blocker for S-009, S-010 and *"HR's
entire navigation"*. **It appears nowhere in `stories/questions-for-karim.md`**, and neither KAFF-113
nor KAFF-115 mentions it — KAFF-115 rule 5 states the consequence (*"HR can staff a team it cannot
look at"*) as though it were settled rather than a problem.

UX's warning is worth repeating because it is the shape of D-035: **"Do not solve this by granting HR
`ProjectRead`, and do not solve it by reusing the internal project list."** Either hands HR the
project surface D-044 ruling 2 was written to remove.

**QA's cases are written so HR is handed a project id, never a picker** — which keeps them executable
and keeps the gap visible rather than accidentally closed.

**Ask Karim (QA-2):** *"HR puts people onto projects. To do that HR has to pick the project from a
list. What is HR allowed to see about a project — just its name and code, or something more? Every
project in the company, or only ones that already have staff?"*

---

### ~~F-04~~ · A Site Engineer can confirm site expenses — **CLOSED, D-052 §1, 2026-08-21**

**Fixed.** `SiteExpenseConfirm` now grants to `Role.Finance`, and to a second grant naming
`Role.TechnicalOffice` **plus** Operations / Administrative
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. Every criterion set on a grant must
match, so the Technical Office holds it only from the Administrative sub-department and a
`Role.SiteEngineer` parked there holds nothing.

**The Architect ruled the mechanism, not the row:** *"No documented exceptions. The gate must pass
with 100% compliant code … Financial permissions like `SiteExpenseConfirm` must never be granted to a
bare department without specifying a role."*

**Regression cover, both green.** `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]
asserts the four outcomes — SiteEngineer in Ops/Admin refused, Finance granted, TechnicalOffice in
Ops/Admin granted, TechnicalOffice in Ops/**Technical** refused — and `No_financial_permission_is_granted_to_a_bare_department`
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]
pins the class across **twelve** money-touching permissions (eleven until `ProjectFinancialsEdit`
joined the list on 2026-08-22 — **F-34**), so the shape cannot return on a different row. **70/70 Domain, clean
rebuild.** D-052 watched both go red first by removing the role line.

**Two things this entry keeps rather than deletes.** First, `PhotoPublish`
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] is **still** a bare-department grant, deliberately — the ruling is
scoped to *financial* permissions and a photo moves no money. It is the last one and needs its own
ruling; the matrix marks those cells 🟡, not ⚠. Second, this file said F-04 was *"not reachable today
(no endpoint requires it)"*. That was true of the **Api** half only. **The Domain half needed nothing
but a call to the evaluator**, which is where the rule lives — and *"no endpoint calls it"* is a
statement about reach, not about whether a rule is wrong. **Do not re-report this finding.**

***(The original text is kept below, because a finding that vanishes comes back next session as a
fresh discovery.)***

`SiteExpenseConfirm` is granted to `{ Department = Operations, OperationsSubDepartment =
Administrative }` with **no role named**. `User.Create` places a `Role.SiteEngineer` in
Operations/Administrative without complaint. So an engineer in that sub-department holds the
permission.

`spec.md` §8 excludes him by name:

> *"Site financial expenses are entered by Finance or Admin, **not the engineer**. An engineer's
> expense entry is a draft that Accounts confirms and posts."*

**This is the third appearance of one mechanism.** D-035: `Role.Client` holding `ProjectRead` opened
the internal surface to the portal. D-044 ruling 2: an HR user in Operations/Administrative would have
inherited this same permission — closed by pinning HR to `Department.Hr`, a *second* mechanism, because
the catalogue could not do it. The hole is closed for HR and open for everyone else.

**Case:** `TC-1-215` — now a **regression case** on its Domain half; its Api half stays unrunnable
until slice 6, KAFF-608. **Risk:** RSK-05, closed. **Matrix:** the ⚠F-04 cells are gone from the
`SiteExpenseConfirm` row.

**Two possible fixes, and choosing between them is the Architect's, not QA's:** require every grant to
name a role, or have `AccessGrant` carry the roles a department grant applies to. A third — refusing to
place a Site Engineer in Operations/Administrative — is narrower and leaves the mechanism intact.
***The Architect took the first, as a rule about financial permissions rather than about this row.***

**Ask Karim (QA-1)**, because the fix depends on the answer: *"Is there anyone on site who is allowed
to confirm a site expense, or is that always the office?"* ***Answered — see QA-1 below.***

---

### F-05 · `HeadOfDesign` holds `ProjectRead` while its documentation says it holds nothing

`PermissionCatalogue`'s XML doc: *"**`Role.HeadOfDesign` holds nothing yet.** spec.md §9 marks it
phase 2."* The `ProjectRead` row grants it anyway.

The data and its own documentation contradict each other, and `spec.md` §9 gives no basis for either
reading — it says only *"Head of Design (phase 2)"*. §5.3 describes design work as having no BOQ, no
extract, no site, which makes `ProjectRead` on an execution project an odd fit.

**Not urgent** — no Head of Design user will exist before phase 2 — **and it is a live cell in the
matrix**, so a test asserting either way today is asserting an accident.

**Fix:** decide whether "phase 2" means "no grants until then" or "no features until then", and make
the doc and the data agree. **Owner:** Architect.

---

### F-06 · `User` has no `ChangeRole`; KAFF-109 is written as though it does — **story/code mismatch**

`User` exposes `MoveToDepartment` and no role setter. `ux/questions.md` Q-UX-8 raises the prior
question — *is the role mutable at all?* — and notes that the absence may be deliberate.

KAFF-109 does not ask that question. It takes mutability as given, is BLOCKED only on Q-BA-7 (what
happens to assignments), and its AC1–AC4 all assume the endpoint exists.

Both readings have consequences the story does not carry: if the role is immutable, S-008 must say so
where the role is displayed, and KAFF-109 becomes "there is no such thing". If it is mutable, changing
it silently changes every grant a user holds and needs a confirmation naming what they gain and lose.

**Fix:** fold Q-UX-8 into Q-BA-7 so the answer covers both halves. **Owner:** BA.

---

### F-07 · `AuditRecord` has no grant-path column, and the window closes with slice 1

Expected — KAFF-116 is the story that adds it, and it has not been built. Recorded here because of the
consequence, which is not recoverable: **audit records are append-only and enforced as such by a
trigger, so a column added after slice 3 cannot be backfilled.**

`TC-1-129` … `TC-1-133` are expected to fail on first run, and `TC-1-135` is the case that proves *why*
the backfill would be impossible.

**Risk:** RSK-09. **Owner:** Backend, in slice 1, not later.

---

### F-08 · Two different error keys for the same refusal — **doc defect**

| Document | Key |
|---|---|
| `stories/KAFF-120` i18n section | `errors.master.individual_client_does_not_withhold` |
| `ux/slice-1-flows.md` S-012 error table and its "new keys" table | `errors.master.individual_does_not_withhold` |

One of them will be implemented and the other will be added to `ar.json` and `en.json`, where it will
sit unreferenced — or worse, the API emits one and the catalogue carries the other, and the screen
shows a raw key to an Arabic-speaking user.

**Fix:** pick one, in both documents. `TC-1-166` asserts the key exists in both catalogues and will
catch the mismatch. **Owner:** BA and UX.

---

### F-09 · `Client` cannot change its primary phone or its name; KAFF-121 requires both

`Client` exposes `SetContactDetails(alternatePhone, email, address, notes)`, `SetWithholding` and
`Archive`. There is **no** setter for `Name` and **no** setter for the primary phone.

KAFF-121's story sentence is *"I correct a client's **name**, alternate phone, email, address and
notes"*, and its rule 2 and AC2 are entirely about **changing the primary phone** and re-running the
duplicate check.

So the story's headline behaviour has no domain path, and a mistyped client name is currently
permanent on a master record `spec.md` §2 says must be *"project-independent, full history"*.

**Cases:** `TC-1-168` (phone change re-runs deduplication) and `TC-1-174` (the name is editable), both
expected to fail. The important half is that **if a phone setter is added without the dedup check,
`TC-1-168` catches it** — which is why the case is written now rather than after the setter exists.

**Owner:** Backend. **Note for the BA:** rule 2 is also entangled with F-19.

---

### ~~F-10~~ · The department claim is never revalidated — **CLOSED, D-048, 2026-08-20**

**Was:** `ProjectAccessPolicy` re-read `IsActive`, `Role` and `ClientId` and **not** the department,
which came from a token claim — so a department move took effect in neither direction, and two
permissions are granted by department.

**Fixed by D-048.** `IPermissionSubjectReader` takes only the user id from the principal and reads
role, department, sub-department, client scope and liveness from the users table on every authorized
request.

**Regression cover:** `TC-1-067`, `TC-1-068`, `TC-1-214`, plus the unit
`A_stale_department_claim_grants_nothing`. **All three have been rewritten from defect cases to
regression cases** and are no longer expected to fail.

**What remains open is F-04, not this** — the two department-only grants are still department-only.

---

### ~~F-11~~ · Company-wide permissions never revalidate liveness or role — **CLOSED, D-048, 2026-08-20**

**Was:** `PermissionAuthorizationHandler` called `IProjectAccessPolicy` only when the request resolved
a project, so every `CompanyWide` permission was decided entirely from a token. A deactivated Owner
kept `UserManage`; a deactivated Finance user could still move company money.

**Fixed by D-048** — QA's own finding, found by reading the handler against the catalogue rather than
by running anything, and **verified by reverting the fix and watching five tests go red**.

**Regression cover:** `TC-1-082`, `TC-1-084`, `TC-1-213`, and the units
`A_deactivated_user_loses_company_wide_permissions_too` / `A_deactivated_owner_cannot_administer_users`.
**Keep the company-wide and project-scoped halves as pairs** — the defect existed because only the
project-scoped half had ever been written.

**The lesson is the part worth keeping:** both pre-existing tests passed, and passed for a reason that
did not generalise. *A green result is not evidence until you know what red would have looked like.*

---

### F-12 · `spec.md` §9 has never been updated — **doc defect, highest priority**

§9 names **eight** roles. It does not mention `Role.Hr`. It does not mention the Owner's global reach.
It does not mention HR's global reach. All three come from `decisions.md` (D-010, D-044) and none has
been written back.

`CLAUDE.md` is unambiguous: *"If code and `spec.md` disagree, `spec.md` wins."* So a Verifier reading
`spec.md` in a fresh session — which is exactly what `agents.md` instructs the Verifier to do — would
be **right** to fail the permission model, and slice 1's gate would fail for a documentation reason.

This is the BA's action A1, raised 2026-08-18 and listed as **N3** in
`stories/questions-for-karim.md`, where it is described as *"the highest-priority documentation task in
the project"*. It is still outstanding.

**`qa/slice-1/permission-matrix.md` carries an explicit warning at the top and cites a D-number on
every cell that does not come from `spec.md`.** That is a mitigation, not a fix.

**Owner:** BA. **Do this before the Verifier runs.**

---

### F-13 · KAFF-105 AC3 gives HR a project list it may not see — **story defect**

AC3: *"Given I am `Role.Hr` … Then **every project is listed** with `ProjectAssignmentManage`."*

Listing a project means naming it. HR holds no permission that permits reading anything about a
project (F-03), and D-044 ruling 2 makes HR *"strictly administrative"*. Either `/api/me` is an
exception to that — in which case it is an undocumented one on the endpoint that drives all navigation
— or AC3 cannot be satisfied as written.

`ux/questions.md` Q-UX-3 identifies the same tension from the other side and says the answer *"is
delivered to the frontend through the `/api/me`-shaped endpoint"*. So both documents point at
`/api/me` and neither says what it may contain for HR.

**Fix:** answer QA-2, then rewrite AC3 to name exactly which fields HR receives. `TC-1-040` is written
against "every project is listed" as the story states it, and will need revising when the answer lands
— which is recorded here so it is revised deliberately.

---

### F-14 · `Marketing` holds `ProjectRead`, cited only as "§9"

§9 does not give Marketing project access. §2 gives Marketing the Client and Opportunity masters. §12
concerns the portal. A salesperson wanting to see the job they sold is plausible — and plausible is
the problem this project keeps hitting.

**Not urgent** and probably right. Recorded because the catalogue requires a citation for every grant
and this one does not identify a sentence. **Owner:** Architect.

---

### F-15 · Owner grants on five master records rest on one reading — **D-045 #2, Q-BA-12**

D-044 ruling 4's *rule* line says the Owner reaches "all master data"; its *action* line names three
(Clients, Suppliers, Banks). The rule line was applied, so the Owner holds `CatalogueManage`,
`BabManage`, `EmployeeManage`, `SubcontractorManage` and `OpportunityManage` as well.

If the list was meant literally, five grants come back out. **Settle before slice 2 opens**, not during
it — the matrix has five ⚠F-15 cells and each is a test that would need rewriting.

---

### F-16 · `BankManage` mapped onto `AccountManage` — **D-045 #1, Q-BA-13**

There is no Bank master record in `spec.md`; a bank is an account of `AccountType.Bank` in the §6.3
tree. `BankManage` was mapped onto `AccountManage`, which opens **every** account type — including,
from slice 3, project ledgers.

Safe meanwhile because `Account.Create` can only turn a floor **on**, never off, and guard 3c freezes
configuration after creation. **Settle before slice 3.**

---

### F-17 · `PeriodClose` granted to Finance as an assumption — Q-BA-23

§6.6 requires a month-end close and does not say who performs it, nor whether the Owner must approve it
as he approves every other financial act (§9). Finance is assumed and the row is marked `Unresolved`.
Slice 7.

---

### F-18 · `AuditRead` granted to the Owner as an assumption — Q-BA-1

`spec.md` requires the audit trail and does not say who reads it. The row is marked `Unresolved` and a
test pins the assumption so it cannot grow quietly (D-012). It is the first open question on the BA's
list because everything else that blocked slice 1 has been answered.

**When it is asked, say this in the same breath:** from slice 3 the trail carries every movement of
money, so "who can see the history of changes" and "who can see the money" are the same question and do
not sound like it. And the shape is uncomfortable: *the only globally-reaching actor is also the only
reader of the trail that watches him.*

`TC-1-142` is PENDING. `TC-1-136` … `TC-1-141` hold under any answer and are written.

---

### F-19 · KAFF-121 AC2 asserts a refusal that UX lists as unanswered — **story/UX mismatch**

KAFF-121 AC2: *"When A's phone is edited to B's phone, **then it is refused** as a duplicate."* Cited
to §2 and §3, and the derivation is sound — §3 says never create a duplicate client, and an edit into a
collision creates one by another route.

`ux/questions.md` Q-UX-4 sub-question 2 lists exactly this as open: *"**May an existing client's phone
be edited into a collision?** And what happens then — the same dialog, or a refusal with no path
forward?"*

**These are not quite the same question**, and that is the fix: the *rule* (refuse) is derivable from
§3; the *interaction* (what the user is offered next) is not. The story should say so, as KAFF-119
does for its own duplicate case.

`TC-1-168` tests the rule only, and no case tests the interaction. **Owner:** BA and UX.

---

### F-20 · KAFF-118 AC1 requires a role change that KAFF-109 owns and is BLOCKED — **story defect**

KAFF-118 AC1 lists the slice-1 changes that must each write exactly one audit record: *"a user is
created, **given a role change**, moved between departments, deactivated; an assignment created and
revoked; a client created, edited and archived."*

Role change is KAFF-109, **BLOCKED on Q-BA-7**. Client creation is KAFF-119, **BLOCKED on Q-BA-8 and
Q-BA-9**. So a `Ready` story's headline AC cannot be fully executed while two of its nine steps belong
to stories nobody may start.

`TC-1-143` is written against the full list and will be partially unexecutable. **Fix:** scope AC1 to
the changes slice 1 actually ships, and add the rest when their stories unblock. **Owner:** BA.

---

### F-21 · Six `Ready` stories depend on a `BLOCKED` story — **planning defect**

| Ready story | Depends on | Status of the dependency |
|---|---|---|
| KAFF-105 | KAFF-101 | BLOCKED Q-BA-2, Q-BA-3 |
| KAFF-106 | KAFF-100 | BLOCKED Q-BA-2, Q-BA-5 |
| KAFF-118 | KAFF-119 | BLOCKED Q-BA-8, Q-BA-9 |
| KAFF-120 | KAFF-119 | BLOCKED |
| KAFF-121 | KAFF-119 | BLOCKED |
| KAFF-123 | KAFF-119 | BLOCKED |
| KAFF-124 | KAFF-119 | BLOCKED |

`stories/backlog.md` proposes *"the 14 Ready stories, 43 points"* as sprint 1.

**The auth dependency is genuinely soft** and `backlog.md` says why: the Api harness issues identities
directly (`TestAuthHandler`), so KAFF-105 through KAFF-124 are testable without a login endpoint.
**The KAFF-119 dependency is not soft.** Four Ready stories need an endpoint that creates a client, and
that endpoint's form has an undecided mandatory field (`Client.Code`, Q-BA-8).

**The risk is not that the sprint stalls — it is that somebody unblocks it cheaply**, by making
`Client.Code` optional or by picking a duplicate-phone interaction "to get things moving".
`stories/README.md`: *"A question is never closed by writing a plausible answer into the story."*

**Owner:** Scrum Master, at the next refinement. **Risk:** RSK-17.

---

### F-22 · "Do portal clients sign in on the same host as staff" is in one register only

`ux/questions.md` Q-UX-9 asks it and gives a slice-1 reason: **a `Role.Client` user can already
authenticate in slice 1**, and must land on the portal shell, *"never in the staff shell — not even for
one frame, not even empty."* A client who sees the staff chrome has seen the shape of the internal
application.

It is absent from `stories/questions-for-karim.md`, and KAFF-101 rule 6 treats same-endpoint sign-in as
settled (*"A `Role.Client` portal user signs in through the same endpoint"*), citing §12 and D-035 —
neither of which addresses where.

**Fix:** add it to the BA register, or record why KAFF-101 rule 6 is a decision. **Owner:** BA.

---

### F-23 · Slice 1 assigns against projects that exist only as seed data

KAFF-113's "Not in this story" is explicit and correct: nothing in slice 1 creates a project. It said
so because `ProjectManage` was granted to **nobody** (§2 names a module that is not a §9 role, D-012,
Q-BA-17); **since D-052 §2 the holders exist — Owner and Technical Office — and the conclusion is
unchanged**, because the row is `ProjectScoped` and cannot authorise a create at all (~~**F-27**~~ —
**closed 2026-08-22, D-055 §3: `ProjectCreate` now can, and its endpoint is slice 4, so slice 1 still
creates no project and this finding's conclusion is still unchanged**). Slice
1 still assigns against seed projects, and slice 4 still has to build the thing properly.
*"Slice 1 assigns against projects that arrive in seed data. That is a test fixture, not a business
rule, and it must not become a habit."*

Recorded here so the Verifier knows the demo script's project is a fixture, and so nobody grants
`ProjectManage` to make slice 4 work. **Not a defect. A boundary that must be re-read in slice 4.**

---

### F-24 · HR's project team screen is ruled, has no permission, and has no story — **gap**

**D-051 Q32:** *"HR may only see the project name and the list of assigned engineers … HR must be
routed to a separate 'Project Team' tab/screen that contains zero financial details."*

Two things follow, and only the first is recorded anywhere.

**The ruling implies a new narrow permission.** D-051 says so — *"implies a new narrow permission
rather than granting HR `ProjectRead`"* — and that **naming it is the story's**. `Role.Hr` holds
`EmployeeManage` and `ProjectAssignmentManage` and **nothing that names a project**, so today the
screen cannot be built at all.

**There is no story.** It is not in slice 1's list, and `KAFF-105b` explicitly does not carry it —
HR's `/api/auth/me` names no project.

**Why this is a finding and not just a backlog gap.** The shortcut is one line: granting HR
`ProjectRead` makes the screen work and hands HR the entire project surface D-044 ruling 2 was written
to remove. `ux/questions.md` warned about exactly this before the ruling existed — *"do not solve this
by granting HR `ProjectRead`, and do not solve it by reusing the internal project list"* — and the
warning is now easier to ignore, because the ruling reads like permission to build something.

**Note the shape of Karim's answer, which is the safeguard:** a **separate surface**, not a filtered
view — the same pattern §12 uses for the portal, and the same reason. *A filtered view leaks the first
time somebody adds a field.*

**Cases:** `TC-1-243`, `TC-1-244`, `TC-1-245`, marked **NO STORY** — uncovered, but not `PENDING`,
because nothing is being invented. **Owner:** BA, to write the story; Architect, to name the
permission.

---

### F-25 · The withholding guard depends on an argument the caller supplies — **design risk**

`Project.SetWithholding(category, clientKind)` takes the client's kind as a parameter. D-049 explains
the signature: *"the client's kind is passed in rather than looked up, because the domain holds only
`ClientId`."* That is the right call for the model, and it moves the invariant's last mile into the
handler.

**None of the six Domain tests can detect a caller that lies.** They pass `ClientKind.Individual`
directly and assert the refusal — correct, and blind to a handler that reads the kind from a request
body or a stale DTO. §6.7's failure mode is a permanent 1–5% shortfall on every collection for that
contract.

**Case:** `TC-1-242`, the only one in the suite that can catch it, running against **KAFF-416**.
**Risk:** RSK-20. **A cheaper structural fix exists and is the Architect's to choose:** load the
`Client` row in the handler and pass `client.Kind`, or move the lookup behind a domain service.

**~~Related and separate: no permission expresses who may set it.~~ CLOSED 2026-08-22 — D-055 §1.**
`ProjectFinancialsEdit` is that permission: `ProjectScoped`, `TouchesMoney`, granted to `Role.Finance`
and `Role.Owner` alone [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`].
**Finance was deliberately not added to `ProjectManage`** — an accountant must not alter the
engineering scope of a project, and a grant written to reach one field hands over the whole record.
**F-25's remaining half is the `client.Kind` design risk below, which this does not touch.** The
original text, kept: *D-049 ruling 10 gives it to Finance and denies it to Marketing; it would sit
under `ProjectManage`, which since D-052 §2 is granted to the Owner and the Technical Office — and to
Finance not at all. The gap changed shape rather than closing: not "granted to nobody" but "granted to
the wrong people for this act", on a row that cannot authorise a create either (**F-27**). A ruled
separation of duties with no mechanism* — see
`permission-matrix.md` F-25.

---

### F-26 · The global kill is declared and not implemented — **code gap**

D-051 N5 makes `SecurityStamp` rotation the global sign-out. It also records what does not exist:
*"`KaffClaimTypes.SecurityStamp` is defined and `User.SecurityStamp` rotates on `SetPasswordHash` and
`Deactivate` — but **nothing compares the two.** The global kill is declared, not implemented … **It
belongs to KAFF-101a and the story must say so.**"*

**It is narrower than it first looks, and getting the scope wrong makes the test worthless.** D-048's
per-request user re-read already covers **deactivation, role change and department change on both
scopes**, and D-048 explicitly **rejected** a stamp as the fix for those. What the stamp is for is the
case the row re-read cannot see: **a password change**, where the user is still active with the same
role — D-049 ruling 2 requires every other session to die, and nothing makes that happen. A reset
(D-051 Q38) is a password change and inherits it.

**Also uncovered by the rotation that does exist:** `Reactivate` does not rotate at all, which D-051
names as *"the one path that should rotate and does not"*.

**The trap, quoted because it is the failure mode a reviewer will wave through:** a check with a
*"skip when the claim is absent"* fallback is **worse than an absent one** — D-051's words — because
it looks implemented. `TC-1-225` must therefore run against a session **without** the claim too, and
must rotate the stamp **without changing anything else about the user**, or D-048's mechanism refuses
the request for an unrelated reason and the case passes with no comparison in the code.

**Cases:** `TC-1-019`, `TC-1-097`, `TC-1-225`, `TC-1-230`, `TC-1-233`. **Risk:** RSK-06.
**Owner:** Backend, in KAFF-101a.

---

### ~~F-27~~ · `ProjectManage` cannot authorise the act it was just granted for — **CLOSED 2026-08-22, D-055 §3**

> **The ruling, and it takes the second of the two ways out named below.** `ProjectCreate` is a new
> **CompanyWide** permission granted to the Owner and the Technical Office; `ProjectManage` keeps its
> name, its `[owner, technicalOffice]` grants and its **`ProjectScoped`** scope for editing
> [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate` and @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]. **Company-wide is
> not a weakening here:** a create request cannot name the project it is about to create, so scope is
> the only instrument that reaches the act — reach cannot, because there is nothing to reach.
>
> **What was rejected is the substance.** Widening `ProjectManage` itself was the one-line fix and it
> fixes creation *by removing the assignment requirement from editing* — the §9 consequence that made
> this a decision rather than a drafting choice. **A later session that "tidies" `ProjectCreate` back
> into `ProjectManage` reopens exactly that hole.**
>
> **A third permission came out of the same ruling — `ProjectFinancialsEdit`** (D-055 §1, closing
> Q-N10-2), because Finance holds no `ProjectManage` grant and D-049 rulings 9–10 gave Finance the
> contract's withholding category. **`ProjectScoped`, `TouchesMoney`, Finance and Owner alone.**
> That closes **F-25**'s permission half; F-25's `client.Kind` design risk is untouched.
>
> **QA's part is not finished by this ruling.** The three new rows shipped with no test of their own
> (**D-056 §3**) and now have three: `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
> `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`, and
> `Hr_may_read_the_user_list_and_still_reaches_nothing_financial`. **The endpoints are slice 4**, so
> `TC-1-206` and `TC-1-207` still cannot assert the scope through an endpoint — they assert it at the
> catalogue and evaluator level now, and the endpoint cases are written in slice 4.
>
> **The text below is the finding as raised, kept unedited.** It is the record of why the split
> exists.

**D-052 §2 answered Q17**, the oldest open question in the catalogue: *"opening a project triggers
engineering items, accounting ledgers, and cost tracking. It is strictly a technical and
administrative responsibility. Site Engineers and Marketing have no business creating projects."*
`ProjectManage` is now granted to `[owner, technicalOffice]`
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`] and has left the catalogue's `Unresolved`
set, leaving `PeriodClose` as the only row in it [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PeriodClose`].

**The row is still `PermissionScope.ProjectScoped`** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`], so the evaluator refuses when the
request names no project — and **a create request cannot name one, because the project does not exist
yet.** As written the permission can authorise *editing* a project and **cannot authorise opening
one**, which is the half Karim ruled on.

**Raised, not taken — and D-052 says why.** Making the row company-wide would also drop the assignment
requirement from *editing*, weakening §9; splitting create from edit means two permissions. *"That is
an architecture decision with a §9 consequence, not a drafting choice."* **Owner:** Architect. **Lands
in slice 4 with KAFF-407.**

**Why it is a finding rather than a note.** The matrix would otherwise carry a cell reading *"the
Owner and the Technical Office may create a project"* — **a capability that does not work.** The
`permission-matrix.md` cells therefore read `RG 🟡` / `A 🟡` with the caveat attached, and neither
`TC-1-206` nor `TC-1-207` asserts the scope. It also **supersedes F-25's** *"it would sit under
`ProjectManage`, granted to nobody"*: the holders exist now; what is missing is a permission that can
authorise a create.

---

### ~~F-28~~ · The `ProjectManage` row cites a test that does not exist — **CLOSED 2026-08-22**

> **CLOSED — fixed by Backend, verified by QA on 2026-08-22 by re-reading the catalogue, not by
> reading a report of it.** The `ProjectManage` row's comment now cites
> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` and
> `Only_the_owner_and_the_technical_office_may_open_a_project`
> [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`], and **both
> identifiers exist under `tests/`**
> [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
> @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`].
> `Opening_a_project_needs_no_project` is gone from `src/` [Verified: 2026-08-22, repository-wide
> search — it remains only in `proposals/N10-project-creation.md` and in the entries recording this
> finding].
>
> **The other three rows of the same batch were checked in the same pass and each cites a test that
> exists**, so SM-30 holds across all four: `ProjectCreate` cites
> `Only_the_owner_and_the_technical_office_may_open_a_project`
> [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate`];
> `ProjectFinancialsEdit` cites `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`
> [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`];
> `UserRead` cites `Hr_may_read_the_user_list_and_still_reaches_nothing_financial` and
> `Hr_holds_exactly_three_permissions_and_none_touches_money`
> [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Hr_may_read_the_user_list_and_still_reaches_nothing_financial`,
> @ `CatalogueCompletenessTests.cs` -> `Hr_holds_exactly_three_permissions_and_none_touches_money`].
>
> **`TC-1-254` is not retired** — it asserts a standing property of the catalogue, not this one
> defect. Its *"expected to fail on first run"* note is now stale and is withdrawn as **F-32**.
>
> **The text below is the finding as raised, kept unedited apart from its citations, which were
> migrated to identifiers under SM-31.**

**Owner: Backend.** Raised by QA 2026-08-22, during the SM-23 relock. **Verified before writing, not
repeated from a document** — `process/agile.md` and `decisions.md` D-057 §1 both describe this, and
SM-29 requires the source to be re-read anyway. It was, and the defect is still there.

In `PermissionCatalogue.cs`, on the `Permission.ProjectManage` row (then at lines 198-199):

> `// Pinned by An_unassigned_holder_of_ProjectManage_cannot_edit_a_project and`
> `// Opening_a_project_needs_no_project.`

**The first test exists** [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`].
**The second does not.** `Opening_a_project_needs_no_project` appears in exactly two places in this
repository: `proposals/N10-project-creation.md:287`, where it was a **proposed** name, and the comment
above. **The identifier is absent from every file under `tests/`**
[Verified: 2026-08-22 — repository-wide search for the identifier returns `decisions.md`,
`process/agile.md`, the proposal and the catalogue comment, and no test source].

**The test the comment means is `Only_the_owner_and_the_technical_office_may_open_a_project`**
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`], which was **repointed
from `ProjectManage` to `ProjectCreate` on 2026-08-22** with the D-055 §3 split and now asserts
`ProjectCreate`'s grants and a `projectId: null` evaluation — which is exactly *"opening a project
needs no project"*, under a different name.

**The exact fix, and it is one line.** In `PermissionCatalogue.cs`, on the second of those two
comment lines, replace `Opening_a_project_needs_no_project` with
`Only_the_owner_and_the_technical_office_may_open_a_project`. **Nothing else changes** — the row's
scope, grants and citation are all correct, and no test needs writing for this finding.

**Why a wrong test name is a finding and not a typo.** SM-30 makes the row's comment the pointer a
reader follows to check that a permission is covered. **A citation nobody can check decays into the
thing SM-29 exists to stop** — the next reader searches for the name, finds nothing, and either
concludes the coverage is missing when it is not, or writes a second test that duplicates one that
exists. D-057 §1 records that this is **SM-30's own first failure**: on the day the rule was proposed,
its mechanism had already produced a confident false claim inside the file it governs. The rule's
answer to itself is *"a cited test name is a claim about the code, so SM-29 already binds it. Verify
the name exists before writing it. This costs one search."*

**QA has not edited `src/`.** **Case:** `TC-1-254`, which asserts both halves — every row named in
some test, and every test name a row cites existing — and is **expected to fail on first run** on the
second half until this line is corrected. D-057 §1 assigns the mechanised form of that check to
Backend as well: *"the enforceable half is coverage, not prose."*

---

### F-29 · `TC-1-040` and `AC-105b-C` assert opposite things, and both cite D-051 Q32 — **contradiction**

**Owner: BA and Nabil. Not Karim's — the ruling exists; what disagrees is two readings of it.**

| | Says |
|---|---|
| **`AC-105b-C`** [Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md:80-85`] | HR calls `GET /api/auth/me` and *"all three are listed with **name and code**"*, with no value, cost, margin, balance, budget, status or client field, and no `ProjectRead`. `AC-105b-E` then flags each entry as reachable through the Project Team surface only, and refuses HR the dashboard. |
| **`TC-1-040`** | HR's payload names **no project at all** — *"not by name, not by code, not by id, and not as a count."* |
| **D-051 Q32, verbatim** (`decisions.md:1787`) | *"HR may only see the project name and the list of assigned engineers … If the main project dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that contains zero financial details."* |

**Both were written from the same ruling and they cannot both pass.** The reading that produced
`TC-1-040` treats *"a separate surface, not a filtered view"* as applying to `/api/auth/me`; the
reading in the story treats it as applying to the **dashboard**, and lets the navigation payload carry
a bounded name and code. The ruling's own words — *"HR may only see the project **name**"* — are a
**bounded** payload rather than an empty one, which is the reading the story took.

**QA does not settle it, and the reason is this file's own rule.** A case whose expected result is the
strict opposite of its criterion certifies something whichever way it lands: pass it as written and
`AC-105b-C` is a defect nobody noticed; pass the criterion and `TC-1-040` was certifying a payload
Karim did not forbid. **`TC-1-040` is marked `DISPUTED` and the Verifier must not run it.**

**This is also the second time this one payload has moved.** `TC-1-040` was itself reversed on
2026-08-21, from *"every project is listed"* to *"no project is listed"*, closing F-13. **The reversal
went one step past the ruling and the story did not follow it** — which is exactly the failure mode a
stable AC ID surfaces and a positional label hides. **`TC-1-041` is unaffected**: its half of
`AC-105b-C` — nothing financial, no `ProjectRead` — is in the criterion verbatim and is not in dispute.

---

### F-30 · `ProjectTeamRead` is specified in four stories and exists in no source file — **gap**

**Owner: Architect, to add the row; Backend, to land it with its test (SM-30).**

**F-24's "no story" half is closed and this is what replaced it.** D-051 Q32 said the ruling *"implies
a new narrow permission rather than granting HR `ProjectRead`"*, and that **naming it is the story's**.
The story has now named it: **`ProjectTeamRead`**, `ProjectScoped`, granted to `Role.Owner` and
`Role.Hr` with the same global reach `ProjectAssignmentManage` already gives both
[Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-115-project-team-panel.md:28, :36-39`], with
`AC-115-H` and `AC-115-I` written against it, and `AC-105b-E` routing HR to it.

**The identifier appears in `stories/questions-for-karim.md`,
`meetings/2026-08-21-sprint-1-refinement.md` and four story files, and in no file under `src/`**
[Verified: 2026-08-22, repository-wide search]. So four acceptance criteria across two `Ready` stories
name a permission the evaluator has never heard of.

**What this changes about four QA cases**, all relocked today:

- **`TC-1-127`** asserted *"HR cannot read the team it staffed"* against `KAFF-115 rule 5` — and
  `AC-115-H` says HR **does** read it, on the other surface. The case has been narrowed to the
  in-project panel, which `AC-115-H` still refuses HR. **It was asserting the opposite of the criterion
  it should have cited, and the criterion had no citation from anywhere.**
- **`TC-1-243`…`TC-1-245`** carried **NO STORY** and cited a D-number. They now cite `AC-115-H` and
  `AC-115-I` and are **BLOCKED on this finding** rather than uncovered.

**Two cases pointing in opposite directions at one criterion, and neither citing it, is the defect the
relock exists to expose** — and it would have stayed invisible under a positional label, because
neither case carried one.

**Under SM-30 the row and a test that names it land together**, and the row's comment must cite a test
name that exists — see **F-28**, which is what happens when it does not. `AC-115-H` cannot be executed
before the row exists, so the four cases above are unrunnable for a reason that is now one small piece
of work rather than an open question.

---

### F-31 · The coverage list for the three new rows names a test that covers a different row — **QA register defect**

**Owner: QA (this file's own registers).** Raised 2026-08-22 during the SM-31 citation migration,
by opening the source each citation named rather than converting the citation mechanically.

`qa/slice-1/permission-matrix.md` §"What this file now owes" says the three rows added on 2026-08-22
— `ProjectCreate`, `ProjectFinancialsEdit`, `UserRead` — are covered by **three tests**, and names
`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
`Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope` and
`Hr_may_read_the_user_list_and_still_reaches_nothing_financial`.

**The first of those three covers `ProjectManage`, which is not one of the three new rows**
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`
— it asserts `PermissionCatalogue.Of(Permission.ProjectManage).Scope` and a `ProjectManage`
evaluation, and names `ProjectCreate` nowhere]. **`ProjectCreate`'s cover is missing from the list:**
it is `Only_the_owner_and_the_technical_office_may_open_a_project`, repointed from `ProjectManage` to
`ProjectCreate` on 2026-08-22 [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`],
which is also the test the catalogue's own SM-30 comment cites on the `ProjectCreate` row
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectCreate`].

`qa/slice-1/test-cases.md` carries the same error in the other direction: *"Backend has since written
three tests"* followed by **four** cited locations. Four tests are in play — three written and one
repointed — and the count was never reconciled with the names.

**Why this is a finding and not a typo.** The list is the answer to *"are the three new rows covered?"*
— the exact question SM-30 exists to make answerable. Read as written it says `ProjectCreate` is
pinned by a test that would stay green if `ProjectCreate` were deleted, and it hides that the one test
which does pin `ProjectCreate` had to be **repointed** to do it. **Both registers are corrected in
place and the corrections cite the test methods**, so the next reader checks a name rather than a
count. Nothing in `src/` or `tests/` changes.

---

### F-32 · `TC-1-254`'s *"expected to fail on first run"* note is stale — **QA register defect**

**Owner: QA.** Raised 2026-08-22, same pass.

`TC-1-254` records that it *"also fails **today**, on the second half: the `ProjectManage` row cites
`Opening_a_project_needs_no_project` … and **is absent from `tests/`**"*. **That was F-28 and F-28 has
been fixed.** The `ProjectManage` row now cites
`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` and
`Only_the_owner_and_the_technical_office_may_open_a_project`, both of which exist
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`, @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`];
`Opening_a_project_needs_no_project` no longer appears in `src/` at all
[Verified: 2026-08-22, repository-wide search — it survives only in `proposals/N10-project-creation.md`
and in the entries that record the defect].

**`TC-1-254` is not retired and its expected result is unchanged.** The case asserts a property of the
catalogue — every row named in a test, every test name a row cites existing — and that property is
what it must go on asserting. **Only the prediction about the first run is withdrawn**, and it is
withdrawn because the code moved, not because the case was rewritten to match the code. **A case whose
expected result is read off the implementation cannot fail**, which is the one thing a QA case must be
able to do.

**Why it is filed rather than quietly deleted.** *"Expected to fail on first run"* is a scheduling
claim about a defect. Left standing after the defect is fixed, the first run reports a **pass where a
failure was predicted**, and a reader has to reconstruct which of the two is wrong. That is a day of
somebody's time, and it is exactly the decay SM-29 was written for.

---

### F-33 · `TC-1-255` presents a paraphrase of the catalogue as a direct quotation — **QA register defect**

**Owner: QA.** Raised 2026-08-22, same pass.

`TC-1-255` attributes to the `UserRead` row the words *"a `UserRead` endpoint returning the full user
row satisfies this permission while breaking the ruling"*, inside quotation marks. **The row does not
say that.** What it says is *"THE PERMISSION IS NOT THE WHOLE CONTROL — THE ENDPOINT'S PROJECTION IS.
Whoever builds the read endpoint projects name and role and stops. The user row also carries usernames,
departments and active state, and returning it would satisfy this permission while breaking the
ruling"* [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.UserRead`].

**The substance is unharmed — the catalogue does warn exactly this, on that row.** What is wrong is the
quotation marks, and they are what a reader trusts: a quoted sentence is checked by searching for it,
the search returns nothing, and the citation now looks broken when the claim behind it is sound. That
is the **loud** failure SM-31 wants, fired on the wrong target. The quotation is replaced with the
words that are in the file.

---

### F-34 · Four places in `qa/` say the money-touching list holds *eleven* permissions — it holds twelve — **QA register defect**

**Owner: QA.** Raised 2026-08-22 during the SM-31 bare-hint sweep, by opening the test each hint
pointed at.

`No_financial_permission_is_granted_to_a_bare_department` writes out the expected set of
money-touching permissions rather than reading it from the `TouchesMoney` flag — deliberately, *"so a
permission cannot quietly stop being financial and still pass"*. **That written-out set has twelve
members**, `ProjectFinancialsEdit` among them, added with the row on 2026-08-22
[Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`].

**`qa/` says eleven in four places** — `qa/questions.md` (twice, in the D-052 write-up),
`qa/risk-register.md` RSK-05, `qa/slice-1/permission-matrix.md` §F-04 and `qa/slice-1/test-cases.md`
`TC-1-215`. Every one was correct until the row landed. **`qa/slice-1/test-cases.md` `TC-1-250` says
twelve in the same file that says eleven**, which is how it was noticed.

**Why a count is worth a finding.** The number is the whole point of writing the list out. A reader
checking coverage compares the register's count against the test's, sees eleven against twelve, and
cannot tell whether a permission was dropped from the test or added to the catalogue without a test.
**That is one permission's worth of doubt about the money boundary**, which is the thing this project
tests first. All four are corrected in place with the reason and the date; **the test and the
catalogue are unchanged and were not touched.**

---

## Questions for Karim

Four, of which **two are answered** — QA-1 by the Architect and QA-4 by Nabil, neither of them Karim.
Each exists because a test case could not be written without the answer.

### ~~QA-1~~ · Is anyone on site allowed to confirm a site expense? — **ANSWERED 2026-08-21, D-052 §1**

**Answered by the Architect, and the answer is no:** *"Financial permissions like
`SiteExpenseConfirm` must never be granted to a bare department without specifying a role."* The rule
is absolute; there is no site-side exception to describe. **The defect it blocked is fixed the same
day** — the grant names `Role.Finance` and `Role.TechnicalOffice` + Operations/Administrative
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`], and `spec.md` §8's *"not the engineer"*
is now delivered by the catalogue rather than only asserted by this file.

**It never reached Karim.** The question was framed for him — *is there anyone on site who can?* — and
the Architect answered it as a rule about how financial grants are written. That is the right level:
QA's question was about the fix's shape, and the shape is not a business rule. Worth noticing, because
this file's own convention routes `QA-n` to Karim by definition, and one of them turned out not to be
his. **Closed:** the ⚠F-04 cells, and nothing is left blocking slice 6's `KAFF-608` from this side.

*(The original question is kept below, because the wording is what got it answered.)*

### QA-1 (as asked) · Is anyone on site allowed to confirm a site expense?

> *"When somebody on site spends money — a taxi for materials, a delivery — who signs it off? Is it
> always the office, or is there anyone on the site who can?"*

**Why QA needed it.** `spec.md` §8 says site expenses are entered *"by Finance or Admin, **not the
engineer**"*, and the catalogue let an engineer placed in Operations/Administrative confirm one (F-04).
The fix depended on the answer: if the rule is absolute, the grant must name roles; if there is an
exception, the exception has to be described before it can be tested.

### ~~QA-2~~ · What may HR see of a project? — **ANSWERED 2026-08-21, D-051 Q32**

**Karim:** *"HR may only see the project name and the list of assigned engineers … If the main project
dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that
contains zero financial details."*

**Closes F-03 and F-13.** What it opens is **F-24**: the permission that screen needs does not exist
and neither does the story. Note the answer's shape — **a separate surface, not a filtered view** —
which is the safeguard, and the thing a later session will be tempted to simplify away.

*(The original question is kept below, because the wording is what got it answered.)*

### QA-2 (as asked) · What may HR see of a project?

> *"HR puts people onto projects. To do that HR has to be able to pick the project from a list. What
> is HR allowed to see about a project — just its name and code, or something more? And can HR see
> every project in the company, or only ones that already have staff?"*

**Why QA needs it.** F-03 and F-13. HR holds no permission that shows a project name, and KAFF-105 AC3
says HR's `/api/me` lists every project. One of the two must give, and choosing would be inventing the
rule. **This is `ux/questions.md` Q-UX-3, which has never reached the BA register.**

**Blocks:** KAFF-105 AC3, KAFF-113's usability, HR's entire navigation.

### QA-3 · Must a deactivation carry a typed reason?

> *"When you switch someone's account off, should the system make you type why, or is that
> optional?"*

**Why QA needs it.** KAFF-110 AC4 makes it mandatory and refuses the request without one, citing
`CLAUDE.md`'s *"why where the flow requires it"*. Nothing in the domain enforces it —
`User.Deactivate` takes only a timestamp, and `IAuditContext.SetReason` is a voluntary call. So AC4 is
a rule the story asserts and no cited source states.

If the answer is yes, the same shape applies to every rejection gate in slice 5 and the mechanism
should be built once. If no, AC4 comes out of the story.

**Blocks:** `TC-1-086`. **Risk:** RSK-07. **Still unasked**, and D-049 did not touch it — ruling 3
covered passwords and lockout, not the reason on a deactivation.

---

### ~~QA-4~~ · Must the first Owner change the password he typed himself? — **ANSWERED 2026-08-21, D-052 §3**

**Nabil's ruling: no.** The forced-change rule of D-049 ruling 4 exists for an account created *for
somebody else* with a credential its creator knows. **The first Owner types his own password at the
setup screen; nobody else has ever known it**, so the non-repudiation the rule protects is not at
risk, and forcing a change would be ceremony. Recorded as a clarification nested inside the §9
amendment rather than as a new rule — *the scope of an existing rule, not an exception to it*.

**What this settles for QA.** `TC-1-006` had the forced change **left unasserted** deliberately;
it now has a definite expected result and the unasserted half can be written: **the first Owner is
not routed to the change-password screen.** `KAFF-100` **AC6** already states it —
*"the Owner types their own password and is not forced to change it"* — so the story and the ruling
agree and the AC needs no rewording. **RSK-19's QA-4 residue closes; RSK-19 itself stays open at High** — its four failure modes are about the endpoint's atomicity and lock, not the password. Story-level, no code.

**Why it needed answering at all is worth keeping:** QA and UX disagreed about whether it was Karim's
question or Nabil's, and the Scrum Master routed it to Nabil rather than resolving it. That is the
routing working — and it is the second `QA-n` in this file that turned out not to be Karim's.

*(The original question is kept below, because the wording is what got it answered.)*

### QA-4 (as asked) · Must the first Owner change the password he typed himself?

> *"When you set up the system for the first time you'll type your own password on the setup screen.
> Should the system still make you change it the first time you sign in?"*

**Why QA needed it.** D-049 ruling 4 forces a change on first sign-in, and Karim's reason is
non-repudiation — *"the Owner does not know the credential that acts as that user."* **That reason
does not apply to the first Owner**, who typed his own password on the setup screen (D-051 Q31), so
nobody else ever held it.

So `TC-1-006` was written for what was certain — **no third party ever learns that credential** — and
the forced change was left unasserted. Asserting either way would have been inventing a rule.

---

## What QA did **not** do

- **Did not resolve any `PENDING`.** Sixteen criteria across eleven stories have no case, and each one
  says which question would give it one.
- **Did not adjust an expected result to match the code.** Cases still expected to fail on first run:
  `TC-1-129`…`TC-1-133`, `TC-1-160`, `TC-1-162`, `TC-1-163`, `TC-1-164`, `TC-1-168`, `TC-1-174`,
  and the F-26 group (`TC-1-225`, `TC-1-230`, `TC-1-233`). That is the correct state for cases written
  before the code, **and none of them is a live defect any more** — `TC-1-067`, `TC-1-068`,
  `TC-1-082`, `TC-1-084`, `TC-1-213`, `TC-1-214` became regression cases when D-048 fixed F-10 and
  F-11, and **`TC-1-215` joined them on 2026-08-21 when D-052 §1 fixed F-04**.
  **Adjusting an expected result to match a *ruling* is a different act and was done four times**
  (SM-12), each citing the ruling: `TC-1-019` (D-051 N5 — the replayed cookie **is** accepted),
  `TC-1-003` (D-051 Q31 — the actor is the new Owner, not null), `TC-1-143` (split, because
  `KAFF-118` AC1b requires four records and AC1 one), and `TC-1-086`, which was **demoted to
  `PENDING Q35`** rather than corrected, because nothing states the answer. **Matching code is what
  QA must never do; matching a decision is the whole job.**
- **Did not write cases for slices 2–9.** `qa/strategy.md` names what each suite owns and
  `qa/risk-register.md` names the specific tests slices 3, 5, 7 and 8 must carry — but the cases
  themselves wait for the stories, per `process/agile.md`'s *"refine one sprint ahead, no further"*.
- **Did not verify anything.** Nothing in `qa/` has been executed. The Verifier runs it in a fresh
  session, and `qa/README.md` says what it may and may not do with a case it cannot execute.

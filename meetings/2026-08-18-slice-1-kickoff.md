# Slice 1 kickoff — BA · UX · Architect

**Date:** 2026-08-18
**Present:** BA agent · UX agent · Architect agent (independent review) · Architect (slice 0 author)
**Convened by:** Nabil
**Purpose:** absorb Karim's two answers, review the slice-0 foundation from three sides, and agree
what blocks slice 1.

---

## Karim's answers, logged

| Question | Answer | Applied |
|---|---|---|
| Is the Owner globally scoped, or does it need a project assignment? | **Global.** "Owner role is like the admin so yes global." | `ProjectAccessPolicy` — the Owner reaches any project that exists, without an assignment row. Reach only: capability still comes from the permission catalogue. |
| Who assigns users to projects? | **Owner and HR.** | `ProjectAssignmentManage` granted to `Role.Owner` and `Department.Hr`. |

Both are recorded in `decisions.md` D-010 and D-012, with the tests rewritten to express the new rule.

---

## 1. Three defects found and fixed during the meeting

These were verified against `spec.md` before any code changed. Details in `decisions.md` D-034–D-036.

### 1.1 تشوينات was modelled with the wrong sign — spec.md §15 could not be posted

The Architect review walked the acceptance table and found that **Extract 1 of spec.md §15 had no
legal representation in the model.**

`300,000 − 60,000 − 75,000 + 75,000 = 240,000`. تشوينات **adds** to what the client pays: the client
pays 75% of the value of material delivered to site but not yet built into certified work. That is
money received for work not yet done — the same shape as `ClientAdvance`. It was modelled as an
**asset with a non-negative floor**, which made the only legal posting direction the one that
*reduces* the client payment. Extract 1 would have netted 90,000, and the correct posting would have
been rejected by the system's own balance guard.

It survived slice 0 because **the Architect's own test posted it in the wrong direction.** The
replacement test asserts that issue and recovery move in *opposite* directions — a property a wrong
sign cannot satisfy.

**Fixed.** `MaterialAdvance` is now `Liability` / `Credit`, floor retained.

> **The finding behind the finding.** Slice 0 shipped no test of the §15 worked example, though
> CLAUDE.md ranks it first. A structural test of the account catalogue passed while the catalogue
> said something economically false. **The §15 fixture must exist before slice 3 opens** — failing or
> skipped, but present, so the gate is a build outcome rather than a judgement call.

### 1.2 The client portal boundary was one careless endpoint from leaking

Two independent paths, both found by UX reading the catalogue rather than the portal code — which is
the point, because the portal code does not exist yet.

1. **`ProjectRead` granted `Role.Client`.** Any internal endpoint requiring only `ProjectRead` — a
   project header, a summary, a BOQ view, the obvious permission to reach for — was reachable by a
   portal user, because the access policy matches their client to the project and lets them through.
2. **A grant written against a department alone matches any role carrying it**, and nothing stopped a
   `Role.Client` user being given `Department.Hr`. That would have handed a client `EmployeeManage`:
   company-wide, evaluated with no project check and no client check at all.

**Fixed.** `Role.Client` removed from `ProjectRead`; portal access runs through `PortalRead` and
`PortalApprove` only. Clients and subcontractors can no longer hold a department. Both pinned by tests.

### 1.3 Every money figure would have rendered in Arabic-Indic digits

`ar-EG` defaults to the `arab` numbering system, so `formatMoney(1234.5)` returned `١٬٢٣٤٫٥٠`.
`styles.css` claimed to prevent this with `font-variant-numeric: lining-nums`, which selects a glyph
style for digits that are *already* Latin and cannot change what `Intl` emits. The comment and the
code said opposite things.

**Fixed.** Locale pinned to `ar-EG-u-nu-latn`, calendar pinned to `gregory`, and `t()` now wraps
interpolated values in U+2068/U+2069 bidi isolates so that `KF-2026-014` stops rendering as
`014-2026-KF` inside an Arabic sentence.

---

## 2. Two new blockers on slice 1 that nobody had raised

Both were found independently by BA and UX, which is why they are here rather than in a backlog.

### 2.1 Nobody can create a user — there is no `UserManage` permission

Slice 1 is *"auth, roles, assignment, audit, Client master"*. The permission catalogue has 26 members
and **none of them covers creating or editing a user.** So slice 1 cannot create the HR user that
Karim's own answer requires, and the bootstrap has to be a database seed.

Worse, once the endpoint exists it becomes the most privileged operation in the system: because
grants can be written against a department, **whoever can set a user's department can grant
project-assignment power.** That permission must be designed deliberately, not added by whoever
writes the screen.

**Blocks slice 1.** Needs a decision on who holds it before the endpoint is written.

### 2.2 HR is a department, and every user must hold one of §9's eight roles — none of which is HR

Karim gave HR a permission. But `User` requires a `Role`, the eight roles are
`Owner · Finance · TechnicalOffice · SiteEngineer · HeadOfDesign · MarketingSales · Client ·
Subcontractor`, and none is HR. So Kaff's HR person must be created as *some* role plus
`Department.Hr` — **and they inherit that role's entire grant set.**

Concretely: HR created as `Role.Finance` inherits `FinancialMovementPrepare`,
`FinancialMovementDisburse`, `TreasuryPostProject`, `TreasuryPostCompany`, `AccountManage`,
`SupplierManage` and `PeriodClose`. Karim answered a question about assignments and may have handed
HR the treasury.

**Blocks slice 1.** The user-creation form cannot be designed until it is answered, and it cannot be
answered by us.

---

## 3. Where the three agents disagreed with slice 0, and what was accepted

| Finding | Source | Verdict |
|---|---|---|
| تشوينات sign, portal boundary, Arabic numerals | Architect / UX | **Accepted and fixed** (§1) |
| The Owner branch skipped the active-user check the class documented, so a deactivated Owner kept unrestricted reach until token expiry | Architect | **Accepted and fixed.** `ProjectAccessPolicy` now re-reads the user on every request and refuses if inactive or if the token's role no longer matches the database |
| `accounts` was freely mutable — one `UPDATE` could switch off the safe floor or invert every balance in the system | Architect | **Accepted and fixed.** A `BEFORE UPDATE` trigger freezes account configuration; rename, close and reopen stay legal |
| A reversal could itself be reversed, walking a chain around the hold rule indefinitely | Architect | **Accepted and fixed.** A reversal cannot be reversed |
| `HoldRelease` was unconditional — nothing enforced "in full" | Architect | **Accepted and fixed.** A deferred trigger requires the hold to be exactly zero after any release. The *handover* precondition stays with the handover flow, as before |
| `PermissionEvaluator`'s doc comment stated the opposite of the implemented rule after D-010 | BA and Architect, independently | **Accepted and fixed** |
| The Owner's access returned `AssignmentLevel.Standard`, so the first grant carrying a minimum level would silently deny the Owner | Architect | **Accepted and fixed.** Returns `Supervisor` |
| `FindMissingGuardsAsync` checked four of nine guards | Architect | **Accepted and fixed.** All triggers and indexes now verified |
| Guard header claimed PostgreSQL 14; `NULLS NOT DISTINCT` needs 15 | Architect | **Accepted and fixed** |
| `using System.Text.Json.Nodes` before `System.Text.Json` — an IDE0055 build break under warnings-as-errors | Architect | **Accepted and fixed** |
| D-005's deadlock-freedom claim is false: locks are ordered *within* a row's trigger, not across a multi-posting transaction | Architect | **Accepted, not yet fixed.** Real, and it needs the transaction seam below. Tracked as action A3 |
| The audit interceptor misses `ExecuteUpdate`/`ExecuteDelete`, disconnected updates, and clears the reason before the save succeeds | Architect | **Accepted, not yet fixed.** Tracked as A4 |
| `PostingType.Adjustment` plus an unconstrained account pairing *is* the free-form journal entry §1 forbids; D-029's test is theatre while it exists | Architect | **Accepted in principle.** The fix is the posting-type × account-pair legality table (A2), which subsumes it |
| Slice 0 over-modelled: loan, VAT, equity, prepayment and accrual account types for assumptions Karim has not answered | Architect | **Partly accepted.** The vocabulary is cheap and §6.2 warns against building cash-only; but D-030 already refuses to *seed* them. Revisit at slice 7 rather than churn now |
| `Employee`/`Worker` decided too early (D-016) | Architect | **Accepted as timing.** The conclusion stands; the entity should not have shipped in slice 0. Left in place, flagged, and slice 2 may replace it freely |
| D-014 "blocks the UX agent" is wrong | UX | **Accepted.** Slice 1 renders no project status anywhere. It blocks **slice 4**, not UX now |
| spec.md §9 now contradicts the code, and CLAUDE.md says spec.md wins | BA | **Accepted — highest-priority documentation action.** See A1 |
| CLAUDE.md says تم تأجيلها; agents.md says متأجلة | BA | **Accepted.** Two spellings of a word required "verbatim" is a defect in the continuity files. Needs Karim's word before either is corrected |
| JWT in `localStorage` is XSS-readable in a system holding real money | UX | **Open.** A real decision, not a default. Deferred to the slice-1 auth design, where it belongs |
| Money crosses the wire as a JSON number and becomes a JavaScript `double` | UX | **Open, and sharper than it looks.** Minimum position agreed now: **the frontend performs no money arithmetic, ever** — every total comes from the server. Whether money crosses as a string is a slice-3 decision |

---

## 4. What each agent needs from the others

**BA → Architect:** record *how* project access was granted on `AuditRecord` — assignment,
Owner-global, or client-of-project. One field, and it must land before there are records to backfill.
The Owner is now the one actor whose authority leaves no row anywhere.

**BA → UX:** build the project team panel from `ProjectAssignment` rows, never from the access check,
or Karim appears on every project team in the system.

**UX → Architect:** an `/api/me` endpoint returning role, department, assignments *with level*, and
the evaluated permission set. Role alone cannot drive navigation — HR is a department and engineer
seniority is per-assignment — so without it the frontend re-implements `PermissionCatalogue` in
TypeScript and the two copies drift. That is precisely the failure D-012 designed the catalogue as
*data* to prevent.

**UX → BA:** password rules (spec.md is silent), the client duplicate-phone interaction, and whether
Marketing or Finance sets the withholding category on a Marketing-owned record.

**Architect → BA:** confirm the تشوينات direction in one sentence with Karim, get the bank list, and
confirm which ledgers actually have hard floors — spec.md names two, slice 0 assumed five.

---

## 5. Questions for Karim

Ordered by what they block. The first three stop slice 1.

| # | Question, as Nabil should ask it | Blocks |
|---|---|---|
| 1 | **"The person in HR who puts staff onto projects — what else is their job here, and should they be able to see money?"** | Slice 1 |
| 2 | **"Who is allowed to create a user account and decide what someone can do?"** | Slice 1 |
| 3 | **"Should HR be able to put someone on any project in the company, or only on projects HR has been put on first?"** | Slice 1 |
| 4 | **"Besides opening any project, should you personally be able to add a new client, a supplier, or a bank account — or is that only for the department that owns it?"** Today "admin" stops at the project boundary: the Owner cannot create a client, and Client master is in slice 1 | Slice 1 |
| 5 | **"Can the same engineer be the supervisor on one site and a junior on another, or is he one or the other everywhere?"** | Slice 1 (deferrable — the current model is the superset) |
| 6 | **"Apart from you, who should be able to see the history of who changed what?"** Currently the only globally-reaching actor is also the only reader of the trail that watches him | Slice 1 (deferred by dropping audit-read from the slice) |
| 7 | **"When you see متعثرة next to a project, does it mean work has completely stopped on site, or that work is still running but late and going badly?"** Plus: is تم تأجيلها a pause you take with the client, do you write these five words on the whole project or on the unit, and does انتهت mean site-finished or file-closed-and-money-collected? | Slice 4 |
| 8 | **"At extract 1 the client pays an extra 75,000 for material delivered to site, deducted from later extracts as it is installed — correct?"** Confirms the D-034 fix | Slice 3 |
| 9 | **"Which banks — QNB, CIB, الأهلي, others?"** §6.5 defaults client collections to bank; §15 cannot be reconciled without one | Slice 3 |
| 10 | **"Do any of your bank accounts have an overdraft?"** And: do the hold, firm advance and عهدة ledgers have hard floors, or only the safe and the client advance? spec.md names two; slice 0 assumed five | Slice 3 |
| 11 | **"When work restarts on a project you had stopped, does it carry on from where it was?"** | Slice 4 |
| 12 | *(accountant, not Karim)* Rounding direction, and whether the contractual figure on an extract is 2 or 4 decimals | Slice 5 |

Question 12 and the FluentAssertions licensing question are Nabil's to route — neither belongs on a
list Karim reads.

---

## 6. Actions

**Documentation, before any slice-1 code**

- **A1 — amend spec.md §9** with a dated, visible superseding note recording Karim's Owner-is-global
  answer. Until then the code contradicts the business truth, CLAUDE.md says the business truth wins,
  and a Verifier reading spec.md in a fresh session would be right to fail slice 0. *BA owns this.*
- **A1b —** resolve تم تأجيلها vs متأجلة once Karim confirms, and make spec.md the single home for all
  five labels with CLAUDE.md and agents.md pointing at it.

**Before slice 3 opens**

- **A2** — a posting-type × account-pair legality table. Nothing currently constrains which posting
  types may move value between which account types in which direction; `HoldAccrual` can legally be
  posted `Safe → CompanyExpense` today. This is the highest-value missing guard and it subsumes the
  `Adjustment` problem.
- **A3** — a transaction seam, and fix the advisory-lock ordering to be per-transaction rather than
  per-row.
- **A4** — close the audit gaps: `ExecuteUpdate`/`ExecuteDelete`, disconnected updates, and clearing
  the reason only after a successful save.
- **A5** — the spec.md §15 fixture test, failing or skipped but present.
- **A6** — a `PostgresException` → `Error` translator for the `KAFF_` markers, so a refused payment is
  a translated message and not a 500.
- **A7** — write down the from/to convention: `from` is the credit side, `to` is the debit side. It is
  currently derivable only from the balances view.
- **A8** — something that creates a project's account set. `AccountTreeSeeder` builds company accounts
  only; slice 3 cannot post to a project until project accounts exist.

**Before slice 8**

- **A9** — the portal guarantees: a separate `/api/portal/*` surface with unshared response types, a
  single mandatory client-scoping helper, and a reflection test failing the build if a Domain entity
  or a cost-shaped property is reachable from a portal response.

**Still blocking, unchanged**

- **A10** — generate the EF migration and `package-lock.json` on a machine with the .NET 10 SDK and
  Node 22. Nothing in this repository has been compiled or run. Every finding above was made by
  reading; the first compile will find more. *This is the next physical action.*

---

## 7. One thing the meeting agreed to protect

Nobody puts a project status chip on any slice-1 screen "because it's useful". The five Arabic labels
are unmapped on purpose, slice 1 needs none of them, and a guessed mapping born on an assignment
screen would be indistinguishable from a decision.

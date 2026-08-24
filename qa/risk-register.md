# qa/risk-register.md — risks the tests must cover because nothing else will

Every entry names **what could go wrong**, **which slice it lands in**, and **the specific test that
must exist to catch it**. A risk with no named test is not managed; it is noticed.

**Revised 2026-08-21** against D-049, D-050 and D-051, and against **D-048**, which closed two of
these outright.

**Three of these are now deliberate** — protection that used to exist, removed on purpose, with a QA
scenario as the only thing left standing. RSK-01 and RSK-02 are Karim's of 2026-08-20 (D-044 ruling
8). **RSK-18 is his of 2026-08-21** (D-049 §8) and has the same shape: a database constraint traded
for a human decision.

**Three are closed and kept rather than deleted**, so nobody re-reports them: RSK-03 and RSK-04
(**D-048**, 2026-08-20 — the token now supplies only the user id) and RSK-11 (**D-049 §9** — the
withholding rate left the client record).

| # | Risk | Slice | Severity |
|---|---|---|---|
| RSK-01 | `FirmAdvance` lost its non-negative database floor | 3 | **High — accepted** |
| RSK-02 | `MaterialAdvance` (تشوينات) lost its floor | 5 | **High — accepted** |
| ~~RSK-03~~ | ~~The department claim is never revalidated~~ | 1 | **CLOSED — D-048** |
| ~~RSK-04~~ | ~~Company-wide permissions never revalidate liveness or role~~ | 1 | **CLOSED — D-048** |
| RSK-05 | ~~Two~~ **one** permission is granted by department with no role named — `PhotoPublish`, deliberately | 1 | **Low** — was High; **D-052 §1 closed the financial half** |
| RSK-06 | Nothing invalidates a **session** before it expires — narrowed to password change | 1 | **High** |
| RSK-07 | A mandatory reason is required by a story and by nothing else | 1 | Medium |
| RSK-08 | The audit interceptor is bypassed by `ExecuteUpdate` / `ExecuteDelete` | 3 | **High** |
| RSK-09 | An audit column cannot be added after the first records exist | 1 | **High** |
| RSK-10 | Databases seeded before 2026-08-20 keep the old ledger floors | 3 | Medium |
| ~~RSK-11~~ | ~~`Client.Create` accepts withholding on an individual~~ | 1 | **CLOSED — D-049 §9** |
| RSK-12 | The §15 fixture is written after the calculator | 5 | **High** |
| RSK-13 | The portal DTO is shared with the internal surface | 8 | **High** |
| RSK-14 | A green suite that ran nothing | all | **High** |
| RSK-15 | Money crosses the wire as a JSON number | 3 | **High** |
| RSK-16 | The firm-advance hard cap does not exist | 3 | **High** |
| ~~RSK-17~~ | ~~Six `Ready` stories depend on a `BLOCKED` story~~ | 1 | **CLOSED — backlog SM-1** |
| **RSK-18** | **The duplicate-client control moved from a database constraint to a human reading a warning** | **1** | **High — accepted** |
| **RSK-19** | **An unauthenticated endpoint creates the Owner** | **1** | **High** |
| **RSK-20** | **`SetWithholding` trusts a client kind the caller supplies** | **4** | **High** |

---

## RSK-01 · `FirmAdvance` lost its non-negative database floor — **accepted exposure**

**Slice 3.** D-044 ruling 8: floors are now `Safe`, `ClientAdvance` and `PettyCashAdvance` only.
`Hold`, `FirmAdvance` and `MaterialAdvance` no longer carry one.

**What could go wrong.** §6.4.3's firm advance is Kaff spending on a client's behalf. Recovery runs
against that ledger, and nothing now stops a recovery running **past zero** — which reads as *Kaff
owing the client on an advance the client never made*. D-044 states the residual protection plainly:
§6.4.3's hard **cap**, which is slice 3's to build and **does not exist yet**. So today there is no
floor and no cap: the ledger is unprotected in both directions.

**Why the database will not catch it.** The trigger reads `accounts.enforce_non_negative` from the
row. It is now false for this account type, on Karim's instruction. There is no application-level
substitute.

**The test that must exist.** In slice 3, against **real PostgreSQL**:

- *A firm advance recovery that exceeds the advance is refused.* Post a firm advance of 50,000, then
  recover 60,000. **Expected: refused.** *Fails if:* the posting succeeds and
  `account_balances.signed_balance` reads −10,000.
- *The exposure is visible.* §6.4.3 requires *"aggregate exposure across all projects visible on the
  owner dashboard"*. Assert the aggregate equals the sum of the project ledgers and is never a stored
  column.
- *A sequence of legitimate recoveries lands exactly on zero* — the positive case, so the refusal
  above is not implemented by refusing everything.

**Revisit if** the ledger goes negative in practice. Karim's own condition: *"That is the signal the
floor was doing work."*

---

## RSK-02 · `MaterialAdvance` (تشوينات) lost its floor — **accepted exposure**

**Slice 5.** Same ruling.

**What could go wrong.** §15's invariant *"تشوينات in equals تشوينات recovered"* is **no longer
enforced at the point of posting**. A recovery larger than what was advanced, or a sequence that ends
non-zero, now posts cleanly. D-044: the residual protection is *"the §15 reconciliation in slice 5,
which catches it later and more expensively"* — later meaning after the extracts are issued and the
client has paid.

**Why this one is worse than it sounds.** The §15 worked example has تشوينات moving in **once**
(+75,000 at extract 1) and out across **two** extracts (−45,000, −30,000). The sign of that flow was
backwards in slice 0 and was corrected by D-034 — **and the correction is still unconfirmed** (Q-BA-14
is one sentence to Karim and it has not been asked). So the ledger with no floor is also the ledger
whose direction nobody has confirmed.

**The test that must exist.** In slice 5, and the §15 fixture is where it belongs:

- *The §15 table posts end to end and تشوينات nets to exactly zero.* Not "approximately", not
  "rounds to". Exactly, at `decimal(18,4)`.
- *A recovery larger than the outstanding material advance is refused.* Advance 75,000, recover
  80,000. **Expected: refused.** *Fails if:* it posts and the ledger goes negative — which nothing at
  the database will now prevent.
- *A project that reaches handover with a non-zero `MaterialAdvance` balance is reported.* Since the
  posting-time guard is gone, the reconciliation must be an assertion somewhere, not an
  after-the-fact query somebody might run.

---

## ~~RSK-03~~ · The department claim is never revalidated — **CLOSED, D-048, 2026-08-20**

**Was:** `ProjectAccessPolicy` re-read `IsActive`, `Role` and `ClientId` and **not** the department,
which came from a token claim. Two permissions are granted by department, so a move did not take
effect on the next request in either direction.

**Closed by D-048.** `IPermissionSubjectReader` takes **only the user id** from the principal and
reads role, department, sub-department, client scope and liveness from the users table on every
authorized request. `ProjectAccessPolicy` lost its own re-read, because the subject reaching it is now
already database-derived.

**Regression cover, and it must not be deleted as duplication:** `TC-1-067`, `TC-1-068`, `TC-1-214`,
plus the upstream unit `A_stale_department_claim_grants_nothing`. **They are no longer expected to
fail.** A case still asserting this defect would report a fixed hole, and the next session would
either "fix" it again or lose an hour proving it was never broken.

**What is NOT closed, narrowed 2026-08-21:** the reason the department axis mattered was that
`SiteExpenseConfirm` and `PhotoPublish` were granted by department **with no role named** — D-048's
own closing line was *"every such grant is a standing invitation to this defect. There are two left."*
**D-052 §1 removed one of the two**: `SiteExpenseConfirm` now names `Role.Finance` and
`Role.TechnicalOffice` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`]. **`PhotoPublish`
is the last** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] and is deliberately left, the ruling being scoped to financial permissions.
**RSK-05 is downgraded, not closed.**

---

## ~~RSK-04~~ · Company-wide permissions never revalidate liveness or role — **CLOSED, D-048, 2026-08-20**

**Was:** `PermissionAuthorizationHandler` called `IProjectAccessPolicy` only when the request resolved
a project, so every `CompanyWide` permission was decided **entirely from a token**. A deactivated
Owner kept `UserManage` — which mints logins, including a new Owner — and **a deactivated Finance user
could still move company money** through `TreasuryPostCompany`.

**Closed by D-048**, the same fix as RSK-03. **Verified the right way:** the handler was reverted to
the claims-based build and the suite re-run — **five tests failed**, the three new ones and two
existing ones. A fix is not verified until you have seen what red looks like.

**Regression cover:** `TC-1-082`, `TC-1-084`, `TC-1-213`, and the units
`A_deactivated_user_loses_company_wide_permissions_too` and
`A_deactivated_owner_cannot_administer_users`. **Keep the company-wide and project-scoped halves as
pairs** — the original defect existed precisely because only the project-scoped half had ever been
written, and deleting one as a duplicate recreates it.

**The lesson outlives the fix**, and it is the third entry with this shape after D-046: both tests
that proved revocation worked used project-scoped probe routes. They passed, for a reason that did not
generalise. **A green result is not evidence until you know what red would have looked like.**

---

## RSK-05 · ~~Two~~ **one** permission is granted by department with no role named — **downgraded 2026-08-21**

**Slice 1**, and it grows with every slice that adds a department grant.

**The financial half is fixed.** **D-052 §1** — the Architect: *"Financial permissions like
`SiteExpenseConfirm` must never be granted to a bare department without specifying a role."*
`SiteExpenseConfirm` now grants to `Role.Finance` and to `Role.TechnicalOffice` **+**
Operations/Administrative [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`], so the case
below — a Site Engineer confirming a site expense — **cannot happen**. Held by
`A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`],
and the *class* is held by `No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]
across ~~eleven~~ **twelve** money-touching permissions — the count was eleven until
`ProjectFinancialsEdit` joined the list on 2026-08-22 and was never updated here; **F-34**. That is
what stops this risk reappearing on a different row.
**F-04 closed; QA-1 answered.**

**What is left is `PhotoPublish`** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`], still a bare-department grant and
**deliberately** so — a photo moves no money, and extending a ruling nobody gave would be worse than
the gap. **It is the last one and it needs its own ruling.** Severity **Low**: the worst case is a
site engineer moved into Operations/Administrative publishing a photo, which §9 arguably permits
anyway.

**What could have gone wrong**, kept because the mechanism is what generalises. Both permissions were
granted to Operations / Administrative with **no role named**, so any role placed there held them.
`spec.md` §8 excludes the site engineer from confirming site expenses **by name** — and `User.Create`
will place a Site Engineer in Operations/Administrative without complaint.

**This mechanism has already leaked twice.** D-035: `Role.Client` holding `ProjectRead` made the whole
internal project surface reachable from the portal. D-044 ruling 2: an HR user parked in
Operations/Administrative would have inherited `SiteExpenseConfirm` — closed by a *second* mechanism
(`User.Create` pins HR to `Department.Hr`), not by the catalogue, because the catalogue cannot do it.
**The same hole is still open for every other role.**

**The tests that must exist.** `TC-1-215` — **now regression cover on its Domain half**, with the Api
half unrunnable until slice 6 (KAFF-608); `TC-1-066` (no department-only grant can ever match
`Role.Hr` — a case that fails the moment somebody writes one); and `TC-1-209` (every grant cites
`spec.md`, so a new department grant cannot arrive silently).

**A cheaper structural fix exists and is not QA's to choose:** require every grant to name a role, or
require a department grant to name the roles it applies to. Raised in `qa/questions.md` F-04 —
**and taken**: the Architect chose the first, as a rule about financial permissions rather than about
one row.

---

## RSK-06 · Nothing invalidates a session before it expires — **narrowed, and still open**

**Slice 1.** `User.SecurityStamp` is rotated by `Deactivate` and by `SetPasswordHash`, and **nothing
compares it to the claim on the session.** D-051 N5 states it plainly: *"`KaffClaimTypes.SecurityStamp`
is defined and `User.SecurityStamp` rotates … but **nothing compares the two.** The global kill is
declared, not implemented."* It is assigned to **KAFF-101a**.

**Narrowed by D-048, and this is the part to get right.** The per-request user re-read now covers
**deactivation, role change and department change on both scopes** — D-048 explicitly **rejected** a
stamp in the token as the fix for those, because it *"only closes liveness, not the role and
department staleness"*. So the stamp's remaining job is the one case the row re-read cannot see:

- **A password change**, where the user row still says active, same role, same department. D-049
  ruling 2 requires every other session to die; nothing makes that happen.
- **A password reset** (D-051 Q38), which is a password change and inherits the same requirement.
- **Sign-out on this device**, which the stamp *cannot* deliver — it is one value per user, so
  rotating it would sign the user out everywhere, which D-049 ruling 2 forbids. **D-051 N5 accepts
  that limit rather than hiding it:** with no per-session identity there is no way to revoke one
  *other* device. Losing a phone means signing out everywhere.
- **`Reactivate` does not rotate at all** — D-051 N5 names it as *"the one path that should rotate and
  does not"*.

**What could go wrong.** An engineer whose phone is lost on site changes his password from the office
machine and the phone's session keeps working until it idles out.

**The tests that must exist.** `TC-1-019` (sign-out), `TC-1-225` (the stamp comparison itself),
`TC-1-230` (password change kills other sessions), `TC-1-233` (reset kills all sessions), `TC-1-097`
(reactivation rotates). All expected to fail.

**Two traps, both named in D-051 and both worth failing a review over.** A check with a *"skip when
the claim is absent"* fallback is **worse than an absent one**, because it looks implemented — so
`TC-1-225` must run against a session **without** the claim too. And `TC-1-225` must rotate the stamp
**without changing anything else about the user**, or D-048's row re-read will refuse the request for
an unrelated reason and the case will pass with no comparison in the code.

**These ACs must not be quietly reworded to match what is built.** If a session store is the answer
instead, that is the Architect's decision, not a QA rewrite.

---

## RSK-07 · A mandatory reason is required by a story and by nothing else

**Slice 1**, and it matters most from slice 3.

**What could go wrong.** `KAFF-110` AC4 makes a reason **mandatory** on deactivation. In the domain,
`User.Deactivate(DateTimeOffset)` takes no reason, and `IAuditContext.SetReason` is a voluntary call a
handler may forget. Nothing refuses a save whose flow requires a reason and did not get one.

From slice 3 this becomes §7's rule — *"Any rejection at any gate returns the extract to Draft with a
written reason … Never a silent step-back"* — and a voluntary mechanism will produce reasonless
rejections the first time somebody writes a handler in a hurry.

**A known adjacent gap:** kickoff action A4 records that the reason is **cleared before the save
succeeds**, so a retried or partially-failing save can lose it.

**The tests that must exist.** `TC-1-086` (refused with no reason — expected to fail on first run),
`TC-1-087` (stored verbatim, in Arabic), `TC-1-147` (survives the save). In slice 5, one per rejection
gate.

---

## RSK-08 · The audit interceptor is bypassed by `ExecuteUpdate` / `ExecuteDelete`

**Slice 3**, due before it opens (kickoff action A4).

**What could go wrong.** The interceptor runs on `SaveChangesAsync`. `ExecuteUpdateAsync` and
`ExecuteDeleteAsync` do not go through the change tracker, so a bulk update writes **no audit record
at all** — silently, with no error, on a code path that looks like normal EF Core. Disconnected
updates have the same shape.

CLAUDE.md makes the audit record non-negotiable for every state change. This is the way that
guarantee stops being true without anybody deciding it should.

**The test that must exist.** In slice 3, against real PostgreSQL: *a bulk update through
`ExecuteUpdateAsync` on any audited entity either writes records or is refused.* **Expected: no silent
success.** *Fails if:* the row changes and the audit count does not. Consider an analyzer or an
architecture test banning `ExecuteUpdate`/`ExecuteDelete` on audited types — a test that fails at build
time is cheaper than one that fails at 2am.

---

## RSK-09 · An audit column cannot be added after the first records exist

**Slice 1 — and the window closes at the end of it.**

**What could go wrong.** `AuditRecord` is append-only, enforced by a database trigger. **A column
added after slice 3 cannot be backfilled, because the rows cannot be updated — by design.**
`KAFF-116` adds the grant-path field for exactly this reason: *"This story is cheap now and expensive
later."*

If slice 1 ships without it, every record written between then and whenever somebody notices will
permanently be unable to say by what authority its actor reached the project — and the Owner is the
one actor whose authority leaves no row anywhere.

**The tests that must exist.** `TC-1-129` … `TC-1-133` (the four grant paths and the null case),
`TC-1-135` (an update is refused at the database, which is *why* the backfill is impossible).

---

## RSK-10 · Databases seeded before 2026-08-20 keep the old ledger floors

**Slice 3.** D-044 ruling 8 changed no SQL: the trigger reads `accounts.enforce_non_negative` from the
row, so the rule is **data**. But **guard 3c freezes an account's configuration after creation**.

**What could go wrong.** Any database created before the ruling carries the old floors on accounts
already opened, and they cannot be changed. Today only `SAFE-MAIN` exists and it is floored either
way — but *"a project created against an old database would carry a floored hold"*, and a floored hold
refuses a legitimate posting at insert time with a message about a rule Karim has withdrawn.

**The test that must exist.** In slice 3: *a project's account set is created with exactly the three
floors Karim named* — `Safe`, `ClientAdvance`, `PettyCashAdvance` — *and no others.* **And an
environment check:** assert the floors on an existing staging database match the current rule, since
guard 3c means a mismatch is unfixable in place and needs the account recreated.

---

## ~~RSK-11~~ · `Client.Create` accepts a withholding category on an individual — **CLOSED, D-049 §9**

**Was:** neither `Client.Create` nor `Client.SetWithholding` checked the combination, and `spec.md`
§6.7 answers it plainly. D-040 called it a defect; D-045 confirmed it open.

**Closed by D-049 rulings 9 and 10, which did more than fix it.** The category left the client record
entirely: `Client.WithholdingCategory` is gone, `Project.WithholdingCategory` exists and defaults to
`None`, `Project.SetWithholding(category, clientKind)` refuses a rate on an individual's contract, and
`Client.SetWithholding` became `Client.SetTaxRegistration`, which refuses a registration number on an
individual. **Six Domain tests pin it** (`tests/Domain.Tests/WithholdingTests.cs`).

**Why the move was the right fix and not just a bigger one:** §6.7 sets the rate by *what is supplied*
and §5.4 links a design project to its execution project **for one client**, so one value per client
could never express 5% on the design and 1% on the execution. The defect and the model were the same
bug.

**What replaces it: RSK-20**, below. The guard now depends on an argument the caller supplies.

---

## RSK-12 · The §15 fixture is written after the calculator

**Slice 5**, prevented in **slice 3**.

**What could go wrong.** A fixture written after the code it checks is a transcription of that code.
It will pass, and it will pass whether or not the calculator is right. §15 is explicit that *"these
numbers are a test, not an illustration"* and that any change breaking them fails the build.

**The mitigation is already in the backlog and must not be dropped:** `KAFF-300` — *"The §15 worked
example as a fixture — present and failing before anything else is built."* The word *failing* is the
whole point.

**The test that must exist.** `KAFF-300`, committed red, before `KAFF-505`. **The Verifier should
check the commit order**, not just the final state.

---

## RSK-13 · The portal DTO is shared with the internal surface

**Slice 8**, and the boundary is drawn in **slice 1**.

**What could go wrong.** §12 is the most absolute sentence in `spec.md`. D-035 records that the
boundary was already drawn wrong once — `Role.Client` held `ProjectRead`, so any internal endpoint
needing only that permission was reachable by a portal user on their own project.

The next version of the same mistake is a shared response type: a `ClientResponse` used by both
surfaces, and somebody adds `Notes` or `TotalBilled` to it for an internal screen.

**The tests that must exist.** Now: `TC-1-171` (internal notes appear in nothing a `Role.Client` can
reach), `TC-1-043` (no trace of another client anywhere in `/api/me`), `TC-1-189` (the client list
refuses, and its refusal body carries no client name). In slice 8: `KAFF-811` — a reflection test that
**fails the build** if anything cost-shaped is reachable from a portal response. That is the strongest
form of this test and it should be brought forward if the portal surface appears earlier.

---

## RSK-14 · A green suite that ran nothing

**Every slice.** D-046, in full, is this risk realised three times in one afternoon.

**What could go wrong.** `dotnet test` reports `Zero tests ran` and exits 5 on this stack — an
SDK 10.0.400 / xunit.v3 4.0.0 / MTP 2.3.3 integration problem that is **not fixable from here**. CI
invokes the executables directly instead. A Verifier who runs `dotnet test`, sees no failures and
reports green has reproduced the exact defect.

The E2E suite has the same shape from the other end: every test carries `[E2EFact]`, which skips
itself when `KAFF_E2E_BASE_URL` is unset. Unconfigured, it is 4 skipped and exit 0.

**The tests that must exist — and already do, so the risk is that they are removed.**
`SuiteConfigurationTests`, a plain `[Fact]` that runs unconditionally and fails when `CI=true` and the
suite is unconfigured. **Plus a habit:** the Verifier records the **test count** it observed, not only
the pass/fail. A run of 0 passing and a run of 215 passing both read as "no failures".

---

## RSK-15 · Money crosses the wire as a JSON number

**Slice 3.** Open decision N2, and `ux/questions.md` Q-UX-15.

**What could go wrong.** A JSON number becomes a JavaScript `double`. `decimal(18,4)` does not survive
that round trip, and `CLAUDE.md` forbids floating point anywhere near money. The failure is not a
crash — it is a figure that is wrong in the fourth decimal place and reconciles to within a few
piastres, which is exactly the kind of error that gets absorbed by an invented adjustment (§6.7's
failure mode again).

**Already agreed as a minimum position:** the frontend performs **no** money arithmetic, ever. Every
total comes from the server.

**The tests that must exist.** In slice 3: *a figure with four significant decimals survives a
round trip through the API unchanged* — post `1234.5678`, read it back, assert equality as a decimal,
not as a formatted string. And an E2E case asserting the browser displays two decimals while the
server holds four (D-044 ruling 6). *Fails if:* the wire format silently truncates or the frontend
recomputes a total.

---

## RSK-16 · The firm-advance hard cap does not exist

**Slice 3.** §6.4.3 requires a firm advance to have *"owner approval, hard cap the system enforces,
aggregate exposure across all projects visible on the owner dashboard"*. Only the owner approval half
exists (`FirmAdvanceApprove`). **Combined with RSK-01 — the floor removed on the same ledger — the
firm advance is currently unbounded in both directions.**

**The test that must exist.** In slice 3: *a firm advance that would take the project past its cap is
refused*, and *the aggregate exposure figure equals the sum of the project ledgers and is derived, not
stored*. Who sets the cap and at what value is a question, not an assumption — see `qa/questions.md`.

---

## ~~RSK-17~~ · Six `Ready` stories depend on a `BLOCKED` story — **CLOSED, backlog SM-1**

**Slice 1 — a planning risk, not a code one.**

`KAFF-118`, `KAFF-120`, `KAFF-121`, `KAFF-123` and `KAFF-124` all depend on `KAFF-119`, which is
BLOCKED on Q-BA-8 and Q-BA-9. `KAFF-105` depends on `KAFF-101`, and `KAFF-106` on `KAFF-100`, both
BLOCKED. `stories/backlog.md` proposes *"the 14 Ready stories, 43 points"* as sprint 1.

**What could go wrong.** The pressure to unblock is real and the cheapest way to relieve it is to make
`Client.Code` optional, or to pick a duplicate-phone interaction, "just to get the client stories
moving". `stories/README.md` names that exact failure: *"A question is never closed by writing a
plausible answer into the story."*

**The mitigation.** The auth dependency is genuinely soft — the Api harness issues identities directly
(`TestAuthHandler`), so KAFF-105 through KAFF-124 are testable without a login endpoint, and
`backlog.md` says so. The `KAFF-119` dependency is **not** soft: four Ready stories need an endpoint
that creates a client, and its form has an undecided mandatory field.

**The check that must exist.** Before the sprint closes, confirm that no `Ready` story was completed
by resolving a question its blocking story owns. The tell is a rule in the code with no D-number.

---

## RSK-18 · The duplicate-client control moved from the database to a human — **accepted exposure**

**Slice 1.** Karim, 2026-08-21 (D-049 §8): a duplicate client phone **warns and does not block**, so
that *"a corporate client and its CEO might be registered as two separate entities sharing the same
contact number."* `ux_clients_phone` **was a unique index — the database refused the save outright.**
It is now `ix_clients_phone`, non-unique.

**This is the third accepted exposure in this register, and it has RSK-01's exact shape:** a database
constraint traded for a human decision, on Karim's instruction, leaving a QA scenario as the only
control. Recorded here so it is not rediscovered as a bug.

**What could go wrong.** Nothing now prevents two client records for one person. `spec.md` §2 said
*"deduplicated by phone"* and §3 said *"never create a duplicate client"*; both are amended, and a
human dismissing a warning is a well-understood failure mode. Two files for one client means a repeat
job lands on the wrong one, and §3's requirement that a reopened opportunity attach to the **same**
client silently stops holding.

**The failure mode inverted, and this is the sentence that matters** — D-049: ***"a missed match used
to mean a wrongly-accepted save; it now means a warning nobody sees."*** Under the unique index, a
normalisation miss produced a duplicate the database would eventually trip over. Now it produces
silence. **Matching is more load-bearing after this ruling, not less.**

**The tests that must exist.** `TC-1-152` (three formats, one warning, no refusal), `TC-1-153` (the
index really is non-unique — the case that catches somebody "restoring" the constraint), `TC-1-240`
(normalisation folds `+20 …`, `0020 …`, `010 …`, separators **and Arabic-Indic digits**), `TC-1-241`
(the warning names the client and is machine-readable), `TC-1-168` and `TC-1-181` (the edit path and
the archived match). **`TC-1-240` is the whole of the control** and belongs in the Domain suite, where
it runs on every commit.

**Revisit if** duplicate clients appear in the data. D-049 names the signal: **two client records with
one phone and overlapping projects.** Nothing watches for it today — that is a report nobody has
asked for, and it is the thing that would tell Karim his ruling cost more than he expected.

---

## RSK-19 · An unauthenticated endpoint creates the Owner

**Slice 1.** D-051 Q31 chose a one-time setup screen over a database seed, for an audit reason — Karim:
*"I do not want hidden database scripts. My name and account creation date must appear naturally in
the Audit Trail from day one."* A seeded account has no actor, and the first row in the trail would
name nobody.

**The cost, in D-051's own words:** *"an unauthenticated endpoint that creates an Owner, whose entire
correctness rests on an emptiness check. **It is the most privileged endpoint that will ever exist
here.**"*

**What could go wrong**, in the order it is likely to:

1. **The check is not atomic.** A read followed by an unrelated write is a check-then-act, and the
   race creates a **second Owner nobody authorised**. The username unique index does not save it — two
   concurrent requests carry different names. `TC-1-216`.
2. **"Locks permanently" is implemented as a flag.** Anything one `UPDATE` clears is not a lock.
   D-051 is explicit that it must be the emptiness test. `TC-1-218`.
3. **The guard asks "is there an Owner" rather than "is the table empty."** A database that has lost
   its Owner row is a support incident, not a re-open. `TC-1-217`.
4. **The request shape accepts a role.** An unauthenticated endpoint taking a role parameter is an
   unauthenticated user-creation endpoint. `TC-1-219`.

**Why it is worth a register entry and not just cases.** The endpoint is correct only while the
database is non-empty, which is a condition that holds forever in production and **never holds in a
test fixture or on a freshly restored staging database**. That asymmetry is what makes it easy to get
wrong and hard to notice.

**~~Also carried: `TC-1-006` and QA-4~~ — ANSWERED 2026-08-21, D-052 §3.** Nabil ruled that the first
Owner is **not** forced to change the password he typed: D-049 ruling 4 exists for an account created
*for somebody else* with a credential its creator knows, and nobody else ever knew this one. It is the
**scope** of the existing rule, not an exception to it. `TC-1-006` and `KAFF-100` AC6 now have a
definite expected result — he reaches the application and is not routed to the change-password screen.

**RSK-19 itself stays open at High.** The four failure modes above are about the endpoint's
correctness, not about the password, and none of them is answered by a ruling — they are answered by
`TC-1-216`…`TC-1-219` passing against a real PostgreSQL.

---

## RSK-20 · `SetWithholding` trusts a client kind the caller supplies

**Slice 4** (KAFF-416), created by **slice 1's** ruling.

`Project.SetWithholding(category, clientKind)` takes the client's kind **as an argument**. D-049
explains why: *"the client's kind is passed in rather than looked up, because the domain holds only
`ClientId` — and the rule is too expensive to leave to the caller."* The signature is right for the
model. It also means **the guard is only as good as the argument**.

**What could go wrong.** A handler that reads the kind from the request body, from a cached DTO, or
from a stale projection will pass `Corporate` for an individual, and the rule §6.7 exists to enforce
will accept the rate it was written to refuse. §6.7's failure mode follows: *"collections will never
match issued extracts and staff will invent adjustments to close the gap"* — a permanent 1–5%
shortfall, small enough to close by hand.

**Why nothing else catches it.** **None of the six Domain tests can.** They call
`SetWithholding(category, ClientKind.Individual)` directly and prove the entity refuses it — which is
exactly right, and exactly blind to a caller that lies about the kind. The domain cannot verify an
argument it was given instead of looking up.

**The test that must exist.** `TC-1-242` — the withholding endpoint called for a project whose client
is an `Individual`, with a request asserting `Corporate`, **still refused**. It is the only case in
the suite that can detect this, and it runs against KAFF-416.

**A cheaper structural fix exists and is not QA's to choose:** have the handler load the `Client` row
and pass `client.Kind`, and make that the only construction the code review accepts — or move the
lookup behind a domain service. Raised as `qa/questions.md` **F-25**.

---

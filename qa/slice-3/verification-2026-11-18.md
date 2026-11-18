# Verification — Module 3, the Treasury base — 2026-11-18

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (claude-opus-5-5), the strongest model; not downgraded.
**HEAD:** `792c1ba8d5933ebd43992118c77571023f53beb3` (working tree clean at start).
**Stories:** KAFF-300, 301, 302, 303, 305, 306, 316, 317, 319, 322; KAFF-304 (FOLDED) checked for where its rule lives.

Findings are written as they are reached. Sections marked *in progress* were not finished if the pass died.

## Gates at HEAD

*in progress*

Build at HEAD: `dotnet build KaffErp.sln -c Release` exit 0, 0 warnings, 0 errors.
Domain.Tests: exit 0, `total: 741`, `failed: 0`, `succeeded: 741`.
Api.Tests: exit 0, `total: 1165`, `failed: 0`, `succeeded: 1163`, `skipped: 2`. Matches the brief's last-known figures (Domain 741/741, Api 1163 pass 2 skip of 1165).

## Method

The dev API on 5080 (PID 4988) was stopped for the build. A scratch database `kaff_probe_m3` was
created from a `pg_dump` of the dev database (the dev database had one idle external session, so a
`TEMPLATE` copy was refused; nothing was terminated). A second API instance ran on port 5081 against
the scratch database for every live probe. Every request below is a real HTTP request with a real
sign-in cookie. Users created on the scratch database only: `vfin1`, `vfin2` (Finance), `vto1`
(Technical Office), `vprep`, `vsup`, `vse_other` (Site Engineer), `vhr` (HR), `vown2` (Owner).

## Findings, in the order they were reached

### F-1 (defect, high) — the application connects to PostgreSQL as a superuser; the least-privilege block never runs

`001_guards.sql` section 5 revokes `UPDATE, DELETE, TRUNCATE` on `postings` and `audit_records`
from a role named `kaff_app`, "applied only if the application role exists". No such role exists in
any environment. `docker-compose.yml`, `deploy/docker-compose.staging.yml` (line 44:
`Username=${POSTGRES_USER}`) and `appsettings.Development.json` all connect as the container's
`POSTGRES_USER`, which the official `postgres` image creates as a superuser
(`select rolname, rolsuper from pg_roles` on the dev cluster: `kaff|t`). The append-only triggers
still refuse a plain `UPDATE` and `DELETE` (proved below), but a superuser can disable a trigger or
set `session_replication_role = replica`, and the application's own connection holds that power.
The brief's hard check 1 ("raw SQL as the application's database role") therefore runs as a role
the guards cannot bind. See the SQL probe section for the proof.

### F-2 (defect, medium) — a partial `HoldRelease` through `POST /api/projects/{id}/treasury/postings` returns a bare 500

Project in `Handover` with a hold of 200,000. Finance posts `HoldRelease` of 150,000 from the hold
account to the client receivable. The domain allows it (it does not check "in full"), the database
trigger `trg_postings_hold_release_in_full` refuses it with `KAFF_HOLD_PARTIAL_RELEASE`, and
`DatabaseGuardTranslation.Map` (`src/Api/Common/Results/DatabaseGuardTranslation.cs:30-49`) has no
entry for that prefix, so the response is
`500 {"title":"An error occurred while processing your request."}` with no `code` and no
`messageKey`. The money is safe (no row is written, the hold still reads 200,000), but this is
exactly the case `AC-319-A` names: a guard firing without a prior domain refusal must come back as a
translated `ProblemDetails`, not a 500. The code comment at lines 21-24 says the prefix is out of
scope "per the story's own list"; the story's *Not in this story* section says no such thing, and
its rule 3 says every guard exception a posting insert can raise must map.
`DatabaseGuardTranslationTests.cs:51` lists the prefix as `outOfScope`, so the exhaustiveness test
was written to agree with the gap.

### F-3 (observation) — the sign request mixes units: `clientAdvance` is percent, the other two rates are fractions

`POST /api/projects/{id}/sign` with `holdPercentage:"0.20", clientAdvance:"0.25",
advanceRecoveryRate:"0.25"` (fractions throughout, as the brief instructs) returned 200 and stored
`advanceRate` **0.002500**, a 0.25% advance. Nothing refuses it. `clientAdvance` is a bare
`decimal` passed to `Percentage.FromPercent` (`SignProject/Handler.cs`), while the read model
returns the same figure as a fraction. The web client is consistent with this (the comment in
`boqs.api.ts:100` says so), so the screen is not wrong, but D-151 says a rate's unit is the fraction
in both directions, and an API caller who follows D-151 silently gets a contract with a 0.25%
advance. Outside module 3, recorded because it sits on the chain.

### F-4 (observation) — signing posts no client advance; there is no API to list a project's accounts

`spec.md` §15's first row is "Advance at signing: client pays 250,000". `SignProject` writes no
posting. The only way to record the advance is a hand-built `ClientAdvanceReceipt` through
`POST /api/projects/{id}/treasury/postings`, which needs the account ids, and no endpoint lists a
project's accounts (`GET /api/treasury/accounts?projectId=` is a 404; the only account read is a
balance by id). This pass read the ids from the scratch database. The existing chain test does the
same thing by inserting the posting through EF (`ClosureThroughExtractChainTests.cs`,
`NewProjectAsync`). The same is true of an owner injection. So the first row of §15, and the owner
injection the safe floor prompts for, have no route a person can actually use.

### F-5 (defect, critical) — the generic reversal route debits the hold after release, drives it negative, and lets it be released a second time

`POST /api/projects/{id}/treasury/postings/{postingId}/reverse` accepts any posting on the project,
including postings that belong to an extract or to the hold release. `ReversePosting/Handler.cs`
checks scope, reversal-of-reversal, already-reversed, period and project state (reversals are exempt
from the state table, `PostingProjectStatePolicy.BuildDefault`, `reversalsFollowTheTable: false`),
and nothing else. Proved on the scratch database, in order, on a project that had run the whole of
§15 cleanly to a released hold of zero:

1. Reverse extract 1's `HoldAccrual` posting (the extract is `Collected`, the project is in
   `Handover`, the hold has been released): **201**. The hold reads **-60,000.0000**. Extract 1 still
   reads `Collected` with a hold of 60,000, so the document and the ledger now disagree.
2. Reverse the `HoldRelease` posting: **201**. The hold reads 140,000.
3. `POST /api/projects/{id}/hold-release` again: **200**, `released: 140000.0000`. The hold has now
   been released twice, for two different amounts.

Every step is legal under the database guards: a reversal is exempt from `KAFF_HOLD_DEBIT`
(`001_guards.sql`, the `reverses_id IS NULL` clause), the hold carries no floor (D-044 §8), and
`trg_postings_hold_release_in_full` fires only for a forward `HoldRelease` from the hold. `spec.md`
§5.1: "It releases once, in full, at handover." `CLAUDE.md`: "If you write code that debits the hold
ledger before handover, you have misread the spec." The same route, before handover, can take an
issued extract's hold accrual back out of the hold while the extract stays issued. `AC-303-D`
sanctions reversing a hold accrual "made in error", but it does not sanction reversing one that a
live extract still owns, and no criterion anywhere sanctions reversing a hold release or debiting a
released hold. The fix belongs where every caller routes (refuse a generic reversal of a posting
whose source document is a flow that has its own reversal, such as an extract, a collection or a
hold release), not in this one probe's path.

The same gap lets a person reverse an extract's `ClientCollection` posting through the generic route
while the collection record stays unreversed and the extract stays `Collected`; that was not run
live, but the handler has no check that would stop it.

**F-5, why the second release went through.** `ReleaseHold/Handler.cs:54-59` treats a release as
"already released" only while it stands unreversed (`posting.ReversesId == null && !Any(other.ReversesId == posting.Id)`).
That is a deliberate allowance for correcting a release, and it is what turns a generic reversal of
the release into a second release.

### F-6 (defect, medium, latent) — `GET /api/projects/{id}/treasury/balances` returns 500 once a project has two عهدة accounts

`BalanceQueries.GetLedgerBalances` (`src/Domain/Treasury/BalanceQueries.cs`) reads each ledger with
`SingleOrDefault(balance => balance.LedgerKind == kind)`. عهدة is per employee
(`AccountScope.ProjectRequired` and `PartyType.Employee`, `AC-302-D`), and the unique index
`ux_accounts_project_dimension` allows one per employee per project, so a project with two
custodians has two `PettyCashAdvance` rows. Proved on the scratch database: two `PettyCashAdvance`
accounts inserted on a project (as a future عهدة flow would open them; no endpoint opens one yet,
`KAFF-311`), then `GET .../treasury/balances` returns **500**. The same method feeds
`ExtractSupport.Balances`, so extract preparation on that project would fail the same way. Latent
today because nothing opens a عهدة account, certain the day something does. The same shape applies
to a project with more than one client (`ClientAdvance`, `Hold` are per client party).

### SQL probes on the scratch database, as the application's own role (`kaff`, `rolsuper = t`)

| # | Attempt | Result |
|---|---|---|
| 1 | `UPDATE postings SET amount = 1 WHERE id = <advance receipt>` | refused, `KAFF_APPEND_ONLY ... UPDATE is not permitted` |
| 2 | `DELETE FROM postings WHERE id = <advance receipt>` | refused, `KAFF_APPEND_ONLY ... DELETE is not permitted` |
| 3 | `TRUNCATE postings CASCADE` | refused, `KAFF_APPEND_ONLY ... TRUNCATE is not permitted` |
| 4 | `BEGIN; SET LOCAL session_replication_role = replica; UPDATE ...; DELETE ...; ROLLBACK` | **both succeeded** inside the transaction (`UPDATE 1`, `DELETE 1`), rolled back, row verified unchanged afterwards |
| 4b | `ALTER TABLE postings DISABLE TRIGGER trg_postings_append_only; UPDATE ...` | **succeeded**. See the note below |
| 5 | direct `INSERT` of an `OwnerWithdrawal` of 99,999,999 from the Safe | refused at commit, `KAFF_NEGATIVE_BALANCE: account SAFE-MAIN would fall to -98899997.0000` |
| 6 | direct `INSERT` of type `TaxWithheldAtSource` between `ContractRevenue` and `ClientReceivable` | **accepted** (rolled back). The domain refuses it (`PostingAccountLegality` rule 11); `001_guards.sql` has no mirror of rule 11 |
| 7 | direct `INSERT` of a second reversal of an already-reversed posting | refused, `ux_postings_reverses` unique violation |
| 8 | direct `INSERT` reversing a reversal | refused, `KAFF_REVERSAL_OF_REVERSAL` |
| 9 | direct `INSERT` `HoldAccrual` out of the hold, not a reversal | refused, `KAFF_HOLD_DEBIT` |
| 10 | direct `INSERT` `SupplierPayment` touching a `Hold` account (`AC-305-B`) | refused, `KAFF_POSTING_TYPE_ACCOUNT_MISMATCH` |
| 11 | direct `INSERT` `Depreciation` touching the Safe | refused, `KAFF_POSTING_TYPE_ACCOUNT_MISMATCH` (the `OwnerCurrentAccount` rule fired first; the non-cash rule is reached only after it) |
| 12 | direct `INSERT` between two projects' receivables | refused, `KAFF_CROSS_PROJECT` |
| 13 | direct `INSERT` from `ClientAdvance` to `Hold` | refused, `KAFF_LEDGER_NETTING` |

**Note on 4b, stated plainly.** The first run of this probe file had a malformed `\echo` line that
swallowed the `BEGIN` before 4b, so the `ALTER TABLE ... DISABLE TRIGGER` and the `UPDATE` ran in
autocommit on the scratch database and committed: the advance-receipt posting's amount became 2 and
the append-only trigger stayed disabled. It was restored in one transaction (amount set back to
250,000 with the trigger still off, then `ENABLE TRIGGER`), and all five posting triggers were
confirmed enabled (`tgenabled = O`) afterwards. Only `kaff_probe_m3` was touched. The accident is
itself the proof of F-1: the application's role mutated a posting with two statements.

**Hard check 1 verdict.** No API route updates or deletes a posting (route scan below), EF has no
update path that survives the trigger, and a plain `UPDATE`/`DELETE`/`TRUNCATE` from the
application's role is refused. But that role is a superuser and can switch the guard off, so
"enforced by the database" holds only against honest SQL. F-1.

**Hard check 2 verdict.** The Safe floor holds through the API (409 `treasury.negative_balance`,
message "Record an owner injection first.") and through a direct insert (refused at commit by the
deferred constraint trigger). Enforced by the database, not application code: `PostMovement` has no
balance pre-check at all.

### F-7 (defect, high) — a posting or a reversal sent without a date is stored as 0001-01-01

`PostMovement/Request.cs` and `ReversePosting/Request.cs` declare `DateOnly PostingDate`
(non-nullable), and neither validator checks it. A request that omits the field binds the default,
and the handler writes it. Proved on the scratch database:
`POST /api/treasury/postings` with no `postingDate` returned **201 `"postingDate":"0001-01-01"`**,
and reversing that posting with `{}` returned **201 `"postingDate":"0001-01-01"`**. A posting in year 1
falls inside no accounting period, so the closed-period guard (`KAFF_CLOSED_PERIOD`) never sees it:
this is a way to land a movement outside every month-end close, from the API, with no error.
D-234 item 1: "today on every document is the site day" — an absent date should take
`SiteTime.Today` or be refused, never become year 1. A far-future date (2031-01-01) is also accepted
with no check. The extract, collection, credit-note and hold-release postings do take the site day
(`ExtractSupport.ContextAsync` passes `Today(clock)`; the 22:30Z theories in
`ExtractEndpointTests.cs:82-96`, `ClosureEndpointTests.cs:341-358` cover them).

### Observations reached while probing

- **O-1. A cheque collection puts the cash in the Safe at `Received`.** `CollectExtract` posts the
  `ClientCollection` to the Safe when the collection is recorded, whatever the method; the cheque's
  `Deposited` and `Cleared` steps write no posting (`AC-316-H`, held on `Q91`). Live: Safe +240,000
  while the cheque still read `Received`. `STATUS.md`'s "cash reaches the safe only at `Collected`" is
  true of the extract state, but for a cheque `Collected` means "cheque in hand". This is a held
  question, not a defect, and it is the largest money question left in this module.
- **O-2. Collections always post to the Safe**, never a bank (`ExtractSupport.ContextAsync` picks
  the first `Safe` by code). `spec.md` §6.5: "Client collections default to bank". `KAFF-316` rule 2
  restates it. No bank account exists in the seed data, and `KAFF-320` (bank master record) is
  blocked, so this is consistent with what exists, but it is not what §6.5 says.
- **O-3. The Owner cannot read a collection or a cheque.** `GetCollection` and `GetCheque` are gated
  by `ExtractCollect` (Finance only, `Unresolved` on `Q97`); Owner got 403 on both. Read access is
  borrowed from the write permission.
- **O-4. The statement's running balance shows intermediate figures that never existed.** Postings
  of one set share a timestamp, and the statement orders them so that, for example, extract 2's
  receivable reads -45,000 after its `MaterialAdvanceRecovery` and before its `RevenueRecognition`.
  The closing figure of each set is right.
- **O-5. The per-project ledger read always shows `ownerCurrentAccount: "0"`.** The owner current
  account is company-level (`ux_accounts_company_ledger`), so a project filter never finds it. The
  field is honest but can never carry information on that route.
- **O-6. The negative-balance message names an owner injection for every floored account.** "Record
  an owner injection first" is right for the Safe and wrong for an over-recovered client advance or
  عهدة, which share the same key (`errors.treasury.negative_balance`).
- **O-7. A generic reversal carries no reason.** `ReversePosting/Request.cs` has only a date; the
  audit row for the reversal has an empty `reason`. `ReverseCollection` does require one. `CLAUDE.md`
  asks for "why" where the flow requires it; no criterion here asks for it, so this is not scored.
- **O-8. Five decimals are rounded silently** (`"1.23456"` stored as `1.2346`, D-008), and a JSON
  number is accepted for an amount (`1.5`), although D-135 says money crosses the wire as a string.
  Neither loses precision.
- **O-9. A zero-work extract with material on site went the whole chain to `Issued`.** The first
  chain attempt recorded no quantity (the check-in was missing) and the extract still prepared,
  passed four gates and issued with work value 0 and تشوينات +75,000. Outside module 3.

### F-8 (defect, medium) — `HoldReleaseCarriesWithholding` is read by nothing, and no route sets a contract's withholding category

- `WithholdingBaseRule.HoldReleaseCarriesWithholding` (`src/Domain/Rulings/WithholdingBase.cs:22-24`)
  appears in the ruling's note and in one default-value test (`RulingsTests.cs:140-144`), and in no
  production code path. The hold release (`ReleaseHold`) posts `HoldRelease` to the receivable, and
  the client's payment of it has no collection route at all: the existing chain test pays it with a
  hand-built `ClientCollection` through `PostMovement` (`ClosureThroughExtractChainTests.cs:146`), and
  so did this pass. Neither path withholds under either value of the flag. `AC-317-E` is NOT MET.
- `Project.SetWithholding` is called by no endpoint (no file under `src/Api/Features` references
  `Withholding` except `ExtractResponse.cs`). Every project created through the API carries
  `WithholdingCategory.None`, so a corporate client never withholds through real requests. The
  withholding tests seed the category through EF (`ExtractEndpointTests.cs:2263-2270`). `KAFF-317`
  rule 3 says Finance sets the rate; there is no request with which Finance can.

## Hard checks 3, 4, 9 and 11 (schema, netting, money type, audit)

- **3. No stored balance.** Every column of every base table in `kaff_probe_m3` scanned
  (`information_schema.columns`): no column named or behaving as an account balance. Names that
  matched `%total%`, `%net%`, `%cumul%` are document snapshots (`extracts.net_payable`,
  `extracts.cumulative_certified_work`, `change_order_lines.total`), not balances. The only balance
  read is the `account_balances` view (`002_views.sql`), which sums `postings` on every query, and the
  floor trigger sums `postings` itself. `BoqLine.QuantityExecuted` is a stored cumulative quantity,
  not money, and outside this module.
- **4. No netting.** The database refuses any movement between two different ledger kinds
  (`KAFF_LEDGER_NETTING`, SQL probe 13). A grep for arithmetic combining two ledgers' balances finds
  only `ProofOfNumbers/Handler.cs:355-357`, which subtracts expected from actual for the same ledger.
  The extract's net payable is a sum of the period's own movements (§5.1), not a net of balances.
  The ledger read has no total field (`GetBalancesTests.cs:63`).
- **9. Money type.** Schema: 36 `numeric(18,4)` columns and 11 `numeric(18,6)` rate columns; the only
  `double precision` columns are five coordinates (`field_entries`, `projects.site_*`). Source grep:
  `double` appears only for coordinates and distances (`GeoDistance.cs`, `CheckInRulings.cs`,
  `LogEntry.cs`); no `float` anywhere. `Posting.Amount` is `Money`. Precision is applied by the
  `Money` convention in `KaffDbContext.ConfigureConventions`.
- **11. Audit.** Every one of the 26 postings on the chain project and the company level has a
  `Created` row in `audit_records` (`left join ... where a.id is null` returned 0). The mistaken
  collection and its reversal each carry actor, role, request path and the full row in `after_json`.
  The reversal's `reason` is empty (O-7).

## Hard check 13 — mutations (each reverted with `git checkout -- <file>`, rebuilt, re-run)

Runner: build the solution, then run `Kaff.Api.Tests` filtered to `TreasuryGuardTests`,
`Features.Treasury.*`, `FreshDatabaseBootstrapTests`, `Features.Extracts.ExtractEndpointTests` and
`Features.Closure.*` (240 tests), and the whole `Kaff.Domain.Tests` where the mutation is in `Domain`.

| # | Mutation | Result |
|---|---|---|
| M1 | Safe floor switched off: `001_guards.sql:306` `IF v_signed < 0` became `IF false AND v_signed < 0` | **red, 5 of 240**: `FreshDatabaseBootstrapTests.The_safe_balance_cannot_go_negative_on_a_migrated_database`, `ExtractEndpointTests.A_posting_that_would_breach_a_guard_writes_none_and_names_the_balance`, `PostMovementTests.TC_3_028_...safes_floor_is_refused`, `TreasuryGuardTests.The_safe_balance_cannot_go_negative`, `TreasuryGuardTests.A_fourth_advance_recovery_past_zero_is_refused_by_the_database` |
| M2 | Double reversal allowed: `ux_postings_reverses` made non-unique (`TreasuryConfigurations.cs:126`, `.IsUnique()` commented out) and the handler's `alreadyReversed` pre-check disabled (`ReversePosting/Handler.cs:90`) | **red, 6 of 240**: `ReversePostingTests.AC_303_B_A_posting_already_reversed_cannot_be_reversed_again`, plus five `FreshDatabaseBootstrapTests` that went red because the model no longer matched the migration snapshot, not because of the rule |
| M3 | Legality row dropped: the `[AccountType.MaterialAdvance]` entry deleted from `PostingAccountLegality.cs:43-44` (run together with M4; failures attribute by name) | **red in Domain**: `PostingAccountLegalityTests.Material_advance_refuses_every_type_other_than_its_two_named_ones` (3 cases). **Green in Api**: the SQL mirror refuses the same pairs with the same mapped error, so no API test can see a domain-only loss; that is the design (the database is the authority) |
| M4 | Domain hold check disabled: `Posting.cs:233` `... && !isReversal && Environment.ProcessorCount < 0` | **red**: Domain `Section15WorkedExampleTests.A_hold_debit_before_handover_is_refused_by_the_domain`, `PostingRuleTests.Nothing_comes_out_of_the_hold_before_handover`, `AdjustmentTests.A_credit_note_posting_cannot_leave_or_enter_the_hold_even_past_the_adjustment`, `ClosureTests.Only_HoldRelease_may_leave_the_hold_and_the_ledger_refuses_every_other_type`; Api `PostMovementTests.TC_3_027_...` (1 of 240) |
| M5-M9 | Run together, failures attributed by name. M5: `001_guards.sql:123` `KAFF_HOLD_DEBIT` check disabled. M6: `:356` hold-release-in-full disabled. M7: `kaff_reject_mutation` returns early for `postings` (UPDATE, DELETE, TRUNCATE allowed). M8: `KAFF_NEGATIVE_BALANCE` removed from `DatabaseGuardTranslation.Map`. M9: `:236` `KAFF_REVERSAL_OF_REVERSAL` check disabled | **red, 8 of 238**. M5: `TreasuryGuardTests.Nothing_comes_out_of_the_hold_before_handover_at_the_database`, `FreshDatabaseBootstrapTests.Nothing_comes_out_of_the_hold_on_a_migrated_database`. M6: `TreasuryGuardTests.A_partial_hold_release_is_refused_by_the_database`. M7: `A_posting_cannot_be_updated_even_by_raw_sql`, `A_posting_cannot_be_deleted_even_by_raw_sql`. M8: `DatabaseGuardTranslationTests.Every_named_posting_guard_prefix_...`, `PostMovementTests.TC_3_028_...`, `ExtractEndpointTests.A_posting_that_would_breach_a_guard_writes_none_and_names_the_balance`. **M9: nothing went red** |

**Tests that could not fail (findings).**

- **T-1. No test proves the database refuses a reversal of a reversal.** With `KAFF_REVERSAL_OF_REVERSAL`
  switched off in SQL (M9), every test stayed green, because `ReversePostingTests.AC_303_C` meets the
  handler's pre-check first. The guard's own comment says it is what stops an endless
  accrue-reverse-reverse chain around the hold rule; it is unproved. (SQL probe 8 shows it works today.)
- **T-2. No test proves the database refuses `TRUNCATE postings`.** M7 also let `TRUNCATE` through and
  no test noticed; `trg_postings_no_truncate` is asserted present by name only
  (`FreshDatabaseBootstrapTests`). (SQL probe 3 shows it works today.)
- **T-3. `DatabaseGuardTranslationTests` was written to agree with the F-2 gap**: it lists
  `KAFF_HOLD_PARTIAL_RELEASE` as `outOfScope` (line 51), so the exhaustiveness check cannot see the
  one posting-insert guard that is unmapped.
- **T-4. Domain-only rules are invisible to the Api suite by design** (M3 stayed green in Api because
  the SQL mirror refuses the same pairs with the same mapped error). Not a defect; it is why the
  Domain suite must stay in the gate.

All mutations reverted; `git status` showed only the report after each revert.

## Hard checks 8 and 14 — §15 and the end-to-end chain, from real requests

Project `PRJ-461B7311` (scratch database), Lump Sum, one BOQ line 1,000 m3 at 1,000 with zero
markup, so the signed contract is **1,000,000.00**. Every step is an HTTP request by the user named.
Ledger figures are `GET /api/projects/{id}/treasury/balances` (signed, summed from postings) and the
Safe is `GET /api/treasury/accounts/{safe}/balance`. The Safe carried 350,000 from an earlier probe
project before this one started.

| Step | Who | Request | Client advance | Hold | تشوينات | Safe | Client pays (spec) |
|---|---|---|---:|---:|---:|---:|---:|
| Owner injection 100,000 | Finance `vfin1` | `POST /api/treasury/postings` `OwnerInjection` | — | — | — | 250,000 to 350,000 | — |
| Payment past the balance | Finance | `OwnerWithdrawal` 350,000.0001 | — | — | — | refused 409 `negative_balance`, "Record an owner injection first." | — |
| Sign | Owner | `POST .../sign` `holdPercentage "0.20"`, `clientAdvance 25`, `advanceRecoveryRate "0.25"` | 0 | 0 | 0 | — | — |
| Advance at signing | Finance | `POST /api/projects/{id}/treasury/postings` `ClientAdvanceReceipt` 250,000 (no automatic posting, F-4) | **250,000** | 0 | 0 | 600,000 | 250,000 |
| Extract 1 (300 m3, material on site 100,000) prepared, QC, TO, deductions, owner, issued | `vprep`, `vsup`, `vto1`, `vfin1`, Owner, Owner | the six extract routes | **175,000** | **60,000** | **75,000** | 600,000 (nothing at Issued) | — |
| Extract 1 collected, cheque | Finance | `.../collect` `Cheque` | 175,000 | 60,000 | 75,000 | **840,000** (+240,000) | **240,000** |
| Cheque deposited, cleared | Finance | `.../cheques/{id}/deposit`, `/clearance` | unchanged | unchanged | unchanged | 840,000 (no posting, O-1) | — |
| Extract 2 (600 m3 cumulative, 60,000 installed) to Issued | as above | | **100,000** | **120,000** | **30,000** | 840,000 | — |
| Extract 2 collected, transfer | Finance | | 100,000 | 120,000 | 30,000 | **960,000** (+120,000) | **120,000** |
| Extract 3 (1,000 m3, 40,000 installed) to Issued | as above | | **0.0000** | **200,000** | **0.0000** | 960,000 | — |
| Extract 3 collected, cash | Finance | | 0 | 200,000 | 0 | **1,150,000** (+190,000) | **190,000** |
| Practical completion, handover | `vto1` | `.../practical-completion`, `.../handover` | | | | | |
| Partial hold release through the treasury route | Finance | `HoldRelease` 150,000 | | 200,000 (refused) | | | **500, F-2** |
| Hold release | Finance | `POST .../hold-release` | 0 | **0.0000** (released 200,000) | 0 | 1,150,000 | — |
| Client pays the release | Finance | `ClientCollection` 200,000 through `PostMovement` (no collection route, F-8) | | | | **1,350,000** (+200,000) | **200,000** |
| A mistaken duplicate collection | Finance | `ClientCollection` 200,000 again | | | | 1,550,000 | — |
| The mistake reversed | Finance | `POST .../treasury/postings/{id}/reverse` | | | | **1,350,000** | — |

Client cash on the project, summed from standing postings into the Safe: `ClientAdvanceReceipt`
250,000 + `ClientCollection` 750,000 = **1,000,000** = contract value. The receivable closes at
0.0000. The advance ledger reached **exactly zero and was never negative** (250,000, 175,000,
100,000, 0). The hold accumulated **60,000, 120,000, 200,000** = 20% of contract value and released
once to zero. تشوينات in 75,000, recovered 45,000 + 30,000, closing at zero. The Safe never went
negative. Every figure in §15's table, row by row, came out of real requests.

**Hard check 14 verdict: the chain runs end to end, and it breaks in four places.**

1. The first row of §15 and the owner injection exist only as hand-typed treasury postings that need
   account ids no endpoint lists (F-4).
2. The hold release's payment has no collection route; it is a hand-typed `ClientCollection` (F-8).
3. A partial hold release through the treasury route is a bare 500 (F-2).
4. After the chain finished clean, the generic reversal route debited the released hold to -60,000
   and the hold was then released a second time for 140,000 (F-5).

A second, earlier attempt on another project broke at the first extract for a reason outside this
module: no quantity was recorded because the check-in was missing, and the extract still prepared,
passed every gate and issued with work value 0 and تشوينات +75,000 (O-9).

## Hard check 10 — permissions, hit directly

Every cell is a real request on the scratch API. `vfin1` is Finance and on the project's team
(Finance is auto-assigned at project creation, `origin: Automatic`, not removable); `vfin3` is
Finance created after the project, so it has the role and no assignment; `vto1` Technical Office
assigned; `vprep` Site Engineer assigned; `vse_other` Site Engineer not assigned; `vhr` HR.
`vfin1` writes are covered by the chain and were not repeated here.

| Route | Owner | vfin1 | vfin3 (no assignment) | vto1 | vprep | vse_other | vhr |
|---|---|---|---|---|---|---|---|
| `POST /api/treasury/postings` (company) | 403 | chain | 201 | 403 | 403 | 403 | 403 |
| `POST /api/projects/{id}/treasury/postings` | 403 | chain | **403** | 403 | 403 | 403 | 403 |
| `POST .../treasury/postings/{id}/reverse` (project) | 403 | chain | **403** | 403 | 403 | 403 | 403 |
| `POST /api/treasury/postings/{id}/reverse` (company) | 403 | chain | 201 | 403 | 403 | 403 | 403 |
| `GET .../treasury/balances` | 200 | 200 | **403** | 403 | 200 | 403 | 403 |
| `GET /api/treasury/accounts/{safe}/balance` | 200 | 200 | 200 | 403 | 403 | 403 | 403 |
| `GET /api/treasury/postings?accountId=` | 200 | 200 | 200 | 403 | 403 | 403 | 403 |
| `GET .../treasury/proof` | 200 | 200 | **403** | 403 | 200 | 403 | 403 |
| `POST .../extracts/{id}/collect` | 403 | chain | **403** | 403 | 403 | 403 | 403 |
| `GET .../extracts/{id}/collection` | **403** | 200 | 403 | 403 | 403 | 403 | 403 |

Role without assignment is refused on every project-scoped route; company-wide routes are open to the
Finance role by design (`TreasuryPostCompany`, `TreasuryReadCompany` are `CompanyWide`). HR is
refused everything, with no financial field in any 403 body. The Site Engineer reads only the
project it is assigned to (D-173).

**Nobody creates and approves the same movement.** There is no approval step anywhere in the
treasury base to separate from: `PostMovement`, `ReversePosting`, `CollectExtract` and `ReleaseHold`
are each executed by one Finance user alone. `vfin3` reversed an owner injection that `vfin1` posted,
and `vfin1` reversed its own mistaken collection; both are allowed because `Q89` (may the creator
reverse?) and `Q97` (does the Owner approve a receipt?) are open. Nothing violates the rule as
written; nothing enforces it either, because the approval it presupposes does not exist here.

## Hard check 7 — the whole legality table, through the API

For each of the seven restricted account types (Hold, MaterialAdvance, ClientAdvance, FirmAdvance,
PettyCashAdvance, OwnerCurrentAccount, TaxWithheldAtSource), every one of the 49 posting types that
is **not** in that type's allowed set was posted through `PostMovement`, in both directions, against
a non-restricted counterpart; and every one of the 21 non-cash types was posted both ways against the
Safe. **697 requests, 696 refused with a translated `ProblemDetails` (`code` and `messageKey`), zero
accepted, zero untranslated.** Refusal codes: `posting_type_account_mismatch` 632, `hold_only_grows`
46, `posting_not_allowed_in_project_state` 18. The one acceptance was the control case: `OwnerInjection`
between a project receivable and its revenue account, a type with no restricted account on either
side, which the table deliberately does not constrain (D-169). A posting type with no row of its own
is therefore accepted between any two unrestricted accounts; that is the recorded design, and it is
the limit of `KAFF-305` rule 1.

Live also: Cost Plus opens exactly `ContractRevenue` "Management/Supervision revenues" and
`ProjectCost` "Project operating costs"; Design opens "Design revenues" and "Consulting costs"
(`AC-302-B`, `AC-302-C`). A collection with a null reference is refused 400
`errors.collection.reference_required` (`AC-316-A`).

## Verdicts per story

Evidence keys: **live** = real request against the scratch API on 5081; **SQL** = direct statement
on the scratch database as the application's role; **test** = a named test at HEAD that passed in
the gate run; **read** = established by reading the code.

### KAFF-300 — the §15 worked example as a fixture — 9 of 9 MET — recommend `VERIFIED verdict=PASS`

| AC | Verdict | Evidence |
|---|---|---|
| A present and red before KAFF-301 | MET | `git log`: fixture `931a52d` 2026-09-14 13:22, `PostMovement` `5605539` 14:13 the same day; the fixture carried two `Skip` reasons naming the missing database proof (commit message) |
| B every row, every column | MET | test: `Section15WorkedExampleTests.Row_*` (five rows) and `Totals_row_matches_spec_exactly`; and live, hard check 8 |
| C five invariants, five assertions | MET | test: the five `Invariant_*` facts |
| D every balance summed from postings | MET | test: `Every_balance_is_recomputed_from_the_posting_list_never_cached`; read |
| E no ledger nets another | MET | test: `Client_pays_is_a_sum_of_periods_own_movements_never_a_net_of_ledger_balances` |
| F hold grows, releases once in full | MET for the fixture | tests `Hold_only_grows_across_the_three_extracts...`, `Handover_releases_the_hold_exactly_once...`, `A_hold_debit_before_handover_is_refused_by_the_domain`; the database half is KAFF-322. **The production system breaks this rule through another route: F-5** |
| G advance to exactly zero, fourth recovery refused by the database | MET | test `TreasuryGuardTests.A_fourth_advance_recovery_past_zero_is_refused_by_the_database`; live: 250,000, 175,000, 100,000, 0.0000 |
| H decimal only, no tolerance | MET | test `Every_monetary_literal_in_this_file_is_decimal_with_no_approximate_assertion`; read |
| I تشوينات rows transcribed, Q14/Q58 cited | MET | test `Tashwinat_recovery_figures_are_transcribed_literals_citing_Q14_and_Q58`; read lines 110-160 |

### KAFF-301 — post a movement, append-only — 9 MET, 1 HELD (J) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A valid posting created | MET | live: owner injection 201, advance receipt 201 with id, date, accounts, amount, type, `isReversal:false` |
| B four decimals, no float | MET | live: `"0.0001"` round-trips; amounts are JSON strings; test `TC_3_024` |
| C zero or negative refused | MET | live: `"0"` 400 `errors.treasury.amount_must_be_positive`; test `TC_3_025` |
| D netting refused | MET | SQL 13 `KAFF_LEDGER_NETTING`; test `TC_3_026` |
| E hold debit refused unless HoldRelease | MET | live: `HoldAccrual`, `Adjustment`, `DebitNote`, `CreditNote` out of the hold all 409 `hold_only_grows`; `HoldRelease` while Active 409 `posting_not_allowed_in_project_state` |
| F Safe floor refused, injection named | MET | live: 409 `treasury.negative_balance`, en "Record an owner injection first."; SQL 5; mutation M1 |
| G project tag mismatch refused | MET | live: project accounts through the company route 400 `treasury.project_tag_required`; test `TC_3_029` |
| H closed period refused | MET | test `TC_3_030` (not run live) |
| I role without assignment 403 | MET | live: `vfin3` (Finance, created after the project, not on its team) 403 on post, reverse, balances, collect |
| J database enforces every domain rule | HELD | HELD by the story. Recorded anyway: the domain's rule 11 (`TaxWithheldAtSource` must touch the tax asset) has no SQL mirror (SQL 6) |

Conditions: F-7 (a missing date is stored as 0001-01-01, outside every closed period) and F-1 (the
database role can switch the guards off) are defects on this endpoint that no criterion names.

### KAFF-302 — a project's account set on creation — 7 of 7 MET — recommend `VERIFIED verdict=PASS`

| AC | Verdict | Evidence |
|---|---|---|
| A Lump Sum: CADV, HOLD, FADV, MATADV, CREC | MET | live and SQL: the five accounts on `PRJ-461B7311` with the class, normal balance and floor `AccountTypes` dictates (only CADV floored) |
| B Cost Plus: no hold, no تشوينات, cost and revenue | MET | test `TC_3_034` (not run live) |
| C Design: same | MET | test `TC_3_035` (not run live) |
| D no عهدة | MET | live: no `PettyCashAdvance` on the new project; test `TC_3_036` |
| E same transaction | MET | test `TC_3_037` (a real unique-index collision; project absent afterwards) |
| F idempotent | MET | test `TC_3_038` |
| G through `Account.Create` | MET | test `TC_3_039`; SQL: an audit `Created` row for every account at the project's own timestamp and actor |

### KAFF-303 — correct a mistake with a reversing posting — 6 of 6 MET — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A mirror, accounts swapped, `ReversesId` | MET | live: mistaken 200,000 collection reversed, 201, `reversesId` set, Safe back to 1,350,000 |
| B second reversal refused | MET | live 409 `posting_already_reversed`; SQL 7 (unique index) |
| C reversal of a reversal refused | MET | live 409 `reversal_of_reversal`; SQL 8 |
| D hold accepts a reversal that debits it | MET | test `AC_303_D_...`; live (F-5) |
| E closed period refused | MET | test `AC_303_E_...` |
| F role without assignment 403 | MET | live: `vfin3` 403 on the project route; the company route on a project posting 404 `reversal_target_not_found`; another project's route 404 |

**Why REJECTED although every criterion reads MET.** `CLAUDE.md`: "These are prohibitions, not
preferences. Violating one is a defect even if every test passes." Through this story's endpoint a
Finance user debited a released hold to -60,000 and caused a second hold release (F-5). The story's
rule 5 grants the reversal exemption so that "an accrual that should never have existed" can be
corrected; the endpoint grants it to every reversal of every posting, including the hold release and
the accruals of an extract that is collected and still standing. The criteria never asked the
question, so the gap is in the criteria as much as in the code. The Scrum Master may prefer to cut a
new story for the guard and hold 303 at CONDITIONAL; the money rule is broken today either way.
F-7 (a missing date becomes 0001-01-01) applies to this endpoint too.

### KAFF-305 — the posting-type × account-pair legality table — 7 of 7 MET — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A legal pair accepted | MET | live: `ClientAdvanceReceipt` CADV to Safe 201 |
| B illegal pair refused, domain and database | MET | SQL 10 `KAFF_POSTING_TYPE_ACCOUNT_MISMATCH`; test `TreasuryGuardTests.A_supplier_payment_into_the_hold_is_refused_by_the_database_even_bypassing_the_domain` |
| C only HoldRelease debits the hold, type-aware | MET | live (301-E) |
| D تشوينات only by its two types | MET | live sweep (hard check 7); SQL mirror present |
| E non-cash type refused on Safe/Bank | MET | live sweep; test `Depreciation_into_the_safe_is_refused_by_the_database...` |
| F every type has a row or is flagged | MET | Domain test; `PendingQ91` nine members |
| G reversal checked with its inherited type | MET | read: `Posting.Reverse` calls `Validate` with the swapped pair and no exemption from `PostingAccountLegality` |

Condition: the domain's rule 11 (`TaxWithheldAtSource`) is not mirrored in SQL (SQL 6), against the
story's rule 10 discipline. Rule 1's wider aim ("no way to combine a real type with an account pair it
was not meant for") is met only for the restricted ledgers, by design (D-169): see the sweep.

### KAFF-306 — treasury read API — 7 of 7 MET — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A read-only, no migration | MET | `git show --stat abc570c` and `6f59e0c`: endpoint, handler, response and tests only |
| B running balance across a reversal, both present | MET | live: the receivable's statement shows the mistaken collection and its reversal side by side, balance back to 0.0000 |
| C derived by summing | MET | read (`account_balances`); test `SchemaInvariantTests.No_entity_stores_a_balance` |
| D five ledgers side by side, no net | MET | live response shape; test `The_ledger_response_carries_no_field_whose_name_suggests_a_total` |
| E per-role permission | MET | live matrix (hard check 10); `GetBalancesTests` |
| F money as JSON string | MET | live: every amount is a string |
| G build and suites green | MET | gates above |

Conditions: F-6 (500 once a project has two عهدة or two client parties), O-4, O-5.

### KAFF-304 — FOLDED

Its rule is held somewhere real. `account_balances` (`002_views.sql`) sums `postings` on every read;
`BalanceQueries` is the one reader (`GetBalances`, `ExtractSupport.Balances`, `ReleaseHold`,
`ProofOfNumbers`); the floor trigger sums `postings` itself; no entity carries a balance
(`SchemaInvariantTests.No_entity_stores_a_balance` and the schema scan). The fold is sound. F-6 is a
defect in that same reader.

### KAFF-316 — collections and cheque states — 6 MET, 4 HELD (D, E, G, H) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence |
|---|---|---|
| A method, date, reference required | MET | test `CollectionTests.A_collection_missing_any_one_of_the_three_is_refused`; live: all three stored and read back |
| B role and assignment | MET | live: unassigned Finance `vfin3` 403 and assigned Site Engineer `vprep` 403 on `collect`; `vse_other` 403 on a cheque move |
| C every legal and illegal cheque transition | MET | live: received to cleared 409, received to bounced 409, deposited twice 409, cleared to bounced 409, deposit and clear 200; test covers all sixteen pairs |
| D extract becomes Paid | HELD | `Q98` |
| E shortfall | HELD | `Q100` |
| F mistaken collection reversed, never edited | MET | tests `Reversing_a_collection_is_finance_alone_needs_a_reason_and_mirrors_the_postings`, `A_reversed_collection_can_be_recorded_again_and_the_read_shows_both` |
| G bounced cheque | HELD | `Q101`, `Q91` |
| H cheque postings | HELD | `Q91`; live: deposit and clearance write no posting (O-1) |
| I project-state policy decides | MET | tests `PostingProjectStateLegalityTests` (ClientCollection in every state) and `PostingSetBuilderTests.Ruling_the_state_table_differently_changes_the_builder_with_no_code_change`; the collection is built through `PostingSetBuilder`. Not exercised through the collect endpoint itself |
| J ledgers do not net | MET | live: client advance 175,000 before and after extract 1's collection |

Conditions: O-1 (cash in the Safe while the cheque is only `Received`), O-2 (always the Safe, never a
bank), and F-5's last paragraph (the generic route can reverse a collection's posting behind the
collection record's back).

### KAFF-317 — withholding at source — 6 MET, 1 NOT MET (E), 1 UNTESTED (I), 2 HELD (C, G) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A corporate client withholds, cash reconciles | MET at the server; unreachable by request | test `A_corporate_client_withholds_and_cash_is_the_net_less_the_withheld_amount_posted_only_at_Collected` (2,400 and 237,600). The category is seeded through EF; no route sets it (F-8) |
| B individual withholds nothing | MET | test `An_individual_client_withholds_nothing_...`; no request field exists through which a withheld amount could be sent |
| C different amount than the rate | HELD | `Q99` |
| D base follows the ruling | MET | test theory `The_withheld_amount_follows_the_ruled_base_with_no_handler_change` |
| E hold release withholds only if the ruling says so | **NOT MET** | `HoldReleaseCarriesWithholding` is read by no production code (`src/Domain/Rulings/WithholdingBase.cs:22-24`); the hold release's payment has no collection route; no test flips the flag (F-8) |
| F no category, no withholding | MET | test `A_corporate_contract_with_no_category_withholds_nothing` |
| G rate changes after the first extract | HELD | `Q30` |
| H only two accounts, two fields | MET | read: `TaxWithheldAtSource`, `TaxWithholdingPayable`; contract category and the collection's withheld amount |
| I collection and withholding commit atomically | UNTESTED | one `SaveChanges` makes it atomic by construction (`GuardedSave.SaveAsync`), but no test makes the withholding leg breach a guard and asserts that neither leg landed |
| J role and assignment | MET | same endpoint as 316-B, live |

### KAFF-319 — a refused posting reads as a translated message — 3 MET, 1 NOT MET (A) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence |
|---|---|---|
| A a guard firing without a domain refusal returns ProblemDetails, never 500 | **NOT MET** | live: a partial `HoldRelease` at Handover returned **500** with no `code`, twice (F-2); `KAFF_HOLD_PARTIAL_RELEASE` is missing from `DatabaseGuardTranslation.Map` (`src/Api/Common/Results/DatabaseGuardTranslation.cs:30-49`) |
| B every named prefix mapped | MET as written | the thirteen prefixes the story lists all map; the test excludes `KAFF_HOLD_PARTIAL_RELEASE` by name (`tests/Api.Tests/Features/Treasury/DatabaseGuardTranslationTests.cs:51`), which is how A's gap passed |
| C same i18n mechanism | MET | live: every refusal carries `messageKey` `errors.treasury.*`, present in `ar.json` and `en.json` |
| D unknown exception still 500 | MET | test `An_exception_that_names_no_guard_prefix_does_not_translate`; live, by F-2 itself |

### KAFF-322 — guard tests against real PostgreSQL — 2 of 2 MET — recommend `VERIFIED verdict=PASS`

| AC | Verdict | Evidence |
|---|---|---|
| A partial hold release refused by the database | MET | test `TreasuryGuardTests.A_partial_hold_release_is_refused_by_the_database`; live (refused, though as a 500, F-2); mutation M6 |
| B fourth advance recovery refused by the database | MET | test `A_fourth_advance_recovery_past_zero_is_refused_by_the_database`; mutation M1 turned it red |

## Summary for the Scrum Master

### Recommended trailer verdicts

| Story | Criteria | Recommended |
|---|---|---|
| KAFF-300 | 9 MET | `VERIFIED verdict=PASS` |
| KAFF-301 | 9 MET, 1 HELD | `VERIFIED verdict=CONDITIONAL` (F-7, F-1) |
| KAFF-302 | 7 MET | `VERIFIED verdict=PASS` |
| KAFF-303 | 6 MET | `BUILT verdict=REJECTED` on a `CLAUDE.md` money rule, not on a criterion (F-5) |
| KAFF-304 | FOLDED | rule held for real in `account_balances` and `BalanceQueries`; no change |
| KAFF-305 | 7 MET | `VERIFIED verdict=CONDITIONAL` (rule 11 not mirrored in SQL) |
| KAFF-306 | 7 MET | `VERIFIED verdict=CONDITIONAL` (F-6) |
| KAFF-316 | 6 MET, 4 HELD | `VERIFIED verdict=CONDITIONAL` (O-1, O-2, F-5 last paragraph) |
| KAFF-317 | 6 MET, 1 NOT MET, 1 UNTESTED, 2 HELD | `BUILT verdict=REJECTED` (`AC-317-E`) |
| KAFF-319 | 3 MET, 1 NOT MET | `BUILT verdict=REJECTED` (`AC-319-A`) |
| KAFF-322 | 2 MET | `VERIFIED verdict=PASS` |

### Defects, ranked

1. **F-5, critical.** The generic reversal route (`src/Api/Features/Treasury/ReversePosting/Handler.cs`)
   reverses any posting, including an issued or collected extract's hold accrual and the hold release
   itself. Proved: a released hold driven to -60,000, then released a second time for 140,000.
2. **F-1, high.** The application's database role is a superuser (`deploy/docker-compose.staging.yml:44`,
   `appsettings.Development.json:12`); `001_guards.sql` section 5 targets a `kaff_app` role that does
   not exist. Proved: `session_replication_role = replica` and `DISABLE TRIGGER` both let the role
   update a posting.
3. **F-7, high.** A missing `postingDate` on `PostMovement` or `ReversePosting` is stored as
   0001-01-01, outside every closed period (`PostMovement/Request.cs`, `ReversePosting/Request.cs`,
   neither validator checks it).
4. **F-2, medium.** A partial `HoldRelease` through the treasury route is a bare 500;
   `KAFF_HOLD_PARTIAL_RELEASE` is unmapped (`src/Api/Common/Results/DatabaseGuardTranslation.cs:30-49`).
5. **F-8, medium.** `HoldReleaseCarriesWithholding` is dead (`src/Domain/Rulings/WithholdingBase.cs:22-24`);
   no route sets a contract's withholding category, so withholding never happens through the API.
6. **F-6, medium, latent.** `BalanceQueries.GetLedgerBalances` uses `SingleOrDefault` per ledger; two
   عهدة accounts (or two client parties) on a project give a 500 on the ledger read and on extract
   preparation.
7. **T-1 to T-3.** Guards no test can see fail: the database's reversal-of-reversal refusal (M9 stayed
   green), `TRUNCATE` refusal, and the translation map's exhaustiveness test that excludes the unmapped
   prefix by name.
8. `KAFF-305` rule 11 has no SQL mirror (SQL probe 6).

### Hard checks, one line each

1. Append-only: no API or EF path; plain `UPDATE`/`DELETE`/`TRUNCATE` refused by trigger; **but the
   app role is a superuser and bypassed the trigger two ways** (F-1).
2. Safe floor: refused by the database through the API (409 with the injection prompt) and by direct
   insert; held.
3. No stored balance column anywhere in the schema; balances come from `account_balances`. Held.
4. No calculation nets one ledger against another; the database refuses a cross-ledger movement. Held.
5. Hold debit before handover refused on every forward route (eight attempts); **the reversal route
   debits it, before and after release** (F-5).
6. Reversal: double reversal refused (handler and unique index), reversal of a reversal refused
   (handler and trigger), `reversesId` set, cross-project and cross-route refused 404. Held; the
   trigger half is untested (T-1).
7. Legality table iterated whole: 697 requests, 696 translated refusals, the one acceptance is the
   designed control. Held, except rule 11 has no SQL mirror.
8. §15 reconciled from real requests row by row; advance ledger exactly zero, never negative; client
   cash 1,000,000.
9. `decimal(18,4)` on every money column, `(18,6)` on rates, `double` only on coordinates, `Money` on
   `Posting.Amount`. Held.
10. Role and assignment enforced on every project route, including correct role not assigned; no
    approval step exists in the treasury base, so "creates and approves" has nothing to separate
    (`Q89`, `Q97` open).
11. Every posting and reversal writes an audit `Created` row (26 of 26); reversals carry no reason.
12. Flow postings (issue, collection, credit note, hold release) take the site day, proved by the
    22:30Z theories in the gate run; the generic treasury routes take a typed date and **store year 1
    when it is missing** (F-7).
13. Nine mutations; eight went red; **M9 (database reversal-of-reversal off) stayed green** (T-1).
14. Chain ran end to end with §15's figures exact; it breaks at the advance and injection (no route,
    F-4), the release payment (no collection route, F-8), a partial release (500, F-2), and after
    close (hold debited and released twice, F-5).

### What this pass did not reach or could not check

- `AC-301-H` and `AC-303-E` (closed period) were not run live: no route on the scratch data closes a
  period. Accepted on their tests.
- A 22:30Z posting was not produced live: the running API reads the real clock. The site-day rule
  rests on the gate's clock-moved theories.
- The UI was not driven; module 3 has no screen for postings, collections or cheques (`STATUS.md`).
- Concurrency (two Finance users overdrawing the Safe at once through the advisory lock) was not
  attempted.
- The mid-project version of F-5 (reversing an issued extract's hold accrual before handover) was
  read in the code but not run live; the post-release version was.
- Staging was not touched.

### Corrections to the brief

- The cheque states are `Received`, `Deposited`, `Cleared`, `Bounced`. `Collected` is the extract's
  state and is reached when the collection is recorded, before the cheque moves at all.
- "Use fraction wire values" holds for `holdPercentage` and `advanceRecoveryRate` only; `clientAdvance`
  is percent units, and a fraction there silently signs a 0.25% advance (F-3).
- "A project is signed (advance posted...)": signing posts no advance; it is a separate hand-typed
  treasury posting (F-4).
- "Postings take today from the site day (D-234 item 1)": true of flow postings; the generic
  treasury routes take a typed date, as D-234 itself allows ("a date the user types is kept as
  typed").

### State left behind

- Scratch database `kaff_probe_m3` dropped. The dev database `kaff` was read once (`pg_dump`) and never
  written.
- Every mutation reverted with `git checkout -- <file>`; the solution was rebuilt with
  `--no-incremental` afterwards, exit 0, 0 warnings, 0 errors. `git status` shows only this report.
- The API was restarted on 5080 against the dev database (Release, `--no-build`) and
  `node .claude/skills/run-kaff-erp/driver.mjs smoke` passed all eight checks. It is left running. The
  web dev server on 4200 was not touched.
- No trailer edited, nothing committed.

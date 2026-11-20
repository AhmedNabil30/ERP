# Verification round 3 — KAFF-500 (the extract chain) — 2026-11-20

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (`claude-opus-5-5`), the strongest model; no downgrade.
**Tree:** `main` at `432b3d3`, working tree clean at start. Commits in scope since round 2 (`2be5a96`): `ec1f15b` (D-253) and `432b3d3` (web).
**Sources read:** `CLAUDE.md`; `STATUS.md` top section; `stories/slice-5-billing/KAFF-500-the-extract-chain.md`; `spec.md` section 7 (lines 329-345) and section 15 (line 594); `decisions.md` D-250 to D-253 (by index offset); round 2, `qa/slice-5/verification-2026-11-20-r2.md`.

Findings are written as they are reached.

## Findings, in the order reached

### F-1 · Code read of D-253 (what it does, judged against D-250 item 1)

`PrepareExtract/Handler.cs` no longer carries forward the last standing extract's `CumulativeCertifiedWork`. It calls
`Extract.PreviouslyCertifiedFor(chain)` over every non-Void extract of the project. That sums, per extract, the full period
(`Cumulative - Previously`) while its held portion is not released, and only `ApprovedWorkValue()` (period less
`HeldWorkValue()`) once `HeldPortionReleasedAt` is set. `BillingInputFor` bills `Previously + ApprovedWorkValue()`, the same split.

Read against the cases that matter:

- Split at 60% of 300,000, released, next extract at cumulative 400,000: previously = 180,000, period = 220,000. The released 120,000 is billed once on the next extract. This is D-250 item 1, "the held remainder released onto the next extract".
- NCR still open when the next extract is prepared: previously counts the full 300,000, so the held 120,000 is not billed while held. Released later: the extract after that drops it from previously and bills it (the sum telescopes; the gap equals the released amount).
- A rejection back to Draft resets `ApprovedPortion`, `HeldForNcrId` and `HeldPortionReleasedAt` together (`Extract.cs:1099-1101`), so a re-split after a rejection cannot inherit a stale release.
- A revision (D-241): the original is Void and excluded; the revision starts from the original's previously certified work, unchanged by D-253. The chain sum therefore still telescopes.
- Nothing is stored: the figure is derived on every prepare from the extracts' own fields. No new column, no balance.

Not changed and noted, not scored: the percentage scales period work only; delivered and installed material are unscaled (D-253 item 6). spec.md section 7 says "approve a percentage for payment" and does not say which figures it scales. Open question for Nabil/Karim, not a fail.

### F-2 · Gate defect in `432b3d3`: a duplicated i18n key turns Domain.Tests red

`432b3d3` adds a second, identical line `"extract.portion.approved"` to both `src/Web/public/locales/ar.json` (lines 2176
and 2177) and `en.json` (lines 2176 and 2177). Duplicate keys are legal JSON to a browser, which keeps the last, so the
screen still reads «المعتمد للدفع». But `TranslationCatalogueTests` loads the catalogues into a dictionary and throws
`System.ArgumentException : An item with the same key has already been added. Key: extract.portion.approved`. Six tests
in `Kaff.Domain.Tests.TranslationCatalogueTests` fail on the committed tree, among them
`Every_domain_error_key_has_an_arabic_and_an_english_translation` and `The_two_catalogues_describe_the_same_set_of_keys`.

This does not touch money or AC-500-E's behaviour. It is a red gate on `main`: the Definition of Done's "builds clean"
and the test gates cannot be reported green while it stands. The fix is deleting one line in each file; not done here
(the Verifier does not fix). Owed to the web lane of `432b3d3`.

### F-3 · KAFF-500 AC-500-E · MET · the released remainder is billed once, on the next extract

My own proofs, written fresh this pass and not the lane's tests (both untracked, deleted after the pass):

- **P1, Api level** (`tests/Api.Tests/Features/Extracts/VerifierR3Probe.cs`): a different split from the lane's, 25% of 300,000
  (75,000 approved, 225,000 held). The Technical Office closes the NCR over HTTP, extract 1 is issued at work value 75,000,
  700 m3 are recorded, extract 2 is prepared and issued. Extract 2 work value **625,000** (400,000 new plus the released
  225,000), hold this period 125,000, hold ledger **140,000** = 20% of the 700,000 billed. Green.
- **P2, section 15 with a split gate** (`tests/Domain.Tests/VerifierR3Probe.cs`, theory, two cases): section 15's three
  extracts through the aggregate, extract 2 split at 50% (150,000 billed, 150,000 held), released either before extract 2 is
  issued or after it is collected. Extract 3's previously certified work comes from `Extract.PreviouslyCertifiedFor`, the
  function the prepare handler calls: 450,000, so extract 3 bills 550,000. Then the hold is released at handover. Asserted
  and green in both cases: work billed 300,000 + 150,000 + 550,000 = 1,000,000; hold accrual 200,000 (20% of contract value);
  client advance ledger exactly zero; تشوينات in equals recovered; **safe = 1,000,000, client cash equals contract value**;
  receivable zero; the safe never went below zero after any commit; the release itself posted nothing and the hold balance
  was unchanged by it.
- **P3, open NCR** (same file): split 60%, NCR left open, next extract at 400,000 bills 100,000. The held remainder is not
  billed while held.

The criterion as written, against D-250 item 1: the extract is `PartiallyApproved` (unchanged since r2, suite green); only
the approved portion proceeds (`BillingInputFor`, M3 below); closing the NCR releases the remainder, and since D-253 the
release reaches a bill, on the next extract prepared, exactly once. r2's proof P1 is now green in its new form.

Condition carried, not a fail: D-253 item 4. An NCR closed while the next extract is already prepared is billed on the one
after it. If the project has no later extract (the last extract was already prepared or issued when the NCR closes), the
remainder waits for an extract someone must still prepare with no new work. Nothing refuses handover while a released
remainder is unbilled, or while an NCR is still open. spec.md does not speak to this; it is a question for Nabil, below.

## Mutations — can the tests fail?

Each mutation applied to `src/Domain/Extracts/Extract.cs`, rebuilt (0 errors), Domain.Tests and the Api class
`ExtractEndpointTests` run, the file restored with `git checkout -- src/Domain/Extracts/Extract.cs`. The two probe files
were present during M1 to M3 and deleted before the clean gates.

| # | Rule | Mutation | Result |
|---|---|---|---|
| M1 | The released remainder is billed (the r2 defect put back) | `PreviouslyCertifiedFor` always counts the full period (`HeldPortionReleasedAt is null` replaced by `true`) | **killed**. Lane Api tests red: `The_released_remainder_is_billed_on_the_next_extract_prepared_after_the_NCR_closes`, `An_open_NCR_keeps_the_remainder_held_and_a_late_close_bills_it_on_the_next_extract_prepared`, `A_project_with_a_split_gate_still_bills_its_whole_contract_value_exactly_once`; my P1 and both P2 cases red. **No lane Domain test went red**: nothing in `Domain.Tests` covers `PreviouslyCertifiedFor` directly (O-1) |
| M2 | A held remainder is not billed while its NCR is open | `PreviouslyCertifiedFor` always counts only the approved portion (`... is null` replaced by `false`) | **killed**. Lane Api `An_open_NCR_keeps_the_remainder_held_...` red; my P3 red |
| M3 | Only the approved portion proceeds | `BillingInputFor` bills the full cumulative (`approvedCumulative = CumulativeCertifiedWork`) | **killed**. Domain `ExtractChainTests.A_partial_approval_bills_only_the_approved_portion_and_releasing_the_remainder_is_recorded_once` red; three lane Api tests and my P1 and P2 red |

## Gates

Order per `.claude/skills/run-kaff-erp/SKILL.md`: every `Kaff.Api`, `Kaff.Api.Tests` and `Kaff.Domain.Tests` host stopped
first; probes deleted and `git status --short src tests` clean; clean build; tests only after it.

| Gate | Result | Exit |
|---|---|---|
| `dotnet build KaffErp.sln -c Release --no-incremental` | 0 warnings, 0 errors | 0 |
| Domain.Tests | total 803, succeeded 797, **failed 6** (all `TranslationCatalogueTests`, F-2) | **2** |
| Api.Tests (full suite) | total 1289, succeeded 1287, failed 0, skipped 2 (the two `ClosureEndpointTests` skips) | 0 |
| `ng test --watch=false` | 64 files, 597 / 597 | 0 |
| `ng build --configuration production` | bundle generation complete | 0 |
| `dotnet format KaffErp.sln --verify-no-changes --no-restore` | only `ENDOFLINE` findings (7,846 lines, nothing else), as D-251 recorded | 2 |

After the gates the solution was rebuilt with `--no-incremental` (0 warnings, 0 errors) from the clean tree, and the API
was started on http://localhost:5080 (Development).

## Live probe, API on 5080

`/api/health`: healthy, `guardsInstalled: true`, `leastPrivilege: true`, `excessPrivileges: []`. The five seeded users
(`docs/uat-manual.md` section 1) each signed in, 204.

On the round 2 probe extract (`PRJ-CE500CBD`, extract `01a1f91c-a119-7053-bb72-e0eeca19afe8`, `PartiallyApproved`, NCR
closed, released at 13:19:48Z):

| Read | Owner | Accounts | Technical Office | Supervisor | Junior engineer |
|---|---|---|---|---|---|
| `GET extracts/{id}`: `approvedWork` / `heldWork` | 0.0000 / 0.0000 | 0.0000 / 0.0000 | 0.0000 / 0.0000 | null / null | null / null |
| `GET ncrs` | 200 | 200 | 200 | 200 | 200 |
| `POST ncrs/{closed id}/close` again | 403 | 403 | 409 `extract.ncr_already_closed` | 403 | 403 |

The two new D-253 fields are on the live read and are null for the site roles, as D-253 item 5 says. The money path
itself (split, close, next extract billing the remainder) was proven through the real HTTP pipeline by P1, not on the dev
database, because the dev project has no new executed quantity to bill. `driver.mjs smoke`: all checks passed, exit 0.

## The money and permission rules, this pass

- **No float near money.** No `double`, `float` or `parseFloat` added in `2be5a96..432b3d3` under `src/` (grep of the
  diff). The web change only displays the server's strings (`approvedWork`, `heldWork`); the browser does no arithmetic.
- **No stored balance.** `PreviouslyCertifiedFor`, `ApprovedWorkValue` and `HeldWorkValue` are derived on every call; no new
  column and no migration in the diff.
- **No posting update or delete path.** None in the diff. The NCR close still posts nothing (P2 asserts the hold balance is
  unchanged by the release).
- **The hold only grows.** The release debits nothing. The hold accrues on the released work when it is billed (P1: 125,000
  on extract 2; P2: 200,000 in all). Nothing debits the hold before handover.
- **Role and assignment.** No route changed. The close route was probed live above and refused to every role but the
  Technical Office.
- **Nobody creates and approves the same movement.** Unchanged. NCR closer separation is still the recorded assumption
  `NcrCloser`.

## Verdict per criterion, KAFF-500

| AC | Verdict | Evidence this pass |
|---|---|---|
| A section 15 end to end | MET, conditional | unchanged since round 1 F-4; `Section15ThroughExtractChainTests` green |
| B section 15 invariants | MET | suite green, and now also with a split gate (P2): hold 20%, advance zero, تشوينات balance, client cash = contract value, safe never negative |
| C Owner gate cannot be skipped | MET | suite green; state machine unchanged |
| D nobody approves their own; control absent | MET | suite green; the new web rows are display only, controls still `@if` on server flags |
| E splittable gate, NCR close releases the remainder | **MET** | F-3; P1, P2 and P3 green; M1, M2 and M3 killed |
| F rejection to Draft with reason | MET | suite green; a rejection resets the portion, the NCR hold and the release together |
| G rejection after posting reverses | MET | suite green |
| H stopped project issues nothing | MET | suite green |
| I Technical Office moves no money | MET | suite green; the release posts nothing |
| J six figures, column foots | MET | suite green |
| K atomic commit | MET | suite green |

**Recommended trailer: `VERIFIED verdict=CONDITIONAL`.** Conditions: AC-500-A's round 1 condition; the open questions
below; and F-2, a red Domain.Tests gate on `main`. F-2 is outside KAFF-500's behaviour, but nobody may report the gates
green until it is fixed.

## Observations and open questions, not scored

- O-1. No Domain-level unit test of `Extract.PreviouslyCertifiedFor`. The rule is covered only by Api tests (M1 went red
  in Api.Tests only). Cheap to add.
- O-2. D-253 item 6: the percentage scales period work only; delivered and installed material are unscaled. spec.md
  section 7 says "approve a percentage for payment" and does not say which figures. Question for Nabil and Karim, not a fail.
- O-3. D-253 item 4 and its consequence (F-3): an NCR closed after the last extract is prepared leaves a released remainder
  that needs another extract, and nothing refuses handover with an open NCR or an unbilled release. Question for Nabil.
- O-4. D-253 item 7, carried: on the extract after a release, the quantities sheet's previous column differs from
  previously certified work by the released remainder.
- O-5. The Technical Office user sees `approvedWork` and `heldWork`, because it holds an extract-money permission (D-194).
  Consistent with the existing read rule; noted only.

## Not reached

- No `driver.mjs layout` at 390px on the extract page. D-252 item 3 (the stepper wrapping «الحسابات») was not re-measured
  after the CSS change in `432b3d3`.
- No live split-to-billing run on the dev database (see Live probe).

## Corrections to the brief

- The brief describes the web commit as adding E3 rows and the stepper fix. It also duplicates an i18n key, which turns
  Domain.Tests red (F-2).
- `spec.md` "~340" is correct: section 7 starts at line 329 and the split sentence is at line 340.

## Trailer moved by this pass

KAFF-500 to `VERIFIED verdict=CONDITIONAL at=432b3d3 on=2026-11-20`. Nothing committed.

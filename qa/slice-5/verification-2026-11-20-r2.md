# Verification round 2 — KAFF-500 (AC-500-E) and KAFF-317 (AC-317-E) — 2026-11-20

**Verifier:** fresh session, did not write the code. **Model:** Opus 5.5 (`claude-opus-5-5`), the strongest model; no downgrade.
**Tree:** `main` at `2be5a96`, working tree clean at start. Commits in scope since round 1 (`363d5ee`): `3d21a33`, `fcd2111`, `2ed1172`, `81537f2`, `2be5a96`.
**Sources read:** `CLAUDE.md`; `STATUS.md` top section; both story files; `spec.md` §7; `decisions.md` D-250, D-251, D-252 (by index offset); round 1, `qa/slice-5/verification-2026-11-20.md`.

Findings are written as they are reached.

## Findings, in the order reached

### F-1 · KAFF-317 AC-317-E · MET (mutation result below)

`ReleasedHoldPayments.AcceptCheque` (`src/Domain/Closure/ReleasedHoldPayments.cs`) now refuses a withheld amount above
zero when `RulingSet.WithholdingBase.HoldReleaseCarriesWithholding` is false (`closure.released_hold_carries_no_withholding`).
`Collect` (cash and transfer) goes through `AcceptCheque`, and so does recording a cheque, so every way a release payment
enters is gated. `ClearCheque` does not re-check; the withheld amount was accepted when the cheque was recorded (D-251 item 6).
Test: theory `ClosureThroughExtractChainTests.The_released_hold_withholds_only_when_the_ruling_says_the_release_carries_withholding`,
true and false, run through a factory that overrides the `RulingSet`: true writes 600.00 to `TaxWithheldAtSource`; false
refuses transfer and cheque, posts nothing, and accepts the same payment without withholding. That is the criterion as
written: "withholding is written in the first case and not in the second".

### F-2 · KAFF-500 AC-500-E · NOT MET · closing the NCR releases a timestamp, and the held remainder can never be billed

What is now built, and is correct as far as it goes:

- A split Technical Office approval opens an `Ncr` in the same save (`ApproveTechnicalOffice/Handler.cs`); the extract is
  `PartiallyApproved`, holds against the NCR's id, and `BillingInputFor` bills only the approved portion of the period.
- `POST /api/projects/{projectId}/ncrs/{id}/close` closes it once, with a required note, under the extract's row lock, and
  calls `Extract.ReleaseHeldPortion`, which stamps `HeldPortionReleasedAt` and adds a `HeldPortionRelease` gate record.

What "releases the remainder" does in the running system: nothing that reaches money. `ReleaseHeldPortion` posts nothing,
and no code reads `HeldPortionReleasedAt` except the read model (`grep` over `src/`: `Extract.cs` and `ExtractResponse.cs`
only). The next extract cannot pick the remainder up either: `PrepareExtract/Handler.cs` sets the next extract's previously
certified work to the last standing extract's **`CumulativeCertifiedWork`**, the full figure including the held part, not
the approved cumulative that `BillingInputFor` billed. So the held remainder is excluded from this extract by the split and
from every later extract by the carry-forward. In the test project, 120,000 of the 300,000 period work is held; after the
close it is billed nowhere, ever, and a project with any split gate can no longer satisfy §15's own invariant that client
cash equals contract value. (Proven by P1 in the mutations section.)

Why this is NOT MET and not a condition:

- `spec.md` §7: "approve a percentage **for payment** and hold the remainder until an NCR closes … the held portion releases
  when the NCR closes". What is held is payment; a release that never makes it payable is not a release of the remainder.
- **D-250 item 1, Nabil's ruling:** "a small NCR record, a close action, and the held remainder released **onto the next
  extract** through the existing `Extract.ReleaseHeldPortion`". The build ships the first two and not the third. D-251 item 3
  then re-files the billing as "stays `ExtractPartialApprovalScope`, Assumed", which turns a ruled item back into an open
  assumption without a new ruling.
- The NCR close sheet approved in D-252 tells the Technical Office «يُفرج عن {amount} ج.م.» ("{amount} EGP is released").
  The server releases no amount; the line states a consequence the system does not have.

Round 1 wrote that "recording the release does not depend on" how the remainder is billed. That was true before D-250;
D-250 then ruled where the remainder goes, so the criterion is now judged against that ruling.

What would close it, for the Scrum Master to brief (not done here): when an NCR closes, the released held work value must
reach a bill. The plain reading of D-250 item 1 is that the next extract carries it, for example by carrying forward the
last extract's *billed* cumulative rather than its full cumulative once its remainder is released, so the released work
falls into the next period. `ExtractPartialApprovalScope` then gains that mode. A test that drives two extracts across a
release (P1 below is the shape) is what proves it.

## Gates

Order per `.claude/skills/run-kaff-erp/SKILL.md`: the 5080 listener and every `Kaff.Api`, `Kaff.Api.Tests`, `Kaff.Domain.Tests`
host stopped first; clean build; tests only after it.

| Gate | Result | Exit |
|---|---|---|
| `dotnet build KaffErp.sln -c Release --no-incremental` | 0 warnings, 0 errors | 0 |
| Domain.Tests | total 803, succeeded 803, failed 0, skipped 0 | 0 |
| Api.Tests (full suite) | total 1286, succeeded 1283, **failed 1**, skipped 2 (the two `ClosureEndpointTests` skips) | **2** |
| Api.Tests rerun, `--filter-class Kaff.Api.Tests.Features.DailyLogs.DailyLogTests` | total 133, succeeded 133, failed 0 | 0 |
| `ng test --watch=false` | 64 files, 595 / 595 | 0 |
| `ng build --configuration production` | bundle generation complete | 0 |

**The one failure is a flaky test, not a defect, and it is out of scope of both stories.**
`DailyLogTests.AC_600_J_an_expense_draft_read_back_by_the_engineer_carries_no_amount` asserts
`drafts.GetRawText().Should().NotContain("350")` (`tests/Api.Tests/Features/DailyLogs/DailyLogTests.cs:322`) over a body
that carries a random entry GUID. This run's GUID was `e972160f-fbc9-4b2f-98f6-362630f350d7`, which contains `350`. The
body carried no amount. The class reran green, 133 of 133. The assertion fails at random about once in a few hundred runs
and should check that no `amount` property exists instead of searching the raw text for a number. Owed to KAFF-600's owner.

## Mutations — can the tests fail?

Each mutation applied to one file, rebuilt (0 errors), the named tests run, the file restored with `git checkout -- <file>`.
After the last one the solution was rebuilt with `--no-incremental` from the clean tree (exit 0, 0 warnings) before the
API was started, so no mutated binary served the live probes.

| # | Rule | Mutation | Run | Result |
|---|---|---|---|---|
| M1 | AC-317-E false case | the refusal in `ReleasedHoldPayments.AcceptCheque` made never true (`withheld.IsPositive` replaced by `false`) | `ClosureThroughExtractChainTests` | **killed**, 1 of 12 red: `The_released_hold_withholds_only_when_the_ruling_says_the_release_carries_withholding(carries: False)`. Round 1's M4 survived on the same rule; this one does not |
| M2 | AC-500-E: closing the NCR releases | `Ncr.Close` no longer calls `Extract.ReleaseHeldPortion` | Domain `ExtractChainTests`; Api `A_split_gate_opens_an_NCR_and_closing_it_releases_the_held_portion_and_posts_nothing` | **killed** in both: `Closing_the_NCR_releases_the_held_portion_once_and_stores_its_note` red (1 of 34), and the Api test red |
| M3 | Role on the close route | `CloseNcr/Endpoint.cs` gated `ExtractRead` instead of `NcrSupport.ClosePermission` | the same Api test | **killed**: an Owner/Finance/Supervisor close expected 403, got 200 |
| P1 | D-250 item 1: the released remainder reaches the next extract | not a mutation: a temporary untracked test (`ExtractNcrVerifierProbe.cs`, deleted after) that splits extract 1 at 60%, closes the NCR, issues extract 1, records 400 executed and prepares extract 2, then expects extract 2's period to carry the released 120,000 plus the new 100,000 | that one test | **red, as F-2 predicts**: `expected periodTwo 220000, found 100000.0000 (difference of -120000.0000)`; `released 120000 at 2026-11-20 13:14:49Z; extract 2 previously=300000.0000, cumulative=400000.0000`. The released 120,000 is billed on no extract |

## Live probes — the NCR routes as each seeded role

API restarted on http://localhost:5080 (Development) from the clean `--no-incremental` build. `/api/health`: healthy,
guards installed, `leastPrivilege: true`, `excessPrivileges: []`. Five seeded users (`docs/uat-manual.md` §1) signed in,
each with its own cookie (204 each).

A real NCR was made for the probe: on `PRJ-CE500CBD` (`01a1f08e-3f29-78e6-b185-d5d8c1187fd8`, every user assigned) the junior
engineer prepared extract 2 (`01a1f91c-a119-7053-bb72-e0eeca19afe8`, December 2026), the supervisor submitted the QC report,
and the Technical Office approved 50%. The extract went to `PartiallyApproved` and the read carried `openNcr`
`01a1f91c-c74a-7ef6-af2a-034a9be7ce2b` with `actions.close: true` for the Technical Office. **This extract is left in the
development database at `PartiallyApproved`, NCR closed.**

| Call | Owner | Finance | Technical Office | Supervisor | Junior engineer |
|---|---|---|---|---|---|
| `POST ncrs/{real id}/close`, assigned project | 403 | 403 | 200 `Closed`, note stored | 403 | 403 |
| `actions.close` on the extract read | false | false | true | false | false |
| `POST ncrs/{random id}/close`, assigned project | 403 | 403 | 404 `extract.ncr_not_found` | 403 | 403 |
| `POST ncrs/{random id}/close`, `PRJ-0761FB3D` (unassigned) | 403 | 403 | 403 | 403 | 403 |
| `GET ncrs`, assigned project | 200 | 200 | 200 | 200 | 200 |

Technical Office, further: the real NCR's id under another project's route (`PRJ-377EC1A2`, assigned) is 404
`extract.ncr_not_found`; a blank note is 400 `extract.ncr_close_note_required`; the close is 200 and the extract then reads
`heldPortionReleasedAt` set and `openNcr: null`; a second close is 409 `extract.ncr_already_closed`.

Role and assignment are both enforced server-side: the Technical Office is admitted only where assigned, every other role is
refused everywhere. The real NCR held `0.0000`, because the project had no new executed quantity in December; see O-2.

`driver.mjs smoke` after the restart: all checks passed, exit 0.

## The money and permission rules, this pass

- **No float near money.** No `double` or `float` added in `363d5ee..2be5a96` under `src/` (grep of the diff). The web portion
  sheet range-checks the typed percentage with `Number(...)` (`extract-detail-page.ts:474`) and sends the string through
  `percentToFraction`; that is a percentage bound check, not money arithmetic.
- **No stored balance.** The NCR carries no money; its held amount is derived on read (`Extract.HeldWorkValue`).
- **No posting update or delete path.** None in the diff. `kaff_app` gains SELECT, INSERT, UPDATE on `ncrs` only, no DELETE
  (`005_app_role.sql`); `ncrs` is not a posting table.
- **The hold is never debited before handover.** The NCR close posts nothing (the Api test asserts the posting count is
  unchanged). The released-hold change only adds a refusal.
- **Role and assignment.** Both NCR routes carry `RequirePermission(..., ProjectScope.FromRoute())`; probed live above; M3 killed.
- **Nobody creates and approves the same movement.** The NCR close is not gated against the person who split the gate
  (D-251 item 4, `NcrCloser` Assumed, on the Karim sheet). The close moves no money and approves no posting, so the rule
  has nothing to bind; recorded as O-1, not a defect.
- **Audit.** The NCR is audited by the interceptor; the close note is the audit reason (asserted in the Api test).

## Observations, not scored

- O-1. The Technical Office user who split the gate may close its NCR (D-251 item 4). Assumption, on the Karim sheet.
- O-2. A split is accepted on an extract with zero period work, opening an NCR that holds `0.0000` (seen live). Harmless, but
  the close sheet then reads «يُفرج عن 0.00 ج.م.».
- O-3. Flaky `DailyLogTests.AC_600_J_...` (see Gates).
- O-4. Because the next extract carries forward the full cumulative, the held remainder is also lost when the NCR is **never**
  closed: preparing the next extract does not wait for it. That is the same defect as F-2 seen from the other side.

## Verdicts per story

### KAFF-500 — 9 MET, 1 MET with condition (A, carried from round 1), 1 NOT MET (E) — recommend `BUILT verdict=REJECTED`

| AC | Verdict | Evidence this pass |
|---|---|---|
| A §15 end to end | MET, conditional | unchanged since round 1 F-4; `Section15ThroughExtractChainTests` green |
| B §15 invariants | MET | same tests green. Note: holds only for a project with no split gate, see F-2 |
| C Owner gate cannot be skipped | MET | suite green; no change to the state machine |
| D nobody approves their own; control absent, not disabled | MET | suite green; the new NCR close and portion controls are `@if` on server flags (`x.openNcr?.actions?.close`, `x.actions.approveTechnicalOffice`) |
| E splittable gate, NCR close releases the remainder | **NOT MET** | F-2; P1 red: the released 120,000 is billed on no extract |
| F rejection to Draft with reason | MET | suite green; a rejection clears the hold and the NCR then closes without a release (`An_NCR_whose_extract_was_rejected_closes_without_a_release`) |
| G rejection after posting reverses | MET | suite green |
| H stopped project issues nothing | MET | suite green |
| I Technical Office moves no money | MET | suite green; the NCR close posts nothing |
| J six figures, column foots | MET | suite green |
| K atomic commit | MET | suite green |

### KAFF-317 — 8 MET, 2 HELD (C, G) — recommend `VERIFIED verdict=CONDITIONAL`

| AC | Verdict | Evidence this pass |
|---|---|---|
| A corporate withholds, cash reconciles | MET | suite green |
| B individual withholds nothing | MET | suite green |
| C different amount than the rate | HELD | Q99 |
| D base follows the ruling | MET | suite green |
| E release withholds only if ruled | **MET** | F-1; M1 killed |
| F no category, no withholding | MET | suite green |
| G rate changes after first extract | HELD | Q30 |
| H two accounts, two fields | MET | the change adds one refusal and no account or field |
| I atomic commit | MET | suite green |
| J role and assignment | MET | suite green; unchanged routes |

Conditions: C and G held on Q99 and Q30; the default `HoldReleaseCarriesWithholding = true` is still unruled by Karim (D-250
item 4 is Nabil's ruling to keep it).

## Not reached

- No `driver.mjs layout` or screenshot pass on the extract screen's new portion and NCR sheets; they carry no criterion in
  these two stories. D-252 item 3 (the stepper wrapping «الحسابات» at 390px) was not re-measured.
- `dotnet format --verify-no-changes` was not run.
- Unassigned Finance and Owner were not probed as distinct from the role refusal: neither holds the close permission, so
  both are refused by role before assignment is asked.

## Corrections to the brief

- The brief points to `spec.md` "~line 340"; §7 starts at line 329 and the NCR sentence is at line 340. Correct.
- The brief does not mention D-250 item 1's "onto the next extract". That clause is what decides AC-500-E this round.

## Trailers moved by this pass

KAFF-500 stays `BUILT verdict=REJECTED`, KAFF-317 to `VERIFIED verdict=CONDITIONAL`, both `at=2be5a96 on=2026-11-20`.
Nothing committed.

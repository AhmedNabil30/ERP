# Verification — 2026-09-03

**Verifier, fresh session, machine to itself.** `CLAUDE.md`: *"If you wrote the code, you do not
certify it."* I wrote none of this.

Five commits in scope. **The brief's framing is wrong in one way that matters, and §1a records it
before anything else**, per `agents.md` principle 7 and the standing instruction to correct the brief.

---

## 0. Progress of this report

Everything is `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Baseline re-measured, not trusted | **done** — §1 |
| 1a | Scope correction — the commit missing from the brief | **done** — §1a |
| 2 | Claim 1 — the −4,000 overdraw, reproduced then re-caught | **done** — §2 |
| 3 | Claim 2 — refuse to start, or report degraded? | **done** — §3 |
| 4 | Claim 3 — `V-30-D` at expression level, and its ceiling | **done** — §4 |
| 5 | **`V-31-A`** — the repair path that does not exist | **done** — §5 |
| 6 | Claim 4 — the inverted trigger, and whether "not now" is right | **done** — §6 |
| 7 | Claim 5 — the Architect's own claims, attacked | **done** — §7 |
| 8 | Claim 6 — staging, as deployed rather than as intended | **done** — §8 |
| 9 | Citations sweep, including what it does *not* check | **done** — §9 |
| 10 | Verdicts per commit | **done** — §10 |
| 11 | What I did not do, as a count | **done** — §11 |
| 12 | Fit to show a client? | **done** — §12 |
| 13 | The one thing Nabil should know | **done** — §13 |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-31-A` | **HIGH** | A misfloored account row that has taken postings cannot be repaired by any supported operation, and the documented repair does not clear the alarm |
| `V-31-B` | **LOW** | `RequiredCheckConstraintDefinitions` verifies a predicate is *unchanged*, never that it is *right*; the "two-file friction" is in practice a red suite with a mechanical copy-paste fix |
| `V-31-C` | **LOW** | The scope handed to this pass omits `c7ae3d1`, the commit that performs the change claim 1 is about |
| `V-31-D` | **LOW** | The citation sweep's "1088 / 0 broken" does not cover 65 file-only references; none is broken today, but the number is not the whole corpus |

---

## 1. Baseline — verified, not trusted

Every number the brief gave, re-measured before any mutation. `/run-kaff-erp` loaded first, per
`agents.md` B0.

| Gate | Brief said | Measured |
|---|---|---|
| `docker ps` | — | `kaff-db  Up 11 days (healthy)` |
| Stranded hosts | none | none — `Get-CimInstance Win32_Process` matched on **command line**, the corrected form |
| Build, `-c Release --no-incremental` | 0 / 0 | **0 warnings, 0 errors**, and `Kaff.Api.Tests.dll` actually written |
| **`MSB3026`** | — | **absent** — the trap the brief names did not fire; the copy really happened |
| `dotnet format --verify-no-changes` | exit 0 | **exit 0** |
| Domain suite | 107/107 | **107/107** |
| Api suite | 235/235 | **235/235** |
| `driver.mjs smoke` | 8/8 | **8/8** |
| Citations | 1088 / 0 / 0 | **1088 / 0 broken / 0 legacy** — and see §9 |

The brief was right to warn about `MSB3026`. I checked for it explicitly rather than reading
`Build succeeded`: the build log names every project's output `.dll`, including
`Kaff.Api.Tests.dll`, so the copy is evidenced rather than assumed.

---

## 1a. `V-31-C` — **LOW** · the scope is missing the commit claim 1 is about

The brief lists five commits and says *"the two commits you are verifying changed the code that
decides whether the host starts."* Claim 1 — the −4,000 overdraw, `FindMissingGuardsAsync` now
asserting the floor — is **D-101's**, and D-101 was committed at **`c7ae3d1`**, which is *not in the
list*:

```
c7ae3d1  The safe floor is data: the guard reads a flag no test asserted on a row
         decisions.md | 186 +   src/Infrastructure/Persistence/DatabaseInitializer.cs | 46 +
         tests/Api.Tests/SchemaInvariantTests.cs | 68 +
```

Of the five commits actually named, **exactly one** touches `DatabaseInitializer.cs` — `2f4b276`.
`172eab0`, `93cd17a` and `4bf81ce` are documentation only.

**Consequence, stated plainly: had I verified only the five commits named, the account-floor check —
the whole of claim 1 — would have gone uninspected while the report said five of five accepted.** I
verified the behaviour at `HEAD` regardless, so nothing is missed here; but `c7ae3d1` is a sixth
commit that also had no independent verification, and it is the one that changed the start-up
decision most directly. It is verified in §2, §3 and §5 below and carries its own verdict in §10.

---

## 2. Claim 1 — the −4,000 overdraw. **Reproduced first, then the fix watched catching it.**

*A fix you have not seen fail is a fix you are taking on trust.* So the defect first.

**The mechanism, re-established rather than taken from D-101.** `kaff_check_non_negative_balance`
loops `FROM accounts a WHERE ... AND a.enforce_non_negative`
[Verified: 2026-09-03 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` ->
`kaff_check_non_negative_balance`], read out of `pg_proc` on the live database rather than from the
file. An account whose flag is `false` is **not in the loop at all** — the trigger fires and floors
nothing.

**`MUT-1` — the defect, on this machine's live `kaff` database.** A `Safe` row inserted raw with
`enforce_non_negative = false`, funded 1,000 from `OWNER-CURRENT`, then drawn 5,000:

| | Result |
|---|---|
| Probe account created | `PROBE-UNFLOORED`, `type = Safe`, `enforce_non_negative = f`, `normal_balance = Debit` |
| The funding posting | **accepted** |
| The overdrawing posting | **accepted** |
| The safe's signed balance | **`-4000.0000`** |

D-101's headline number is exact. **A `Safe` account went 4,000 negative and PostgreSQL raised
nothing** — `CLAUDE.md`'s *"the safe balance can never go negative … enforced by a database
constraint, not application code"* was not enforced for that row.

**And this is at `HEAD`, after the fix.** The fix does **not** close the hole. The overdraw is still
accepted; what changed is that the *deployment* is now reported as unsafe. That distinction is
correct — D-101 §3 argues for it explicitly and I agree with the design — but *"`FindMissingGuardsAsync`
now asserts the floor"* must not be read anywhere as *"the safe can no longer go negative on a
misfloored row."* It still can. The row is caught at boot, not at posting time.

**The fix, watched catching it.** With that row present, `/api/health` on the running API:

```
503 {"status":"degraded","databaseReachable":true,"guardsInstalled":false,
     "missingGuards":["accounts.enforce_non_negative on PROBE-UNFLOORED"]}
```

Exactly D-101 §3's claim, reproduced independently. **Claim 1 holds.**

---

## 3. Claim 2 — it does **both**, and the split is by environment. D-033 is intact.

The brief is right that *"reporting and refusing are different guarantees and only one is D-033"*,
and right to insist the difference be established rather than assumed. It is not, however, an
either/or in the code — the two behaviours are the same check read in two environments
[Verified: 2026-09-03 @ `src/Api/Program.cs` -> `missingGuards`]:

```csharp
if (missingGuards.Count > 0 && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Refusing to start: database guards are missing — " ...);
}
```

**Driven, both environments, same misfloored database, same binary.**

| Environment | Missing-guard case | Wrong-predicate case |
|---|---|---|
| `Development` | **Host starts.** `/api/health` → `503 degraded`, row named | **Host starts.** `503 degraded`, predicate named |
| `Staging` | **Host refuses to start** — see below | not separately driven; same code path, same list |

```
fail: Kaff.Infrastructure.Persistence.DatabaseInitializer[0]
      Database guards are missing: accounts.enforce_non_negative on PROBE-UNFLOORED.
Unhandled exception. System.InvalidOperationException: Refusing to start: database guards are
missing — accounts.enforce_non_negative on PROBE-UNFLOORED. The append-only and
non-negative-balance rules are not enforced on this database.
   at Program.<Main>$(String[] args) in D:\ERP\src\Api\Program.cs:line 311
```

Process exit code non-zero; no listener; nothing served. **D-033's guarantee is real, and the new
account-floor check is inside it** — the floor is now one of the things that will stop a production
host booting.

**So D-102's `503 degraded` is a Development observation, not a description of the guarantee.** Both
D-101 §3 and D-102 §1 report `503 degraded` as their live evidence, and both were driven in
Development on port 5080. Neither entry states that the environment is why they saw a running host,
and a reader could take *"health goes to 503 degraded"* as the whole of the behaviour. It is the
weaker half of it. **Not a defect — a reporting imprecision in two entries**, worth one sentence in
each.

**One incidental finding, recorded because it will be met on a real deploy.** Outside `Development`
there is no connection string in configuration — `appsettings.json` carries none — so a non-Development
host fails first with `ArgumentException … (Parameter 'connectionString')` from
`DependencyInjection.AddKaffInfrastructure` before any guard check runs. Expected for a
secrets-driven deployment, and staging supplies it; noted only so the next session that tries this
does not read it as a defect.

---

## 4. Claim 3 — `V-30-D` works, and `V-31-B` is its ceiling

### Both mutations reproduced, live, by me

Against a throwaway `kaff_verify` database, API running on 5080, each constraint dropped and re-added
under **its own required name**:

| Mutation | `/api/health` |
|---|---|
| `ck_postings_amount_positive` → `amount >= 0` | `503 degraded` · `["ck_postings_amount_positive predicate changed: expected \"CHECK ((amount > (0)::numeric))\", found \"CHECK ((amount >= (0)::numeric))\""]` |
| `ck_users_subcontractor_cannot_log_in` → `1 = 1` | `503 degraded` · `["ck_users_subcontractor_cannot_log_in predicate changed: expected \"CHECK ((((role)::text <> 'Subcontractor'::text) OR (password_hash IS NULL)))\", found \"CHECK ((1 = 1))\""]` |

Both restored; `/api/health` returned to `200 healthy … missingGuards: []`. **D-102 §1's evidence
table reproduces exactly.** The mechanism does what it claims: a constraint kept under its required
name with a different predicate is now caught, where before it was invisible.

### `V-31-B` — **LOW** · the snapshot is hand-maintained, and the friction is a red suite with a mechanical fix

The brief asked the right question: *"is the snapshot updated by hand? If so, a wrong edit and a
matching snapshot edit defeat it in one commit."* **Yes, and yes.**

`RequiredCheckConstraintDefinitions` is thirty hand-written string literals
[Verified: 2026-09-03 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
`RequiredCheckConstraintDefinitions`], and `FindMissingGuardsAsync` compares **the snapshot against
the live database and against nothing else**
[Verified: 2026-09-03 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
`FindMissingGuardsAsync`]. Nothing compares the snapshot to the authored predicate, to `spec.md`, or
to any independent statement of the rule.

**`MUT-2` — driven, not argued.** `ck_babs_not_own_parent` — a constraint deliberately chosen because
it is *not* one of the two pinned in the test's `InlineData` — reduced in the live database to
`CHECK (true)`, and its snapshot entry edited to `"CHECK (true)"` in the same working tree:

```
200 {"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}
```

Build clean, 0 warnings, 0 errors. **The rule is gone from the database, and the mechanism whose whole
purpose is to notice that reports the deployment safe.** The host starts. This is `V-30-A`'s shape —
the forged marker — arriving one level down, exactly as the brief predicted.

**Then I ran the Api suite against that same mutation, and the result corrected my own first
reading — so it is recorded rather than quietly dropped.**

```
Test run summary: Failed!   total: 235   failed: 196   succeeded: 39
```

**196 of 235.** A snapshot that disagrees with the migrations is not quietly tolerated: `KaffApiFactory`
defaults to the `Testing` environment, `Testing` is not `Development`, so **D-033's refusal fires
inside the test host** and every test that needs a booted API dies with it. That is
`KaffApiFactory`'s own stated intent — *a broken guard must still fail the build here* — working
exactly as written, and it is much louder cover than I expected before running it.

**So the honest statement of the ceiling is narrower than "a matched pair of edits defeats it", and
sharper.**

* A **snapshot-only** edit is caught overwhelmingly — 196 failures.
* A **live-database-only** edit is caught — §4's two mutations.
* A **fully coordinated** edit — EF configuration, a new migration, *and* the snapshot, all in one
  commit — leaves the test database and the snapshot in agreement and would pass. **I did not drive
  that one; it needs a hand-written migration, and I state it as a reading of
  `FindMissingGuardsAsync` and of the two `SchemaInvariantTests` assertions (which compare *names*
  and *keys*, never predicates), not as something I watched.**

**And this is where the real point is, which is not the file count.** D-102 argues the snapshot's
value is *"a deliberate edit across two files"*. In practice the second file does not ask for a
decision: a developer who changes a predicate meets 196 red tests whose failure message names the
expected and found definitions verbatim, and the fix that turns them green is to paste the new
re-print into the dictionary. **The mechanism is loud, but what it trains is a mechanical
copy-paste — it stops drift, and it does not ask whether the new predicate is right.** That is worth
knowing before it is relied on as a review gate for the slice-3 money constraints.

**Two of the thirty are pinned a third time and escape that**, in literal `InlineData`
[Verified: 2026-09-03 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
`A_check_constraints_predicate_changed_while_its_name_did_not_is_reported_as_a_missing_guard`]:
`ck_users_subcontractor_cannot_log_in` carries `role <> 'Subcontractor' OR password_hash IS NULL` and
`ck_postings_amount_positive` carries `amount > 0` as test data, so a coordinated change to either
must also edit an assertion that spells the old rule out in full. **One of those two is the money
constraint Nabil named** — which is the right one to have pinned hardest.

**None of this is a reason to reject `2f4b276`.** The commit strictly increases what is caught: before
it, all thirty were name-only; after it, a drifted database is caught for all thirty, loudly, and the
two constraints that most matter carry a third pin. `LOW`, and recorded mainly so *"verified by
predicate"* is not later read as *"the predicate is verified to be correct."* It is verified to be
**unchanged**. Those are different claims, and the second one no mechanism here makes.

---

## 5. `V-31-A` — **HIGH** · the repair path D-101 prescribes does not work

**This is the finding of this pass, and it is new.** It is not in D-101, not in D-102, not in the
brief.

D-101 §7 names the exposure and prescribes the repair:

> *"**If any deployed database carries a row seeded before 2026-08-20, this change will refuse that
> host's start-up** outside Development — which is D-033 working as designed, and is the point. **The
> repair is not an `UPDATE`:** `MUT-2b` shows the immutability guard refuses one. Such a row must be
> closed and the account reopened."*

The first half is right. **The second half does not work.** Every route out, driven on the live
database against the row from §2:

| Repair attempted | Result |
|---|---|
| `UPDATE accounts SET enforce_non_negative = true` | **Refused** — `KAFF_ACCOUNT_IMMUTABLE: account PROBE-UNFLOORED configuration cannot be changed after creation` |
| `DELETE FROM postings WHERE …` | **Refused** — `KAFF_APPEND_ONLY: postings is append-only; DELETE is not permitted` |
| `DELETE FROM accounts WHERE code = 'PROBE-UNFLOORED'` | **Refused** — `violates foreign key constraint "FK_postings_accounts_from_account_id" … Key (id)=(…) is still referenced from table "postings"` |
| **Close the account** — D-101's own prescription, and the trigger's own `HINT` | **Succeeds, and does not help.** The row is *still reported.* |

That last row is the defect. Closing the account is permitted — the immutability trigger allows it,
and its `HINT` recommends it — and I confirmed `is_active = f`, `closed_on = 2026-09-03`. But
`FindMissingGuardsAsync`'s query has **no `is_active` filter**:

```sql
SELECT code::text AS "Value" FROM accounts
WHERE enforce_non_negative <> (type = ANY({flooredTypes}))
```

so the closed row still comes back, still lands in `missingGuards`, and the host still refuses to
start. Verified directly against the closed row: the query returns `PROBE-UNFLOORED | Safe | f`.

**The consequence, stated as an operator would meet it.** A production or staging database carrying
one misfloored account that has ever taken a posting is a host that **will not boot, and that no
supported operation can make boot again.** The guards close the update, the append-only rule closes
the delete, the foreign key closes the account delete, and the check ignores the close. The only exit
is `ALTER TABLE … DISABLE TRIGGER` plus a raw `DELETE` — superuser DDL surgery against live financial
data, which is precisely the operation this entire subsystem exists to make impossible.

**How likely is such a row?** D-101 answers it itself, and the answer is *not hypothetical*:
`001_guards.sql` §3 warns that *"a database seeded before 2026-08-20 therefore keeps the old floors"*,
and D-101 §4 names the three types that carry them — `Hold`, `FirmAdvance`, `MaterialAdvance`. Any
environment provisioned before Karim's 2026-08-20 ruling is in this state the moment it is redeployed
with this build.

**What I am not saying.** I am not saying the check should be removed or weakened, and I am not
saying closed accounts should simply be excluded — an unfloored *closed* `Safe` with a negative
balance is still a wrong number in the books, and silently ignoring it would be D-046's green light
again. **This is a routing item, not a fix I should choose.** It needs a decision from the Architect
about what "repaired" means for such a row, and `CLAUDE.md`'s own rule points at the shape: a
correction is a **new reversing posting** and a new correctly-floored account, not an erasure. But
the mechanism to *retire* the wrong row from the guard check does not exist, and until it does, the
alarm has no off switch.

**Recorded honestly: I could not clean up after myself.** The probe row and its two postings are
still on this machine's `kaff` database, closed but present, and `/api/health` against `kaff` will
report `503 degraded`. I did not perform the trigger surgery — my own tooling refused it as a
dangerous operation, which is the correct answer and is itself a data point about how hard this
repair is. **This is left for Nabil to clear deliberately; §11 states exactly what to run.** The
verification work moved to a fresh `kaff_verify` database, and the `8/8` smoke in §1 was measured
there.

---

## 6. Claim 4 — the inverted trigger, and whether `V-30-B`'s "not now" is right

### The trigger

Confirmed from the live database rather than the file:

```
CREATE TRIGGER trg_accounts_configuration_immutable BEFORE UPDATE ON public.accounts
FOR EACH ROW EXECUTE FUNCTION kaff_accounts_configuration_is_immutable()
```

`BEFORE UPDATE` only — no `INSERT`. `enforce_non_negative` is among the eleven columns it pins
[Verified: 2026-09-03 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` ->
`kaff_accounts_configuration_is_immutable`]. **D-101's characterisation is exactly right and is the
uncomfortable one:** the trigger does not protect a correct row from being made wrong — a wrong row
can only be *created*, and creation is the one path it does not watch — it protects a wrong row from
being made right. `MUT-2b` reproduced above.

**This is the mechanism behind `V-31-A`**, and the two findings should be read together: the
immutability guard is not merely unhelpful here, it is one of the four locks on a door with no key.

### The `V-30-B` ruling — I agree with "not now", and I disagree with one of the three conditions

`V-30-B` is `LOW` and about a *different* thing: `IsApplied` reads a `Marker` in endpoint metadata,
so what a test asserts is a declaration rather than a behaviour
[Verified: 2026-09-03 @ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`]. D-101 §6 ruled
*"not now"* on building a behavioural sweep over `SelfOnlyEndpoints`, on the grounds that its two
members are each covered concretely by hand-written tests.

**Is "not now" right given a client demo? Yes, and more clearly than D-101 argues it.** The ruling
rests on cost. The stronger argument is scope: `SelfOnlyEndpoints` has two members, both acting on
the caller's own credential, **neither touching money**, and a demo does not enlarge that set. A
generic sweep bought this week would assert, weakly, what
`MeTests.A_password_changed_on_another_device_ends_this_endpoints_answer_too` and
`ChangePasswordTests.The_change_ends_every_other_session` already assert strongly. Against `V-31-A`
sitting unrouted, spending a session on `V-30-B` would be the wrong order of work by a wide margin.

**The three reopen conditions — two are right, one is the wrong shape.**

* **Condition 1 (a third member) — right.** Concrete and countable.
* **Condition 3 (a self-only route touching money) — right, and it is the one that matters.**
* **Condition 2 (*"a self-only route is added whose per-member tests do not cover all three checks"*)
  — this one cannot fire on its own.** It asks a future session to notice that a test it is writing
  is *incomplete*. A session that noticed the gap would close it directly and never reach for the
  sweep; a session that did not notice is exactly the session the condition needed to stop, and it
  will not read a trigger list in `decisions.md` D-101 §6 while writing a new endpoint. **A reopen
  condition that depends on the author already having the insight it exists to supply is not a
  trigger, it is a hope.** Conditions 1 and 3 are mechanical — a count, and a category of route — and
  a future session can be asked to check them. Condition 2 should be replaced by something a checker
  or a reviewer can evaluate without judgement, or dropped so the list is not read as more
  load-bearing than it is.

**Stated plainly, as asked:** I would not reopen `V-30-B` before the demo. I would fix condition 2's
wording when someone next touches D-101, and not before.

---

## 7. Claim 5 — the Architect's own claims, attacked

The Architect falsified two of the briefing's claims. The brief asks that the same scepticism be
turned on its own. Six checks, all against the files and the database today:

| D-101 claim | Verdict |
|---|---|
| The overdraw reaches **−4,000** | **True** — reproduced, `-4000.0000` (§2) |
| `/api/health` said `healthy, guardsInstalled: true` while it did | **True** by construction — the check that reports it did not exist before `c7ae3d1` |
| `AccountTreeSeeder` seeds `SAFE-MAIN` and never rewrites it; **fourteen** company accounts exist | **True** — 14 rows, `SAFE-MAIN` is `Safe` with `enforce_non_negative = t` |
| `kaff_check_non_negative_balance` loops `WHERE a.enforce_non_negative` | **True** — read from `pg_proc`, not the file |
| `trg_accounts_configuration_immutable` is `BEFORE UPDATE` and makes a wrong row permanent | **True** — `pg_get_triggerdef`, and `MUT-2b` (§6) |
| Every test builds accounts through `Account.Create`, so no test can produce a misfloored row | **True** — and `Account.Create` is *stronger* than D-101 says; see below |
| **D-101 §7: "such a row must be closed and the account reopened"** | **FALSE — `V-31-A`, §5.** Closing is permitted and does not clear the check |
| D-102 §2: the Api test host can run as `Development` because the refusal is Development-exempt | **True** — driven in §3, both directions |
| D-101 §5 / D-102 §1: PostgreSQL's re-print is a stable normal form and the authored SQL is not | **True** — the two live re-prints match the entries verbatim, and my own mutations re-printed exactly as predicted (§4) |

**One place the Architect was harder on itself than it needed to be, recorded because accuracy runs
both ways.** D-101 §2 says the exposure is *"a row written by a past catalogue, and no test in this
repository can produce one."* `Account.Create` in fact carries a one-way ratchet —
`bool enforce = meta.EnforceNonNegative || (enforceNonNegative ?? false)`
[Verified: 2026-09-03 @ `src/Domain/Treasury/Account.cs` -> `Create`] — so the domain cannot even be
*asked* to unfloor a floored type. That makes the "no test can produce one" claim true for a stronger
reason than the one given, and it narrows the exposure to raw SQL and to pre-existing rows. It does
not change the finding.

**The scepticism applied to itself found one false claim, and it was the remediation.** That is the
same pattern as the Architect's own §1 — the wrong half was not the headline, it was the sentence
that made the problem look survivable.

---

## 8. Claim 6 — staging, as deployed rather than as intended

The brief says *"the staging SPA smoke has never been observed green."* **It is green, and I have now
observed it.** `.github/workflows/deploy-staging.yml`, run `33672702763` for `4bf81ce`, job `Deploy`:

```
[success] Check the target is configured
[success] Copy the compose file
[success] Pull and restart
[success] Smoke check
[success] SPA smoke check
```

**`success`, not `skipped`** — which is the distinction that matters, because both steps are gated on
`if: vars.STAGING_URL != ''` and a skipped step is reported as `skipped`, never `success`. The
variable is set, the steps ran, and the SPA check — which greps for `<kaff-root>` rather than
accepting a `200` — passed. Every one of the five in-scope commits plus `c7ae3d1` has a completed,
successful run.

**And staging really is asserting the safe floor, for the reason D-101 §3 claims.** The health step
greps `"guardsInstalled":true` through `curl -sf`; a misfloored row makes the API return `503`, which
`-sf` fails, and outside Development the container does not come up at all. Its passing is therefore
positive evidence that **no deployed row disagrees with the catalogue as of `4bf81ce`** — the
pre-2026-08-20 case D-101 §7 feared has not bitten this environment.

**The limit of that evidence, stated rather than papered over.** I could read step *conclusions* from
the public Actions API but **not the step logs** — `GET /actions/jobs/{id}/logs` returned `403` with
no credentials available in this session. So I have the pass/fail of each step and not the text it
printed, and I could not reach `STAGING_URL` directly because it is a repository variable I cannot
read. **What is established: the checks ran and passed. What is not: the body staging returned.**
That is one notch weaker than driving staging myself, and `2f4b276` has no run of its own — it was
pushed together with `8767c90`, so the workflow ran once for the pair, which is normal GitHub
behaviour and not a gap in coverage.

---

## 9. `V-31-D` — **LOW** · what the citation sweep counts, and what it does not

`scripts/check-citations.ps1`: **1088 checked · 0 broken · 0 legacy.** The brief's number is exact.

**And 1088 is not the whole corpus.** *Silence must never be readable as success* — so I counted the
citation-shaped strings the sweep's own pattern cannot match. Repo-wide there are **1175** occurrences
of `@ \`…\``; the sweep parses **1088**. The gap is **65 file-only references** of the form
``@ `src/Api/Program.cs` `` with no `` -> `Identifier` `` — the sweep requires the identifier arrow and
skips the rest silently.

**Checked, so the number is not left as an open worry:** of those 65, exactly **one** names a file
that does not exist, and it is `` @ `File.cs` `` — SM-31's own *format example* in prose, not a claim
about anything. **So: no broken file-only reference exists today.** The sweep is honest; its headline
just describes a subset, and the subset is the one SM-31 actually rules on.

---

## 10. Verdicts — per commit

| Commit | Subject | Verdict |
|---|---|---|
| `172eab0` | `V-30-B` ruled: not now, and what reopens it | **ACCEPT** — the ruling is sound and I would rule the same way (§6). One reopen condition is the wrong shape; recorded, not blocking |
| `93cd17a` | Refinement §2.1 amended — two false sentences corrected | **ACCEPT** — both corrections independently confirmed true (§7) |
| `2f4b276` | `V-30-D` closed at expression level | **ACCEPT** — both claimed mutations reproduced live by me (§4). `V-31-B` is a recorded ceiling, not a reason to reject: the commit strictly increases what is caught |
| `8767c90` | `V-30-G` regression cover + D-102 | **ACCEPT** — the `Development`-host claim driven and confirmed (§3); suite 235/235 with the two new cases present |
| `4bf81ce` | D-100, `KAFF-125` cut, sprint-2 record | **ACCEPT** — documentation only; the three `[Verified:]` citations it adds resolve, and the sweep is 0-broken (§9) |
| **`c7ae3d1`** | **D-101 — the safe floor is data** *(not in the brief's scope — §1a)* | **ACCEPT WITH A DEFECT ATTACHED** — the mechanism is correct and I watched it catch the defect I first reproduced (§2, §3). **`V-31-A` (HIGH) is against this commit**: the entry's own prescribed repair does not work. The check should stay; the remediation must be designed |

**No commit is rejected.** All six do what they claim. The two findings that matter — `V-31-A` and
`V-31-B` — are about what is *not yet* true, and neither is a reason to unwind work that made the
system strictly safer than it was on 2026-09-01.

---

## 11. What this session did **not** do — as a count, not as prose

1. **Did not test 28 of the 30 constraint predicates individually.** Two were mutated live (§4); the
   other 28 rest on the same one code path and were not separately driven.
2. **Did not mutate 7 of the 8 required triggers, or any of the 3 required indexes.** Only
   `kaff_check_non_negative_balance` (via its data) and `trg_accounts_configuration_immutable` were
   exercised.
3. **Did not drive the `Staging` refusal for the wrong-predicate case.** Driven for the missing-guard
   case only; the same list feeds the same `throw`, but I did not watch it.
3a. **Did not drive the fully coordinated three-file constraint change** (configuration + a new
   migration + snapshot). It needs a hand-written migration; `V-31-B`'s statement about it is a
   reading of the code, explicitly flagged as such in §4, not a measurement.
4. **Did not reach the staging host.** Step conclusions only, no logs, no `STAGING_URL` (§8).
5. **Did not run the E2E suite** (`Kaff.E2E.Tests`), and did not screenshot or drive either screen —
   this pass was spent entirely on the start-up decision, per the brief's ordering.
6. **Did not verify any money behaviour beyond the safe floor.** There is still no posting endpoint,
   no `spec.md` §15 assertion, and nothing here should be read as coverage of the worked example.
7. **Did not re-verify the ~1088 citations by hand.** The sweep was run; 65 unparsed file-only
   references were counted and existence-checked (§9), nothing further.
8. **Did not examine the four open business questions**, `KAFF-125`'s scope question, or any story
   file. Out of scope for this pass and untouched.
9. **Did not fix anything.** `CLAUDE.md` and `agents.md` §7 — the Verifier reports.

**And one thing I could not undo — needs Nabil, deliberately.** This machine's local `kaff` database
carries my `MUT-1` probe: account `PROBE-UNFLOORED` (closed) and two postings under
`source_document_id = 00000000-0000-0000-0000-0000000000c1`. **`/api/health` against `kaff` will
report `503 degraded` until they are removed, and the API will not start outside `Development`.**
That is `V-31-A` demonstrating itself. Clearing it needs exactly the surgery §5 describes:

```sql
ALTER TABLE postings DISABLE TRIGGER trg_postings_append_only;
DELETE FROM postings WHERE id IN ('00000000-0000-0000-0000-0000000000b1',
                                  '00000000-0000-0000-0000-0000000000b2');
ALTER TABLE postings ENABLE TRIGGER trg_postings_append_only;
DELETE FROM accounts WHERE code = 'PROBE-UNFLOORED';
```

The throwaway `kaff_verify` database created for this pass can be dropped; nothing depends on it.
`git status` is otherwise clean apart from this report — the `MUT-2` snapshot edit in
`DatabaseInitializer.cs` is reverted (§12).

---

## 12. Is this system fit to be shown to a client?

**Yes — for a demo, on a clean database, with one condition and one thing not said out loud.**

**Why yes.** The build is clean, `format` is clean, 342 tests pass across two suites, the smoke check
is 8/8 against a real stack, staging has deployed green with its SPA actually rendering, and the two
mechanisms this pass attacked both did what they claim when I tried to break them. The start-up
decision is *safer* than it was on 2026-09-01, not less safe: a database that has lost a trigger, an
index, the balances view, a check constraint, a constraint's *predicate*, or an account's floor now
stops a production host from booting. Nothing I mutated slipped past that was supposed to be caught.

**The condition.** Demo against a **freshly provisioned database**, not one seeded before
2026-08-20. `V-31-A` means a database in that state is a host that will not start and cannot be
repaired in front of an audience. Staging is currently clean (§8), so this is a *don't restore an old
dump the morning of the demo* condition, not a blocker.

**What is not said out loud.** What the client will be shown is a sign-in screen, a status page and a
change-password screen. **There is no money in this system yet** — no posting endpoint, no extract, no
`spec.md` §15. The safety work verified today is the *foundation* under money that has not been built.
That is a good thing to have built first and a bad thing to let a client infer is finished. The
demo should not imply that treasury works, because none of it exists to work.

---

## 13. The one thing Nabil should know

**The guards are now good enough to stop a bad database from starting, and there is no way to unstick
one once it happens.**

Everything this sprint built is the alarm — and the alarm is real; I set it off four different ways
today and it fired every time. What nobody built is the *reset*. A single account row with the wrong
floor, on a database that has taken even one posting, is a host that will not boot: the immutability
trigger refuses to fix the row, the append-only rule refuses to remove the postings, the foreign key
refuses to remove the account, and closing the account — which is what `decisions.md` D-101 tells the
next person to do — leaves the alarm ringing. The only remaining move is to disable a guard trigger by
hand on live financial data.

I proved this the hard way: **I created such a row on this machine's database in order to reproduce
the defect, and I could not remove it.** §11 has the SQL; it needs your hand, not an agent's.

**So the one thing:** before this goes anywhere near a client's data, ask the Architect for the
*repair* story, not another detector. `CLAUDE.md` already says what shape it has to take — corrections
are new reversing postings and a new correctly-floored account, never an erasure — but the mechanism
that retires a wrong row from the guard check does not exist, and every additional check added to
`FindMissingGuardsAsync` makes one more way to reach a host that cannot be started. **The next
increment on this subsystem should be the off switch, not the ninth alarm.**

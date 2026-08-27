# Sprint 1 — close · 2026-08-27

**Scrum Master.** Slice 1, Foundation. Gate (`agents.md`): *permission tests pass*.

The run began on 2026-08-26 and crossed midnight; the status sweep landed that evening as `33f1779`.
Gates below were re-run in this session on 2026-08-26 except the citation check, re-run 2026-08-27.

**`process/agile.md` says the retrospective is one section appended to the sprint's meeting file.**
This is a separate file because it carries three other things the sprint's close owes — the recomputed
numbers, the routing, and the Definition of Done statement — and appending four products to
`2026-08-21-sprint-1-refinement.md` would bury them under a five-day-old agenda. The deviation is
recorded rather than taken silently.

---

## 1. The numbers, recomputed rather than restated

**Nabil's lock stands: 15 stories / 57 points.** Nothing was added, cut or re-estimated in this close.
`KAFF-118`'s cut is his and he has not ruled — **"close sprint 1" is a scheduling instruction, not a
ruling on scope**, and this session has not treated it as one.

| Bucket | Stories | Pts |
|---|---|---:|
| **Accepted** — verified by a session that did not write the code, no defect open, own behaviour unchanged since | 116, 108, 113, 100, 111, 112, 114 | **25** |
| **Verified, then the code moved underneath the verdict** | 101a, 103 | **10** |
| **Rejected, fixed, not re-verified** | 109, 105a, 102 | **9** |
| **Built and verified with a criterion still held** | 106, 110 | **10** |
| **Unbuilt** | 118 | **3** |
| | **15** | **57** |

**25 of 57 points stand. 19 do not, and they are the sprint's finding.**

**Five stories' shipped code changed after the session that judged it.** Established from
`git show --stat`, not from a report's summary of itself:

| Story | What moved after `2e56943` | Commit |
|---|---|---|
| KAFF-109 | `User.ChangeRole` gained the guard closing `V-26-A` | `7ff500e` (D-088) |
| KAFF-105a | `WhoAmI` endpoint and handler — hand-copied checks replaced by `RequireLiveSession()` | `f807364` (D-089) |
| KAFF-102 | `SignOut/Handler.cs` — `LiveSession.ResolveAsync` before the audit row | `f807364` (D-089) |
| **KAFF-101a** | `SignIn/Handler.cs`, `StaffSessionMinter.cs` — role bar became `StaffSessionRules.MayHoldStaffSession` | `f807364` (D-089) |
| **KAFF-103** | `ChangePassword` endpoint and handler rewritten onto `LiveSession`; the `V-26-F` ordering pinned | `f807364`, `4f9fc62` |

The last two were **accepted**, at a commit that is no longer HEAD. That is SM-29 applied to an
acceptance rather than to a story: a verdict is a claim about a tree, and this one aged in eleven
hours. **Green is not accepted.** Three of these five were green on the morning they were rejected.

**No story in this sprint has passed Nabil's acceptance gate.** `process/agile.md` §4 makes acceptance
Nabil running the demo script; there is no screen to run one against. `ACCEPTED` in `stories/` means
*an independent session verified it and found no open defect* — the meaning the file has used since
KAFF-116.

**Gates at HEAD `33f1779`:** build **0 warnings / 0 errors** (`--no-incremental` Release, every project
relinked — not the MSB3026 "succeeded and copied nothing" case), `dotnet format --verify-no-changes`
exit 0, Domain **97/97**, Api **215/215**, `scripts/check-citations.ps1` **872 checked, 0 broken, 0
legacy**.

---

## 2. Routing — every finding has an owner, not a register row

`agents.md` §3b: *a defect recorded in a register and assigned to nobody is a defect nobody is
fixing.* Nothing below is closed by being written here.

### 2.1 The seven findings of `qa/slice-1/verification-2026-08-26.md`

| Id | State today, re-established against the files | Owner |
|---|---|---|
| `V-26-A` HIGH | **Fixed, unverified.** `ChangeRole` refuses the conversion as a `Result` before the revocation loop [Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ChangeRole`]. D-088, `7ff500e` | **Verifier** — re-verify at HEAD |
| `V-26-B` HIGH | **Fixed, unverified.** The three checks live in one place and declaring the exemption is the same act as paying for it [Verified: 2026-08-26 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`]. D-089, `f807364` | **Verifier** |
| `V-26-C` MEDIUM | **Fixed by construction, unverified.** Sign-out asks `LiveSession` the same question every other exempt route asks. D-089 | **Verifier** |
| `V-26-F` MEDIUM | **Fixed, unverified.** Pinned at both levels — the pure function and the wire. D-090, `4f9fc62` | **Verifier** |
| `V-26-D` LOW | **Fixed, and the record does not say so.** The comment now states both reasons and names which one was false [Verified: 2026-08-26 @ `src/Api/Features/Auth/ChangePassword/Handler.cs` -> `HandleAsync`]. **D-089's "Not done" list does not mention `V-26-D`**, so the written record says open and the code says closed | **Backend** — one line crediting it, then **Verifier** |
| `V-26-E` observation | **Open, and it changed shape under the fix.** See below | **QA**, and **Nabil** |
| `V-26-G` MEDIUM | **Open, and worse than when it was written.** See below | **QA**, copied to **BA** |

**`V-26-E`.** `TC-1-021`'s given — *"given a signed-in `Role.Client`"* — was unsatisfiable when the
Verifier wrote it. After D-089 it is unsatisfiable **and** the test that stood in for it asserts the
opposite of what it did: `A_client_role_session_can_sign_out_too` now asserts a `204`, a cleared cookie
and **no** audit row, where it asserted a row [Verified: 2026-08-26 @ `tests/Api.Tests/SignOutTests.cs`
-> `A_client_role_session_can_sign_out_too`]. QA rewrites the case. **Nabil owns the other half** —
D-089 §🟡 1 flags this as a change to what an accepted criterion (`AC-102-F`) is proved against, and
that is his to accept, not Backend's to assert and not mine to wave through.

**`V-26-G`.** `stories/ac-id-map.md` retired `AC-105a-F` on 2026-08-22 and instructed that `TC-1-042`
be relocked to `AC-105a-H` **and rewritten**, *"whose assertion is the inverse — it cannot be carried
across unrewritten"* [Verified: 2026-08-26 @ `stories/ac-id-map.md` -> the `KAFF-105a AC6` row]. Five
days on it still cites the retired id and still asserts the withdrawn rule, so **it fails against
correct code**. **And `V-26-B`'s fix makes it more wrong, not less:** `/api/auth/me` now refuses
`Role.Client` outright, so the case cannot be relocked at all — the behaviour it would assert no longer
exists at that route. QA rewrites it and must choose the level; the BA is copied because the criterion's
provable level moved out of the story's own endpoint.

### 2.2 New this run — `SM-32`: `AC-105a-H` lost its Api-side coverage by consequence, not by decision

D-089 and the verification report both cited a test named
`A_portal_client_holds_no_company_wide_permission`. **No such test exists, and after `V-26-B` none can**
— a case asserting that a portal client reaches `/api/auth/me` and receives an empty permission set has
nothing left to assert, because the route refuses that caller. Both citations now point at the fact
where it survives [Verified: 2026-08-27 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` ->
`A_client_holds_no_company_wide_permission`], corrected in `33f1779`.

**The fix that closed the defect removed the test the documentation went on citing**, and the
substance of `AC-105a-H` moved from the Api suite to the Domain suite as a side effect. Backend
recorded it in D-090's neighbourhood; **a reader of `KAFF-105a` will not have read D-090.**

* **BA** — record it in the story, so the criterion says where it is now proved.
* **QA** — the replacement for `TC-1-042` is the same decision; do not write two.
* **Nabil** — D-089 §🟡 2 already flags it as a change to what an accepted criterion is proved against.

### 2.3 Uncovered QA cases, from §7 of the report

| Case | What it pins | Owner |
|---|---|---|
| `TC-1-120` | KAFF-114 rule 7 — revoking the last person on a project is allowed. The case exists to pin an **absence**, so nothing goes red the day somebody adds a minimum-team rule | **QA → Backend**, P2 |
| `TC-1-094` | KAFF-112 rule 4 — the username stays reserved while the account is off; asserts `ux_users_user_name` is not filtered on `is_active`, exactly the index predicate a later migration adds without noticing | **QA → Backend**, P2 |
| `TC-1-027` Api half | `AC-103-H` — the Domain half passes, the Api half has no test | **QA → Backend**, P2 |
| `TC-1-046` | The `/api/auth/me` payload carries no money — satisfied structurally, by inspection rather than by an assertion | **QA**, P2 |
| `TC-1-079` | `PENDING Q27 (residual)`. **The register says Q27 is closed** — *"REVERSED and closed"* [Verified: 2026-08-26 @ `stories/questions-for-karim.md` -> the `Q27` row] — so the case is pending on something no open entry names | **BA** — number the residual or retire the marker. Not mine to answer |

### 2.4 W-numbered findings, re-checked against the files rather than inherited

**Closed:**

| Id | Why it is closed |
|---|---|
| `W-1` | The constraint exists [Verified: 2026-08-26 @ `src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` -> `ck_audit_records_actor_is_named_completely`] and refuses a half-named actor outright |
| `W-6` | The *"two members means two decisions"* comment is gone; `AllowList` now carries five reason-bearing entries [Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `AllowList`] |
| `W-7` | D-074 and D-077 are the build entries it asked for |
| `W-9` | The deferral marker was already in the story; the Scrum Master's ruling of 2026-08-25 confirmed it |
| `W-11` | **legacy = 0** at 872 checked, 2026-08-27 |
| `W-12` | Three mutations re-executed and watched red — `verification-2026-08-26.md` §4.4 |
| `W-8` | **Closed by this run's sweep, `33f1779`.** It named three stale story statuses; by the time it was actioned there were twelve |

**Open, and re-routed rather than re-logged** — none carries a closing entry anywhere in D-076…D-090:

| Id | What | Owner |
|---|---|---|
| `W-2` | The `ActorRole` test helpers read the role from the database at request time, so the claim/database divergence D-073 names is **unobservable by any test** | **QA → Backend** |
| `W-3` | `AC-106-B`'s *"every refusal is logged"* is implemented and emits, and no test can fail on it disappearing | **QA** |
| `W-4` | `TryAdd` in the problem-details callback is untested; flattening a specific domain `Forbidden` key to the generic one turns nothing red [Verified: 2026-08-26 @ `src/Api/Program.cs` -> `AddProblemDetails`]. Latent — no handler returns an `ErrorType.Forbidden` today | **QA → Backend** |
| `W-5` | Framework-produced 400 / 404 / 415 carry no `messageKey`; only 401 and 403 are filled. No criterion requires it — a scope question, not a defect | **Architect** |
| `W-10` | `AC-108-G` appears nowhere in `qa/slice-1/test-cases.md` [Verified: 2026-08-26 — searched, absent]. Covered in code, absent from traceability | **QA** |

### 2.5 The citation checker's remaining blind spot — Scrum Master's ruling

`e9f3dcf` repaired a matcher that worked line by line, so a citation wrapped across two lines was
never checked: **118 were silently unverified**, 753 → 871, and two of them were broken. What the fix
reports as still open is that **a citation missing the `@` prefix is neither counted as legacy nor
verified** — D-058 and D-059 already knew.

**It bit twice this week.** The same non-existent identifier appeared in three places and the checker
saw one: the flagged citation in `decisions.md`; the same name in the verification report's prose,
invisible because it carried no `@`; and the same name in an XML doc comment in
`tests/Api.Tests/MeTests.cs`, invisible because the checker walks `*.md` and not source.

**Ruling: the `@` boundary stays, and is named rather than left to be rediscovered.** Widening the
matcher to every `` `A` -> `B` `` would flag SM-31's own writing convention, every meeting file, and
every discussion of a past citation — which is D-059 §9's exemption problem inverted. **A checker that
cries wolf gets muted, and a muted checker is D-046's green light by another name.** What closes the
gap is not a wider regex but a sentence: **a reference without `@` is not a citation, carries no
verification claim, and must not be written where one is needed.** One line added to SM-31 in
`process/agile.md` — Scrum Master, done in this run.

**What is *not* closed, and is larger:** source-file citations are outside the checker entirely. Every
`<c>File.cs</c> -> <c>Identifier</c>` in an XML doc — and this codebase is full of them, deliberately,
because the reasoning lives beside the code — is unverified by anything. Extending the checker to
`*.cs` needs the writing convention restated for XML docs first, so it is not a regex change.
**Routed to Backend as a named, open gap. Nobody should assume the 872 covers source.**

---

## 3. The four business questions standing with Nabil

**None of these is answered here, and none may be answered by consensus or to close a sprint**
(`agents.md` §3b). Each is recorded with what is built in the meantime and what the build cost if the
ruling goes the other way.

1. **Converting a user to `Role.Subcontractor` — refuse, or succeed and clear the credential?**
   D-088 records both readings and built the **reversible** half: `ChangeRole` refuses while a
   credential is stored, returning the `409` the database constraint already implies. A later ruling
   can relax it to "convert and clear" and nothing is lost; clearing a credential the Owner did not ask
   to clear destroys it, kills every session, and cannot be undone by a ruling that arrives afterwards.
   `spec.md` §9 — *"record only, no login"* — satisfies both readings, which is why it is a question.

2. **`KAFF-118`'s cut from a locked sprint.** Unbuilt, 3 points. It depends on `KAFF-119`, deliberately
   deferred out of the sprint, so its client-registration half cannot complete as written whatever is
   ruled. The standing proposal — cut it as a story, keep rule 2 as an acceptance check, since the
   interceptor's own tests already assert that no handler constructs an audit record — is sound and is
   **still not the Scrum Master's to take.** The scope lock is Nabil's.

3. **The reach of a `mustChangePassword` session** beyond `/api/auth/me` and the change-password
   endpoint. `KAFF-101a` rule 8, `AC-101a-F` and `AC-103-B` all assert the strict reading and all three
   cite **D-049 ruling 4, which names no endpoint.** `TC-1-018` carries it as a flag; `AC-101a-F` is
   covered by no test and D-084 reports it uncovered rather than quietly dropping it. The two readings
   differ by whether a hostile client can skip the change screen.

4. **Q28 — the lockout is per account, and trivially exploitable.** Anybody who knows a site engineer's
   username can hold him out fifteen minutes at a time, from anywhere, indefinitely; the suite does it
   in five HTTP requests. Registered [Verified: 2026-08-26 @ `stories/questions-for-karim.md` -> the
   `Q28` row], and the row records that **Karim was not shown this consequence** when he ruled.

**And four smaller ones already routed to Nabil by the entries that raised them, confirmed still
open:** whether a no-op sign-out should leave a trace and naming whom (D-085); whether the inactive
account's generic `401` — an extension of Nabil's own Q47 reasoning to a case he was not asked about —
is what he wants (D-084 §🟡 2); whether the Owner may change his own role (D-082 §5); and D-089's two
changes to what an accepted criterion is proved against (§2.1 and §2.2 above).

### Q54 — the brief this session was given was wrong about it, and the correction matters

**Q54 is answered.** Nabil ruled it on 2026-08-24, D-072 §3, verbatim: *"Once we reach Slice 9
(Compliance/Archival), we will implement **PostgreSQL table partitioning by month** on `audit_records`
… drop entire historical partitions once the legal retention period expires."*

What is open is **N11, its consequence**, routed to the **Architect** by Nabil's own instruction:
converting a *populated* append-only, trigger-protected table into a partitioned one is a new table
plus a data migration plus a swap — precisely the table you least want to rewrite. **The deadline is
not slice 9; it is before the first real rows exist**, and slice 3 is when money history starts.

**D-079 does not reopen Q54 — it makes N11 more urgent.** Until the trusted-proxy work landed, the
column would have recorded a Docker bridge address, which is not personal data by any reading. From
the next deploy it records a real end user's address, which is. Same ruling, same mechanism, a subject
that now exists.

**And the register row is stale:** `stories/questions-for-karim.md` -> the `Q54` row still reads *"Not
settled by any agent"* [Verified: 2026-08-26]. **Routed to the BA** — mark it answered against D-072 §3
and carry the consequence to N11. This is bookkeeping against a ruling Nabil already made, not a
resolution.

# qa/ — test cases for Kaff ERP

Written by the QA agent **from `spec.md` and the stories, before the code exists**. Nothing in this
directory was derived by reading a handler. Where a story is `BLOCKED`, the cases stop where the
citation stops and the rest is marked `PENDING`.

> **The hard rule (`agents.md` §3c):** a test case must be able to **fail**. A scenario that passes
> whether or not the rule is implemented is worse than no scenario, because it reports safety that
> does not exist.
>
> This is not theoretical. `decisions.md` **D-046** documents three cases of exactly that shipping in
> this project inside one afternoon: `dotnet test` running zero tests and reporting success; a schema
> test asserting on a value that was always null; and an end-to-end suite that skipped all four of its
> tests and exited 0. Read D-046 before writing a case here.

---

## What is in here

```
qa/
  README.md              this file
  strategy.md            the test strategy for the whole product, not just slice 1
  risk-register.md       risks the tests must cover because nothing else will
  questions.md           what QA needs answered, and every story ↔ spec contradiction found
  slice-1/
    hls.md               high-level scenarios — business journeys, readable by Nabil and Karim
    test-cases.md        the detailed cases, grouped by story
    permission-matrix.md 9 roles × 31 permissions, with expected outcome and citation
```

---

## The ID scheme

| Kind | Format | Example | Notes |
|---|---|---|---|
| High-level scenario | `HLS-<slice>-<nn>` | `HLS-1-03` | A business journey across several stories. |
| Test case | `TC-<slice>-<nnn>` | `TC-1-047` | One case. Traces to exactly one story and one AC. |
| Permission cell | `PM-<Role>-<Permission>` | `PM-Hr-ProjectRead` | A cell in `permission-matrix.md`. |
| Risk | `RSK-<nn>` | `RSK-03` | An entry in `risk-register.md`. |
| Finding | `F-<nn>` | `F-07` | A defect or contradiction found while writing, listed in `questions.md`. |

**IDs are permanent.** Like story IDs (`process/agile.md`), a renumbered test case silently detaches
from whatever referenced it. A deleted case leaves its number burned.

### Question numbering — read this, it is a trap

There are **two** question registers in this repository and their numbers collide:

| Register | Range | `Q1` means |
|---|---|---|
| `stories/questions-for-karim.md` | Q1–Q26 | Who may read the audit trail |
| `ux/questions.md` | Q1–Q15 | How does the first Owner come to exist |

A bare `PENDING Q3` is therefore ambiguous — BA's Q3 is session length, UX's Q3 is what HR may see of
a project. **This directory always writes `Q-BA-n` or `Q-UX-n`.** The collision itself is a finding
(`questions.md` F-01) and should be resolved by merging the two registers into one.

---

## Priority scale

| | Meaning | What it gates |
|---|---|---|
| **P1 — blocker** | Money, permissions, audit immutability, or portal leakage. A failure here means the slice does not ship. | Slice 1's gate is *permission tests pass* (`agents.md`), so every permission case is P1. |
| **P2 — major** | A rule `spec.md` states that is not in the money or permission path — deduplication, state guards, refusal keys, RTL correctness. Shippable only with Nabil's explicit acceptance of the gap. |
| **P3 — minor** | Presentation, empty states, i18n coverage, ergonomics. Recorded, not blocking. |

A case that is P3 at the API layer and P1 at the domain layer is written twice, at both layers, with
the priority each deserves. Hiding a control is presentation; refusing the request is security
(`CLAUDE.md`).

---

## Layers, and what runs where

| Layer | Project | What belongs here |
|---|---|---|
| **Domain** | `tests/Domain.Tests` | Pure rules: `PermissionEvaluator`, entity invariants, `Result<T>` failures. No database, no HTTP. |
| **Api** | `tests/Api.Tests` | The gate through real routes against **real PostgreSQL**: policy provider, project resolution from the route, assignment lookup, database guards, the audit interceptor. |
| **E2E** | `tests/E2E.Tests` | Playwright against the running stack: the demo script, Arabic RTL at 390px, i18n resolution. |

Every case in `slice-1/test-cases.md` names its layer. A case whose rule is enforced by a **database
constraint or trigger must run at the Api layer against real PostgreSQL** — see `strategy.md`,
"Why a fake provider cannot test a database rule".

---

## Traceability, in both directions

`agents.md` §3c: *"every acceptance criterion has at least one test case, and every test case names
the story and the `spec.md` section it comes from."*

**Forward — AC → case.** `slice-1/test-cases.md` is grouped by story, in `KAFF-1nn` order, and every
AC in all 25 slice-1 stories appears with either a case or an explicit `PENDING Q-xx-n` line. There
are no silent gaps: an AC with no case is a visible row saying so.

**Backward — case → source.** Every case carries a `Story` field naming the story ID and the AC, and
a `Source` field naming the `spec.md` section or `decisions.md` D-number the expected result comes
from. **A case with no citation is not a case; it is an invented rule.**

**The permission matrix carries its own citation per cell**, because the matrix is slice 1's gate and
a cell justified by "that is what the catalogue says" would certify the catalogue against itself.

---

## The handoff: QA writes, the Verifier executes

They are different agents, in different sessions, and the separation is deliberate.

```
QA (this directory)                     Verifier (fresh session)
  reads spec.md + stories        →        reads spec.md + these cases
  writes cases before the code   →        executes them against the built code
  never reads a handler          →        never reads a handler, never fixes anything
                                          reports; failures go back to the author
```

`agents.md` principle 2: *"An agent asked to test what it just wrote will write tests that pass."*
The same logic applies one step earlier — an agent asked to judge whether its own scenario ran
meaningfully will conclude that it did. So QA does not execute, and the Verifier does not author.

### What the Verifier needs from a case, and what it may not do

- **Execute the case as written.** If a case cannot be executed as written, that is a finding to
  report, not a case to rewrite. Rewriting a case to make it pass is the failure this split exists to
  prevent.
- **Record every case's result**, including the ones that failed and why they now pass
  (`process/agile.md`, Definition of Done).
- **Never derive an expected result from the implementation.** If the code does X and the case says
  Y, the case is right until `spec.md` says otherwise.
- **Treat a `PENDING` case as not run**, never as passed. A `PENDING` line is a hole in coverage that
  a business answer fills, not a case that happens to be green.
- **Apply the mutation check to any case that has never been seen red.** `strategy.md`, "How we know
  a test can fail".

### Existing harness the cases are written against

Slice 0 left a working harness and the cases assume it: `PostgresDatabase` (a collection fixture, one
database per run), `KaffApiFactory`, `TestAuthHandler` (signs the caller in from `X-Test-*` headers so
no token issuer is needed — **authorization still runs for real**), `ProbeEndpoint`, `DatabaseGuard`,
`UniqueNames`.

Two harness facts that change how a case must be written, both from D-046:

1. **`dotnet test` reports `Zero tests ran` and exits 5.** CI invokes the test executables directly.
   A Verifier that runs `dotnet test`, sees no failures and reports green has reproduced D-046.
2. **The database is shared across the whole run and re-seeded per test method.** Use `UniqueNames`
   for usernames, codes and phone numbers. A hand-rolled random suffix is the 5%-flaky bug D-046
   fixed, and a phone collision *merges* two clients rather than failing loudly.

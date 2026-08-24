# KAFF-122 · Set a corporate client's withholding category and tax registration number

> # ⛔ SUPERSEDED — 2026-08-21
>
> **Replaced by `KAFF-416` (slice 4), and partly absorbed by `KAFF-120` (slice 1).**
>
> **Karim changed his mind, and the change removes this story's subject from the client record and
> from slice 1 altogether.** The file stays, marked here rather than edited quietly, per
> `agents.md` (BA duties) and `stories/README.md`.
>
> **Do not build this story. Do not re-create it in slice 1.**

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 *(moved to slice 4 with KAFF-416)* ·
**Status:** Superseded
**Spec:** §6.7 (**amended**), §2 · **Decisions:** D-040, D-045, **D-049 (rulings 9, 10)**
**Superseded by:** KAFF-416 · **Absorbed into:** KAFF-120

---

## What was asked, and what Karim answered

The story was blocked on two questions. **Both are answered, and the first answer moved the field.**

**Q10 — does the withholding rate belong to the client, or to what is being billed?**

> **The rate belongs to the contract, not to the client.** Karim: *"The same client (e.g. a government
> body) might sign a design contract (one rate) and an execution contract (another rate). Storing it on
> the client profile breaks this reality."* — `spec.md` §6.7, amended · D-049 ruling 9

**Q11 — Marketing or Finance?**

> **Finance sets it, during contract creation or approval. Marketing cannot.** The rate *"directly
> dictates ledger entries and money reconciliation. It is a strict accounting parameter, not a
> marketing detail."* — D-049 ruling 10

**The story's own analysis was what surfaced it**, and it turned out to be a contradiction inside
`spec.md` rather than a gap: §6.7 sets the rate by *what is supplied* — 1% contracting and supplies, 3%
services, 5% professional fees — while its own last line asked the Client to carry *"a flag"*. §5.4
lets one client hold a design contract and an execution contract at once, so one value per client could
never express both. §6.7 now carries the amendment that resolves it.

## What has already been built, so nobody rebuilds it

| Change | Where |
|---|---|
| `Project.WithholdingCategory`, defaulting to `None` | [Verified: 2026-08-22 @ `src/Domain/Projects/Project.cs` -> `WithholdingCategory`] |
| `Project.SetWithholding(category, clientKind)`, refusing a rate on an individual's contract | [Verified: 2026-08-22 @ `src/Domain/Projects/Project.cs` -> `SetWithholding`] |
| `Client.WithholdingCategory` **removed**; `Client.Create` no longer takes one; there is no setter | [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `class Client`, `Create` — no withholding member on the entity] |
| `Client.SetWithholding` → **`Client.SetTaxRegistration`**, refusing a registration number on an individual | [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `SetTaxRegistration`] |
| Migration `WithholdingOnContractAndSoftPhoneDedup`, applied | [Verified: 2026-08-22 @ `src/Infrastructure/Persistence/Migrations/20260821121804_WithholdingOnContractAndSoftPhoneDedup.cs`] |

**`Down` cannot restore the data**, and the migration says so: a project's rate cannot be pushed back
onto its client when two projects for one client disagree. Reversing restores the shape, not the data.

## Where the remaining work went

| Work | Story | Slice |
|---|---|---|
| Refusing a rate on an individual's contract, and a registration number on an individual — proof and endpoint wiring | **KAFF-120** | 1 |
| **Finance sets a contract's withholding category** at contract creation or approval | **KAFF-416** | 4 |
| Computing the withheld amount on a collection and posting it to the tax-withheld-at-source asset | KAFF-317 | 3 |
| Withholding Kaff carries as a liability on subcontractor and supplier payments | KAFF-318 | 3 |

**Why slice 4 and not slice 1.** The rate now belongs to a contract, and **nothing in slice 1 creates
or edits a project** — no slice-1 story creates or edits one.

> **Corrected 2026-08-22 (marked, per README — this file is Superseded and is not edited silently).**
> This said `ProjectManage` "is granted to nobody". **False since D-052 §2:** Karim ruled on
> 2026-08-21 that the Owner and the Technical Office may open a project, and the row now carries those
> grants. Q17 is answered. The reason this story is still slice 4 is unchanged and does not depend on
> the corrected sentence — nothing in slice 1 creates a contract for the rate to sit on. There is no endpoint in slice 1 for
this field to live on, and adding one to keep the story would be granting a project permission to make
a story fit, which is the mistake KAFF-113 already names.

> **Corrected again, 2026-08-22 — the permission this story's successor needs is no longer
> `ProjectManage`, and naming the wrong one is how a Superseded file misdirects the story that
> replaced it.** The row was split three ways on 2026-08-22 (**D-055 §§1–3**), all
> [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs`]:
>
> | Permission | Scope | Grants | Governs |
> |---|---|---|---|
> | `ProjectCreate` | `CompanyWide` | Owner, Technical Office | **opening** a project (`:213-215`) |
> | `ProjectManage` | `ProjectScoped` | Owner, Technical Office | **editing** a project (`:200-202`) |
> | **`ProjectFinancialsEdit`** | `ProjectScoped`, `TouchesMoney` | Owner, **Finance** | the contract's tax and financial settings alone (`:238-241`) |
>
> **`KAFF-416` is gated on `ProjectFinancialsEdit`.** The reason it is a third row rather than a
> Finance grant on `ProjectManage` is the substance of D-055 §1: **the Finance department will never
> hold `ProjectManage`, because an accountant must not alter the engineering scope of a project.** A
> grant written to reach one field would hand over the whole record — the same shape as D-035, D-044
> ruling 2 and F-04, seen from the other direction.
>
> **And N10 is no longer open.** Anywhere in this file or the backlog that says slice 4 is blocked on
> the scope of `ProjectManage` is stale: **it is approved and built**. What is open for KAFF-416 is
> **Q-N10-2b**, a workflow question for Karim — Finance has no global reach, so Finance cannot set a
> new contract's withholding until somebody assigns Finance to that project
> [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`].

## Two things that survive this story and must not be lost

**The tax registration number stayed on `Client`** — it identifies the legal entity and does not vary
by contract (D-049 ruling 9). Ruling 10 moved the **rate** to Finance and said nothing about the
number, which `ClientManage` still governs (Marketing and Owner, §2, D-044 ruling 4).

**An ordering problem for slice 3.** KAFF-317 computes withholding on a collection using a rate that
KAFF-416 sets — and slice 3 runs **before** slice 4. Slice 3 works against project fixtures, so it is
not a blocker, but the fixtures must carry a rate that somebody set deliberately, and the §15 worked
example is the thing that will notice if they do not. Recorded in `backlog.md` against both slices.

## Open, and carried forward rather than closed

- **Q29** — 🟡 **Not ruled on: subcontractors and suppliers.** §6.7's next paragraph — *"when Kaff pays
  subcontractors and suppliers, Kaff withholds"* — has exactly the same shape, and those rates are
  still held on the party record. **Karim's ruling named the client only, so nothing was changed
  there**, and extending it would be inventing the ruling he did not give.
- **Q30** — whether a contract's withholding rate may change after the first extract is issued.
- **§16 assumption 18** — which clients are corporate withholding entities — is unaffected by rulings 9
  and 10 and remains Karim's.

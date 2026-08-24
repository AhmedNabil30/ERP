# KAFF-120 · An individual's contract cannot carry a withholding rate, and nor can the individual

**Slice:** 1 · **Epic:** Foundation · **Points:** 2 · **Status:** Ready
**Spec:** §6.7 (**amended**) · **Decisions:** D-040 (**closed**), D-045, **D-049 (rulings 9, 10)**
**Depends on:** KAFF-119

## Story
As Kaff, I need the system to refuse a withholding rate wherever it could be claimed for an
individual client — on the contract, and on the client's own record — because `spec.md` says plainly
that individuals do not withhold, and until 2026-08-21 the code accepted one in both places.

## What changed, and what this story is now for
**The domain rule now exists.** It was a defect when this story was written; it was built as part of
D-049 and the story has been rewritten around what is left rather than deleted:

| Was | Is | Where |
|---|---|---|
| `Client.SetWithholding` accepted any category on any client | **`Client.SetTaxRegistration`** refuses a registration number on an individual | [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `SetTaxRegistration`] |
| `Client.Create` took a withholding category | It does not. **The client carries no category at all** | [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `class Client`, `Create` — the properties are `Code`, `Name`, phone pair, `AlternatePhone`, `Email`, `Address`, `Kind`, `TaxRegistrationNumber`, `Notes`, `IsActive`, `CreatedAt`; no withholding member] |
| Nothing enforced the rule for a contract | **`Project.SetWithholding(category, clientKind)`** refuses a rate on an individual client's contract | [Verified: 2026-08-22 @ `src/Domain/Projects/Project.cs` -> `SetWithholding`, with `WithholdingCategory`] |

**That closes both halves of D-040.** So this story is no longer the domain rule. **It is the wiring
and the proof:** the endpoints that can reach those two methods must surface the refusal correctly,
in both languages, and a test must exist that fails if either guard is removed.

**The client's kind is passed into `Project.SetWithholding` rather than looked up**, because the
domain holds only `ClientId` — and D-049 records why it is not left to the caller: the rule is too
expensive to trust to whoever writes the next handler.

## The i18n key — closing finding F-08
Two documents carried two keys for one refusal. **The real one, in the code and in both catalogues
today, is:**

```
errors.master.individual_does_not_withhold
```

`stories/KAFF-120` previously said `errors.master.individual_client_does_not_withhold`, which exists
nowhere. `ux/slice-1-flows.md` S-012 had it right. **The story is corrected**; the UX document needs
no change. `TC-1-166` asserts the key exists in both catalogues and would have caught the mismatch.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | *"Individual clients do not withhold."* Unchanged by ruling 9, and now enforced in two places | §6.7 · §6.7 amendment |
| 2 | A withholding category other than `None` on a contract whose client is an `Individual` is refused | §6.7 amendment · D-049 ruling 9 · `Project.SetWithholding` |
| 3 | A tax registration number on an `Individual` is refused — it is the same claim by another field | §6.7 amendment · `Client.SetTaxRegistration` |
| 4 | The tax registration number stays on the **client**, because it identifies the legal entity and does not vary by contract. The **rate** does not | D-049 ruling 9 |
| 5 | Both refusals live in the **domain**, not in a validator. A validator guards one endpoint; the invariant belongs to the entity | CLAUDE.md (*entities have behaviour*) · D-040 |
| 6 | The refusal is a `Result` failure carrying `errors.master.individual_does_not_withhold`, not an exception | CLAUDE.md |
| 7 | A project defaults to `WithholdingCategory.None` — the safe default, because between creation and Finance's decision the contract must claim no rate rather than guess one | D-049 ruling 9 |
| 8 | Every endpoint that can reach either method translates the refusal. A key that reaches the screen unresolved is an Arabic-speaking user reading `errors.master.…` | slice 0 `problem-details.ts` · D-047 (the test that fails on a missing key) |

## Permissions, money, audit, i18n
- **Permissions:** `ClientManage` (Marketing, Owner) for the client half
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ClientManage`]. **The contract half
  is Finance's and it now has a permission of its own: `ProjectFinancialsEdit`** — `ProjectScoped`,
  `TouchesMoney: true`, granted to `Role.Owner` and `Role.Finance` and to nobody else
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`; D-055 §1].
  **It is not `ProjectManage`, and that is the whole point of it:** the Finance department will never
  hold `ProjectManage`, because an accountant must not alter the engineering scope of a project.
  Still no endpoint in slice 1 — see below.
- **Money:** **this story is money.** The withholding rate decides how much cash a collection is
  expected to carry against an issued extract (§6.7). A wrong rate makes every collection for that
  contract reconcile short — *"collections will never match issued extracts and staff will invent
  adjustments to close the gap"*. It is a 2 rather than a 5 only because the rule itself is now built;
  what is left is wiring.
- **Audit:** no new record. It adds refusals, and a refusal path writes nothing (AC-118-I).
- **i18n:** `errors.master.individual_does_not_withhold` — **already present in both `ar.json` and
  `en.json`.** Nothing to add; the story's job here is to stop a second key being invented.

## Acceptance criteria
**AC-120-A — a tax registration number on an individual is refused, through the API** *(fails if the rule is broken)*
Given I am Marketing, and a client of kind `Individual`
When I set a tax registration number on them
Then it is refused with `errors.master.individual_does_not_withhold`, and the stored value is unchanged

**AC-120-B — and the refusal reads as Arabic, not as a key** *(fails if the rule is broken)*
Given the refusal of AC-120-A with the UI in Arabic
When the message is rendered
Then it resolves from `ar.json`, and the raw key appears nowhere on the screen

**AC-120-C — a rate on an individual's contract is refused** *(fails if the rule is broken)*
Given a project whose client is an `Individual`
When `Contracting`, then `Services`, then `ProfessionalFees` is set on it
Then all three are refused with `errors.master.individual_does_not_withhold`, and the stored category stays `None`

**AC-120-D — `None` is always legal**
Given a project whose client is an `Individual`
When `None` is set
Then it is accepted

**AC-120-E — a corporate contract is unaffected**
Given a project whose client is `Corporate`
When a category is set
Then it is accepted — **which category it should be is KAFF-416 in slice 4** (D-049 rulings 9 and 10). **Who may set it is settled: `ProjectFinancialsEdit`, Owner and Finance** [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`]. This criterion is asserted against the domain method, with no permission in play

**AC-120-F — the client record has no category to set** *(fails if the rule is broken)*
Given the `Client` entity, its API contract and the `clients` table
When each is inspected
Then there is no withholding category on any of them, and no endpoint accepts one

**AC-120-G — the rules live in the domain** *(fails if the rule is broken)*
Given Domain unit tests calling `Client.SetTaxRegistration` and `Project.SetWithholding` directly, with no HTTP involved
When an individual is given a registration number, and an individual's contract a rate
Then both return `Result` failures — the rules are not reachable only through the API

**AC-120-H — one key, not two** *(fails if the rule is broken)*
Given `ar.json` and `en.json`
When they are searched
Then `errors.master.individual_does_not_withhold` is present in both and `errors.master.individual_client_does_not_withhold` is present in neither

## Not in this story
**The endpoint that sets a contract's withholding rate.** Ruling 10 gives it to Finance, and ruling 9
puts it on the contract — but **nothing in slice 1 creates or edits a project**. The guard is proved
by domain test here and wired to an endpoint in **KAFF-416**, slice 4. Anyone tempted to add a project
endpoint to slice 1 to close this should read KAFF-113's "Not in this story" first.

**The permission side is fully settled now, and this paragraph used to say otherwise.** Three rows,
not one [all Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs`]:
**`ProjectCreate`** — `CompanyWide`, Owner and Technical Office, opening a project (`:213-215`);
**`ProjectManage`** — `ProjectScoped`, Owner and Technical Office, editing it (`:200-202`);
**`ProjectFinancialsEdit`** — `ProjectScoped`, `TouchesMoney`, **Owner and Finance**, the contract's
tax and financial settings alone (`:238-241`). **KAFF-416 is gated on `ProjectFinancialsEdit`, not on
`ProjectManage`.**

> **Corrected 2026-08-22 under SM-29.** This paragraph said *"the endpoint is still not buildable,
> because the row is `ProjectScoped` and a create request names no project (**N10**, Architect)"*,
> and cited `PermissionCatalogue.cs` at lines 180-182, which is not where `ProjectManage` is. **N10 is
> approved and built** (D-055 §3), and **Q17 is closed in full** — holder by D-052 §2, scope residual
> by D-055 §3. **Slice 4 is no longer blocked on a permission.** What is open for KAFF-416 is a
> workflow question — **Q-N10-2b**: Finance has no global reach, so on a newly-opened project Finance
> cannot set the withholding category until HR or the Owner assigns Finance to it, while Karim said
> Finance sets it *"during contract creation or approval"*
> [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync` — global
> reach is `Role.Owner` and `Role.Hr` only; everyone else falls through to the assignment lookup].
> It is registered in `stories/questions-for-karim.md` and is Karim's, not this story's.

Computing the withheld amount, posting it to the tax-withheld-at-source asset, or anything else in
§6.7's money path: slice 3 (KAFF-317). Withholding Kaff carries as a liability on subcontractor and
supplier payments: slice 3 (KAFF-318), and **whether those rates follow ruling 9 at all is Q29, open.**

## Questions for Karim
None. §6.7 and its amendment answer this one, which is what made it a defect and now makes it wiring.

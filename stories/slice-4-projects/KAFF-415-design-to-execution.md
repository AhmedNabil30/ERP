# KAFF-415 · A design project turns into an execution project

<!-- kaff id=KAFF-415 slice=4 points=8 state=VERIFIED verdict=CONDITIONAL at=91cdcb3 on=2026-11-17 -->

**Slice:** 4 — Projects · **Points:** 8 (the `design_to_execution` half only; `parent_child` stays in the backlog).
**Owner:** Backend (money: the 30% credit), then UI Designer (the action on the design project).
**Source:** `spec.md` §5.4 `design_to_execution`; client comment relayed by Nabil 2026-11-09, `decisions.md` D-207 §2.
**Spec:** §5.3 (Design has no BOQ), §5.4, §6.4 (five ledgers never net), §6.9 (one `Adjustment` object; the 30% design credit is one of its types).

## What is already true (verified 2026-11-09 against the tree)

- `Project.LinkedProjectId`, `Project.LinkType` and `Project.LinkTo(Guid, ProjectLinkType)` exist; `ProjectLinkType.DesignToExecution` exists. Nothing calls it for this purpose.
- `ContractDefaults.DesignToExecutionCreditRate` is 30%. No posting uses it.
- `AdjustmentType.DesignCredit` exists and is marked "not built". `Adjustment.RaiseCreditNote` targets an issued extract; there is no extract at signature.
- A Design project's contract value is area x rate (`ContractValueSource`, `AreaTimesRate`). `ContractValueSource.AllowsBoq(Design)` is false: **a design project has no BOQ.**

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-415-A | A design project offers "convert to execution". It creates a new project for the same client, of an execution type (Lump Sum or Cost Plus, chosen by the user), linked back to the design project with `ProjectLinkType.DesignToExecution`. The design project is not mutated into another type | `spec.md` §5.4 ("A project MUST NOT mutate") · D-207 §2 · Q-207-9 |
| AC-415-B | The action is refused on a non-design project, and on a design project that already has an execution project | Q-207-7 |
| AC-415-C | The execution project gets its own account set, created exactly as any new project's. No account is shared with the design project | `spec.md` §5.4 ("separate accounts") · §6.4 |
| AC-415-D | When the execution project is signed, one `Adjustment` of type `DesignCredit` is raised for 30% of the design project's contract value, with a stored reason and the design project as source document | `spec.md` §5.4, §6.9 · Q-207-6 |
| AC-415-E | The credit posts through `PostingSetBuilder` on the execution project's accounts only. It never touches the hold, the client advance or any other of the five ledgers, and never posts on the design project. Postings are append-only; a correction is a reversing posting | `spec.md` §5.1, §6.1, §6.4 · `CLAUDE.md` |
| AC-415-F | The person who signs (and so raises the credit) cannot approve it; approval is by a different user through the existing adjustment approval path | `CLAUDE.md` Permissions · Q-207-8 |
| AC-415-G | "Design quantities seed the execution BOQ": the design project has no BOQ, so the only quantity it holds is its area. The execution BOQ starts empty with its explicit empty state; the design area and a link to the design project are shown on the execution project | `spec.md` §4.5 ("never phantom pre-filled rows"), §5.3 · Q-207-5 |
| AC-415-H | Every state change writes an audit record. Role and assignment checked server-side on the conversion endpoint | `CLAUDE.md` · `spec.md` §9 |
| AC-415-I | Both projects show the link to each other. All copy i18n, RTL | D-207 §2 · `CLAUDE.md` |

## Running on assumptions — RulingNotes, not rulings

| Q | Question | Default built |
|---|---|---|
| Q-207-5 | What are "design quantities", given a design project has no BOQ (§5.3)? | Nothing seeds BOQ lines; the area is carried as a reference. If Karim means lines, they need a design-quantity list that does not exist |
| Q-207-6 | Is "the design total" the design contract value, or what the client has paid? | The design contract value (area x rate) |
| Q-207-7 | May one design project convert more than once? | No, one execution project per design project |
| Q-207-8 | Does the 30% credit post at signature by itself, or does it go through approval? | Raised at signature, posts on approval by a different user (maker-checker) |
| Q-207-9 | Which execution types may a design convert into? | Lump Sum or Cost Plus, the user's choice |

## Not in this story

`parent_child` (furnishing). Portal view of the link (`spec.md` §12).

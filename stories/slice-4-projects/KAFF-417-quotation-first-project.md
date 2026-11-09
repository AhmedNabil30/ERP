# KAFF-417 · A project can start as a price: terms move to signature, the unsigned BOQ prints as a quotation

<!-- kaff id=KAFF-417 slice=4 points=5 state=READY -->

**Slice:** 4 — Projects · **Points:** 5.
**Owner:** Backend (`SignProject`), then UI Designer (create screen, signature step, quotation print).
**Source:** client comment relayed by Nabil 2026-11-09, `decisions.md` D-207 §1. A client ruling; it
overrides `spec.md` §3 where the two differ (the estimate lives on the unsigned project, because the
Opportunity module is not built).
**Spec:** `spec.md` §4.4 (freeze rule, untouched), §5.1 (hold 20%, advance 25%), §5.3 (Design: no hold, no advance, no BOQ).

## What is already true (verified 2026-11-09 against the tree)

- `POST /api/projects` needs only name, client and contract type. `HoldPercentage` and `ClientAdvance`
  are nullable in `CreateProject/Request.cs`; defaults are `ContractDefaults.HoldRate` (20%) and
  `ContractDefaults.AdvanceRate` (25%), applied in `CreateProject/Handler.cs` only for `ContractType.LumpSum`
  through `Project.SetLumpSumTerms`.
- The create screen (`project-create-page.html`) already shows hold and advance only when `isLumpSum()`.
  **D-207 §1's statement that it shows them on every contract type, including Design, is wrong.**
- `POST /api/projects/{projectId}/sign` (`SignProject`) accepts only `SignedOn`. It is called from the BOQ page (`boq-page.ts`).
- No quotation or print view of a BOQ exists in `src/Web`.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-417-A | The create screen asks for name, client and contract type only (plus area for Design, which prices the contract). Hold and advance are not on it for any contract type | D-207 §1 |
| AC-417-B | `SignProject` accepts optional hold percentage and client advance. For Lump Sum, an omitted value keeps the project's current term (the 20% / 25% defaults if none was set); a supplied value is applied through `Project.SetLumpSumTerms` before signing, in the same transaction | D-207 §1 · `spec.md` §5.1 |
| AC-417-C | For Cost Plus and Design, `SignProject` refuses a request that carries hold or advance with a validation error, and signing without them works as today | `spec.md` §5.2, §5.3 |
| AC-417-D | The signature step on the BOQ page shows hold 20% and advance 25% (or the project's current terms) pre-filled and editable, for Lump Sum only. It never shows them for Design or Cost Plus | D-207 §1 · `spec.md` §5.3 |
| AC-417-E | An unsigned project's BOQ can be priced and opened as a printable quotation: client, project, date, lines with quantity, unit price and line total grouped by باب, and the total. Printing uses the browser's print with print CSS; no PDF library | D-207 §1 · Q-207-2 |
| AC-417-F | A signed BOQ is still frozen by copy. Nothing in this story writes a catalogue reference onto a BOQ line or edits a signed line; the existing freeze tests stay green | `spec.md` §4.4 · `CLAUDE.md` |
| AC-417-G | Changing terms at signature writes an audit record with before and after | `CLAUDE.md` Audit |
| AC-417-H | Role and assignment are checked on the changed endpoint, as today | `spec.md` §9 |
| AC-417-I | All new copy is i18n, Arabic, RTL correct at mobile width | `CLAUDE.md` |

## Running on assumptions — RulingNotes, not rulings

| Q | Question | Default built |
|---|---|---|
| Q-207-1 | What happens to an unsigned project whose client never signs? | It stays in `Setup`. No new state. **Archive is not built**: no project archive exists today |
| Q-207-2 | Does the quotation printout show line prices, or only the total? | Line prices |

## Not in this story

Project archive (Q-207-1), the Opportunity module, re-pricing alerts on open quotations (`spec.md` §4.4 second paragraph).

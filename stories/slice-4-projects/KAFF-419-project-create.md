# KAFF-419 · Project create: no preselected type, one name per client, add a client in the form

<!-- kaff id=KAFF-419 slice=4 points=5 state=BUILT -->

**Slice:** 4 — Projects · **Points:** 5.
**Owner:** Backend, then UI Designer (`build` from `.design/v2/team-a/project-create.html`, commit `726f68a`).
**Source:** Nabil's rulings D-217 items 34 and 36, D-218 items 8 and 9. Journey brief 4 in
`ux/journey-project-module-2026-11-09.md`.
**Spec:** `spec.md` §2, §5.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-1 | No contract type is preselected. A request with no type is refused. | D-217 item 34 |
| AC-2 | A project name already used for the same client is refused with "يوجد مشروع بهذا الاسم لهذا العميل". The same name for another client is accepted. | D-217 item 34 |
| AC-3 | Anyone who may create a project can add a client from inside the form with a name, a phone and a kind (فرد / شركة). The full client screen keeps `Permission.ClientManage`. | D-218 item 8 |
| AC-4 | A quick-add phone that already belongs to another client warns and allows. | D-218 item 9 |
| AC-5 | Finance is assigned to every new project at creation. Built as a `RulingNote`; Karim to confirm. | D-217 item 36 |
| AC-6 | The screen follows the approved mock, including the quick-add panel (a bottom sheet at 390 px). | D-218 |
| AC-7 | Creating a project and quick-adding a client each write an audit record. | `CLAUDE.md`, Audit |

## Not in this story

- Editing a client from the form.

# KAFF-420 · The project frame with real tabs, and the overview

<!-- kaff id=KAFF-420 slice=4 points=8 state=VERIFIED verdict=PASS at=91cdcb3 on=2026-11-17 -->

**Slice:** 4 — Projects · **Points:** 8.
**Owner:** Backend only if the API lacks a field, then UI Designer (`build` from `.design/v2/team-a/project.html` r3, commit `07878d8`, with the D-218 changes).
**Source:** Nabil's rulings D-217 items 37 to 39 and D-218 items 1 to 7. Journey brief 5 in
`ux/journey-project-module-2026-11-09.md`.
**Spec:** `spec.md` §5, §15.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-1 | One project frame (name, client, status, contract type, stage line, tabs) sits above the overview, the BOQ, the daily log, the crew screen and the extracts. It is one kit part, not copied per screen. | D-218 items 1, 2 |
| AC-2 | The stage line shows المقايسة, التنفيذ and التسليم only. A stage not reached shows as coming, with no sentence and no lock. | D-218 item 3, D-217 item 38 |
| AC-3 | Tabs by role: the owner and the supervising site engineer see every tab; the junior site engineer sees نظرة عامة, اليومية and الفريق, with no المقايسة and no المستخلصات. A tab a role may not open is absent. The server refuses the route as well. | D-218 items 5, 6; D-217 item 37 |
| AC-4 | The overview of the owner and of the supervising site engineer shows the percent of the contract done, the أبواب, the three money figures, and آخر يومية. | D-218 items 6, 7 |
| AC-5 | The overview of the junior site engineer shows the percent done, the أبواب and آخر يومية, and no money figure, no extract waiting line and no closing group. The API does not send him the money figures. | D-217 item 37 |
| AC-6 | An unsigned project shows نظرة عامة, المقايسة and الفريق, and its main action opens the BOQ. | mock r3 |
| AC-7 | No code or id on the screen. | Nabil, 2026-11-10 |

## Not in this story

- The stages after handover (Q-218-3).
- Who may reopen an archived project, and where stop and restart live: not ruled; the build keeps what the app does today.
- The Cost Plus and Design overviews beyond what the mock draws.

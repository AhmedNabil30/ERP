# KAFF-613 · Check-in and check-out are their own buttons and record location and time

<!-- kaff id=KAFF-613 slice=6 points=5 state=VERIFIED verdict=PASS at=91cdcb3 on=2026-11-17 -->

**Slice:** 6 — Execution · **Points:** 5.
**Owner:** Backend (endpoint and storage), then UI Designer (the buttons, mobile-first).
**Source:** client comment relayed by Nabil 2026-11-09, `decisions.md` D-207 §3.
**Spec:** `spec.md` §8 (daily log captures check-in/check-out; period deltas; offline-first, money never moves offline).

## What is already true (verified 2026-11-09 against the tree)

- `DailyLogField.CheckIn` and `DailyLogField.CheckOut` live in `src/Domain/DailyLogs/LogEntry.cs` (the brief said `DailyLog.cs`).
- Check-in is chosen from `FIELD_OPTIONS` in `daily-log-page.ts`, shown in the field kind select. `DailyLog.RecordField` stores only the time; endpoint `src/Api/Features/DailyLogs/RecordField/`.
- There is no offline queue in `src/Web`.

## Acceptance criteria

| ID | Criterion | Source |
|---|---|---|
| AC-613-A | The daily log screen has a check-in button and a check-out button, outside the field select. Pressing one takes `navigator.geolocation` and the device time with nothing to choose | D-207 §3 |
| AC-613-B | Check-in and check-out are removed from the field select | D-207 §3 |
| AC-613-C | The server stores latitude, longitude, accuracy in metres, device time and server time on the entry. Location is required (AC-613-D); accuracy may be null | D-207 §3 |
| AC-613-D | Missing location (denied, unavailable, timed out, or coordinates incomplete or out of range) refuses the check-in or check-out with a stored reason. The screen says why and how to turn location on. Nothing is recorded | D-208 Q-207-4 |
| AC-613-E | The first located check-in on a project sets the project's reference point. A later check-in farther than the threshold from it is recorded and flagged, never refused | Q-207-3 · Q-207-3a |
| AC-613-F | The flag and its reason (far from site) are visible on the entry in the log | D-207 §3 |
| AC-613-G | Device time is the time the button was pressed; server time is the time the server received it. Both are kept; neither overwrites the other. The daily log stays period deltas | D-207 §3 · `spec.md` §8 |
| AC-613-H | Only an engineer assigned to the project can check in; role and assignment server-side, as for every daily-log entry | `spec.md` §8, §9 |
| AC-613-I | Mobile-first, RTL, buttons reachable by thumb at 360 px width, all copy i18n | `CLAUDE.md` |
| AC-613-J | Recording writes an audit record | `CLAUDE.md` |

## Ruled by the client (D-208)

- Q-207-3: a check-in far from the site is accepted and flagged.
- Q-207-4: with location off, check-in is refused until location access is on. This reverses D-207's default.

## Running on assumptions — RulingNotes, not rulings

| Q | Question | Default built |
|---|---|---|
| Q-207-3a | How far is "far", and what is the reference point? | 500 m from the first located check-in on the project, held in the RulingSet so a ruling changes one value |

## Not in this story

An offline queue (none exists; device time is captured so one can be added). Site coordinates entered by hand. Attendance reports.

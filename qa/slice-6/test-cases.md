# Slice 6 — test cases

**Range allocated: `TC-6-001` … `TC-6-004`.**

Checked before writing: `qa/slice-6/` did not exist, and `grep -rIn "TC-6-" qa stories tests decisions.md process agents.md spec.md STATUS.md src/Web/src` returned nothing. `qa/README.md`'s scheme is `TC-<slice>-<nnn>`, so `TC-6-nnn` cannot collide with `TC-1-nnn`, `TC-2-nnn` or `TC-3-nnn`. `TC-6-001`…`004` are taken by this file; `TC-6-005` onward is free.

Covers only the phone-width primary-action reachability finding of commit `1a0321c` (`KAFF-600`, `AC-600-K1`). It is not a full slice-6 case file. The rest of `KAFF-600` has no QA cases yet; that is a gap, not a design choice.

## How to read a case

Same layout as `qa/slice-3/test-cases.md`:

```
**TC-6-000 · what it checks**
`AC-xxx-X` · Priority · Layer · source
Given … When … Then …
*Fails if:* the one defect this case catches.
```

## Runnable today? No. What is missing.

`tests/E2E.Tests/` exists and is a real Playwright project. `Kaff.E2E.Tests.csproj` references `Microsoft.Playwright`; `PlaywrightFixture.NewMobilePageAsync()` already opens a 390x844 Arabic page; `E2ESession.SignInAsync` exists. [Verified 2026-11-01 by reading the directory.] So the harness is not the gap. What is missing, found by reading:

1. **No test class for these two screens.** The existing classes cover the smoke test, users, clients and audit. Nothing mentions the daily log or the worker register.
2. **No seeded user who can reach either screen.** `scripts/seed-demo.ps1` seeds an owner, a finance user and a portal client user. The daily log route is guarded by `dailyLogWriteGuard`, which needs the signed-in user to hold `DailyLogWrite` on the project in the URL. The worker register route sits behind `dayLabourSiteManageGuard`. No seeded account or assignment provides either, and `E2ESession` has no constant for such a user.
3. **No seeded project to put in the URL.** Both routes are `projects/:projectId/...`.
4. **No measurement helper.** The cases need a small in-page evaluation, the same shape as `MeasureEveryBdi` in `BidiGeometryTests.cs`.

Until 2 and 3 exist the cases are written and **not runnable**. They must not be reported as passed, failed or skipped-green. `[E2EFact]` self-skips when `KAFF_E2E_BASE_URL` is unset, so a Verifier records the observed test count (`RSK-14`), not only "no failures".

## TC-6-001 · the daily log's Save is reachable at 390px without scrolling

`AC-600-K1` · P2 · E2E (Playwright, 390px, Arabic) · `CLAUDE.md` "Test at mobile width"; `KAFF-600` `AC-600-K1`; instance `1a0321c`

**Given** an engineer holding `DailyLogWrite` on a project, signed in, a 390x844 viewport, Arabic locale, and the daily log at `/projects/{id}/daily-log` loaded with its form showing (not a loading state).
**When** the page has settled and **nothing has been scrolled**. `window.scrollY` and the `scrollTop` of every ancestor of the button are 0 at measurement time; the case asserts this precondition and does not assume it.
**Then** the element with `data-testid="daily-log-save"` satisfies all of:

1. **Inside the viewport.** Its bounding box lies fully inside `0..innerWidth` and `0..innerHeight`.
2. **Not covered.** `document.elementFromPoint` at the centre of its box returns the button or a descendant of it. This is the check that sees the shell's fixed bottom bar sitting over the button, which a rectangle comparison against the viewport alone cannot.
3. **No horizontal overflow.** `document.documentElement.scrollWidth <= innerWidth`.

The case asserts positions and hit-testing only. It reads no computed `overflow`, `position` or `inset` value, names no CSS class, and does not look at where the button's parent is. See "Why it asserts behaviour, not CSS".

*Fails if:* the Save button's box extends below the viewport or under the shell bar, or the point at its centre belongs to another element. This is the state `1a0321c` fixed. With `.daily-log { overflow-x: hidden }`, measured at 390px, Save sat at y=888 to 936 while the visible main region ended at y=819, so the button was partly outside the 844px viewport and under the bar. Putting that one declaration back to `hidden` must turn this case red. **That revert is the mutation check (`qa/strategy.md` section 5); a Verifier runs it once before trusting a green.** A green never seen red is not evidence.

## TC-6-002 · the worker register's primary action is reachable at 390px without scrolling

`AC-600-K1` by pattern · P2 · E2E · `KAFF-600` `AC-600-K1` and its "Dependency" paragraph; `KAFF-936`

`AC-600-K1` is the daily log's criterion. `worker-register` has no AC of its own in this file's scope, so this case is evidence about the shared pattern and does not discharge a `worker-register` criterion.

**Given** a user able to reach `/projects/{id}/day-labour/new` (`dayLabourSiteManageGuard`), 390x844, Arabic, form loaded, nothing scrolled.
**When** the page has settled.
**Then** the element with `data-testid="worker-submit"` meets checks 1 to 3 of `TC-6-001`. One difference: the button is disabled until the form is valid (`[disabled]="!canSubmit()"`). A disabled button still has a box and still hit-tests, so the case measures it **disabled, with the form untouched**. It must not fill the form first, because filling it could move the layout and the case would no longer measure the state a user sees first.

*Fails if:* same as `TC-6-001`. `worker-register-page` carries the same private sticky `.footer` pattern as the daily log (the `KAFF-600` text says the daily log's footer was copied from it), so it has the same exposure, and no commit has measured it yet. If this case passes today it records that `worker-register` is not currently broken; it does not say the pattern is safe.

## TC-6-003 · the cases outlive `KAFF-936`

`AC-600-K1` now; `AC-600-K2` and `AC-936-J` when `KAFF-936` lands · P2 · E2E

`TC-6-001` and `TC-6-002` are written against `data-testid` values (`daily-log-save`, `worker-submit`) and against geometry, not against the markup that carries the buttons. When `KAFF-936` moves each action into the shell's bottom bar, the same two cases run unchanged and must still pass: the button is then in the bar, inside the viewport, and is its own hit-test target. A legitimate move to the shell bar does not turn them red.

If the `data-testid` values change in that story, the cases change with them. `AC-600-K2` already requires `daily-log-save` to survive.

*Fails if:* the cases are edited to name `.footer`, `position: sticky`, `overflow-x` or `inset-block-end`. Such an edit makes them fail after `KAFF-936` for a reason that is not a defect, and makes them pass on a screen whose button is off-screen for a different reason. A reviewer rejects that edit.

`AC-600-K2` stays **HELD** on `KAFF-936`. This case does not discharge it and must not be reported as doing so.

## TC-6-004 · the measurement can fail (positive control)

`AC-600-K1` · P2 · E2E · `qa/strategy.md` section 5; `D-046`

**Given** the measurement helper used by `TC-6-001` and `TC-6-002`.
**When** it is run, in the same test run, against a control element known to be unreachable, for example an element placed below `innerHeight` on a page the test builds, or a button at the end of normal flow on a page forced taller than the viewport and left unscrolled.
**Then** the helper reports it as not reachable.

*Fails if:* the helper reports "reachable" for the control. Without this case `TC-6-001` could be a measurement that never fires, for example `elementFromPoint` returning `null` for an off-screen point while the assertion is written as "the result is not another element". The check must assert the result **is** the button, so `null` fails it.

## Why it asserts behaviour, not CSS

A case asserting `overflow-x: clip` on `.daily-log` would pass on a screen whose Save is still off-screen for any other reason: a different ancestor with `overflow: hidden`, a `contain`, a changed shell height. It would also fail after `KAFF-936` removes `.footer`, though nothing is wrong. Where the button is, and what receives a click there, is the property the user cares about and the only one that survives the migration.

## Why `ng test` cannot do this

The Angular unit suite runs in jsdom/vitest. jsdom does no layout: `getBoundingClientRect` returns zeros, and sticky and scroll-container behaviour do not exist. `ng test` was 255/255 with the button below the fold and is 255/255 with it fixed. That is `RSK-21`.

## Open items reported, not decided here

- Which seeded user and project the E2E run uses is a harness decision for the builder lane. The stories do not say which role is the daily-log engineer in `seed-demo.ps1`, so this file names none.
- The request called this the "fifth standing E2E gap". Only `V-38-L` (`qa/slice-2/verification-2026-09-12-batch.md`) was found by reading, so this file states no count.

## Story coverage index

| Story | Criterion | Cases | Note |
|---|---|---|---|
| `KAFF-600` | `AC-600-K1` | `TC-6-001`, `TC-6-002`, `TC-6-004` | not runnable: no seeded user, project or test class |
| `KAFF-600` | `AC-600-K2` | `TC-6-003` (forward-compatibility only) | **HELD** on `KAFF-936`; not executable, not failed, not passed |
| `KAFF-600` | `AC-600-K` | none | retired in `d29ea4e`; superseded by K1 and K2 |

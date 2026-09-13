# KAFF-915 · Restyle: worker mobile registration

<!-- kaff id=KAFF-915 slice=design-system points=2 state=BUILT -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 2 — a conversion, not a
feature; no money, no permission change, no new behaviour.
**Depends on:** `KAFF-900` (BUILT `76bcf00`), `KAFF-901` (BUILT `d821b37`).
**File:** `src/Web/src/app/features/day-labour/worker-register/worker-register-page.ts` + `.html` +
`.css`. Route: `projects/:projectId/day-labour/new`.

## Story
As a Site Engineer registering a worker on a phone, I want this screen restyled to KAFF-900's tokens
and KAFF-901's `kaff-field`/`kaff-button`, matching `WorkerMobile.dc.html` at 390px — this is the
screen that mockup was designed for.

**Conversion, not a feature.** `worker-register-page.spec.ts` already covers behaviour; do not
re-derive it. The duplicate-phone warning banner here (warn-and-acknowledge, D-141/D-146/D-152/D-153)
keeps its exact wording and both actions ("افتح المسجل" / "متابعة الحفظ") — this story restyles it,
it does not rewrite its copy or its warn-vs-block behaviour.

## What to do
1. Replace field wrappers with `kaff-field` — name, phone, باب, trade (chips), day rate.
2. Replace the trade selector's chip row and the primary "حفظ العامل" / "إلغاء" actions with
   `kaff-button`/whatever chip primitive already exists — if trade selection needs its own control
   beyond `kaff-badge`/`kaff-segmented-filter`, keep the existing chip markup and only re-token it;
   do not build a new shared component in this story.
3. Grep and report colour-literal count before/after.
4. `var(--color-accent)` → `var(--color-interactive)` for interactive uses (buttons, active trade
   chip); the phone-warning banner's warning colour stays `--color-warning`, not touched by this
   rule. List any ambiguous case for Nabil.
5. **44px tap targets are load-bearing here** — this is the mobile field screen. Verify, do not
   assume, that every chip, input and button clears `--tap-target` after the conversion.

## Acceptance criteria — `apple-erp-design` §8, applied to this screen

**AC-915-A** Zero colour literals in `worker-register-page.css`. Report before/after counts.
**AC-915-B** Correct in light and dark, screenshotted at 390px (this screen's real width).
**AC-915-C** 0px horizontal overflow at 390px.
**AC-915-D** Every `<bdi>` (the phone number) still isolates.
**AC-915-E** Every `data-testid` that existed before still exists after — grep before/after, report
both counts, confirm identical set.
**AC-915-F** No new string outside i18n; no key added or removed. The warning banner's exact wording
is unchanged.
**AC-915-G** `ng build` clean, `npm test`/`vitest` green, via `/run-kaff-erp`.
**AC-915-H** No new token needed beyond KAFF-900's.
**AC-915-I** Every tap target — chip, button, field — is at least 44px, measured.
**AC-915-J** No flex/grid child truncates without `min-inline-size: 0`.

## Not in this story
The warn-and-acknowledge mechanism itself, the phone-normalisation logic, the day-rate visibility
rule (owner/finance/responsible engineer only — D-153).

## Questions for Karim
None.

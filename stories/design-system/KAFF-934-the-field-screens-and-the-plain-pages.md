# KAFF-934 · The field screens, and the plain pages nobody restyled

<!-- kaff id=KAFF-934 slice=design-system points=8 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 8.
**Depends on:** `KAFF-901`, `KAFF-928`, `KAFF-930`, all on `main`. This story consumes the kit and
changes none of it. If a component needs a change, stop and report it.
**Design:** the canvas artboards in `.design/` — `WorkerMobile.dc.html` for the field screens and
`Main.dc.html` for the list direction. `WorkerMobile.dc.html` is the approved mobile direction at
390px: a sticky header carrying a back arrow, the screen title and the project name beneath it; 48px
controls and 16px type because it is used outdoors on a phone; the duplicate-phone warning as an
amber-bordered block holding both of its actions; the specialisation as a wrapped row of 44px pills,
not a dropdown; and a footer that stays at the bottom of the viewport with a 52px primary action and
cancel as plain text below it.
**Reference implementations already on `main`:** `features/employees/employee-list/*` for a converted
list, `features/catalogue/catalogue-form/*` for a converted form.
**Files:** `features/day-labour/worker-pool/*`, `features/day-labour/worker-history/*`,
`features/day-labour/worker-register/*`, `features/landing/*`, `features/forbidden/*`,
`features/not-found/*`.

## Story

Two groups of screens were never in a restyle story.

**The field screens.** `worker-register` is on `kaff-field` and `kaff-button` but not on the mobile
direction the artboard sets; `worker-pool` carries only `kaff-state` and lays out its own rows;
`worker-history` carries nothing from the kit at all. These are the screens a site engineer uses on a
phone, outdoors, in Arabic, and they are the ones Nabil reached last. They are the point of the mobile
direction, so they get it properly.

**The plain pages.** `landing`, `forbidden` and `not-found` use none of the kit. They are short, they
are seen constantly, and `forbidden` in particular is what a correctly refused user sees — it must
read as part of the same application, not as a stack of unstyled paragraphs.

**Order matters. Do `worker-register` first**, because it is the artboard's own subject and it sets the
mobile pattern the other two field screens then follow. Gate it, commit it, then move on. If the budget
runs out, the artboard's own screen is done and the remainder is named.

## Per screen

**`worker-register`** — match `WorkerMobile.dc.html`: sticky header with back arrow, title and project
name; 48px controls at 16px type; the existing duplicate-phone warning (`shared/duplicate-phone-warning/*`)
restyled to the artboard's amber block, keeping both of its actions and every one of its test ids; the
specialisation as a wrapped pill row with 44px hit targets; the day rate keeping its
"owner, finance and the responsible site engineer only" hint; a bottom-anchored footer with the 52px
primary action and cancel as text.

**`worker-pool`** — onto `kaff-table` for its rows. Its single primary action per row stays a button
rather than becoming a row menu — `KAFF-931` already ruled on that and the ruling holds. At 390px the
rows read as stacked records, not as a table scrolled sideways.

**`worker-history`** — the same treatment. It is a read-only record list: `kaff-table` plus
`kaff-state`, no row actions.

**`landing`, `forbidden`, `not-found`** — the page frame, the type scale and `kaff-button` for whatever
action each one offers. `landing`'s role dispatch behaviour must not change; `landing.spec.ts` and
`landing-page.spec.ts` stay green without being weakened.

## Acceptance criteria

**AC-934-A** `worker-register` at 390px matches `WorkerMobile.dc.html` in structure: sticky header with
back arrow, title and project name; a bottom-anchored footer; the specialisation as a pill row. Proven
by a screenshot at 390px.
**AC-934-B** Every interactive target on the three field screens is at least 44px on its block axis at
390px, measured.
**AC-934-C** `worker-pool` and `worker-history` render their rows through `kaff-table`. No row geometry
remains in either stylesheet.
**AC-934-D** The duplicate-phone warning keeps both actions and every test id it has today. Grep
before and after and report both counts.
**AC-934-E** Every `data-testid` on every screen touched still exists. Grep before and after, report
both counts per screen, and name any id that had to move because a component owns its spelling, with
the reason.
**AC-934-F** `landing`'s role dispatch is unchanged in behaviour. Name the spec files that prove it and
report their result.
**AC-934-G** Zero colour literals in any stylesheet touched. No user-facing string outside i18n; any
new key added to both catalogues. Kaff's status vocabulary appears verbatim where a status is shown:
لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها.
**AC-934-H** Zero horizontal overflow at 390px on every screen touched, measured.
**AC-934-I** `ng build --configuration production` exit 0 with no warning line, and `npm test` green.
Report the counts as measured.
**AC-934-J** Screenshots light and dark, desktop and 390px, for every screen touched, through
`/run-kaff-erp`. A visible criterion passes with the image or it does not pass. `worker-pool` and
`worker-history` need a real project in the URL — create one through `POST /api/projects` as
`owner_demo`, the way `KAFF-931`'s close did, never with a raw SQL insert.

## Not in this story

Any change to what these screens fetch, submit or validate. Any new field. Offline behaviour. The
engagement flow itself. Any change to a kit component. Any edit to `src/Api`, `src/Domain` or
`src/Infrastructure`.

## Questions for Karim

None — presentation only. The day-rate visibility hint restates a rule `spec.md` already carries; if
the wording on screen and the rule in `spec.md` disagree, stop and report rather than choosing.

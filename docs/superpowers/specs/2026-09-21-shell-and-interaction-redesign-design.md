# Kaff ERP — shell and interaction redesign

**Date:** 2026-09-21 · **Status:** design, approved in outline by Nabil, awaiting spec review
**Canvas:** https://claude.ai/artifact/KmuT1UYyXMiSqocN2MCkgY — eight artboards, Arabic RTL, at 1440×980 and 390×844
**Supersedes nothing.** `KAFF-928` and `KAFF-929` remain correct as far as they go; this design is what
they were not: a change to how the application is organised, not to how its tables look.

---

## 1 · Why

Nabil, 2026-09-20 and 2026-09-21, looking at the running app: *"this is not a UI for an ERP system,
using the same traditional table and lists with the actions at the far left… the lists follow the
traditional ERP lists you can find everywhere, like the ABP default."* And: *"the demo is blocked on
the UI/UX changes."*

He is right, and three consecutive stories of tightening the same table did not touch it. The problem
is not typography or column alignment. It is that the application is a sidebar of eleven master-data
lists, each rendering a grid with per-row buttons and a form page per record. That is the
AspNetZero/ABP default shape. An operator does not open an ERP to browse أبواب; they open it to find
out what is waiting on them and to act on a project.

Evidence used, all screenshots taken against the running stack on 2026-09-20/21:

- `.design/shots/sm-babs-desktop-light.png` — every row spawns a second band carrying three buttons,
  the largest and reddest being `أرشفة الباب`, repeated eight times. The screen's dominant visual is
  *destroy*. It is called a tree and has no hierarchy, no indentation, no expand; `نقل إلى أب آخر` is
  a button standing in for a drag the user should be able to see.
- `.design/shots/sm-catalogue-desktop-light.png` — the action column sits at the far physical left,
  which in RTL is the farthest point from where the eye starts.
- `.design/shots/target-worker-mobile.png` — the existing mobile mockup is good. The design language
  was never the problem; the app was not built to it, and `.design/canvas.json` shows why the gap
  survived: the mockups only ever covered four master-data screens. No artboard existed for a home, a
  project, or anything with money in it.

## 2 · Constraints, from Nabil

1. The demo is **a full walkthrough of the product** — it must feel like one designed system,
   including screens with no code behind them.
2. The story it tells is **both** a project's life (client → BOQ → work → extract → approval →
   payment) **and** a day in the life of each role.
3. **The phone is in the demo.** The site engineer's screens get shown on a device, so mobile is a
   first-class design surface with its own flows, not a narrowed desktop layout.

## 3 · What the research changed

Sources are cited because two of these reversed a choice already drawn.

1. **Western digits 0–9 are the norm in Arabic digital and business contexts.** The first canvas used
   Arabic-Indic ٠١٢٣; it was redrawn. The app is already pinned to `ar-EG-u-nu-latn` and was right.
   Whether *stored* phone numbers should be normalised remains an open question for Karim (§9).
2. **Bold is heavy in connected Arabic script** — reserve it for short headings and critical labels.
   Hierarchy carries on size and colour before weight. Body line-height 1.7+, letter-spacing exactly
   0 (adding it breaks the letter joins and is the commonest Arabic typography mistake).
3. **Full page beats side sheet** when columns get compared, when the thing needs a shareable URL, and
   for multi-step flows that demand full attention. This is what makes extract approval a page.
4. **An approval needs its chain visible** — who reviewed, who is next, who may override — with
   comment and return-with-reason inline.
5. **Ledger figures drill down** to the transactions behind them, with a running balance. This turns
   "balances are derived, never stored" from a rule in a document into something demonstrable.
6. **A "universal inbox" that sends you elsewhere to act just creates another place to check.** The
   home links straight into the act.
7. **Colour is never the only signal** — every status chip carries an icon and text.

## 4 · Section 1 — Shell and navigation

Three shells, exactly as `ux/navigation.md` already specifies. **This design is largely catching up to
that file**, which the built app diverged from: it defines per-role navigation built from the
permission set, and eventual landings (`S-037` Owner dashboard, `S-073` today's daily log in a site
shell with bottom navigation, `S-043` pipeline board). What shipped instead is one staff shell with
eleven master-data links in it, for everybody.

1. **Home replaces the module list as the landing.** Queues, not tiles. Each row is work with a name
   on it, the oldest item named, the amount where there is one, and a direct action. A queue with no
   data behind it in the current slice **does not render at all** — `ux/navigation.md`: *"Do not build
   a placeholder dashboard with invented tiles."* That rule is honoured here and is why §6 matters.
2. **Project becomes a container**, not a row in a list: one workspace per project, tabs for نظرة عامة
   · المقايسة · المستخلصات · الحركة المالية · يوميات الموقع · الفريق.
3. **Master data moves under `الإعدادات`, role-sensitively.** For Owner and Finance it is setup, done
   once. For TechnicalOffice the catalogue *is* the daily job and its landing is `S-017`, so it stays
   top-level for that role. The nav is already built from the permission set; this changes grouping,
   not mechanism.
4. **SiteEngineer on a phone gets the site shell** — bottom navigation, at most five destinations,
   thumb-reachable. `ux/navigation.md` schedules it for slice 6; the demo moves it up.

## 5 · Section 2 — The record interaction model

**One decision rule for where a record opens**, so the next session does not re-litigate it per screen:

| Surface | When | Screens |
|---|---|---|
| Side panel | One record, few fields, no comparison, quick edit | catalogue item, client, employee, supplier, باب |
| Full page | Columns get compared, a shareable link is needed, multi-step, or the decision has consequences | extract approval, BOQ builder, ledger, Excel import, project workspace |
| Modal | Only a destructive act that genuinely cannot be undone, and the permission refusal | almost nothing |

- **Everything has a URL.** A panel is `?item=CON-1014` on the list route. Filters, search text, open
  tab and selection all live in query params. Today they live in signals, so a filter cannot be
  linked, bookmarked or restored by a refresh. Rows are real `<a routerLink>`; Ctrl-click and
  middle-click work.
- **Zero visible buttons per row.** The row is the primary action. Everything else is one `⋯` at the
  row end, revealed on hover **and on keyboard focus** — hover-only is a trap for anyone tabbing.
  Multi-select raises a floating action bar.
- **Destruction differs by kind.** Archive executes immediately with an 8-second undo and no confirm.
  **Money is the opposite**: creating a posting gets a confirm, because it is permanent in the ledger
  by design — the correction is a reversing entry both parties see forever. Undo is a presentation
  affordance and never a delete path; `CLAUDE.md`'s append-only rule is untouched.
- **Inline edit** is for a single scalar with an obvious type — price, margin %, unit, name —
  optimistic, rolled back on a server refusal. **Never inline:** anything that changes what money is
  legal, meaning posting amounts, extract quantities, hold percentages.
- **Keyboard, modest:** Tab order, Enter opens, Escape closes the panel, `Ctrl-K` search, `Alt+arrow`
  moves a باب. No vim bindings — the audience is on a building site in Arabic with a phone.

**Six shared components**, after which every list screen loses its own copy of all six:
`kaff-detail-panel`, `kaff-row-menu`, `kaff-bulk-bar`, `kaff-undo-toast`, `kaff-inline-edit`, and the
state set (`loading` / `empty` / `refusal` / `offline`).

## 6 · Section 3 — Data, and the gap this design found

Every slice in `src/Api/Features/` was listed. **Three of the eight artboards have no API behind them.**

| Screen | API today |
|---|---|
| Home, catalogue, أبواب, states | Auth, Catalogue, Babs, Clients, Employees, Suppliers, Subcontractors, Users, Departments, DayLabour, Audit, Assignments all exist |
| **مساحة المشروع** | **No `Features/Projects/` exists.** `Domain/Project.cs`, `ProjectAssignment` and `ProjectAccountSetFactory` exist; nothing creates, lists or reads a project. `AssignUserToProject` assigns users to projects that no endpoint can fetch. |
| **الخزينة** | `Features/Treasury/` holds exactly `PostMovement` and `ReversePosting`. **Nothing reads.** No posting list and no derived-balance query, although `Domain/BalanceQueries.cs` exists. |
| **اعتماد مستخلص** | No Extracts slice anywhere. |

**Therefore the UI is not what blocks the walkthrough on the money spine — the API is.** The role home
can only carry queues that have data behind them, which today means day-labour and master data.

Also confirmed: no list endpoint returns a total, which is why `KAFF-929`'s record counts are computed
client-side from the fetched page.

Permissions are unchanged by this design. `PermissionCatalogue`, `PermissionEvaluator` and
`IProjectAccessPolicy` stay the authority; every endpoint keeps checking role **and** assignment.
Hiding a `⋯` entry is presentation and never a gate.

## 7 · Section 4 — Order

| Phase | Work | Blocked by |
|---|---|---|
| **A** | Shared interaction kit; URL state for filters, search, tab and selection | nothing — lands on the seven existing screens |
| **B** | Shell: role home, settings grouping, per-role nav | nothing; one optional `GET /api/home` aggregate |
| **C** | `Features/Projects/` backend slice, then the project workspace | nothing; blocks D and E |
| **D** | Treasury read API (posting list with running balance, derived ledger balances), then الخزينة | C |
| **E** | Extracts: the approval chain backend, then the full-page approval screen | C, D |
| **F** | Site shell: bottom navigation, today's log, offline drafts | C |

A and B are days of frontend work. C, D and E are the product, and they are backend-first.

## 8 · Section 5 — Verification

- Every screen: screenshot at **1440 and 390, light and dark**, actually rendered through
  `/run-kaff-erp`, never hand-rolled.
- **No acceptance criterion passes on a claim.** `KAFF-928` reported `AC-928-F` green when the
  screenshot showed it red; it was caught only because the Scrum Master took the picture. An AC about
  something visible is proven with the image or it is not proven.
- Permission tests hit endpoints directly, one per role, asserting what each role cannot reach.
- The الحالات artboard is the state checklist every screen is measured against: loading, first-run
  empty, filtered-empty, server refusal, no-permission, offline, return-with-reason, undo.
- The demo script runs end to end as the final gate.

## 9 · Open questions for Nabil and Karim

1. **Phone digits.** Stored data mixes `01001234567` and `٠١٠٠١٢٣٤٥٦٧`. Rendering is already pinned
   to Latin; whether a phone is *normalised on entry* is a business rule, not a UI choice. Open since
   2026-09-20.
2. **`GET /api/home`, or client fan-out?** An aggregate is one round trip and one place to enforce
   role filtering; fan-out adds no backend. Recommend the aggregate once phase C exists, and fan-out
   before that.
3. **Does the walkthrough need a project the demo can create live**, or is seeded data enough? This
   decides whether phase C must include create-project UI or only read.

## 10 · Deliberately not in this design

- The palette. `--color-interactive: #b4551f`, the ink sidebar and the warm surface are the agreed
  design and do not change. This is a change of hierarchy and structure, not hue.
- Any change to what a list fetches, filters or sorts, beyond moving that state into the URL.
- A general ledger with free-form journal entries, multi-company, multi-currency, tax modules, the
  consultant role — all out of scope per `CLAUDE.md` and unaffected here.
- Replacing `kaff-table`/`kaff-table-row`/`kaff-table-header`. `KAFF-928` built them correctly; the
  interaction kit sits on top of them.

# The project journey, end to end — 2026-11-09

Written by the journey-analyst for Nabil, to take to Karim. It covers one project from the day it is
created to the day it closes. Every step carries one source tag: `SPEC §n`, `RULED D-nnn`, `ASSUMED`
(a RulingNote default nobody has confirmed), `PRACTICE [url]`, or `PROPOSAL`. Where this journey
differs from `spec.md`, the spec stands and the difference is a question in section 4.

Read for this: `spec.md` §4, §5.1, §7, §8, §10, §11, §13; `stories/slice-4-projects`, `slice-6-execution`;
`src/Domain/Rulings/Closure*`; `qa/consultant-review.md`; STATUS.md top section; `app.routes.ts`; the
BOQ, daily-log, worker-pool, project-create and project-workspace templates.

## 0. What I checked before treating anything as new

- **Templates or copying a BOQ.** I searched `spec.md`, every story, `decisions.md` and `ux/` for template,
  copy, clone, duplicate and base project. Nothing exists. The only "template" hits are the Excel
  catalogue import template (D-129, Q60) and a duplicate-phone warning. Starting a project from another
  project's BOQ is therefore a PROPOSAL and a question for Karim (Q-A below).
- **Is the BOQ "a form"?** Not any more. `/projects/:id/boq` lets the Technical Office search the
  catalogue, pick an item, type surveyed and estimated quantity, and save. One item per round trip. It
  is a search-and-pick panel, not drag and drop. Spec §4.5 already requires exactly this ("Add item is
  always visible and searches the catalogue"), so what Nabil wants is faster, not new in kind.
- **The consultant review is partly stale.** It was written 2026-11-06, before the primary action slot
  and the workspace changes. Today a project can be created (`/projects/new`), a BOQ can be built and
  signed, and the workspace names the extract step and who holds it. Still true from that review: no
  screen to create an extract, record a collection, raise a change order or dispute, or move a cheque.
  The daily log still shows no date (checked in the template).

## 1. The journey

Roles: Owner, Technical Office (TO), Site Engineer (SE), Supervisor, Accounts/Finance.

### Phase A — Setup

1. Owner or TO opens المشاريع and presses مشروع جديد; types a name, picks client and contract type.
   Screen: `/projects/new`. Exists. `RULED D-207 §1`: hold and advance moved to signature, where the
   defaults of 20% and 25% show and can change (`SPEC §5.1`, §16 assumption 1). Checked 2026-11-10: the
   create template has no hold or advance field. A Lump Sum project then opens the BOQ page
   (`project-create-page.ts`, `destination`); the other types open the workspace.
2. The project lands in Setup. The workspace should show what is missing: BOQ empty, nobody assigned.
   `PROPOSAL` (a setup checklist). The primary action slot exists (`d702110`).
3. Finance is assigned so it can set the withholding category. `SPEC §9` amendment. Whether opening a
   project implies assigning Finance is open (Q-N10-2b), so this step is `ASSUMED`: a human assigns.
4. Start the BOQ: blank, or copy an earlier project's BOQ and edit it. Blank exists. Copy is `PROPOSAL`.
   See Q-A.
5. TO builds the BOQ. Today: search, pick, quantities, save, repeat. Lines sit under their باب, markup is
   inherited from the باب. `SPEC §4.5`. Conditions and per-line markup: `SPEC §4.2`. Checked 2026-11-10 in
   `boq-page.html`: the add and edit forms hold only surveyed and estimated quantity. There is no control
   for conditions, per-line markup or a custom item, though `AddBoqLine/Request.cs` accepts all three.
   The quotation is the BOQ and prints with line prices (`RULED D-208`, D-210 §4).
6. TO signs the BOQ. Catalogue values are copied, the contract value becomes the derived total, and the
   BOQ freezes. Exists. `SPEC §4.4`. Hold, advance and تشوينات then derive from that value. `SPEC §5.1, §15`.
7. Project moves Setup to Active. `SPEC §13`.

### Phase B — Running the site

8. SE opens يومية اليوم on his phone. One log per engineer per project, assigned projects only. `SPEC §8`.
   Today there is no date on screen (consultant B4). `PROPOSAL`: show the day and allow yesterday.
9. SE records field notes, executed quantities against BOQ lines (period delta, never cumulative),
   materials and تشوينات, photos, check-in and out. `SPEC §8`. Exists, with two changes since the first
   draft. Check-in and check-out are now their own buttons that capture GPS and time, and are refused
   when location is off (`RULED D-207 §3, D-208`, route `daily-logs/check-ins`). Photos have an API
   (`DailyLogs/RecordPhoto`) but the page offers four entry kinds only (field, labour, material,
   quantity), so an engineer cannot add one.
10. SE records day labour. `SPEC §8, §10`. Today: pick one worker from a dropdown, type hours, save, and
    repeat for every worker. Workers come from the project's pool; engaging, rating and closing a
    worker's engagement are on a different screen (the worker pool). The rate is set by someone else
    (`SetEngagementDayRate`) and the engineer's request has no rate member. `RULED D-139, Q71`.
11. A new man turns up: SE registers him from site and engages him. `RULED D-139 §2, Q71`. Exists.
12. Payroll, weekly, owner-approved. `SPEC §10`, assumption 10 (Karim). No screen. Outside this module,
    but the labour screen is where its data is born.
13. Money spent on site is a draft the engineer raises and Accounts confirms. `SPEC §8`. API only.

### Phase C — Billing

14. At period end the preparing role creates an extract, quantities pulled from the daily logs.
    `SPEC §7`. No screen. Who prepares and how far back is `ASSUMED` (ExtractPreparationScope).
15. Chain: Supervisor QC report, TO quantity gate (may split into PartiallyApproved while an NCR is
    open), Accounts (deductions, hold, advance recovery), Owner approval on every extract, Issued.
    `SPEC §7`. Exists and is the best-built part. Rejection returns to Draft with a stored reason.
16. Issued: the client owes money. Accounts or Owner records the collection (cash, cheque, transfer),
    moves a cheque (received, deposited, cleared, bounced), records withholding at source.
    `SPEC §6.5, §6.7`. No screen. Cash reaches the safe only at Collected (STATUS 2026-11-06).
17. A disputed issued extract resolves as a revised extract or a credit note. `SPEC §7`. No screen. An
    extract with an approved credit note can never reach Collected (open, STATUS 2026-11-06).

### Phase D — Change

18. The client asks for more work. TO raises a change order. Lump Sum priced: TechnicalReview,
    ClientApproval, OwnerApproval, Approved, and the revised value is original plus approved change
    orders. Cost Plus and Design: logged, unpriced. `SPEC §13`. No screen.

### Phase E — Completion and close

19. Owner or TO declares practical completion (الاستلام الابتدائي). Project goes to HandoverPending.
    `SPEC §11, §13`. Exists on the closure screen. Who may declare, and any precondition (progress, all
    extracts issued), are `ASSUMED` (ClosureActors, PracticalCompletionPrerequisites).
20. TO and SE build the snag list. Major snags block handover, minor do not. `SPEC §11`. That a major
    snag blocks until Verified, not merely Fixed, is `ASSUMED` (SnagHandoverBlock, Q115).
21. Handover. Warranty starts on the handover date, 4 months. `SPEC §11`. The act that starts it is
    `ASSUMED` (WarrantyStart, Q110).
22. The hold releases once, in full, at handover. `SPEC §11` and CLAUDE.md. Finance alone, with no Owner
    step, is `ASSUMED` (ClosureActors, Q109). The Verifier found 802-L NOT MET: nothing separates
    whoever releases from whoever approves.
23. Callbacks during warranty; cost on the subcontractor if at fault, else Kaff. `SPEC §11`. Who decides
    fault and which account carries it is `ASSUMED` and books nothing today (Q112, Q116).
24. Close when extracts are issued, advance recovered, hold released and subcontractor accounts closed.
    `SPEC §11`. The Owner closes (`ASSUMED`, ClosureActors).
25. Termination: `Terminated` settles nothing. `SPEC §13` names it and says "settlement"; nothing says
    what settles. Waits on Karim.

## 2. Where today's app breaks the journey

| # | Break | Step |
|---|---|---|
| 1 | No screen creates an extract. The chain can only act on extracts a developer inserted. | 14 |
| 2 | No screen records that the client paid, or moves a cheque. An issued extract is a dead end on screen. Largest hole by business value. | 16 |
| 3 | No change-order, dispute or credit-note screen. | 17, 18 |
| 4 | Day labour goes in one worker at a time, as hours, through a dropdown. Ten men is ten saves. The pool, rating and engagement sit on a separate route from the log. | 10 |
| 5 | The daily log shows no date and cannot take yesterday (consultant B4). | 8 |
| 6 | A new project means a blank BOQ. Every villa starts from zero. Re-checked 2026-11-10: still true, no copy route exists. | 4, 5 |
| 7 | BOQ add is a pick-then-form round trip per line. No multi-add, no reorder, no drag. Re-checked 2026-11-10: still true. | 5 |
| 8 | The workspace has a Setup stage but no checklist saying what ends it. | 2 |
| 9 | The Supervisor holds the next extract action; consultant A5 said he had no route to it. The waiting line now exists (both site-engineer and owner workspace dumps, 2026-11-10, show "المستخلص رقم 3: بانتظار ..."), and the site-engineer dump carries a المستخلصات link under التنفيذ. The dump does not say whether that account is the Supervisor or the junior, so consultant B7 (a money link on a money-free role) may be open. Verify with a named account. | 15 |
| 10 | The closure screen still carries a "rule not yet decided" sentence and a termination button that only refuses (consultant B3). | 25 |
| 11 | Verifier rejected 802 and 803: hold release has no separation of duties; the debit-note posting is missing. | 22, 20 |
| 12 | Five unapproved status words on production screens. "تم التسليم" does not say ابتدائي or نهائي (consultant A8). | 19-24 |
| 13 | A site engineer cannot add a photo. The API exists, the page has no entry kind for it, and I found no file-upload endpoint under `Features/DailyLogs`, `Boqs` or `DayLabour` (the record takes a `FileReference` string). | 9 |
| 14 | Codes and explaining sentences reach the screen (Nabil, 2026-11-10). Seen in the 390 px dumps of 2026-11-10: project code on the workspace and the project list, worker code on the day-labour list, catalogue code on every BOQ row, a question line per workspace group, hints under the project-create type chips, a sentence under the check-in button, and "سعر البيع" printed on every BOQ row. Each is dealt with in its screen brief. | 1, 4, 5, 8-10 |
| 15 | A BOQ line is added one at a time (one POST per line), there is no copy-from-project route anywhere in `src/Api` (searched for template, copy and clone; only the catalogue import template exists), and the page has no custom-item form although `SPEC §4.5` allows custom items. | 4, 5 |
| 16 | The daily-log labour dropdown lists the whole worker pool, not the men engaged on this project (`listPool` in `daily-log-page.ts`), and `RecordDayLabour/Handler.cs` shows no check for an open engagement. | 10 |

## 3. What other systems do, and whether it transfers

**Copy from an earlier job.** Procore's estimating tool has "Copy Selected Estimate" to start from an
existing estimate, and adds items from a cost catalog by ticking checkboxes
([Add an Estimate](https://support.procore.com/products/online/user-guide/project-level/estimating/tutorials/add-an-estimate),
[Edit an Estimate](https://support.procore.com/products/online/user-guide/project-level/estimating/tutorials/edit-an-estimate)).
Odoo converts a project into a template that carries stages and tasks, and says to duplicate the
original first because conversion archives it
([Odoo 19 docs, Project templates](https://www.odoo.com/documentation/19.0/applications/services/project/project_management/project_templates.html)).
This transfers, with one care. Kaff's BOQ is catalogue lines plus quantities, so copying a previous BOQ
is the same idea. Spec §4.4 says a signed BOQ holds values, not references, so a copy should bring the
item and quantity but price from the current catalogue; otherwise last year's rate silently enters a new
contract. That is a rule, not a layout choice (Q-B).

**Drag and drop.** The Procore pages I found use drag only to reorder rows and move bid items between
groups; items are added from the catalog by checkbox and Select
([Edit an Estimate](https://support.procore.com/products/online/user-guide/project-level/estimating/tutorials/edit-an-estimate)).
I found no construction ERP whose main way to build an estimate is dragging from a catalogue. So drag is
a PROPOSAL, not practice. On a phone in RTL it is also the weaker input (small targets, accessibility).
I recommend multi-select with checkboxes as the way in, and drag-to-reorder later. If Nabil still wants
drag, that is a design decision for the design agent, not a rule.

**Crew time entry.** Field-time products let one foreman enter or clock in a whole crew from a phone and
submit it for approval, and expect offline capture
([Pacific Timesheet, crew-based tracking](https://blog.pacifictimesheet.com/blog/crew-based-time-tracking-for-construction-foremen),
[Workyard, crew time tracking](https://www.workyard.com/employee-time-tracking/construction-crew-time-tracking),
[Procore timesheets](https://www.procore.com/resource-tracking/timesheets)).
These are vendor marketing pages; treat feature claims as self-reported. It transfers directly: Kaff's
engineer is the foreman, the labourers are his crew, and spec §8 already says period deltas and offline
drafts. The Kaff twist is that the engineer must never see or set a rate (D-139), so the crew grid shows
a name and an hours control and nothing monetary.

**Handover vocabulary.** Arabic sources describe two stages: الاستلام الابتدائي after the works are
complete, then a warranty or maintenance period set by the contract, then الاستلام النهائي, when the
retention is released
([Bayt](https://specialties.bayt.com/en/specialties/q/348830/%D9%85%D8%A7%D9%87%D9%88-%D8%A7%D9%84%D8%B6%D9%85%D8%A7%D9%86-%D8%A7%D9%84%D9%86%D9%87%D8%A7%D8%A6%D9%8A-%D9%88%D8%A7%D9%84%D8%A7%D8%A8%D8%AA%D8%AF%D8%A7%D8%A6%D9%8A-%D9%88%D8%B6%D9%85%D8%A7%D9%86-%D8%A7%D9%84%D8%A7%D8%B9%D9%85%D8%A7%D9%84/),
[Alkifah, استلام مشروع مقاولات](https://alkifahco.com/construction-project-handover-checklist/),
[Daftra, ضمان الأعمال](https://www.daftra.com/blog/manual/%D8%AE%D8%B5%D9%85-%D8%B6%D9%85%D8%A7%D9%86-%D8%A7%D9%84%D8%A3%D8%B9%D9%85%D8%A7%D9%84/)).
Caution: the results were mostly Saudi or general Arabic, not Egyptian, and the sources do not all agree
on when retention is released. The Kaff spec releases the hold at handover, before warranty; these
sources release it at the final handover, after warranty. That mismatch goes to Karim (Q-F). It does not
change the spec, which is his ruling.

**Added 2026-11-10.** Quotation templates in Odoo, drag-and-drop assemblies in PlanSwift-class tools and
Procore bulk crew entry are recorded with URLs in `ux/research/practice-boq-and-crew-entry.md`. The
finding that matters: no source I could cite uses drag from a catalogue into a list as the main way to
build an estimate, so drag stays a PROPOSAL (Nabil's direction), and tap or tick is the equal path.

**Does not transfer.** Procore, Aconex and Primavera assume a consultant, an owner's engineer and
multi-company bid packages. CLAUDE.md puts the consultant role and bidding out of scope. Take the
estimate-copy idea and the crew time grid, and leave the rest.

## 4. Questions for Karim

| # | Question | Step it changes |
|---|---|---|
| Q-A | When a job starts, does Kaff want to start from an earlier project's BOQ, from a saved base BOQ (for example a standard villa finish), or only blank? Nabil remembers a base project engineers used and edited. Is that real, and how is it chosen? | 4 |
| Q-B | If a BOQ is copied, do rates come from today's catalogue (my recommendation, matches the freeze rule) or from the old project? Do quantities carry over, or only the item list? Do conditions and markups carry? | 4, 5 |
| Q-C | Day labour: is the unit hours (what is built) or days and half days? On site, is a man counted by the day? Is there overtime? Which fields must the engineer type per worker? | 10 |
| Q-D | Does a labourer work on the same site most days, so the engineer wants "same crew as yesterday" pre-filled? Who confirms the crew list is right? | 10 |
| Q-E | May the engineer back-fill a log for yesterday, and for how many days? | 8 |
| Q-F | The hold releases at handover (spec §11). Practice as I could find it releases retention at the final handover after the maintenance period. Which is Kaff's? Are the two handovers called الاستلام الابتدائي and الاستلام النهائي? | 19-24 |
| Q-G | What five words replace the unapproved status words? | 19-24 |
| Q-H | Who prepares the monthly extract, and how far back may it reach? | 14 |
| Q-I | What does a terminated project settle, and to whom? | 25 |
| Q-J | How does a client pay the reduced amount of an extract with a credit note, and does withholding apply to it? | 17 |
| Q-K | Who may release the hold, and does the Owner approve it? | 22 |
| Q-L | Does opening a project staff Finance automatically? | 3 |

Q-F to Q-L restate questions already open (Q109, Q110, Q112, Q115, Q116, N10-2b and the STATUS list).
Q-A to Q-E are new to this module's journey.

Added 2026-11-10 with the screen briefs. Each names the brief it blocks and the default running today.

| # | Question | Blocks | Default running today |
|---|---|---|---|
| Q-M | Does anyone at Kaff say, write or search by a system code: the project code (PRJ-...), the worker code (E-...), the catalogue code on a BOQ line? Where? | Briefs 2, 3, 5 | All three shown. The briefs hide them, except the catalogue code in the add picker, because `SPEC §4.5` searches by code. |
| Q-N | Before a survey exists, which quantity does the person type on a quotation line: the estimated one (`SPEC §4.3`, Sales) or the surveyed one? | Brief 3 | Surveyed is required, estimated is optional, and the printed quotation uses surveyed (`boq-page.html`). Not a RulingNote; nobody ruled it. |
| Q-O | Does the junior engineer see the extract waiting line and a المستخلصات link, or only the Supervisor? | Brief 5 | Both are in the site-engineer dump. |
| Q-P | Does a site engineer need the الإقفال group (handover, warranty, closing) on his workspace? | Brief 5 | Shown (site-engineer dump). Consultant A4 asked to take end-of-project controls out of the daily path. |
| Q-Q | May the site engineer see "percent of the contract done"? The mock's own question 3 asks it and was never answered. | Brief 5 | Shown (12.94% in the site-engineer dump). |
| Q-R | Does a group that does not apply yet (BOQ unsigned, so execute and pay) show as locked with a reason, or not appear at all? | Brief 5 | The approved mock locks it with one sentence. The brief hides it and asks. |
| Q-S | Who may create a client when the owner opens a project and the client does not exist yet? | Brief 4 | The page tells the user to register the client on the clients screen first (`projects.create.no_clients`). |
| Q-T | Does the engineer's quantity add to what he saved earlier today, or replace it? | Brief 1 | Adds (`DailyLogQuantityEntry.Default`, a RulingNote, and the sentence `daily_log.quantity.period_hint` says so). |
| Q-U | Does the printed quotation say "عرض سعر" or "مقايسة"? Already open as Q-210-2. | Brief 3 | "عرض سعر". |
| Q-V | May a BOQ line be a custom item, entered from the builder, by whom? `SPEC §4.5` allows it and queues it for review. | Brief 3 | API accepts it, no screen offers it. |
| Q-W | The advance hint at signature says the client "pays an advance at signing", while `SPEC §5.1` makes the 25% an advance recovered from each period's work. Which is right? | Brief 3 (signature sheet) | `boq.sign.hint.advance` states the first meaning. The workspace says "نسبة استرداد الدفعة المقدمة" (the second). |
| Q-X | Does the workspace in the approved mock (`project.html`) change? Nabil's rule of 2026-11-10 removes things it draws. | Brief 5 | The mock stands until the design agent redraws it. See the conflict note under brief 5. |

## 5. The three changes I would make first

1. **Close the cash gap: create an extract, record a collection, move a cheque.** The consultant called
   it the largest hole, STATUS says the same, and the server already does the work. Without it the
   journey stops where the money starts, and Nabil's verdict is that the journey is the selling point.
   All `SPEC` (§7, §6.5, §6.7); only Q-H (who prepares) is open.
2. **Replace one-worker-at-a-time labour entry with a crew grid on the daily log.** One list of the
   project's engaged workers, each with a name and one hours control, one save for the lot, and a visible
   date. It answers Nabil's labour complaint directly, matches how crew-time products work
   ([Pacific Timesheet](https://blog.pacifictimesheet.com/blog/crew-based-time-tracking-for-construction-foremen)),
   and needs no new business rule beyond the unit (Q-C), which can ship as hours first.
3. **Make the BOQ faster to build: multi-select from the catalogue now; copy-from-project once Karim
   answers Q-A and Q-B.** Multi-add removes the round trip per line and is already inside spec §4.5.
   Copy-from-project may save more per job but is a new rule, so it waits. Drag and drop goes last: no
   source I found uses it as the main way to build an estimate, and it is the weakest input on a phone
   in RTL.

The project overview (Nabil's fourth complaint) is not in my three. Checked against the 390 px dumps of
2026-11-10: what the consultant asked for (progress, money, who is waiting) has landed. The problem has
flipped. The owner's workspace now repeats facts (the contract value in two groups, the running babs in
two groups, the extract holder in the waiting line and again under المال) and prints a question above
each group. Nabil's rule of 2026-11-10 asks for less, so brief 5 cuts instead of adds.

Update 2026-11-10: Nabil's direction on the BOQ is drag from the catalogue and start from a base
project. Change 3 stands as the order of work (tap or tick first, because it works on a phone and has
sources), and brief 3 draws drag as the desktop way in alongside it.

## Screen briefs

Written 2026-11-10 for five screens. Read these rules before any brief.

- Nabil, 2026-11-10: a clear journey needs no extra information, and not everything in the database
  belongs on a screen, codes included. So codes and ids are off unless a tagged step has the person read,
  type or search by them, and each brief lists the sentences it still wants in "Words beyond labels".
- Routes below were checked in `src/Web/src/app/app.routes.ts` on 2026-11-10.
- "Dump" means the `signed-text` output at 390 px in `qa/shots-consultant/text-2026-11-10/`.
- A state sentence (empty, failed, refused) is not a "word beyond labels". It is what the screen says
  when it cannot show data.
- **Nabil's answers, 2026-11-10 (D-212). Where a brief below says otherwise, these stand.**
  - Labour leaving the daily log for its own crew screen (brief 2): not decided. Nabil asked to see the
    proposal, so brief 2 is drawn as a mock first and brief 1 waits for his word on it.
  - Q-E: the engineer may enter yesterday's log. A back-dated entry carries a visible flag. How many
    days back is still open; the mock draws yesterday only.
  - Q-C: labour is entered in hours and in days. Nobody has ruled how a day converts to hours, so no
    screen converts one into the other and totals are shown per unit. The server stores hours only today
    (`RecordDayLabour/Request.cs`, `Hours`); days need a backend lane.
  - Q-D: yes, "same crew as yesterday".
  - Q-M: nobody at Kaff uses the codes; the system introduced them. People search by name. Project,
    worker and catalogue codes stay off every screen, the catalogue add picker included, and every
    search box searches by name.
- Order of the briefs: daily log, day labour, BOQ, project create, workspace. Open questions are in
  section 4, Q-M to Q-X, plus Q-C, Q-D, Q-E, Q-A and Q-B above.

### Brief 1. Daily log

**Rests on ASSUMED and PROPOSAL.** The day shown and yesterday's entry rest on Q-E (`PROPOSAL`). Whether a
second quantity entry adds or replaces rests on Q-T (`ASSUMED`, `DailyLogQuantityEntry`). Which project
states take a log rests on `ASSUMED`, `DailyLogProjectStates`.

- **Screen and route.** `/projects/:projectId/daily-log` (exists, `app.routes.ts`). Changed. The labour
  entry leaves this screen and moves to brief 2.
- **Who.** Site engineer, junior and Supervisor, on a phone at 390 px, one hand, outdoors. Assigned
  projects only (`SPEC §8`).
- **The one job.** Put today's site facts into my log: quantities, materials, a note, a photo.
- **Primary action.** One button, "حفظ", pinned to the bottom (AC-600-K, already built). After pressing,
  the entry appears at the top of "today so far" with its figure, and the form clears and stays open for
  the next entry. Check-in and check-out are separate buttons in a strip at the top (`RULED D-207 §3,
  D-208`). They are not the page's primary button. Before the engineer checks in, the strip is a pair of
  large buttons. After check-in it collapses to one row with the time and a "الانصراف" button. Today both
  check buttons and "حفظ" are primary-styled on screen at once (`daily-log-page.html`).
- **What they must see to decide.** In order: (1) today's date, tappable to yesterday if Q-E is answered
  yes (the API accepts an occurred-at date, `OpenDailyLog/Request.cs`; the page never sends one);
  (2) attendance, as the time he checked in; (3) "today so far", the entries already saved, grouped by
  kind with one figure each; (4) the entry form for the chosen kind, with the kinds as a row of four
  choices: ملاحظة, كمية, خامات, صورة; (5) for a quantity, the line's surveyed quantity and what is
  executed so far.
- **What stays off the screen.**
  - The estimated quantity in the quantity picker (`dl.states` shows estimated, surveyed, executed
    today). It is a Sales number (`SPEC §4.3`). `PROPOSAL`. Ask Nabil whether the engineer ever uses it.
  - The المستخلصات link in the header strip (consultant B7). The engineer's workspace hides money.
    The Supervisor reaches extracts from the workspace waiting line (brief 5, Q-O).
  - Entry ids, the photo's `FileReference`, and the "unit" text field as a free box on materials, where a
    unit list would be quicker (`PROPOSAL`, no data source checked).
  - The worker dropdown and hours box (`kind=labour`): moved to brief 2.
  - Project code and contract value. Neither is on this page today.
- **Words beyond labels.** None kept, with two exceptions that are states.
  - "عند الضغط يُسجَّل مكانك الحالي (GPS) ووقت جهازك." (dump). Off. The button names the act, and the
    time that appears after pressing shows what was recorded.
  - "أدخل ما تم تنفيذه في هذه الفترة فقط. يُضاف إلى ما سُجّل من قبل ولا يحلّ محلّه." (the quantity hint).
    Off. The layout carries it: the input is labelled "تنفيذ اليوم", and above it sits "المنفَّذ حتى الآن",
    which moves to the new total after each save. If Q-T comes back as "replaces", the label changes and
    this reasoning is redone.
  - Kept, as a state: the location refusal. `RULED D-208` says the screen must say why check-in was
    refused and how to turn location on.
- **States.**
  - Empty: "لم يُسجَّل شيء في يومية اليوم بعد." Offers the form directly below. No second prompt.
  - Loading: skeleton of the strip and the list.
  - Failed to load: one refusal line with a retry button. The strip stays visible, because attendance
    does not depend on the list.
  - Location off or denied: refusal naming location and the way to switch it on (`RULED D-208`). The
    check-in button stays pressable.
  - A far-from-site check-in: accepted. The row carries the tag "بعيد عن الموقع" as built (`RULED D-208`).
  - Refused project state: if the project is not in a state that takes a log (Setup is not, by
    `ASSUMED` default), one line says the project does not take a log yet, with a back link to the
    workspace. No form.
  - Offline: `SPEC §8` says offline creates drafts. I did not check whether the page queues anything. Ask the
    builder before drawing an offline state.
- **Next.** Nobody is told. Entries wait until the extract preparer pulls quantities (`SPEC §7`). The
  owner's workspace shows the last log date, so a missing log is visible only to someone who looks.
- **Sources.** Journey steps 8 and 9. `SPEC §8`. `RULED D-207 §3`, `D-208`, `D-210 §3`. `ASSUMED`
  `DailyLogQuantityEntry`, `DailyLogProjectStates`. `PROPOSAL` for the date control and the photo kind,
  with the API at `RecordPhoto` (file upload not found, journey break 13). Consultant B4 and B7.

### Brief 2. Day labour

**Rests on PROPOSAL.** The whole crew-of-today layout is `PROPOSAL`. The questions that would settle it
are Q-C (hours or days) and Q-D (same crew as yesterday). Without Q-C the unit stays hours, which is what
the server stores (`RecordDayLabour/Request.cs`, `Hours`).

- **Screen and route.** `/projects/:projectId/day-labour` (exists). Changed: today it is the worker
  pool, with engage, close and rate on each row (`worker-pool-page.html`), and it records no hours.
  After this brief it is "today's crew". Registering a new man stays at `.../day-labour/new` (exists).
  The history route `.../workers/:workerId/history` is untouched and is not linked from this screen.
- **Who.** Site Supervisor and site engineer, on a phone. Rates are not theirs (`RULED D-139`, no rate
  member in `RecordDayLabour`). The dump shows the history link to the Supervisor, which means his
  entry has `DayLabourRateManage` (`worker-pool-page.ts` `canSeeRates`). Ask whether he should.
- **The one job.** Say which men worked today and for how many hours, in one save.
- **Primary action.** "حفظ" for the whole list. After pressing, each saved row shows a tick and its
  hours, and the header shows the totals ("٦ عمال · ٤٨ ساعة"). If some rows fail, those rows keep their
  hours and show a retry; the saved rows do not resubmit. Each row sends its own entry id so a retry
  cannot double it (the API takes a client entry id and appends; there is no de-duplication,
  `DailyLog.cs` `RecordDayLabour`).
- **What they must see to decide.** (1) The men engaged on this project, one row each, with name and
  trade; (2) an hours control per row (a stepper in half-hour steps, or a tap on a few common values;
  which values is Q-C); (3) the totals; (4) "أضف عاملاً" at the bottom; (5) the date, shared with brief 1.
- **What stays off the screen.**
  - Worker code (E-10003 in the dump). No tagged step has anyone read it. Q-M.
  - Phone number on the row. It exists to find duplicates (`SPEC §10`); it shows in the add-a-man sheet.
  - Times engaged, average rating, "لم يُشغَّل من قبل" twice per row, the history link, the opened-on
    date, the rating box on every row. Rating belongs to closing an engagement (`ux/research/fields.md`
    F-6, 2026-09-26, not re-verified). These live in the add-a-man sheet, where they help choose.
  - The day rate, anywhere (`RULED D-139`).
- **Words beyond labels.** None. The sheet for "أضف عاملاً" has a search box with the placeholder
  "اسم أو مهنة" (a label).
- **States.**
  - Empty (no one engaged): "لا عمالة مشغَّلة على المشروع" and the "أضف عاملاً" button, which becomes the
    primary action in this state.
  - Loading: skeleton rows.
  - Failed: a refusal line with retry.
  - Refused (not assigned): the project-scoped refusal used elsewhere.
  - An hours value the server refuses (zero, or over the limit in `DayLabourEntry.MaxHours`): the error
    shows on that row, in place.
  - Add-a-man sheet: search by name or trade, each result with trade, times engaged, rating and one
    "تشغيل" button; a "تسجيل عامل جديد" link at the bottom opens the registration route.
- **Next.** Weekly payroll, approved by the Owner (`SPEC §10`, assumption 10). No screen exists for it. A
  man whose engagement has no day rate cannot be costed. `worker-history-page.html` shows the rate editor
  only while the rate is empty (`fields.md` §4.8, 2026-09-26, not re-verified). Nobody who sets rates is
  told that a new man needs one. That is a gap to raise with Nabil, not a rule.
- **Sources.** Journey steps 10 and 11. `SPEC §8, §10`. `RULED D-139 §2`. `PROPOSAL` for the layout.
  Practice: Procore bulk crew entry
  (https://support.procore.com/procore-mobile-ios/user-guide/timesheets-ios/tutorials/bulk-enter-time-entry-ios),
  which transfers because the engineer is the foreman. Break 4 and break 16.

### Brief 3. BOQ and quotation builder

**Rests on PROPOSAL and unruled defaults.** Drag, tick-to-add and start-from-a-base-project are
`PROPOSAL` and Nabil's direction, not a spec rule. Which quantity is typed on a quotation is Q-N. Copy
rules are Q-A and Q-B. The printed title is Q-U.

- **Screen and route.** `/projects/:projectId/boq` (exists). Changed. Signing moves out of the page body
  into a sheet.
- **Who.** Technical Office, and the Owner (`ProjectManage`). Mostly a desktop or laptop. The page must
  still work at 390 px, where drag is replaced by tick and "أضف".
- **The one job.** Build the priced list of work for this project until it can be quoted and signed.
- **Primary action.** One button, which changes with the state. Empty BOQ: "ابدأ" opens the choice
  "مقايسة فارغة" or "ابدأ من مشروع سابق" (the second is a `PROPOSAL`, blocked on Q-A and Q-B, and is
  hidden until they are answered). BOQ with lines: "توقيع المقايسة". After pressing, a sheet shows the
  total, the hold and the advance rates with their defaults, and one confirm button that says the BOQ
  will freeze (`SPEC §4.4`, `RULED D-207 §1` for hold and advance at signature). After signing, the page
  shows the frozen date and the contract value, and the primary button goes away. "طباعة عرض السعر"
  stays as a secondary button at any time (`RULED D-208`, D-210 §4).
- **What they must see to decide.**
  1. The lines, grouped by باب, each with description, quantity with unit, and line total.
  2. The draft total, or the contract value once frozen.
  3. The catalogue beside the lines (desktop), as a searchable list grouped by باب. Drag a row into the
     BOQ and it lands in its باب section (`SPEC §4.5`). A "+" on each row and a tick on each row do the same
     without drag. Several ticked rows add together with "أضف (٣)". The catalogue list endpoint already
     filters by text and by باب (`ListCatalogueItems`, `search`, `babId`), so the pane needs no new route.
     At 390 px the catalogue is a full-height sheet opened by "إضافة بنود", with ticks only.
  4. The quantity typed on the line itself, directly after adding, not in a separate form.
  5. The state of an unsigned project: the date its follow-up period ends (`RULED D-208`), or "مؤرشف".
- **What stays off the screen.**
  - Item codes on lines (MAS-3004, FIN-3002 in the dump). No tagged step has the Technical Office read a
    line by code. The code stays in the catalogue pane and its search, because `SPEC §4.5` searches by
    code. Q-M.
  - The word "سعر البيع" on every row (dump). The rate reads as "١٬١٤٦ م٢ × ٤١٠٫٥٥"; the column header
    names it once on a wide screen.
  - Cost price on lines. Margin is a separate view (`SPEC §4.2`: cost, sell and profit shown apart). I did
    not find that view in the routes.
  - Both quantities on every row. One shows (Q-N); the other is in the edit panel.
  - Line positions and ids.
- **Words beyond labels.** None kept on the page.
  - "المبالغ بالجنيه المصري (ج.م)" (dump). Off. "ج.م" sits on the total and on each line total.
  - "هي التي يُحسب بها إجمالي البند." under the quantity field. Off.
  - "لا توجد بنود بعد. ابدأ بإضافة بند من الكتالوج." Becomes a state: the empty state shows the catalogue pane
    open and, after Q-A, the base-project choice. Layout says it.
  - Kept: the signature sheet's one sentence that the BOQ freezes and cannot be edited afterwards
    (`boq.sign.lead`). Reason: it is irreversible (`SPEC §4.4`) and the sheet is where the user decides.
  - The advance hint under the advance field is wrong against `SPEC §5.1`. See Q-W; off until answered.
- **States.**
  - Empty: the catalogue pane open beside an empty list.
  - Loading: skeleton lines.
  - Failed: refusal with retry (as built).
  - Total pending: when a line has no surveyed quantity the total is "-" and the line is marked. The
    sentence in the dump, `boq.total.pending`, becomes the mark on the line.
  - Frozen: lines are read-only, no add pane, the date shows, the primary button is gone.
  - Archived or follow-up: shown per Q-208-1 (30 days by default, a RulingNote).
  - Not for this type: Design has no BOQ (`SPEC §5.3`); the page says so and links to the workspace.
  - A drop that fails (catalogue item archived meanwhile): the row snaps back with one line.
  - Partial multi-add: the API adds one line per call (`AddBoqLine/Request.cs`), so a ticked batch can
    stop halfway. The lines that landed stay, the ones that did not stay ticked.
- **Next.** After signing: the project moves Setup to Active (`SPEC §13`) and the Owner's workspace shows
  the contract value. Nobody is told by a message. For an unsigned quotation the follow-up date is the only
  clock; nobody is told when it nears.
- **Sources.** Journey steps 4 to 6. `SPEC §4.2, §4.3, §4.4, §4.5`. `RULED D-207 §1`, `D-208`, `D-210 §4`.
  `PROPOSAL` for drag, base project and the single changing primary button. Practice:
  Procore ticks to add and drags to reorder (journey section 3); Odoo quotation templates fill lines from a
  saved template
  (https://www.odoo.com/documentation/18.0/applications/sales/sales/sales_quotations/quote_template.html);
  drag assemblies exist in PlanSwift-class tools but onto a plan, not a list (vendor pages, see
  `ux/research/practice-boq-and-crew-entry.md`). What the data supports today: add by catalogue id, one
  line per call, filter by text and باب, custom item by fields, conditions rate and line markup on the
  request. What is missing: a copy-from-project route, a "base project" flag or template entity (searched
  `src/` for template, copy, clone, base project, source project, and found only the catalogue import
  template), a multi-line add, reorder, and any screen control for conditions, markup or a custom item
  (Q-V).

### Brief 4. Project create

**Rests on RULED.** No `ASSUMED` or `PROPOSAL` step except client search and the missing-client exit, which
rest on Q-S.

- **Screen and route.** `/projects/new` (exists). Changed: copy only.
- **Who.** Owner and Technical Office (`ProjectCreate`). Desktop mostly, phone possible.
- **The one job.** Open a project with a name, a client and a contract type.
- **Primary action.** "إنشاء المشروع". After pressing, a Lump Sum project opens its BOQ page, empty and
  ready to fill; Cost Plus and Design open the workspace (`project-create-page.ts`, `destination`). A
  user without `ProjectManage` goes to the workspace or the list.
- **What they must see to decide.** (1) Name; (2) client, as a field with typing to filter, because the
  dump shows seven clients in one dropdown, mixing companies and individuals (`PROPOSAL`); (3) the three
  contract types as three choices, named as they are now.
- **What stays off the screen.**
  - Contract value, hold, advance and every other contract term (`RULED D-207 §1`). Hold and advance are
    already gone from the template.
  - The project code. It is generated; it does not belong to the person creating the project.
  - The follow-up period and start date.
  - A "start from a base project" choice. It belongs on the first BOQ screen, where lines are chosen, so
    this form keeps three inputs (`PROPOSAL`).
- **Words beyond labels.** None.
  - "قيمة التعاقد تأتي من إجمالي المقايسة عند توقيعها." (dump, type hint). Off.
  - "لا تُكتب قيمة التعاقد هنا. تُحسب من المقايسة عند توقيعها. يبدأ المشروع بحالة «لم تبدأ»." (dump, banner).
    Off. If there is no value field, nobody looks for one. The status is visible on the next page.
  - The two other type hints (`projects.create.type_hint.CostPlus`, `.Design`). Off. The three names are the
    choice. If Nabil finds that the names alone do not tell an owner what Cost Plus is, that is a labelling
    question, not a reason for a sentence.
- **States.**
  - Empty clients: "لا يوجد عملاء مسجّلون" with a link to the clients screen (the current sentence also
    says to register first; the link replaces that half). Q-S.
  - Loading clients: the field shows a loading state and stays disabled.
  - Failed to load clients: refusal with retry.
  - Validation: name required, client required. The error sits under the field.
  - Refused (role): the project-create guard is convenience only; the server refuses (`ProjectCreate`).
    The refusal line names the missing permission, not a person.
  - Duplicate project name: not checked by me. Ask before drawing.
- **Next.** The creator is the next actor: BOQ page for Lump Sum, workspace otherwise. Nobody else is told.
  The follow-up clock starts (`RULED D-208`, 30 days by default, RulingNote Q-208-1), and nothing says so on
  this screen. The workspace shows the date (brief 5).
- **Sources.** Journey step 1. `RULED D-207 §1`, `D-208`, `D-210 §1`. Fields research:
  `ux/research/fields.md` §4, "fields asked too early". Q-S.

### Brief 5. Project workspace, owner and site engineer views

**Rests on PROPOSAL and a conflict with an approved mock.** What stays and what goes is `PROPOSAL`
against Nabil's rule of 2026-11-10. Whether the site engineer sees the waiting line, the الإقفال group and
the percent done is Q-O, Q-P, Q-Q. Locked versus absent groups is Q-R.

- **Screen and route.** `/projects/:projectId` (exists). Changed: fewer groups, each fact once, no
  question lines.
- **Who.** Owner (desktop and phone) and site engineer (phone). Other roles see the Owner's view minus what
  their permissions bar. A group a role may not read is absent (`project.html` README, "Permissions and
  money rules").
- **The one job.** Show where this project stands and what I do next.
- **Primary action.** The one the shell already places (`primaryAction` in `workspace-declaration.ts`): open
  the BOQ (Setup), the extract (when it is my turn), "سجل يومي جديد" (site engineer), or the next closure
  step. When it is nobody's turn on me, none. The Owner's dump shows none; the site engineer's shows
  "سجل يومي جديد". After pressing, the user lands on that screen (brief 1 for the log).
- **What they must see to decide, Owner.** In order: (1) the waiting line, with one name: "المستخلص ٣ عند
  المكتب الفني: اعتماد الكميات", so the holder shows once, where today it shows in the line, in a second line
  "يملك الصلاحية الآن: ..." and again under المال; (2) percent done with the contract value beside it;
  (3) money in three figures: billed, collected, outstanding; (4) the date of the last daily log and the
  running babs; (5) the team by name and role.
- **What they must see to decide, site engineer.** (1) Today's log state: done or not, from the last log
  date, with the primary button; (2) the running babs with percent each; (3) "عمالة اليومية" as a second
  button into brief 2; (4) the team by name; (5) percent done, only if Q-Q says yes. Execution comes first
  for this view, before "الفهم" (`PROPOSAL`).
- **What stays off the screen.**
  - The project code (PRJ-377EC1A2 in both dumps). Consultant D5 asked for it smaller. Nabil's rule asks
    for it off unless someone searches by it. Q-M.
  - The question printed above each group: "ما هذا العقد، وكم أُنجز منه؟", "بماذا تعاقدنا، وبأي كميات؟",
    "ما الذي يجري في الموقع؟", "ما المستحق، وما الذي يتحرك الآن؟", "أين وصل المشروع من التسليم والضمان
    والإقفال؟", "من يعمل على المشروع؟". The group name carries it.
  - Repeated facts. The contract value appears under الفهم and again as "إجمالي المقايسة" under الكميات.
    "الأبواب الجارية الآن" appears under الفهم and as "الباب الجاري" under التنفيذ. The latest extract
    appears in the waiting line and under المال. Each shows once.
  - Hold and advance rates, the contract signing date and the start date on the first view. They are
    contract terms the Owner set at signature. They sit one tap away, under the contract value.
  - Provenance chips ("منقول", "محسوب") on money figures (consultant D1, D5, D6).
  - For the site engineer: the client name, contract type badge, any money, the contract value (already absent
    in his dump), and the الإقفال group if Q-P says no.
- **Words beyond labels.** None kept, with one that is a state.
  - "قيمة المنفَّذ مقابل قيمة العقد: 604,646.65 / 4,672,694.01" (dump): the label becomes the figures
    "٦٠٤٬٦٤٦ من ٤٬٦٧٢٬٦٩٤" under the percent. Executed value (from logs, 604,646.65) and certified value
    (from extracts, 110,734.65 under المال) are two different numbers for "done" in the dump. Each gets a
    short label: "منفَّذ في الموقع" and "معتمد في المستخلصات". Without both labels an owner will ask which is
    right.
  - "المشروع غير موقّع. تنتهي فترة المتابعة في {date}." Kept as a state line while unsigned
    (`RULED D-208`). The second line, "وقّع التعاقد قبل هذا التاريخ وإلا يُؤرشف المشروع.", is off.
  - "قيمة التعاقد لم تُحدَّد بعد" (`value_null.title`) kept as the state word. Its body, "تُحسب القيمة من
    المقايسة بعد توقيعها ولا تُكتب يدويًا.", is off.
- **States.**
  - Loading: header keeps shape, the groups show skeletons (as in the mock).
  - Failed to load: refusal banner with retry, no groups.
  - One group fails alone: a refusal with retry inside that group; the rest stays (mock).
  - Setup (BOQ unsigned): contract value reads "لم تُحدَّد بعد", never 0.00 (mock rule). The groups that
    cannot apply yet are locked with one sentence in the mock; Q-R asks whether they are absent instead.
  - Archived: one line and a "إعادة فتح" button for those who may (as built).
  - Refused (not assigned): a lock and one line naming the missing permission, no project details (mock).
  - No log yet: "لا يوجد" in the last-log slot.
  - Cost Plus and Design: the same screen with the differences in the mock's table. I did not re-check
    those two types against the dumps; both dumps are Lump Sum.
- **Next.** The waiting line is how a person learns it is his turn, but only if he opens the project.
  Nothing pushes a message (journey break 9). The Owner learns a log is missing from the last-log date.
- **Sources.** Journey steps 2, 8, 14 and 15 are read here. `SPEC §7, §8, §9`. `RULED D-208` for the
  follow-up line. `PROPOSAL` for the cuts, from Nabil, 2026-11-10. Consultant B1 (questions without
  answers, now answered), D5, D6.
- **Conflict with the approved mock, recorded and not resolved.** `.design/v2/team-a/project.html`, in
  `.design/v2/team-a/README.md` under "The project workspace (`project.html`)", draws: "one intention
  question" in the heading column of every group; a provenance chip (`pv`) and an assumption chip (`asm`)
  on every money figure ("Every money figure carries a kind chip"); a page-header meta line with the code,
  client and type badge; a "locked" group line with one sentence; and the null contract value with a bold
  reason and a smaller explanation. Nabil's rule of 2026-11-10 removes the question line, the chips, the
  code and the explanation. The dashed amber "Review note" blocks in the mock are review chrome and were
  never to be built. The mock also draws five groups; the built page has six, because الإقفال was added
  after the mock. The design agent owns the redraw (D-211: a screen is mocked and approved before it is
  built). Q-X.

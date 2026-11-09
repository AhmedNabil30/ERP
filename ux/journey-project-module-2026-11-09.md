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

1. Owner or TO opens المشاريع and presses مشروع جديد; picks client, contract type, hold %, advance %.
   Screen: `/projects/new`. Exists. `SPEC §5.1` (hold 20% and advance 25% defaults, §16 assumption 1).
2. The project lands in Setup. The workspace should show what is missing: BOQ empty, nobody assigned.
   `PROPOSAL` (a setup checklist). The primary action slot exists (`d702110`).
3. Finance is assigned so it can set the withholding category. `SPEC §9` amendment. Whether opening a
   project implies assigning Finance is open (Q-N10-2b), so this step is `ASSUMED`: a human assigns.
4. Start the BOQ: blank, or copy an earlier project's BOQ and edit it. Blank exists. Copy is `PROPOSAL`.
   See Q-A.
5. TO builds the BOQ. Today: search, pick, quantities, save, repeat. Lines sit under their باب, markup is
   inherited from the باب. `SPEC §4.5`. Conditions and per-line markup: `SPEC §4.2`. I did not find a
   control for conditions or markup in the BOQ template; confirm with a live walk before relying on this.
6. TO signs the BOQ. Catalogue values are copied, the contract value becomes the derived total, and the
   BOQ freezes. Exists. `SPEC §4.4`. Hold, advance and تشوينات then derive from that value. `SPEC §5.1, §15`.
7. Project moves Setup to Active. `SPEC §13`.

### Phase B — Running the site

8. SE opens يومية اليوم on his phone. One log per engineer per project, assigned projects only. `SPEC §8`.
   Today there is no date on screen (consultant B4). `PROPOSAL`: show the day and allow yesterday.
9. SE records field notes, executed quantities against BOQ lines (period delta, never cumulative),
   materials and تشوينات, photos, check-in and out. `SPEC §8`. Exists.
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
| 6 | A new project means a blank BOQ. Every villa starts from zero. | 4, 5 |
| 7 | BOQ add is a pick-then-form round trip per line. No multi-add, no reorder. | 5 |
| 8 | The workspace has a Setup stage but no checklist saying what ends it. | 2 |
| 9 | The Supervisor holds the next extract action; consultant A5 said he had no route to it. The new waiting line may have fixed this. Verify live. | 15 |
| 10 | The closure screen still carries a "rule not yet decided" sentence and a termination button that only refuses (consultant B3). | 25 |
| 11 | Verifier rejected 802 and 803: hold release has no separation of duties; the debit-note posting is missing. | 22, 20 |
| 12 | Five unapproved status words on production screens. "تم التسليم" does not say ابتدائي or نهائي (consultant A8). | 19-24 |

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

The project overview (Nabil's fourth complaint) is not in my three. What the consultant asked for
(progress, money, who is waiting) has largely landed in the workspace template: progress, certified,
billed, collected and outstanding figures, and a waiting line. Verify it live before spending more
design effort there.

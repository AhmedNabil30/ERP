# Slice 9 screen briefs (phone first)

Rules carried from the project: no database codes on screen, no explanatory text unless listed (Nabil, 2026-11-10, memory "screens explain themselves"). Arabic RTL, mobile width. Every screen goes through a design mock and Nabil's approval before build (memory "designer owns the design"). A primary button is never disabled for something the user can fix there (D-229 item 3). Step numbers refer to journey.md.

Screens already built and CHANGED (daily log page `projects/:projectId/daily-log`, photo input, check-in button) are listed as changes, not rebuilt. Two screens are NEW.

---

## 1. Daily log (existing route `projects/:projectId/daily-log`): add the "not sent" state

- **Who.** Junior and supervising site engineer, phone.
- **The one job.** Let him keep recording while there is no signal, and see which lines the server has.
- **Primary action.** Unchanged: the primary on the current screen. After pressing, the line appears at once with a mark "not sent". It never waits for the network. (steps 4, 6, 8)
- **Must see to decide.** 1) Check-in state (pending or on server). 2) His lines with a sent / not sent / refused mark. 3) A single count of unsent lines. 4) The oldest unsent day when it is close to the 3-day window. (steps 13, 16)
- **Off the screen.** Entry ids, queue internals, rates, costs, the cumulative executed total of other engineers. No money anywhere (steps 11 and gate).
- **Words beyond labels.** One sentence when the oldest unsent line is 2 days old, because the layout cannot tell him that his work will be refused tomorrow: needs the Karim answer Q3 for the exact figure. Otherwise none.
- **States.** Empty: as today. Loading: as today, from the phone's copy first. Failed to send: the line keeps "not sent" and a retry; no scary error. Refused: the line shows the server's reason in plain Arabic and offers fix or drop (step 14). No signal: no banner of its own; the marks carry it.
- **Next.** The supervisor and owner read the log online; late lines show back-dated to them only. (step 17)
- **Sources.** Steps 4 to 8, 13, 14, 16 [spec §8, D-217, D-219, D-226] plus PROPOSAL for the marks. Rests on ASSUMED midnight default (Q2) and PROPOSAL for the window warning (Q3).

## 2. Check-in button (existing control on the daily log)

- **Who.** Site engineers, phone.
- **The one job.** Record that he is on site now, with no network needed.
- **Primary action.** Check-in. Next moment: «تم تسجيل الحضور» appears at once; the entry area opens (D-217 item 9). It shows "not sent" until the server has it.
- **Must see.** 1) Time pressed. 2) Whether it reached the server. 3) If far: «بعيد عن الموقع» with metres once the server answers (D-226 item 2).
- **Off.** Coordinates, accuracy, device versus server time (kept in the data, not drawn).
- **Words.** If location is off, refusal text with how to switch it on (D-208 Q-207-4). Already in the build.
- **States.** Location off: refused with reason, nothing saved. Check-out missing: «لم يُسجَّل الانصراف» in review (D-226 item 3). Refused on arrival (for example, no longer today): the reason, no silent drop.
- **Next.** Supervisor and owner see attendance on the review. HR sees none; no attendance report exists (KAFF-613 line 44).
- **Sources.** Step 2, 3 [D-207, D-208, D-217 items 9, 18]. Attendance use for performance is [spec §10 line 516 🟡]. Rests on Q4.

## 3. Photo capture (existing control on the daily log, now a queue)

- **Who.** Site engineers, phone.
- **The one job.** Take site photos now and let them reach the server later.
- **Primary action.** Take photo. After: the thumbnail appears in the day with "not sent"; he can take the next at once (the build today holds one picked photo only).
- **Must see.** 1) Thumbnails of the day's photos. 2) Sent / not sent per photo. 3) Caption box.
- **Off.** File reference, byte size, upload id, publish state for the engineer (publication is deliberate and not his, unless Q9 says so).
- **Words.** None.
- **States.** Too large or not an image: refused with the existing reason, drop or retake. Phone storage full: refused at capture, says so. Failed upload: stays queued.
- **Next.** The photo is visible to the client only after someone publishes it (spec §8 line 357; KAFF-606 unbuilt).
- **Sources.** Step 9 [spec §8 lines 357, 359; D-225 item 2; exists UploadPhoto/RecordPhoto]. The shrink on the phone is PROPOSAL.

## 4. Invoice photo (NEW, or a change to the expense draft; waits on Q6)

- **Who.** Site engineers, phone.
- **The one job.** Photograph a receipt so Accounts can see it.
- **Primary action.** Take photo. After: it shows on the draft it belongs to, "not sent".
- **Must see.** 1) The photo. 2) What it is attached to. 3) Sent state.
- **Off.** Everything money beyond the amount the engineer typed. No balance, no paid state.
- **Words.** None.
- **States.** As screen 3.
- **Next.** Accounts reads it when it confirms the draft (release 2, D-225 item 3).
- **Sources.** Step 10 [spec §8 line 359] BLOCKED: the target is unruled. Rests on Q6.

## 5. Site expense draft and عهدة request draft (NEW surface for عهدة; expense draft screen today is part of the daily log)

- **Who.** Junior drafts; supervisor submits online (Q5). Phone.
- **The one job.** Write down what money is needed or was spent, as a draft only.
- **Primary action.** Save draft. After: the draft shows in a list with "draft, not sent" and later "with the supervisor". There is no "paid" or "approved" state on this screen.
- **Must see.** 1) Amount he typed. 2) Reason / description. 3) Photo of receipt if any. 4) Where it is: draft, sent, waiting for supervisor.
- **Off.** Safe balance, عهدة ceiling, per-request cap figure, previous عهدة balance (those are checked on arrival and are not read offline). Account names, postings.
- **Words.** One sentence, if the cap check fails on arrival: the refusal reason carries it. None otherwise.
- **States.** Cap or "previous عهدة not cleared" failure on arrival: refused with the reason, in his list. Empty: no drafts.
- **Next.** Junior: the supervisor submits online. Accounts pays online. Neither happens on this screen. How the supervisor finds out: PROPOSAL, a list of drafts waiting on him when he is online.
- **Sources.** Steps 11, 12 [spec §8 line 355, spec line 250, §9 line 378, D-188]. عهدة part BLOCKED by KAFF-609 and KAFF-610 and rests on Q5 (ceiling and cap are ASSUMED 🟡 assumption 4).

## 6. Sending list (NEW)

- **Route.** NEW, for example `sync`.
- **Who.** All site engineers, phone.
- **The one job.** Show what has not reached the server and why, and let him act on a refused item.
- **Primary action.** Send now. After: items turn sent one by one in queue order; refused ones stay with the reason.
- **Must see.** 1) Count unsent. 2) Each item: kind (quantity, crew, photo, check-in, draft), project, day. 3) Refused items first, with the reason. 4) The oldest item's day against the 3-day window. 5) Sent today count.
- **Off.** Entry ids, payloads, retry counters, server errors in raw form.
- **Words.** None beyond reasons; the reasons come from the server's error messages already in the i18n files.
- **States.** Empty: nothing waiting. Offline: Send now says so and stays available. Refused: fix (opens the entry) or drop (asks him to confirm, because dropping loses work). Sign-out with unsent items: the screen is where he is sent first (Q8).
- **Next.** Nobody; once sent, supervisor reads on the review.
- **Sources.** Steps 13, 14, 16, 18 PROPOSAL, built on [exists: Request.cs EntryId] and [D-219]. Rests on Q3 and Q8.

## 7. New worker draft (change to the crew screen's add-worker sheet)

- **Who.** Site engineer, phone.
- **The one job.** Add a worker he met on site, even without signal.
- **Primary action.** Save worker. After: he appears in the crew list at once with "not sent".
- **Must see.** Name, trade, specialty, phone (the phone is NOT shown in the sheet today, D-217 item 6; it is needed to dedupe, spec §10 line 514, so this brief only matches today's sheet and flags the clash in Q7).
- **Off.** Rate (D-232 item 4), HR flags, history.
- **Words.** None.
- **States.** Duplicate phone on arrival: warn and acknowledge, or refuse for a salaried phone (D-144, D-146). The reason shows in the sending list.
- **Next.** Worker joins the pool; HR does not review it (D-233 item 2).
- **Sources.** Step 7. Rests on Q7.

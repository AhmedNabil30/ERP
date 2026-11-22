# Release 6, slice 9: the site day on a phone with weak signal

Planning only. Nothing here is built or ruled unless the tag says so. Tags: [spec §x line n], [D-nnn], [exists: path], [practice: source], [QUESTION] (see questions.md, Q numbers).

Gate for the slice: offline cannot move money (backlog.md, "Slice 9"). Offline creates drafts only (CLAUDE.md; spec §8 line 359).

## What exists today (checked 2026-11-22)

- Every daily-log write already carries a device-made `EntryId` and an `OccurredAt` with the device offset, so a retried send is recognised and not counted twice: [exists: src/Api/Features/DailyLogs/RecordQuantity/Request.cs, RecordCheckIn/Request.cs, RecordPhoto/Request.cs, DraftExpense/Request.cs].
- The server judges "today" on its own clock in Africa/Cairo, never the device clock: [exists: src/Api/Features/DailyLogs/DailyLogAccess.cs, GateAsync] and [D-219 item 4].
- Photo is two calls: upload bytes (returns a reference), then record the entry naming it. Upload is capped and type is sniffed from bytes: [exists: UploadPhoto/Handler.cs, RecordPhoto/Request.cs]. The page keeps the upload reference across a failed record: [exists: src/Web/src/app/features/daily-log/daily-log-page.ts line 106].
- The engineer's BOQ read has no rate, cost or total: [exists: ListBoqLines/Response.cs] and [D-188].
- Expense from site is a draft that posts nothing: [exists: DraftExpense/Request.cs], [D-188], [spec §8 line 355].
- There is NO offline queue, no service worker, no local store in `src/Web`: [exists: stories/slice-6-execution/KAFF-613 line 14; grep of src/Web/src found none].
- There is NO عهدة (PettyCashAdvance) endpoint or screen. `src/Api` mentions it only in Treasury balance reads. KAFF-609 and KAFF-610 are unbuilt (backlog line 1122).
- There is NO endpoint for "invoice photos" as such; `RecordPhoto` is a site photo.
- There is no timesheet, no worked-time record and no attendance report. Check-in is a field entry only: [D-188 "Not done"], [KAFF-613 line 44].

## Rulings that overtake the slice-9 backlog text

- KAFF-902 says "latest timestamp wins" for the same field. [D-217 item 11] and [D-219 items 1, 6] ruled: a second quantity ADDS, a correction is a separate action, notes keep both. "Latest wins" now applies to check-in and check-out only. KAFF-902 shrinks accordingly.
- KAFF-906 (8 pts, "money never moves offline") is a gate, not a build: no offline screen holds a money action, so the work is a test and a review, see stories.md S9-6.

## The journey (engineer on a phone, site with patchy signal)

Role: junior or supervising site engineer [spec §9 line 367]. Device: a phone.

1. Engineer opens the app once with signal; the app keeps today's work set on the phone: assigned projects, the BOQ lines without prices, the day's log so far, the project's crew names. [spec §8 line 359 "offline-first"] [exists: ListBoqLines money-free read]. Storage: [practice: MDN storage quotas, https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria, fetched 2026-11-22: Safari deletes script-written data after 7 days without a visit when tracking prevention is on]. Context only. [QUESTION Q1]
2. Engineer arrives on site and presses check-in. The phone takes GPS and device time with no network and saves a pending check-in. The button does not wait for the server. [D-207 §3] [D-208 Q-207-3: far is flagged, not refused] [D-217 item 18: far from 500 m]. If location is off, check-in is refused with the reason, also offline. [D-208 Q-207-4].
3. The pending check-in is the first item in the queue; nothing else sends before it. [D-217 item 9: no entry is saved before check-in] [exists: DailyLogAccess.GateAsync refuses an entry without a check-in today]. Until it arrives, entries are kept as pending. [ASSUMED by design of this plan, not a ruling; see Q2]
4. Engineer records quantities against BOQ lines, one delta each. Each press stores one entry locally with a fresh `EntryId` and `OccurredAt`. The screen shows it at once with a "not sent" mark. [spec §8 line 354 period deltas] [CLAUDE.md "never cumulative"] [exists: RecordQuantity/Request.cs].
5. Same item twice the same day: second entry adds. To fix a mistake he uses edit, which is a correction entry, not an overwrite. [D-217 item 11] [D-219 item 1]. A correction of an entry that has not yet reached the server: the phone changes the pending entry itself. A correction of an entry already on the server is online-only. [QUESTION Q10: the pending-entry half is a PROPOSAL, nothing rules it]
6. Engineer adds crew hours (16 per man per day, half-hour steps). [D-217 items 1, 2] [D-219 item 7] [exists: RecordDayLabour]. Day labour is costed from this log. [spec §10 line 513]. The 16-hour cap is enforced by the server on arrival; the phone also checks it on the pending total so he sees the problem on site. [PROPOSAL, mirrors an existing server rule]
7. Engineer registers a new worker from site: name, phone, trade, specialty. [spec §10 line 514]. Offline he saves a draft worker; the phone-number duplicate check runs on arrival. [QUESTION Q7] [D-144/D-146: salaried phones refuse, other matches warn and acknowledge]
8. Engineer records materials, notes, a stoppage reason (text only). Several notes are kept side by side. [exists: RecordMaterial, RecordField] [D-217 items 12, 14, 16]. Stockpile control stays absent. [D-225 item 4]
9. Engineer takes a site photo. The phone saves the image locally and queues it. On signal it uploads, then records the entry with the returned reference, as two steps. [exists: UploadPhoto + RecordPhoto] [D-225 item 2]. A photo is not published by this step. [spec §8 line 357]. Photo size shrink on the phone is [PROPOSAL]; the server cap is `SitePhotoOptions.MaxBytes`. [QUESTION Q9: who publishes, and may he offline]
10. Engineer takes a photo of an invoice or receipt. [spec §8 line 359 "invoice photos"]. Today nothing says what it attaches to. [QUESTION Q6]
11. Engineer drafts a site expense (description, amount). Stored as a draft; Accounts confirms and posts later, release 2. [spec §8 line 355] [D-188] [D-225 item 3]. The phone never shows a balance or a "paid" state.
12. Junior engineer drafts a عهدة request. A supervisor submits it. Accounts pays. [spec line 250, the عهدة rule] [spec §9 line 378]. Offline: only the junior's draft is stored. The ceiling and "previous عهدة cleared with receipts" checks cannot run offline; they run when the draft arrives. [spec line 250: both are 🟡 assumptions 4] [QUESTION Q5] [BLOCKED: KAFF-609 and KAFF-610 are unbuilt online]
13. Signal returns. The queue sends in order: check-in first, then uploads, then entries by `OccurredAt`. Each item shows sent, refused with the reason, or waiting. [PROPOSAL] Safe to resend because each item has its own `EntryId`. [exists: Request.cs doc comments]. Entry order on arrival does not matter to the totals because quantities add. [spec §8 line 354] [D-188: ties break on entry id]. Context: [practice: MDN Background Sync is "Limited availability", https://developer.mozilla.org/en-US/docs/Web/API/Background_Synchronization_API, fetched 2026-11-22; so the app must also send when it is open, not rely on background sync].
14. A refused item stays visible with its reason and a way to fix or drop it. A refusal never disappears silently. [CLAUDE.md "rejections return with a reason"] [PROPOSAL for the phone display]. The refusal reasons the server can give today: project closed to logs [D-188], entry outside the 3-day window [D-217 item 7], no check-in [D-217 item 9], hours cap, photo too large or not an image, assignment revoked.
15. An entry made offline arrives after midnight. Built today: it is judged by the day it arrived, so a night's work can be refused for "no check-in today". [exists: DailyLogAccess.cs doc comment line 113] [D-219 open item] [QUESTION Q2]
16. A phone offline longer than the 3-day window: those entries are refused on arrival. [D-217 item 7]. The phone must warn him before day 3, so he does not lose them. [PROPOSAL] [QUESTION Q3]
17. Supervisor or owner reads the log online and sees back-dated entries flagged. [D-217 item 8] [D-219 item 5] [D-226]. Nothing on the review read changes for offline; a late send is just a back-dated line. Check-out missing reads «لم يُسجَّل الانصراف». [D-226 item 3]
18. Engineer signs out, or his account is deactivated, with unsent items on the phone. The phone must not discard them silently and must not send them under another user. [PROPOSAL] [QUESTION Q8]

## HR and attendance touchpoints (only where spec §10 ties them to the site day)

- Day labour is costed from the crew entries of the daily log; salaried staff from timesheets; nobody in both. [spec §10 line 513] [D-231, D-232: two screens, one register]. So a crew entry made offline is a costing input; it never carries a rate on the phone. [D-232 item 4: the rate is per engagement] [exists: money-free read]. Sync must not decide a rate.
- Check-in and check-out are the engineer's attendance. The performance review derives "attendance" and "report submitted and on time" and "photos attached" from them, scored weekly. [spec §10 line 516 🟡, assumption 11]. A late-synced check-in must not make the report look late: "on time" needs the device time. [QUESTION Q4]
- No timesheet for salaried staff exists, and the spec does not say check-in feeds one. Not planned here. [spec §10 line 513] [QUESTION Q4]
- Not touched: HR does not review site registrations. [D-233 item 2]. HR is not assignable to a project. [D-234 item 2].

## Where today's code would break this journey

1. No local store, queue, service worker, or sync status anywhere in `src/Web` (steps 1, 4, 13, 14).
2. Steps 2 and 3: the server refuses any entry without a check-in on the server's today. Queue order and the midnight rule decide whether offline work is lost. [exists: GateAsync]
3. Step 9: the page keeps one picked photo, not a queue of them. [exists: daily-log-page.ts line 250]
4. Steps 10 and 12: invoice photo target and عهدة do not exist online.
5. Step 7: worker registration endpoint takes a phone and refuses or warns by rule; an offline duplicate has no resolution screen.

## Practice context (labelled, not a rule)

- Background Sync is not available everywhere (cited at step 13). The slice treats "send when the app is open and signal returns" as the baseline.
- Safari clears script-written storage after seven days without a visit (cited at step 1). Matters only if site engineers hold iPhones. [QUESTION Q1]
- No comparable-system claim beyond the two cited above is made; an uncited one is not practice.

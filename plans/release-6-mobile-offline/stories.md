# Slice 9 draft stories

Draft, from backlog KAFF-900 to 907 (47 points) re-cut after reading what the code already does. Points are rough estimates. Nothing starts before the questions marked BLOCKS are answered. Each AC cites its source. Story ids are placeholders (S9-n); a Scrum Master gives real ids.

Overlap with backlog: S9-1 and S9-2 split KAFF-900 and KAFF-901. S9-3 is KAFF-900 and KAFF-902 (shrunk). S9-4 is KAFF-905. S9-5 is KAFF-904. S9-6 is KAFF-906. S9-7 is KAFF-903. KAFF-907 is folded into every story's mobile-width AC (spec: RTL at mobile width, CLAUDE.md).

---

## S9-1 Local store and queue on the phone · 5 pts · BLOCKS: Q1

Given the engineer has opened the app once with signal.
- AC1 When he opens a project offline, then he sees its BOQ lines (no rate, cost or total), his own log so far and the crew names. [spec §8 line 359; exists ListBoqLines/Response.cs]
- AC2 Given a response carrying a money member, when the phone stores it, then the store refuses to keep it. [CLAUDE.md money rule; D-188 "money-free by construction"]
- AC3 When he records anything offline, then one queue item is stored with a new `EntryId` and `OccurredAt` with the device offset. [exists: RecordQuantity/Request.cs]
- AC4 Given the phone is closed and reopened offline, when he looks, then his unsent items are still there. [spec §8 line 359 offline-first]
- AC5 Given a different user signs in on the same phone, then the first user's items are not shown or sent under the second. [PROPOSAL; Q8]

## S9-2 Send queue in order, safe to repeat · 5 pts · BLOCKS: Q2, Q3

- AC1 When signal returns and the app is open, then the queue sends check-in first, then photo uploads, then entries by `OccurredAt`. [D-217 item 9; exists GateAsync check-in rule; PROPOSAL for the rest of the order]
- AC2 Given the same item is sent twice, when the server receives the second, then totals do not change. [exists: Request.cs "a retried sync is recognised and not counted twice"] Test must assert it on every entry type, because the doc comment is the only source seen.
- AC3 Given two engineers each add a quantity to one line in any arrival order, then the line's executed total is the sum. [spec §8 line 354; D-188 "ties break on entry id"]
- AC4 Given a refused item, when the queue continues, then it does not block the items after it, and the refusal reason is stored beside it. [CLAUDE.md "rejections return with a reason"; PROPOSAL for continuing]
- AC5 The app does not depend on background sync; it sends when open. [practice: MDN Background Sync limited availability, https://developer.mozilla.org/en-US/docs/Web/API/Background_Synchronization_API; context only]
- AC6 An entry made on a project that has since moved to a state closed to logs, or after the engineer's assignment was revoked, is refused with the reason and kept visible. [D-188 project states; spec §9 role and assignment on every endpoint]

## S9-3 Offline daily log entries · 5 pts · BLOCKS: Q2 (day rule)

- AC1 Quantity: each press is one delta, never a running total; the phone never sends a cumulative figure. [spec §8 line 354; CLAUDE.md]
- AC2 A second quantity for the same line and day adds; edit makes a correction. [D-217 item 11; D-219 item 1]
- AC3 Crew hours: the phone blocks above 16 hours per man per day (sum of his pending entries) and steps of half an hour; the server enforces it again on arrival. [D-217 items 1, 2; D-219 item 7]
- AC4 Notes: a second note is kept beside the first. Stoppage reason: text only. [D-217 items 12, 16]
- AC5 Materials: no stockpile control. [D-225 item 4]
- AC6 A stopped project still takes quantities, materials, photos. [D-217 item 15; spec §8 line 356]
- AC7 Correction of an entry already on the server is not offered offline. [PROPOSAL; Q10]
- AC8 Given a late send, the supervisor and owner see the line flagged back-dated; the engineer does not. [D-217 item 8; D-219 item 5]

## S9-4 Check-in and check-out offline · 3 pts · BLOCKS: Q4

- AC1 With no signal, pressing check-in saves a pending entry with GPS and device time. [D-207 §3; exists RecordCheckIn/Request.cs]
- AC2 With location off, it is refused and nothing is saved. [D-208 Q-207-4]
- AC3 A check-in more than 500 m from the site is saved and flagged, never refused. [D-208 Q-207-3; D-217 item 18]
- AC4 Both device time and server time are stored; the server time is never taken from the request. [D-207 "Offline: ..."; exists Request.cs doc]
- AC5 Same day, two check-ins or two check-outs: the latest wins. [D-219 item 6]
- AC6 Check-in is dated today only; a past day gets none. [D-217 item 10; exists GateAsync CheckIn]
- AC7 Missing check-out reads «لم يُسجَّل الانصراف». [D-226 item 3]

## S9-5 Photos offline · 5 pts

- AC1 Photos are captured with no signal and queued as files on the phone. [spec §8 line 359; exists input capture="environment"]
- AC2 On send: upload first, then record the entry with the returned reference; a failed record keeps the upload reference so the file is not uploaded again. [exists UploadPhoto, daily-log-page.ts line 106]
- AC3 A photo not an image or over the server cap is refused with the existing reason. [exists UploadPhoto/Handler.cs]
- AC4 A photo is not published by capture or by send. [spec §8 line 357; exists RecordPhoto/Request.cs "Not published by this call"]
- AC5 Many photos queue; today's page holds one. [exists daily-log-page.ts line 250]
- AC6 Download is gated by `DailyLogRead`, unchanged. [D-226 "built before a ruling"]

## S9-6 The gate: offline cannot move money · 3 pts

Replaces KAFF-906 (8 pts): no offline surface holds a money action, so this is proof, not build.
- AC1 Test: every endpoint the phone queue calls creates no posting and changes no ledger balance. [CLAUDE.md "money never moves offline"; D-188 "writes no posting"]
- AC2 Test: no queue item kind is approval, disbursement, confirm, submit, or publish. [spec §8 line 359; spec §9 line 375]
- AC3 The phone store holds no balance, no rate, no ceiling figure. [CLAUDE.md "never store a balance"; D-188]
- AC4 Each of the five role-and-assignment rules is tested by hitting the endpoints directly with a sync-style retry. [CLAUDE.md permissions]

## S9-7 Expense draft and عهدة draft offline · 5 pts expense (done in the daily log today, add queue), +5 عهدة · BLOCKS: Q5, Q6, KAFF-609, KAFF-610

- AC1 An expense draft is stored offline and sent; it writes no posting. [spec §8 line 355; D-188]
- AC2 A عهدة request is drafted by a junior only; submit is not on the phone. [spec §9 line 378; spec line 250]
- AC3 The 10,000 cap and the ceiling and "previous عهدة cleared with receipts" are checked on arrival, not trusted from the phone. [spec line 250 🟡 assumption 4]
- AC4 Nobody both creates and approves a draft. [spec §9 line 376; CLAUDE.md]
- AC5 An invoice photo attaches to what Q6 says. [spec §8 line 359; UNRULED]

## S9-8 Sending list screen · 3 pts

- AC1 Shows unsent and refused items with reasons, refused first. [PROPOSAL; CLAUDE.md "never a rejection without a stored reason"]
- AC2 Drop asks for confirmation. [PROPOSAL]
- AC3 Shows the oldest unsent day against the window. [D-217 item 7; Q3]
- AC4 RTL at mobile width. [CLAUDE.md]

## S9-9 Late-arrival day rule · 3 pts · BLOCKS: Q2, Q3

- AC1 The day of an entry comes from its `OccurredAt`. [D-188 "OccurredAt separate"; D-219 item 4 "today is the server clock"]
- AC2 Whether it is judged by the day made or the day arrived follows the answer to Q2; today it is the day arrived. [D-219 open item; RulingNote DailyLogEntryRule; Q-219-1]
- AC3 A device clock far from the server's is flagged. [PROPOSAL; Q4]
- AC4 Test around midnight in `Africa/Cairo` including a daylight-saving change. [D-234 item 1; SiteTime]

## S9-10 App shell and install · 3 pts · BLOCKS: Q1

- AC1 The app opens with no signal after one online visit. [spec §8 line 359]
- AC2 Persistent storage is requested so the phone does not clear unsent work. [practice: MDN storage eviction, https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria; context]
- AC3 Zoneless, signals, standalone components only. [CLAUDE.md Angular conventions]

## S9-11 Session end with unsent work · 3 pts · BLOCKS: Q8

- AC1 Sign-out with unsent items warns and offers to send. [PROPOSAL]
- AC2 A deactivated user's queue is refused on arrival and kept visible to him until he drops it. [spec §9; D-053 session rules per KAFF-102]
- AC3 A worker registered offline is reconciled by phone on arrival: salaried phones refuse, other matches warn and acknowledge. [D-144, D-146; spec §10 line 514; Q7]

---

Total re-cut: S9-1 5 + S9-2 5 + S9-3 5 + S9-4 3 + S9-5 5 + S9-6 3 + S9-7 10 + S9-8 3 + S9-9 3 + S9-10 3 + S9-11 3 = 48 pts (backlog said 47). 11 stories, about 58 ACs.

Not in the slice: offline approvals, offline payment, offline corrections of server entries (Q10), photo publishing (KAFF-606), attendance report, timesheets. Any of these needs a ruling first.

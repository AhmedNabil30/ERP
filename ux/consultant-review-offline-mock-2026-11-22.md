# Consultant review: offline mock (offline.html), 2026-11-22

Mode: mock. Scope: all 24 views at 390, light and dark, judged as a site engineer on a phone with bad signal. Judged against `plans/release-6-mobile-offline/screens.md`. Layout checks were not repeated. Shots: `qa/shots-consultant/off-<view>-<light|dark>.png`. Light was opened for every view. Dark was opened for `unsent`, `warn`, `refused`, `photos` and `sync`; the marks, pills and refused bar hold up there and dark raised nothing that light did not.

Overall: the four marks (not sent, sending, sent, refused) work. Shape plus word, the same on every screen, so an engineer can tell sent from unsent at a glance. No money action anywhere, no ids, no balances. The faults are in what a refused line lets him do, in places where one count means two things, and one clipped screen.

## Findings, ranked by cost to the business

### 1. A refused line offers "fix" when fixing cannot work. Views `refused`, `sync`, `drafts`
- Quantity line "مرّت أكثر من 3 أيام على هذا اليوم" has تعديل as its primary. Editing the quantity cannot change the age of the day, so he edits, resends, and is refused again. Same on `sync` (first row).
- عهدة draft "لم تُسوَّ العهدة السابقة" also offers تعديل. The draft is not wrong; the previous عهدة is still open. Editing solves nothing.
- Worker "هذا الهاتف مسجَّل لموظف بمرتب" offers تعديل: right here, he can correct the phone.
- Fix: the action must match the reason. Past-window line: حذف only (a "move to today" would need a ruling, not drawn). Previous عهدة open: no fix, حذف only. Flow change, not copy.

### 2. The late check-in refusal is clipped and dead-ends. View `cilate`
- The reason "هذا الحضور لم يعد لليوم الحالي" runs off the left edge of the card, so he cannot read it. The layout check did not catch it because the overflow is inside the card.
- The card has a refused mark and no تعديل or حذف. Right under it the screen says "لم يُسجَّل الحضور بعد", so two statements about attendance disagree, and the bottom primary is الحضور again. He does not know whether he is checked in.
- Fix: stack the reason under the mark like the log rows. Offer one clear action. Layout plus flow.

### 3. The same count means different things. Views `refused`, `sync`, `syncsending`, `syncoffline`
- `sync` strip says 8 لم يُرسل and the group below is headed "مرفوض 3". Are the 3 inside the 8 or on top? A stranger guesses. `refused` has the same problem: "1 سطر · مرفوض 1".
- `syncsending`: the group is headed "لم يُرسل 5" but two of its five rows already show أُرسل. The header is the state of one row, not of the group.
- "أُرسل اليوم 12" beside "لم يُرسل 5" on the offline view reads as "it worked" when nothing went out.
- Fix: refused in its own figure in the strip (not sent / refused / sent today), refused rows in their own block at the top, other rows grouped by day or kind, not by state. Layout.

### 4. Red is used for "no signal" as well as "refused". View `syncoffline`
- "لا يوجد اتصال. السطور محفوظة على التليفون." sits in the red error banner with the same bar as a refused row. No signal is normal on site and nothing is wrong. Red tells him his work was rejected. The brief said no scary error and no banner of its own.
- Fix: no banner at all (the not-sent marks already say it), or at most a neutral one. Copy is fine. This is also an explaining sentence on screen (see rule check).

### 5. The 2-day warning and the refused row for the same line contradict. Views `warn`, `sync`
- On `sync` the banner "سطر من الأحد لم يُرسل، ويُرفض غداً." sits above a row for the Sunday line already refused today. One says tomorrow, the other says now.
- On `warn` the sentence names الأحد but the unsent rows carry no day, so he cannot find the line it means.
- Fix: show the banner only while nothing from that day is refused. Put the day on the rows it names. Flow plus layout.

### 6. "عند المشرف" is a fifth mark and reads as success. View `drafts`
- The notes say four marks. A green "عند المشرف" pill (same green as أُرسل) is the only status that is a place, not a state, and the green hints at approved. The junior cannot tell whether money is coming.
- Fix: neutral pill, no green, and use the screen brief words (draft, sent, with the supervisor). Copy plus mark.

### 7. "متابعة" does not say what it does. View `sync`, duplicate worker row
- Reason: "هذا الهاتف مسجَّل لعامل آخر." Button: متابعة. Continue what? It means add him anyway. Nothing says a second record will exist.
- Fix: a word that names the action, or the same confirm-first pattern as `syncdrop`. Wording needs Nabil.

### 8. The daily log bottom bar says حفظ with nothing to save. Views `unsent`, `ci`, `refused`, `warn`, `failsend`
- The big black "حفظ" shows while the note field is empty, and on `refused` and `warn` there is no field at all. What does it save? A dead primary. The real next step on that page is the entry or الانصراف.
- Fix: the bar shows حفظ only when something is typed.

### 9. Entry to the sending list is a count row with the same word as the mark. Views `unsent`, `failsend`, `sending`
- The row is titled "لم يُرسل" with "4 أسطر", the same words as the mark on a single line. On `failsend` it grows إعادة المحاولة, while `sync` has إرسال الآن: two words for one action.
- Fix: title the row الإرسال like the page it opens. One verb for retry and send now.

### 10. Photos: caption box has no owner. Views `photos`, `photofull`, `photobig`
- One empty "وصف الصورة" input sits above four tiles that already have captions. Is it for the next photo or does it edit a tile?
- In `sync` the photo row is only "صورة", so several photos look identical.
- Fix: the caption field appears after capture and belongs to that tile. In `sync` show the caption.

### 11. Invoice screen reads as a form. View `invoice`
- "مرتبطة بـ" is a label above a grey box that looks like a disabled field. The amount he typed (850) is not on screen, so he cannot tell which draft this is.
- Fix: draw the draft as a small card with amount and reason.

### 12. Smaller words and noise
- `sync`: refused group headed "مرفوض", every row marked "مرفوض", every row with a reason. Three times for one fact. Drop the group header or the row pill.
- `sync`, `syncoffline`: strip cell "الأقدم" shows a bare day. Say oldest of what. `syncempty` shows "-" there: hide the cell when empty.
- "سطر / أسطر" is used for log lines, check-ins, photos and drafts alike. A photo or a draft is not a سطر. Pick one word for item.
- `syncsignout`: خروج beside a warning reads as "leave anyway"; what happens to the five lines is not drawn. Q8 is open, so this cannot be judged.
- `worker`: no phone field, so a duplicate shows up only after sending, probably the next day when the man is gone. Q7 is open; do not approve until answered.
- `draft`: the مصروف / طلب عهدة switch draws the selected side solid dark, which first reads as the disabled one.

## Rule check
- Explaining sentences on screen, quoted: «سطر من الأحد لم يُرسل، ويُرفض غداً.» (allowed by the brief, see 5); «لا يوجد اتصال. السطور محفوظة على التليفون.» (not allowed, the marks carry it, remove); «سيُحذف هذا السطر من التليفون ولن يصل.» (allowed, drop consequence); «مساحة التليفون ممتلئة. لم تُحفظ الصورة.», «الصورة كبيرة جداً.» and «الموقع مغلق على التليفون. شغّله من إعدادات المتصفح ثم اضغط «الحضور».» (reasons, allowed).
- Database codes: none seen. Money: no pay, approve or submit; amounts on drafts are only what he typed. Pass.
- Status words: the mock uses its own marks (لم يُرسل, جارٍ الإرسال, أُرسل, مرفوض) and not the approved project status words. No clash as long as the two sets never share a screen.

## Brief errors
- Brief says layout PASS at 360/390 for every view; `cilate` still clips its reason inside the card (finding 2). The layout command does not see overflow inside a card.
- Dark checked on 5 of 24 views only.

## First three changes
1. Make the refused row's action match the reason (finding 1). The commonest refusal sends him into a fix that cannot work, and he loses the line.
2. Fix the counts and grouping in the sending list (findings 3 and 5). It is the one screen he opens to learn what happened, and today the numbers do not add up.
3. Fix `cilate` and drop the red no-signal banner (findings 2 and 4). One hides the reason, the other makes normal site conditions look like a failure.

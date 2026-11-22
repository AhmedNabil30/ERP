# Offline states (`offline.html`), notes for the README

Mock of the Slice 9 offline states, from `plans/release-6-mobile-offline/screens.md` (7 briefs). Revised 2026-11-22 after the consultant review (`ux/consultant-review-offline-mock-2026-11-22.md`, 12 findings). Not yet approved by Nabil. Open `offline.html` directly; `embed=1` hides the preview strip (review chrome, do not build it). One phone-width column (max 520 px) on every screen size: the briefs are phone first and ask for nothing on desktop.

This file is the draft of the README section. The README itself was not edited; paste this under a heading `# Offline states (offline.html)`.

## `?view=` list (25 views)

1. Daily log: `unsent`, `typing`, `warn`, `sending`, `failsend`, `refused`, `loading`
2. Check-in: `ci`, `cifar`, `cioff`, `cilate`
3. Photo capture: `photos`, `photofull`, `photobig`
4. Invoice photo: `invoice`
5. Drafts (expense and عهدة): `draft`, `drafts`, `draftsempty`
6. Sending list: `sync`, `syncdrop`, `syncoffline`, `syncsending`, `syncempty`, `syncsignout`
7. New worker: `worker` (sheet), `workersaved`

The daily-log base is the approved `daily-log.html` (D-217): check-in strip, log groups, entry rows, bottom bar with one primary. Only the offline states are drawn on top of it. The entry form is reduced to the note field; the other kinds are unchanged and not redrawn.

## Marks

Four state marks, always shape plus word, made from the kit pill: **لم يُرسل** (dashed outline, clock), **جارٍ الإرسال** (dashed outline, spinner), **أُرسل** (green, check), **مرفوض** (error colour, cross).

**عند المشرف** is not a mark. It is a place the draft has reached, drawn in a neutral pill (`s-place`, no green, so it does not read as approved) and shown only after arrival.

## Rules applied after review

- **The action matches the reason (finding 1).** A refused line offers تعديل only when editing can fix it. Past-window line: حذف only. عهدة with the previous one not cleared: حذف only. Worker with a salaried phone: تعديل and حذف (he can correct the phone). Duplicate phone of another worker: «إضافة رغم ذلك» and حذف. Photo too large: إعادة التقاط and حذف.
- **Late check-in (finding 2).** One refused row (mark, reason under it, حذف). The contradicting "not checked in yet" block is gone; the bottom primary الحضور is the way to check in now.
- **Counts add up (findings 3, 12).** The sending list's strip has disjoint figures: not sent, refused, sent today (a figure is hidden when it is zero or meaningless). Refused rows sit in their own block at the top (the block header says مرفوض once; rows carry no mark). Everything else is grouped by day (اليوم, أمس), not by state; a group header never names a state, so a sent row inside it is no contradiction. The oldest day is the first day group. The daily-log count row has one figure with its state word.
- **No signal is not an error (finding 4).** `syncoffline` has no banner: the not-sent marks carry it. The sent-today figure is left out on that view because it reads as "it worked".
- **Warning and refusal do not contradict (finding 5).** The 2-day sentence shows only while nothing from that day is refused: on `warn`, not on `sync`. On `warn` the Sunday item sits in its own group named الأحد.
- **One verb (finding 9).** The count row is titled الإرسال like the page it opens; retry and send now are both «إرسال الآن».
- **The bar saves only when there is something to save (finding 8).** `unsent` has no bar; `typing` shows حفظ.
- **Caption belongs to the photo (finding 10).** The field is inside the new tile; sending-list photo rows show the caption.
- **The invoice screen draws the draft** (reason and amount) above the photo (finding 11).
- **Selected side of the expense / عهدة switch carries a check** (finding 12).
- Item word: counts use the state word with the figure («4 لم يُرسل») instead of a noun.

## New in the kit

`s-stuck` and `s-place` pills (from existing tokens, no new colour), the refused row (5 px start bar, reason line, actions), the photo tile with its caption inside, and the 1 to 3 cell total strip.

## Not on any screen, on purpose

No pay, approve or submit action anywhere (money never moves offline). No balance, ceiling, cap figure, account name or entry id. No publish control on photos. No banner for "no signal". Sentences appear only where the brief allows: the 2-day warning, refusal reasons, the drop consequence line.

## Open questions this mock depends on

- **Q1 (phones):** nothing drawn depends on it. If iPhone-only, the marks become the only safety net for the 7-day purge; no warning is drawn.
- **Q2 (midnight):** drawn as the default, the day it was made. The refusal «مرّت أكثر من 3 أيام» follows that default.
- **Q3 (offline over 3 days):** default drawn: refuse and warn. Whether a past-window line may be moved to today or accepted by the supervisor is not ruled, so only حذف is offered. Needs Karim.
- **Q4 (attendance clock):** far-from-site shows with metres once the server answered; no clock-gap flag drawn.
- **Q5 (عهدة offline):** default drawn. Open: a عهدة refused for "previous not cleared" offers only حذف; whether the draft should instead wait and resend after the previous is cleared is not ruled.
- **Q6 (invoice photo target):** default drawn: the expense draft only. No عهدة clearing screen exists.
- **Q7 (worker phone):** do not approve `worker` until answered. The sheet has no phone field (today's sheet, D-217 item 6), so a duplicate surfaces only after sending, possibly when the man has left site.
- **Q8 (sign-out and lost phone):** `syncsignout` only lands him on the list with خروج beside the count. What happens to the unsent items after sign-out or deactivation is not drawn and cannot be judged until answered.
- **Q9 (photo publishing):** no publish control drawn.
- **Q10 (correct a quantity offline):** not drawn separately.
- **New (needs Nabil):** the words «إضافة رغم ذلك» for acknowledging a duplicate worker phone (D-144/D-146 say warn and acknowledge; the label is a guess).
- **New (needs Nabil):** whether the sent-today figure should appear while there is no signal.
- **New (needs Nabil):** the neutral «عند المشرف» place label and what a junior should read about it.

## Not decided here

- The crew screen's own primary and day-labour rows are not redrawn.
- «لم يُسجَّل الانصراف» belongs to the owner's review (D-226 item 3).
- Storage-full and too-large reasons are guessed words; the too-large reason should come from the existing text.
- Route name `sync` for the sending list, and where it is reached from on every screen (drawn only as the count row on the daily log).
- Layout checks do not see overflow inside a card (`cilate` was caught by eye); screenshots were opened for `cilate`, `sync`, and (first pass) `unsent`.

## Arabic strings invented (all guesses)

Marks: لم يُرسل · جارٍ الإرسال · أُرسل · مرفوض.
Actions: إرسال الآن · تراجع · إضافة رغم ذلك · خروج · التقاط صورة · إعادة التقاط · التقاط صورة الفاتورة · حفظ المسودة · مسودة جديدة · إضافة عامل · حفظ العامل. (تعديل and حذف are generic.)
Labels: الإرسال · المسودات · لا مسودات · أُرسل اليوم · عند المشرف · مصروف · طلب عهدة · وصف الصورة · لا شيء بانتظار الإرسال · مصروف موقع · الأحد (group).
Sentences and reasons: لم يُرسل ما سجّلته يوم الأحد، ويُرفض غداً. · مرّت أكثر من 3 أيام على هذا اليوم. · لم يعد هذا الحضور لليوم الحالي. · الموقع مغلق على التليفون. شغّله من إعدادات المتصفح ثم اضغط «الحضور». (stand-in for the text already in the build) · مساحة التليفون ممتلئة. لم تُحفظ الصورة. · الصورة كبيرة جداً. · سيُحذف هذا السطر من التليفون ولن يصل. · لم تُسوَّ العهدة السابقة. · هذا الهاتف مسجَّل لعامل آخر. · هذا الهاتف مسجَّل لموظف بمرتب.
Reused from `daily-log.html`: الحضور · الانصراف · تم تسجيل الحضور · بعيد عن الموقع · لم يُسجَّل الحضور بعد · اليومية · المسجَّل · الكميات التقديرية · العمالة.

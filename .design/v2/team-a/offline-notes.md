# Offline states (`offline.html`), notes for the README

Mock of the Slice 9 offline states, from `plans/release-6-mobile-offline/screens.md` (7 briefs). Not yet approved by Nabil. Open `offline.html` directly. `?view=<name>`; `embed=1` hides the preview strip (review chrome, do not build it). One phone-width column (max 520 px) on every screen size: the briefs are phone first and ask for nothing on desktop.

This file is the draft of the README section. The README itself was not edited (another lane is appending to it); paste this under a heading `# Offline states (offline.html)` when that lane is done.

## Views

| Brief | Views |
|---|---|
| 1 Daily log | `unsent`, `warn`, `sending`, `failsend`, `refused`, `loading` |
| 2 Check-in | `ci`, `cifar`, `cioff`, `cilate` |
| 3 Photo capture | `photos`, `photofull`, `photobig` |
| 4 Invoice photo | `invoice` |
| 5 Drafts (expense and عهدة) | `draft`, `drafts`, `draftsempty` |
| 6 Sending list | `sync`, `syncdrop`, `syncoffline`, `syncsending`, `syncempty`, `syncsignout` |
| 7 New worker | `worker` (sheet), `workersaved` |

The daily-log base is the approved `daily-log.html` (D-217): check-in strip, log groups, entry rows, bottom bar with one primary. Only the offline states are drawn on top of it. The `daily-log.html` entry form is reduced to the note field; the other kinds are unchanged and not redrawn.

## The one new thing: the four marks

Every line, photo, check-in, draft and worker carries one mark, always shape plus word (never colour alone), made from the kit pill:

- **لم يُرسل** (not sent): dashed outline pill, clock.
- **جارٍ الإرسال** (sending): dashed outline pill, spinner.
- **أُرسل** (sent): the kit's green pill `s-run`, check.
- **مرفوض** (refused): `s-stuck` pill, cross. Below it, the server's reason on one line, then **تعديل** (fix, outline) and **حذف** (drop, quiet). Drop asks first in the sending list (`syncdrop`): one consequence line, then حذف and تراجع.

## New in the kit

- **Mark** (`pill` with the four variants above). `s-stuck` is added as a pill colour from existing tokens (`--st-stuck-bg`, `--st-stuck-ink`); no new colour literal.
- **Refused row**: an entry row with a 5 px start bar in the error colour (same bar as the edited row), the reason line with a warning icon, an action row.
- **Photo tile**: 96 px thumbnail area, caption, mark under it; two columns. Refused variant has the error border and the reason and actions inside.
- **Three-figure total** (`tot-in t3`): the kit total strip with three cells (unsent, sent today, oldest day).

## Not on any screen, on purpose

No pay, approve or submit action anywhere (money never moves offline). No balance, ceiling, cap figure, account name or entry id. No publish control on photos. No banner for "no signal" on the daily log: the marks carry it. A sentence appears only where the brief allows it: the 2-day warning and the refusal reasons, plus one consequence line before drop.

## Open questions this mock depends on

- **Q1 (phones):** nothing drawn depends on it, but if it is iPhone-only the "sent/not sent" marks become the only safety net for the 7-day purge. No warning is drawn.
- **Q2 (midnight):** drawn as the questions.md default (the day it was made). The refusal reason on a line past the window is an assumption of that default.
- **Q3 (offline over 3 days):** default drawn: refuse and warn. `warn` and `sync` carry the one sentence «سطر من الأحد لم يُرسل، ويُرفض غداً.»; the exact day depends on the answer. No supervisor "accept late" control is drawn.
- **Q4 (attendance clock):** `cifar` shows far-from-site with metres once the server answered. No clock-gap flag is drawn; both times stay in the data.
- **Q5 (عهدة offline):** default drawn: junior drafts, supervisor submits online, Accounts pays online. «عند المشرف» appears only after arrival. The refusal «لم تُسوَّ العهدة السابقة» is the previous-not-cleared check on arrival. The cap refusal reuses the same row.
- **Q6 (invoice photo target):** default drawn: attached to the expense draft (`invoice`, and the photo row in `draft`); the same tile would sit on a عهدة clearing, not drawn because that screen does not exist.
- **Q7 (worker phone):** the add sheet is today's (no phone field, D-217 item 6). Duplicates are drawn in the sending list as refused rows: «هذا الهاتف مسجَّل لعامل آخر» with **متابعة** (acknowledge) and «هذا الهاتف مسجَّل لموظف بمرتب» with edit only. If the phone must be typed, the sheet needs one more field.
- **Q8 (sign-out with unsent work, lost phone):** `syncsignout` is drawn only as "land on the sending list first, with خروج beside the count". What happens to the items after a deactivation is not drawn.
- **Q9 (photo publishing):** default: online only, not the engineer; no publish control drawn.
- **Q10 (correct a quantity offline):** default yes for unsent lines; the fix button on a refused or unsent line is the same تعديل. Not drawn separately.

## Not decided here

- The crew screen's own primary and its day-labour rows are not redrawn; `worker` and `workersaved` show only the add control, the sheet and the new row.
- The «لم يُسجَّل الانصراف» mark belongs to the owner's review (D-226 item 3), not to this screen.
- Storage-full and file-too-large reasons are the guessed words below; the too-large reason should come from the existing i18n text.
- The route name for the sending list (`sync`) is not decided.
- Where the sending list is reached from on every screen: drawn as the count row on the daily log only.

## Arabic strings invented (all guesses, to be confirmed)

Marks: لم يُرسل · جارٍ الإرسال · أُرسل · مرفوض.
Actions: إعادة المحاولة · تعديل · حذف · تراجع · متابعة · إرسال الآن · خروج · التقاط صورة · إعادة التقاط · التقاط صورة الفاتورة · حفظ المسودة · مسودة جديدة · إضافة عامل · حفظ العامل.
Labels: الإرسال (title) · المسودات · لا مسودات · أُرسل اليوم · الأقدم · عند المشرف · مرتبطة بـ · مصروف · طلب عهدة · وصف الصورة · لا شيء بانتظار الإرسال · مصروف موقع.
Sentences and reasons: سطر من الأحد لم يُرسل، ويُرفض غداً. · مرّت أكثر من 3 أيام على هذا اليوم. · هذا الحضور لم يعد لليوم الحالي. · الموقع مغلق على التليفون. شغّله من إعدادات المتصفح ثم اضغط «الحضور». (stands in for the text already in the build) · مساحة التليفون ممتلئة. لم تُحفظ الصورة. · الصورة كبيرة جداً. · لا يوجد اتصال. السطور محفوظة على التليفون. · سيُحذف هذا السطر من التليفون ولن يصل. · لم تُسوَّ العهدة السابقة. · هذا الهاتف مسجَّل لعامل آخر. · هذا الهاتف مسجَّل لموظف بمرتب. · لم يُرسل N أسطر.
Reused from `daily-log.html`: الحضور · الانصراف · تم تسجيل الحضور · بعيد عن الموقع · لم يُسجَّل الحضور بعد · اليومية · المسجَّل · الكميات التقديرية · العمالة.

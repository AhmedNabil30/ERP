# Consultant review: people.html mock (salaried staff and day-labour workers), 2026-11-16

Mode: mock. Judged against D-231 and the README section. Rendered at 390 (RTL, light): staff, staff-create, staff-taken, staff-dup, staff-menu, staff-none, staff-empty, staff-failed, staff-archive, workers, workers-site, workers-create, workers-dup, workers-edit, workers-edit-owner, workers-menu, workers-unarch, workers-ro, nav. Rendered at 1440: staff, workers. Not rendered: staff-createerr, staff-edit, staff-archived, staff-loading, workers-createerr, workers-edit-owner at 1440, workers-archive, workers-archived, workers-none, workers-empty, workers-loading, workers-failed (same kit states as the staff ones seen). Screenshots in qa/shots-consultant/ppl-*.png.

Verdict: approve after must-fixes.

## Must fix before approval

1. workers-unarch (390): after restoring, the list shows an empty body under the filter «مؤرشف», while the count line still says "يعرض عاملاً مؤرشفاً واحداً". Count and rows disagree; HR will think the restore failed or the row vanished. Either the restored row stays visible with the tint, or the count and an empty state update.
2. Phone rows of the worker list (390): the label «تشغيل 12» next to «4.5/5» is unclear (12 what?). The desktop column says «عدد مرات التشغيل». Use a label that reads on its own, e.g. «شُغِّل 12 مرة», and keep one phrase for the same idea on phone and desktop.
3. Worker registered from site (workers-site): the row is an ordinary row in the real product. HR cannot tell who is new and unchecked, and the phone, trade and specialty entered on site go unreviewed. At minimum a plain «جديد» or similar mark until HR opens it, or this is a question to Nabil; as drawn, the review job has no screen.

## Should fix

1. Staff vs worker at a glance: the title and the primary button («تسجيل موظف» / «تسجيل عامل») carry it, which is enough on a phone. But on 1440 the sidebar highlight is the only second cue, and the staff list and worker list look the same except columns. Acceptable; a different icon per list already exists. Keep the title large.
2. Menu words: «الموظفون» next to «عمالة اليومية» still reads, for some HR people, as "all employees, with day-labour a sub-part". Nabil's alternative «الموظفون بالراتب» is clearer to a stranger; the cost is a longer label. I recommend it. Also «عمالة» (labour as a group) vs «عامل» (one person, button «تسجيل عامل») are two words for related things; acceptable in Arabic usage, but the count line «7 عمال» and the title «عمالة اليومية» should not be mixed with «موظفين» for the same person.
3. Duplicate phone, warn (staff-dup, workers-dup): the banner and checkbox sit in the middle of the form and push the الباب field down, so on a phone the person may miss them. The wording of the checkbox «رأيت من يحمل الرقم وأريد المتابعة.» is clear. The two cases (warn vs refuse) are distinguishable: red field error with no checkbox vs a banner with a checkbox. Fine; but the refusal text «لموظف آخر» appears even when the form is the worker form, which is odd for a worker: check the wording for workers.
4. Day labourer becoming staff: with no paragraph, HR sees only the warning with the badge «يومية» and «مؤرشف». That is understandable if the old row was archived; if the old worker is NOT archived the person is blocked or warned with no hint on what to do. Not obvious to a stranger. Raise with Nabil whether a «نقل» action is wanted, since this is a common site event.
5. Owner panel (workers-edit-owner): «الأجر اليومي 720.00» is visible at a glance, with an eye-off icon. On a shared desk screen the number is readable by anyone behind the Owner. The icon is not a protection. Consider tap-to-show. Also the average day rate figure is not a defined concept to the Owner when a worker has had different rates; label risk.
6. «سجل التشغيل» link in the Owner panel is a bare underlined link above the form; reads as a title decoration, not an action. Layout only.
7. Staff list at 390 shows name, phone, job and department, but not hire date or national id; fine for finding a person. At 1440 the hire date is shown. A date in dd/mm/yyyy with latin digits matches the phone digits; consistent.
8. Count line «يعرض 7 موظفين نشطين»: this is a sentence stating what the screen already shows. Under Nabil's rule it is explanation on screen. Remove or keep as a pure number.
9. Employee code removed: HR will not miss it for searching by name/phone, but an HR person writing a payslip or a contract may ask for a number. Ask HR once (README Q5 already asks).

## Fine

- Two separate menu rows, two titles, two bottom-bar primaries, two forms with only their own fields; a day-labourer cannot be put in the staff list through the form.
- No kind select, no permanent-kind sentence, no codes on screen.
- Staff form: name, phone, national id with «14 رقماً», job title, department, hire date. Worker form: name, phone, باب, specialty. Nothing extra.
- Archive row menu, archive confirmation, failed-load banner with «إعادة المحاولة», empty states are consistent and carry no explaining paragraph.
- HR never sees a rate; Owner sees it in one place only.
- Read-only view (workers-ro) is clear with the lock badge and no phone; not needed today, as the README itself says.
- No clipped labels found at 390 in any rendered view.

## For Nabil, not design faults

- No salary field exists for a salaried employee. A contractor's HR will expect at least a monthly salary (and usually insurance number, bank or cash method, contract date) on the staff form; without it the staff list is a phone book, not a payroll base. Say so when he approves.
- No single day rate on a worker. HR in a contractor will expect a standard يومية per worker, which the site then overrides. Here rate lives per engagement, shown to the Owner only as an average; that average may confuse.

## The designer's eight questions

1. Two menu labels: use «الموظفون بالراتب» and «عمالة اليومية»; the first removes the "everyone" reading.
2. One heading or two entries: two entries, as drawn; a heading for two rows adds a level.
3. Screens only, not records: agree; one record keeps the phone check across both lists.
4. Day rate visibility: Owner only here is right; HR never. Tap-to-show on the Owner's screen.
5. Code off: agree; ask HR once whether they quote it.
6. Moving a person: nothing on screen is acceptable only if archive-then-register is taught once; the common case (day-labourer hired on salary) deserves a decision from Nabil.
7. Read-only role: do not build it until a role needs it.
8. التخصص dropped from staff: agree; the job title says the same.

## New words

Natural: «ابحث بالاسم أو الهاتف», «اختر الباب», «علّم هذا المربع للمتابعة.», «14 رقماً», «لا يوجد عمال مؤرشفون», «تمت استعادة «الاسم» إلى القائمة النشطة.».
Needs care: «تشغيل» as a phone label (Must fix 2); «تعذّر تحميل العمال.» is fine; «لم يُشغَّل من قبل» is natural.
Terms: عامل / عمالة / يومية / موظف are used consistently except the «تشغيل» label above. Kaff status words are not used on these screens.

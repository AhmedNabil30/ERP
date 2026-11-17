# Arabic copy review, 2026-11-17 (lean pass)

Scope: keys added or changed in `src/Web/public/locales/ar.json` between `9fb1c83` and HEAD (about 60 with new text). Compared with the approved mocks in `.design/v2/team-a/`. Rulings D-223, D-228, D-229, D-232, D-233 are taken as given. Everything marked "suggested" is a reviewer suggestion, not Kaff's ruling. Nothing under `src/` was edited.

Result in one line: the range matches the mocks word for word almost everywhere. The defects are number and gender agreement, which the mocks share, so the mocks need the same fix.

## 1. Safe fixes (grammar, no change of meaning)

| Key | Current | Suggested | Why |
|---|---|---|---|
| `projects.team.added` | أُضيفت {name} | تمت إضافة {name} | Feminine verb. The mock hard-codes a woman's name, but the string is shared by every person, so a man would read "أُضيفت خالد". Matches `people.restored` ("تمت استعادة"). |
| `projects.team.removed` | أُزيل {name} | تمت إزالة {name} | Same the other way round: masculine verb with a woman's name. |
| `people.count.query` | {count} نتيجة لـ «{query}» {scope} | النتائج: {count} لـ «{query}» {scope} | "2 نتيجة" and "5 نتيجة" are wrong (نتيجتان, نتائج). The colon form needs no agreement and copies "يعرض الكل: {count}". Mock has the same error. |
| `people.worker.engaged_times` | شُغِّل {count} مرة | مرات التشغيل: {count} | "شُغِّل 5 مرة" and "شُغِّل 2 مرة" are wrong. The mock shows only the number under the header «عدد مرات التشغيل»; the phone card has no header, so the label carries it. Keep `engaged_once`. |
| `errors.master.employee_phone_taken` | ...لموظف بالراتب نشط. | ...لموظف نشط بالراتب. | "نشط" after "بالراتب" reads as describing the salary. Mock has the same order. |
| `errors.project.design_not_signed` | لا يمكن تحويل مشروع التصميم إلى تنفيذ قبل توقيعه. | وقّع تعاقد التصميم أولاً ثم حوّله إلى تنفيذ. | Same meaning, tells the person what to do. |
| `projects.sign_design.area` | مساحة التصميم (م²) | مساحة المشروع (م²) | One meaning, two words: the project facts and the mock say «مساحة المشروع». |

## 2. People count line, assembled

Code: `employee-list-page.ts` `countText`. It matches the mock's `people.html` logic exactly, so it is faithful. Sentences below are the real output.

Staff, active filter:

| Count | Output | Verdict |
|---|---|---|
| 1 | يعرض موظفاً نشطاً واحداً | right |
| 2 | يعرض 2 موظفين نشطين | readable, not clean: a digit before the dual. Proper: يعرض موظفين نشطين اثنين |
| 3 to 10 | يعرض 5 موظفين نشطين | right |
| 11+ | يعرض 15 موظفاً نشطاً | right |
| 103 to 110 | يعرض 105 موظفاً نشطاً | wrong, should be موظفين نشطين. The rule must use count mod 100; the code tests the raw count |

Workers, active filter: يعرض عاملاً نشطاً واحداً / يعرض 2 عاملين نشطين / يعرض 5 عمال نشطين / يعرض 15 عاملاً نشطاً. Same verdicts.

Archived filter: same pattern with مؤرشفاً / مؤرشفين. Right for 1, 3 to 10, 11+; the same two flaws for 2 and 103 to 110.

All filter: يعرض الكل: 12. Right.

Search text set: «{count} نتيجة لـ …». Wrong for 2 to 10 (see fix above). Right for 11+; "1 نتيجة" is acceptable.

Zero with a filter and no text: يعرض 0 موظفاً نشطاً. Normally hidden by the empty state; if it ever shows, it should say "لا يوجد موظفون".

Minimum fix: (a) use `count % 100` for the 3 to 10 and 11+ tests; (b) for exactly 2, add a key `people.count.two` = يعرض {noun} {adj} اثنين (noun is the existing `.two` form, adj the existing plural); (c) the `people.count.query` change above. (a) and (b) touch the component, so they are Nabil's call; (c) is a string only.

## 3. Other checks

- Team tab keys match `project-team.html` verbatim, including the remove question, the consequence sentence, «لم يُسنَد مهندس الموقع بعد», «لا يوجد أحد متاح للإضافة», «اختر شخصاً», «اختر الدرجة». None is developer talk. «لم يُسنَد…» states a fact; the Owner sees the add button beside it, so he can act.
- Sign sheet and facts: «نسبة الحجز», «نسبة الدفعة المقدمة», «نسبة الخصم من كل مستخلص», «مساحة المشروع», «سعر المتر», «توقيع التعاقد», design stages «التعاقد · التصميم · التسليم» all match the mocks and the rulings. «تنتهي فترة المتابعة في {date}.» matches `project.html`.
- People screens match `people.html`: menu «الموظفون» / «عمالة اليومية», search placeholder, filter labels, empty and failed lines, archive question and detail, «تمت استعادة», «لم يتغير شيء.», phone error, hint «14 رقماً», «علّم هذا المربع للمتابعة.».
- One word one meaning: عامل (day-labourer), موظف (salaried person), عمالة اليومية (menu name of the day-labour pool) are used consistently and match D-232. For the record: «اليومية» is also the daily-log tab and «آخر يومية», so «عمالة اليومية» shares the word. Already ruled; not re-raised.
- الدرجة (team tab level) agrees with the existing level values «عادي / مبتدئ / مشرف» and with `errors.identity.assignment_level_not_applicable` («درجة الإسناد»). No clash.
- إزالة (take a person off a project), أرشفة (take a person off the register), حذف (not used for people): each has its own meaning. Good.
- Server refusals in the range: `finance_cannot_be_removed` is plain and gives no way out, which is correct since Finance cannot be removed. `signing_terms_required` names الحجز and الدفعة المقدمة, and `advance_recovery_rate_required` covers the third rate; two separate messages, acceptable. `employee_duplicate_phone_not_acknowledged` is actionable. `design_not_signed` see fix above. No developer talk found in the range.
- `en.json` parity for the range: no missing keys. Every added `ar.json` key exists in `en.json`. `errors.project.signing_terms_required` was edited in `ar.json` only; the English text is generic and still correct.

## 4. Questions for Nabil

1. Count line: do you want the two small code changes (mod 100 for 103 to 110; a dual form for exactly 2), or keep the mock's behaviour? Only the case of 2 is common.
2. Un-archive has three words: «إلغاء الأرشفة» (menu), «استرجاعه» (archive detail), «استعادة» (button, and «تمت استعادة» in the toast). Use «استعادة» in all three? Suggested: `people.archive.detail` becomes «يختفي من القائمة النشطة، ويمكنك استعادته من «مؤرشف» في أي وقت.»
3. Removing someone from a project: the team tab says «إزالة», but `errors.identity.assignment_already_revoked` says «الإسناد ملغى بالفعل». Should the error say «تمت إزالته من المشروع بالفعل»? (Suggestion only.)
4. `projects.team.remove_detail` uses masculine pronouns for every person, including women on the team (the mock shows نورا and هبة). Masculine default is normal Arabic; keep, or write neutral: «تُسحب صلاحية الوصول إلى المشروع وتبقى الأعمال السابقة. يمكنك إضافة الشخص من جديد.»
5. Outside the range, «المحجوز» is used for the hold amount (25 keys in `extract.*`, `closure.*`, `errors.closure.*`) while the ruling fixes «الحجز» for the rate. Is «المحجوز» the right word for the amount, so that only the rate labels change?

## 5. Contradicted keys outside the range (keys only)

Rate labels against «نسبة الحجز» and «نسبة الخصم من كل مستخلص» (7):
- `extract.input.hold_rate` (نسبة المحجوز)
- `extract.input.advance_recovery_rate` (نسبة استرداد الدفعة المقدمة)
- `billing.component.AdvanceRecovery`
- `proof.contract.rates`
- `proof.col.advance`
- `proof.asm.A11.name`
- `proof.asm.A11.shows`

«العقد» where «التعاقد» was ruled (6):
- `proof.contract.no_value`
- `proof.check.holdIsRateOfContract.title`
- `proof.check.holdIsRateOfContract.rule`
- `proof.check.collectionEqualsContract.title`
- `proof.check.collectionEqualsContract.rule`
- `projects.convert.type` and `errors.project.execution_type_not_allowed` («نوع عقد التنفيذ»), counted as 2

Count: 14 keys outside the range. The 25 «المحجوز» amount keys are not counted; see question 5. `projects.sign_design.area` (inside the range) is in section 1.

## Anything wrong in the brief

- Nothing blocking. The diff holds about 60 keys with new text, not 80. The `hr.*` changes are mostly removals (employee list count, create and edit titles, field code, field kind, archive confirm and others) with no replacement text to review.

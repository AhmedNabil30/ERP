# Release 2, site money: mock notes (`site-money.html`)

One file draws the 13 screens of `plans/release-2-site-money/screens.md`. Open with `?screen=s1..s13&view=<state>`; `&embed=1` hides the preview strip. The strip lists every screen and every state. The default is `?screen=s1`. The sidebar of `layout.html` is not redrawn; a top bar stands in for it. Nothing here is approved. This file replaces a README section until the README lane is allowed to take it.

## Screens and states drawn

| Screen | Views |
|---|---|
| S1 `s1` عهدة request | normal (my drafts, state «مسودة»), empty, form, loading, failed (typed text stays), refused: earlier عهدة (offers settlement), refused: over the cap (no ceiling figure) |
| S2 `s2` drafts to submit | normal (one open item carrying إرسال / إرجاع), return (required reason), empty, loading, failed, refused (not assigned: page does not open) |
| S3 `s3` settle | normal, empty (no lines, send disabled), returned (Finance reason on top), loading, failed (photos stay on the phone), refused (lines exceed received) |
| S4 `s4` waiting on Finance | normal (list with per-row state), detail request / settlement / expense (one primary each), reject (required reason), empty, loading, failed |
| S5 `s5` waiting on Owner | normal, reject, empty, loading, failed, refused (Owner prepared it: no approve button) |
| S6 `s6` firm advance | Finance, Owner, Owner over the cap (figures shown), empty, loading, failed |
| S7 `s7` safe is short | sheet over S4: normal, failed (injection refused with reason), refused (user may not inject, names who may) |
| S8 `s8` owner current account | normal, form (kind chosen first), empty, loading, failed, refused (preparer is approver) |
| S9 `s9` firm-advance exposure | normal, empty, loading, failed |
| S10 `s10` subcontractor extracts | normal, viewer (role that does not prepare: button absent), empty, loading, failed, refused |
| S11 `s11` subcontractor extract | Technical Office, Finance deductions, Owner, Finance pay, reject, empty draft, loading, failed, refused (own creation) |
| S12 `s12` withholding carried | normal, empty, loading, failed |
| S13 `s13` QC report, تشوينات balance | normal, capped (recovery limited to outstanding), hidden (supervisor may not see money), loading, failed |

Money is `.fig` (LTR, tabular), currency named once in a column header, field affix or the hero. Each ledger is shown alone: no figure adds عهدة, firm advance, hold, client advance and owner account. No code, id or GUID is on any screen.

## Reused from the kit, unchanged
Buttons (one filled per screen), pill, banner, state (empty, refused), field, segmented control, skeleton, `kv` list, `chain`, back link, phone bottom action bar.

## New parts (put in `components.html` before building)
- `.rw`: one row that is a table row at 821 px and wider and a stacked card on a phone, cells labelled by `data-l`.
- `.qi`: queue item; only the open item shows its actions (S2, S5). Avoids a filled button per row.
- `.rcpt`: receipt thumbnail with camera icon (S3, S4).
- `.ovl` + `.dlg`: the safe-is-short sheet; a bottom sheet on a phone, a centred dialog on desktop (S7).

## Question dependencies (default drawn where questions.md gives one)
- **Q1** (عهدة and expense draft: one spend or two): S3, S4. Drawn as two kinds of row in one S4 list, settlement and expense separate. If Karim says "separate", split S4 in two.
- **Q2** (certifier, sub-BOQ): S10, S11. Drawn: supervising engineer prepares, Technical Office certifies the quantities. No sub-BOQ lines drawn.
- **Q3** (Owner approves every act): S4, S5, S11. Default "yes, all, no threshold": S4 rows show «بانتظار المالك» before approval and «جاهز …» after. A threshold would remove rows from S5.
- **Q4** (withholding base, remittance): S11, S12. The 1% rate and the base are SAMPLE figures. S12 has no action (default: carry the balance, build no remittance).
- **Q5** (firm advance cap): S6, S9. Drawn per project (the only case the brief shows the cap for). Cap figures are sample.
- **Q6** (ceiling, cap, holder): S1, S3. S1 refusal names the reason, never the ceiling. Holder drawn as the junior (S3 and S2 treat the supervisor as submitter only).
- **Q7** (owner account): S7, S8. Default: user repeats the payment after the injection (nothing kept pending). Who may record is open; S7 "refused" says «يسجّله المالك» as a placeholder.
- **Q8**: S13 keeps the supervising engineer entering the stockpiled value at the QC report.
- **Q9** (engineer types an amount on an expense draft): S1 shows the amount field because the عهدة request needs it; S4 expense detail shows the amount the engineer typed.
- **Q10, Q11, Q12**: nothing drawn. S11 has no QC step; retention 5% is fixed.
- **D-218 item 6** (supervisor sees project money): S13 `hidden` draws the figure absent; `normal` shows it.

## Departures and judgement calls
- Brief S6 says the Owner's action is disabled on a breach (D-229 item 3), while the product rule says an unusable control is absent. Drawn disabled with the figures shown, as the brief says.
- S2, S5 use the open-item pattern instead of an action button on every row.
- S4 and S5 do not show a project column in S4 (it is per project route) and do not draw the company-level variant (Q3 decides it).
- S7 refused: names the Owner only; confirm with Q7.

## Sentences left on the screen (each listed in screens.md)
- S1: «توجد عهدة سابقة لم تُسوَّ بعد», «المبلغ أعلى من الحد المسموح لطلب العهدة».
- S3: «مجموع البنود أكبر من المبلغ المستلم»; «تعذّر الرفع. الصور محفوظة على هاتفك» is a failed-state line.
- S6: «المبلغ يتجاوز الحد بمقدار …».
- S7: «الخزنة لا تكفي لهذا الصرف».
- Refusal lines for own-preparer (S5, S8, S11); one-line empty states.

## Invented Arabic (guesses, check with Nabil)
طلب عهدة جديدة · مسوداتي · مسودة · عهد بانتظار الإرسال · تأكيد الإرجاع · تأكيد الرفض · سبب الرفض · إرجاع · إرسال للمحاسبة · إضافة بند صرف · بنود الصرف · المستلم · المصروف · غير المصروف · أُعيدت التسوية · بانتظار المحاسبة · جاهز للصرف / جاهزة للاعتماد / جاهز للتأكيد · بانتظار المالك · معتمد من المالك · اعتماد التسوية · تأكيد · صرف · بانتظار اعتمادك · لا شيء ينتظرك · سلفة على عميل · تجهيز سلفة · تدفع مقابل · الحد · الحد الأقصى · الخارج حالياً · بعد هذه السلفة · إجمالي المعرّض لكل المشاريع · العجز · مبلغ الضخ · تسجيل ضخ من المالك · الخزنة لا تكفي · حساب المالك الجاري · مستحق للمالك · تسجيل حركة · ضخ / سحب / سداد · سلفة قابلة للرد · سحب نهائي · نوع السحب · السلف على العملاء · إجمالي الخارج · مستخلصات مقاولي الباطن · مستخلص جديد · الدور على · اعتماد الكميات · تطبيق الاستقطاعات · الاستقطاعات · الضمان هذا الأسبوع · الضمان حتى الآن · استقطاع الخصم والإضافة · الصافي المستحق · الخصم والإضافة المستحق توريده · المستفيد · الأساس · النسبة · المخصوم · تقرير الجودة · إرسال التقرير · تشوينات متبقية · مواد مشوّنة بالموقع · مواد مركّبة · محدود بالمتبقي.

## Sample data, not rules
Names (خالد منصور, هبة فؤاد, منى سامي, أحمد نبيل, سارة عادل, شركة النور للكهرباء, الأمل للسباكة, حسن للنجارة) and every figure are sample. The 1% withholding, 500,000 cap, 10,000 limit (30,000 shown as a breach) are placeholders for Q4, Q5, Q6.

## Not drawn
Company-level S4 variant (Q3), the Owner dashboard tile frame around S9, the supervisor count on his project screen (S1 "who tells him" is open), dark/1440 screenshots (none taken in this run), sidebar entries for the new routes.

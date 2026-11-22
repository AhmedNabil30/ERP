# Release 2, site money: mock notes (`site-money.html`)

One file draws the 13 screens of `plans/release-2-site-money/screens.md`. Open with `?screen=s1..s13&view=<state>`; `&embed=1` hides the preview strip. The strip lists every screen and every state. The default is `?screen=s1`. The sidebar of `layout.html` is not redrawn; a top bar stands in for it. Nothing here is approved. This file replaces a README section until the README lane is allowed to take it.

## View names (exact `?screen=` and `?view=` values)

Open `site-money.html?screen=<screen>&view=<view>&embed=1`. An unknown view falls back to the first one and logs `unknown view` in the console. Hyphens are ignored and four guessed names are aliased (`refused-cap`, `refused-earlier`, `owner-cap`, `owner-over-cap`).

| Screen | `view=` values (first is the default) |
|---|---|
| `s1` عهدة request | `normal` (my drafts, each names who holds it), `empty`, `form`, `loading`, `failed`, `refused` (earlier عهدة, offers settlement), `refusedcap` (over the cap, no ceiling figure) |
| `s2` drafts to submit | `normal`, `return` (required reason), `empty`, `loading`, `failed`, `refused` (not assigned) |
| `s3` settle | `normal` (third line has no photo yet), `empty`, `returned` (Finance reason on top), `loading`, `failed`, `refused` (lines exceed received, send disabled) |
| `s4` waiting on Finance | `normal` (group «دورك», then group «بانتظار غيرك»), `req`, `settle`, `exp`, `reject`, `empty`, `loading`, `failed` |
| `s5` waiting on Owner | `normal`, `reject`, `empty`, `loading`, `failed`, `refused` (Owner prepared it, no approve button) |
| `s6` firm advance | `finance`, `owner`, `overcap` (Owner, breach: banner, figures, approve disabled), `empty`, `loading`, `failed` |
| `s7` safe is short | sheet over S4: `normal`, `failed`, `refused` |
| `s8` owner current account | `normal`, `form`, `empty`, `loading`, `failed`, `refused` |
| `s9` firm-advance exposure | `normal`, `empty`, `loading`, `failed` |
| `s10` subcontractor extracts | `normal`, `viewer` (button absent), `empty`, `loading`, `failed`, `refused` |
| `s11` subcontractor extract | `tech`, `fin`, `owner`, `pay`, `reject`, `empty`, `loading`, `failed`, `refused` (own creation) |
| `s12` withholding carried | `normal`, `empty`, `loading`, `failed` |
| `s13` QC report, تشوينات balance | `normal`, `capped`, `hidden` (supervisor may not see money), `loading`, `failed` |

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

## Consultant review applied (2026-11-22, `ux/consultant-review-site-money-mock-2026-11-22.md`)

| # | Done |
|---|---|
| 1 | S5: every row has a one-line purpose, the amount with ج.م on every row, and the oldest item is the open one (list sorted oldest first). |
| 2 | S7 refused: NOT solved. Spec is silent (Q7 point 4: keep a refused payment pending, or repeat it). Drawn as a lock and one line, no invented next step. Question below. |
| 3 | S3: the figure is named by the action («المتبقي للإرجاع»). Each line shows a receipt state: photo attached (check badge) or «بلا صورة فاتورة» (dashed). Whether a line without a photo blocks the send is open (question below); send stays enabled in `normal`. |
| 4 | S1: each draft names who holds it («عند المشرف: هبة فؤاد»); the pill icon is a document, no longer the clock used for «waiting». Journey Flow 1 step 5 ("who tells him") is still open. |
| 5 | S4: two groups, «دورك» and «بانتظار غيرك». One phrase for "your turn": «دورك». |
| 6 | S11: the figures come first; the chain is collapsed to «الدور على: name · step» and opens on tap. |
| 7 | S13: nothing redrawn. The hero must update as the supervisor types, but the formula (stockpiled minus installed?) is not in the spec; the builder needs it from Karim. |
| 8-10 | Wording, see the table below. |
| 11 | «العجز» became «الناقص»; «ضخ» became «إيداع» (see choices); «سلفة على عميل» now shows the client on S5 and S6 (sample name). «الأساس / النسبة» (S12) and «تقرير الجودة» left: question below. |
| 12 | S13 hero is «رصيد التشوينات»; the field stays «مواد مشوّنة بالموقع». |
| 13 | S7 sentence deleted. S3 refused keeps one: the banner now carries the excess («… بمقدار 500 ج.م»); the red figure row is removed. S7 refused: dashed box removed, lock and one line. |
| 14 | S4 phone rows drop the «من / المبلغ / التاريخ» labels; ج.م sits on every amount. |
| 15 | S10 phone cards drop the «الأسبوع» and «القيمة» labels and show the date range instead of the week number. The «الدور على» label stays on the phone, because a bare «المالك» does not say what it is. S11 subtitle uses the date range too. |
| 16 | S11 shows only the lines that exist at the step: Technical Office sees the certified value only; Finance adds the retention lines; net and withholding appear from the Owner step on. Whether Technical Office may see retention and net at all is a Karim question. |
| 17 | S9: stray chevron removed; a row at 90% of its cap or more carries «قريب من الحد» (sample: 680,000 of 700,000). The 90% line is a placeholder. |
| 18 | ج.م on every amount in S2, S4, S5, S10. |
| 19 | At 821 px and wider the page action sits beside the title, not at the far edge (all screens). |

## Wording choices (one term per act; for arabic-copy and Karim)

| Act | Term drawn | Replaced |
|---|---|---|
| Finance pays out | «دفع» (button S4, S11; chain step «الدفع») | «صرف» as a button, «جاهز للصرف» |
| Money spent on site | «مصروف» (S3 «بنود المصروف», «إضافة بند مصروف», S4 row «مصروف موقع», S4 settlement figure «المصروف») | «بنود الصرف», row title «مصروف» |
| Paid state of an extract | «مدفوع» (S10) | «مصروف» |
| Owner approves | «اعتماد» (Owner only: S5, S6, S11 Owner step, S8 «معتمد») | |
| Technical Office certifies | «تصديق الكميات», figure «القيمة المصدّقة» | «اعتماد الكميات», «القيمة المعتمدة» |
| Finance clears a settlement | «قبول التسوية» | «اعتماد التسوية» |
| Extract under Technical Office or Finance | «قيد المراجعة» (S10 pill) | «جاري الاعتماد» |
| Withholding | «الخصم والإضافة» everywhere (S11 chain step, S11 row, button «تطبيق الخصم والإضافة», S12 title). Column «المخصوم» stays | «الاستقطاعات», «استقطاع الخصم والإضافة» |
| Subcontractor retention | «الضمان» unchanged; Nabil to rule against the client-side term | |
| Owner puts money in the safe | «إيداع» («تسجيل إيداع من المالك», S8 kind) | «ضخ». «توريد» was not used: S12 already uses it for «المستحق توريده» |
| Safe shortfall | «الناقص» | «العجز» |
| S7 amount field | «المبلغ» | «مبلغ الضخ» |

## Questions this pass leaves open (spec and plan are silent; nothing invented)
- **Q7 (4), S7 refused:** keep the refused payment pending, or the user repeats it? Until answered the Finance user who may not record the deposit has no named next step. Q7 (1), who records the deposit, decides who the line «الإيداع يسجّله المالك» names.
- **S3 hand-back:** when and to whom the leftover cash returns (journey Flow 1 step 10 says it goes back to the safe; no step says how the holder hands it over). Also whether a line without a receipt photo blocks «إرسال للمحاسبة».
- **S1:** who tells the supervisor a draft waits (journey Flow 1 step 5).
- **S11:** may Technical Office see retention and net money?
- **S13:** what the «رصيد التشوينات» figure is computed from as the supervisor types.
- **S12:** «الأساس» and «النسبة» are accountant words; Q4 decides the base. «تقرير الجودة» as the title of a screen where money is typed.
- New status words «قيد المراجعة», «دورك», «قريب من الحد», «بانتظار غيرك»: Nabil to approve (with «بانتظار المالك», already new).

## Invented Arabic added in this pass
دورك · بانتظار غيرك · عند المشرف · بلا صورة فاتورة · المتبقي للإرجاع · بنود المصروف · إضافة بند مصروف · مصروف موقع · مدفوع · دفع · الدفع · قبول التسوية · تصديق الكميات · القيمة المصدّقة · قيد المراجعة · الخصم والإضافة · تطبيق الخصم والإضافة · إيداع · تسجيل إيداع من المالك · الناقص · المبلغ · الإيداع يسجّله المالك · قريب من الحد · رصيد التشوينات · العميل · محمود الشريف (sample name).
The earlier list above still names words now replaced (صرف, بنود الصرف, جاهز للصرف, اعتماد التسوية, اعتماد الكميات, الاستقطاعات, العجز, مبلغ الضخ, ضخ, تشوينات متبقية); read it with the table above.

## Sentences left on the screen, changed
S7 «الخزنة لا تكفي لهذا الصرف» is gone; the title and the «الناقص» figure carry it. S3 refused keeps one banner sentence, now with the excess figure. S7 refused is one line. The other sentences listed above are unchanged.

# Accounting (Release 4) mock: notes

File: `.design/v2/team-a/accounting.html`. Preview: `?embed=1` hides the strip; `?only=a6&view=a6:partial` shows one screen in one state. Source briefs: `plans/release-4-accounting/screens.md`. All 14 briefs are drawn, A14 only if Q8 is yes. This file is not part of `README.md`; the coordinator merges it.

All figures are sample values and are not reconciled across screens. Depreciation rates, the 10% tolerance and the month names are placeholders. No VAT line, no free-form journal screen, no stored balance (every figure is derived), no codes on screen.

## Screens and states drawn

| Id | Screen | States |
|---|---|---|
| A1 | Opening balances | balanced, difference not zero (save disabled, difference red with icon), empty (all zero), failed (entries kept), loading, forbidden |
| A2 | Asset register | Finance, Owner (read, no add, rows not clickable), empty, loading, failed. Fully depreciated row carries a pill and stays listed |
| A3 | Add or edit asset | new, edit, rate changed (shows only the next monthly charge), purchase date in closed month (field error), loading, failed, forbidden |
| A4 | Accruals and prepayments | list, empty, add sheet, add sheet with tag missing (error), loading, failed, forbidden |
| A5 | Company expense | normal, tag missing, safe too small (error plus owner injection button), saved (balance shown after), failed, loading, forbidden |
| A6 | Month close | all open, some done, ready, failed (names the step), waiting for owner, owner approves, closed (next month open), loading, forbidden |
| A7 | Revenue recognition | before posting, after posting (adds the carried contract asset or liability), Owner read-only, empty, loading, failed. A project with no progress reading shows dashes |
| A8 | Trial balance | balanced, out of balance (red banner, export still present), empty, loading, failed, forbidden |
| A9 | Balance sheet | balanced, out of balance, empty, loading, failed, forbidden. Five ledgers on separate lines |
| A10 | Year close | ready, months open (named, button disabled), waiting for owner, owner approves, closed (profit zero), loading, failed, forbidden |
| A11 | Overhead spread | normal (one line: report only, not in the books), empty, loading, failed, forbidden |
| A12 | Budgets overview | normal (within, near, over), empty, loading, failed, forbidden |
| A13 | Project budget tab | Owner (tolerance field and save), Finance (read only, no control), tolerance missing, loading, failed. Baseline has no edit control; a change order shows in its history |
| A14 | Loans | list, add instalment sheet, empty (offers add loan), loading, failed, forbidden |

## Question dependencies (defaults drawn)

- Q1: A7 posts the difference, as the change since last month (default). A6 step "revenue" depends on it.
- Q2, A2: A1 uses calendar year start (01/01/2026) and shows no counter-account. A1 save is blocked until difference is zero.
- Q3: A6 and A10 draw "Finance prepares, Owner approves": button, then "waiting for owner", then the owner view with approve and reject. If Q3 says Finance alone, drop the `waitowner` and `owner` states.
- Q4: A10 shows no drawings line (default: they stay on their own line and nothing moves).
- Q5: no VAT line anywhere.
- Q6: A12, A13 draw "committed" as a single figure and tolerance 10% (invented placeholder).
- Q7: A3 draws straight-line, monthly charge = cost x rate / 12, no disposal.
- Q8: A14 exists only if yes.
- Q9: A11 sizes by contract value.
- Q10: A8 shows a plain export button; format not drawn.
- Q11: A5 uses the six categories; correction by reversal is not drawn (see not decided).
- Q12: A7 lists Lump Sum projects only.
- A1: A6, A10, A9 assume profit is a derived sum. Nothing drawn depends on the other route.
- A3: A5 has the category as a plain field.
- A4: A5 shows the safe balance and an owner injection button, which assumes a minimal safe balance view exists.

## Invented Arabic

Labels and states: الأرصدة الافتتاحية، تاريخ بدء الاستخدام، إجمالي الأصول، إجمالي الالتزامات وحقوق الملكية، الفرق، حفظ الأرصدة، أرصدة العملاء، أرصدة الموردين، الأصول الثابتة، مهلك بالكامل، مجمع الإهلاك، القيمة الدفترية، الإهلاك الشهري، الإهلاك الشهري التالي، نسبة الإهلاك، الشهر مقفل، المستحقات والمقدمات، مستحق، مدفوع مقدمًا، المتبقي، متبقي N أشهر، مفتوح، مصروف جديد، دفع، الدفع من، رصيد الخزنة، المبلغ أكبر من رصيد الخزنة، ضخ من المالك، اختر الشركة أو مشروعًا، إقفال الشهر، البنود المفتوحة، تم، اعتماد الإقفال، إرجاع، مراجعة الإيراد بالمنجز، ترحيل التسوية، أصل عقد، التزام عقد، التغيير عن الشهر السابق، الرصيد المرحّل، ميزان المراجعة، تصدير، الميزانية، الدفاتر الخمسة، إقفال السنة، الأرباح المحتجزة قبل وبعد، الأشهر المقفلة، توزيع المصروفات العامة، الحصة، للعرض فقط، غير مسجل في الدفاتر، الموازنات، خط الأساس، الملتزم به، ضمن الموازنة، قريب من الحد، تجاوز، نسبة السماح، سجل خط الأساس، الحد، القروض، إضافة قسط، إضافة قرض، الأصل، الفائدة, القسط القادم، الأقساط المسددة.
Sentences: "لا تملك صلاحية فتح هذه الشاشة" (forbidden page, approximation of the standard one), "تعذّر الإقفال: ميزان المراجعة غير متوازن.", "أشهر مفتوحة: نوفمبر 2026، ديسمبر 2026", "بانتظار أحمد نبيل: اعتماد إقفال ...", "الفرق بين المدين والدائن N ج.م", "لا توجد ...". Ledger names reuse `ar.json` (الحجز، سلفة الشركة، العهدة، جاري المالك، دفعة العميل المقدمة).

## Left on screen on purpose

- A11: "للعرض فقط، غير مسجل في الدفاتر" (brief allows it: the figure sits next to real project cost).
- Error and refusal reasons (month closed, tag missing, safe too small, months open, out of balance). No explanatory paragraphs.

## Not decided

- Expense list (needed to raise a reversal for a wrong tag, A5 Next): no screen briefed.
- Owner injection screen or sheet, opened from A5: not drawn.
- Owner rejection with reason in A6 and A10: reject button drawn, the reason sheet not drawn.
- Add loan form (A14), add accrual months rule, how months left is computed (A4 is a proposal).
- Whether the five ledgers in A9 sit inside the three totals or beside them.
- Which route opens A9 and A10 from the shell navigation (sidebar here lists eight entries only; A10, A11, A7 are reached from the close page or are not linked).
- Forbidden page uses a plain locked-state box; the real standard forbidden page was not copied.

## Revision 2026-11-22 after the consultant review (`ux/consultant-review-accounting-mock-2026-11-22.md`)

Applied in `accounting.html`:
- A6 and A10 owner view: month result and trial balance totals above the steps (A6), the year figures (A10), one consequence line ("the month cannot be reopened after approval", from the journey), approve as the only filled button, «إرجاع» as a secondary action under the content. New states: owner reject sheet with a required reason, reason missing, returned to Finance (shows the reason). The waiting line names the role and the date, not a person.
- Two meanings of «رفض» split: the Owner's button is «إرجاع», a failed step pill is «تعذّر». Nabil ruled this in D-263 item 1: staff send-back is «إرجاع» everywhere; «رفض» is kept only for the client's final no on the portal.
- A6: numbered steps, the next step marked (bar, strong weight, arrow), the trial balance is dashed and not a link until the other steps are done, the counter is «N من 4» steps.
- A12: sorted over, near, within. Pill weight follows danger (filled and triangle for «تجاوز», tinted for «قريب من الحد», plain outline for «ضمن الحد»). New column: percent of the limit used. Whole row opens the project.
- A13: order is status and percent, baseline, tolerance, limit, committed, paid. The limit changes while the tolerance is typed (mock script). The repeated heading is gone.
- A5: «دفع» is absent when the amount is over the safe, one error on the field, one «ضخ من المالك» button, the balance under «الدفع من» changes when the bank is picked (new state `bank`).
- A7: no sign anywhere, direction by pill and by the words «زيادة / نقص»; «الرصيد المرحّل» column dropped; the no-reading project says «لا توجد قراءة تقدم»; after posting the primary action is «العودة إلى الإقفال».
- A8 phone: two headed groups (debit, credit) instead of repeating the side on every row.
- A10: «قبل الإقفال» and «بعد الإقفال» on the retained earnings pair.
- A11: the shown column is marked «للعرض»; the one sentence stays.
- A14: «إضافة قرض» is a secondary action on the list and has a sheet (lender, amount, date).
- A1: the date field is narrow and the totals bar sticks while scrolling.
- Sidebar: twelve entries, each opens its screen (A7, A10, A11, A14 added). A13 is reached from the project tab and from a row in A12. No static current marker. The loans entry exists only if Q8 is yes.

### Word choices, for the arabic-copy pass and Karim (one meaning per word)

| Idea | Used now | Alternatives to put to Karim |
|---|---|---|
| Fixed asset | الأصل (A2, A3) | الأصل الثابت |
| Loan principal | المتبقي من القرض، سداد القرض | الأصل المتبقي، أصل القسط |
| Contract asset / liability | مستحق من العميل، مقدّم من العميل | أصل عقد، التزام عقد |
| Budget (project) / balance sheet | موازنات المشاريع / الميزانية العمومية | الموازنة التقديرية / الميزانية |
| Committed | المعتمد للصرف | الملتزم به، المرتبط به |
| Step not done | لم يتم | بانتظار |
| Accrual not yet paid | لم يُسدَّد | قائم |
| Month or year not closed | لم تُقفل | مفتوح |
| Expense kind (A5 label) | نوع المصروف | البند |
| Count of accrual lines | بند، بندان، 3 إلى 10 بنود | |
| Return a close to Finance | إرجاع | إعادة |
| Failed step | تعذّر | |
| Budget status | ضمن الحد، قريب من الحد، تجاوز | ضمن الموازنة |

«بند» now means one accrual or prepayment line only.

### Open questions this revision does not decide (for Nabil)

1. Opening balances: does Finance type one «الأصول الثابتة» total in A1 and the asset register must sum to it, or are the asset register, prepayments, accruals and loans filled first and A1 shows their totals as read-only lines? A1 draws the current default (one typed total). Also: do the five ledgers get opening lines in A1, or start at zero? Not drawn.
2. Balance sheet: do the five ledgers sit inside the liabilities and equity groups, each on its own line, or in a separate block? A9 draws the current default (separate block under the totals, heading «الدفاتر الخمسة»).
3. Who presses «ضخ من المالك» in A5, and what happens after (screen not drawn).
4. Owner reject: does it return to the Finance user who requested, with the reason shown (drawn that way)?

Still not drawn: expense list for reversal, owner injection, forbidden page copy.

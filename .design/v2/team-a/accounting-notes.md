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

Labels and states: الأرصدة الافتتاحية، تاريخ بدء الاستخدام، إجمالي الأصول، إجمالي الالتزامات وحقوق الملكية، الفرق، حفظ الأرصدة، أرصدة العملاء، أرصدة الموردين، الأصول الثابتة، مهلك بالكامل، مجمع الإهلاك، القيمة الدفترية، الإهلاك الشهري، الإهلاك الشهري التالي، نسبة الإهلاك، الشهر مقفل، المستحقات والمقدمات، مستحق، مدفوع مقدمًا، المتبقي، متبقي N أشهر، مفتوح، مصروف جديد، دفع، الدفع من، رصيد الخزنة، المبلغ أكبر من رصيد الخزنة، ضخ من المالك، اختر الشركة أو مشروعًا، إقفال الشهر، البنود المفتوحة، تم، رفض، اعتماد الإقفال، رفض، مراجعة الإيراد بالمنجز، ترحيل التسوية، أصل عقد، التزام عقد، التغيير عن الشهر السابق، الرصيد المرحّل، ميزان المراجعة، تصدير، الميزانية، الدفاتر الخمسة، إقفال السنة، الأرباح المحتجزة قبل وبعد، الأشهر المقفلة، توزيع المصروفات العامة، الحصة، للعرض فقط، غير مسجل في الدفاتر، الموازنات، خط الأساس، الملتزم به، ضمن الموازنة، قريب من الحد، تجاوز، نسبة السماح، سجل خط الأساس، الحد، القروض، إضافة قسط، إضافة قرض، الأصل، الفائدة, القسط القادم، الأقساط المسددة.
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

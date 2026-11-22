# Release 4 questions, ordered by rebuild cost (highest first)

Each question has plain Arabic for Karim, the English meaning, the step or story it changes, and the default
that is running or proposed. Where the spec leans, the lean is cited. Part B is for Nabil and the Architect,
not Karim.

## Part A, for Karim

### Q1. هل نسجّل فرق الأعمال المنفذة والمستخلصات في الدفاتر، ولأي نوع عقد؟
**Arabic:** في آخر الشهر، لو الأعمال اللي اتنفذت أكبر من اللي طلعنا بيه مستخلص، هل نسجّل الفرق في الدفاتر
كأصل عقد؟ ولو المستخلصات أكبر من المنفذ، نسجّله التزام عقد؟ وهل ده لعقود المقاولة بمبلغ مقطوع بس، ولا يشمل
التكلفة زائد والتصميم كمان؟
**English:** At month end, post the executed-versus-billed difference as a contract asset or liability, or
only show it? For Lump Sum only, or also Cost Plus and Design?
**Changes:** journey steps 17-18; KAFF-706 AC1-AC5; screen 7. Rebuild cost: highest. It changes profit and
the balance sheet.
**Default:** post it for Lump Sum only, as the change since last month. The spec leans here: it says
"present the difference as a contract asset or liability" (spec §6.6 line 281), lists `WipAdjustment` as a
posting type (§6.2 line 233), and assumption 14 says "WIP line displayed" (§16 line 635). Cost Plus and Design
are not mentioned, so that part is not spec.

### Q2. تاريخ بداية السنة المالية، وأرصدة أول المدة
**Arabic:** السنة المالية عند كاف بتبدأ إمتى (يناير ولا يوليو)؟ وإيه رأس المال المدفوع، والأرباح المحتجزة،
وأرصدة الخزنة والبنوك والعملاء والموردين في يوم بدء استخدام النظام؟ من غير الأرقام دي الميزانية مش هتتوازن.
**English:** Fiscal year start, and the opening balances at go-live: paid-in capital, retained earnings, cash,
banks, receivables, payables, fixed assets.
**Changes:** steps 1-3; KAFF-705 AC1, AC5; screen 1. Rebuild cost: high. Opening postings cannot be edited,
only reversed.
**Default:** calendar year. Opening figures come from Karim's records (spec §16 #17 line 638; backlog Q25).

### Q3. مين يقفل الشهر، وهل المالك لازم يوافق؟
**Arabic:** مين اللي بيقفل الشهر؟ المحاسب لوحده، ولا لازم المالك يوافق بعده؟ وقفل السنة كمان؟
**English:** Who closes the month, and does the Owner approve? The year close too?
**Changes:** steps 20, 26; KAFF-703; screens 6, 10. Rebuild cost: high. It adds or removes a screen and a
permission.
**Default:** Finance prepares, the Owner approves. The spec leans: "Owner approves all financial movements"
and "nobody creates and approves the same movement" (spec §9 lines 371-376). Today the catalogue assumes
Finance alone (Q23, D-012).

### Q4. السحب النهائي للمالك في آخر السنة
**Arabic:** لما بنقفل السنة، المسحوبات النهائية للمالك بتتخصم من الأرباح المحتجزة؟ ولا بتفضل ظاهرة لوحدها؟
**English:** At year close, do final owner drawings reduce retained earnings, or stay on their own line?
**Changes:** step 25; KAFF-705 AC4; screen 10. Rebuild cost: medium-high.
**Default:** stay on their own line, nothing moves. The spec names only the profit roll (§6.6 line 278).

### Q5. هل كاف مسجلة في ضريبة القيمة المضافة؟
**Arabic:** هل كاف مسجلة في ضريبة القيمة المضافة؟ لو آه، نضيف سطر ضريبة المخرجات وحساب الضريبة المستحقة، وده
بيأثر في الميزانية.
**English:** Is Kaff VAT-registered?
**Changes:** journey (not yet a step); trial balance and balance sheet; extracts. Rebuild cost: medium-high.
**Default:** not registered, no VAT line. Spec §6.7 line 314 and §16 #15; the account type exists and is not
seeded.

### Q6. إيه معنى "الملتزم به" في تنبيه الموازنة، ونسبة السماح، ومين يتنبه؟
**Arabic:** تنبيه الموازنة بيحسب المبالغ الملتزم بيها، مش المدفوع بس. إيه اللي يتحسب ملتزم عندكم: فاتورة
مورد معتمدة؟ أمر شراء؟ مستخلص مقاول باطن معتمد؟ وقد إيه نسبة السماح فوق تكلفة المقايسة؟ ومين ياخد التنبيه؟
وأمر التغيير المعتمد بيرفع الموازنة بتكلفته ولا بسعره؟
**English:** What counts as committed money (approved supplier invoice, purchase order, approved subcontractor
extract)? What tolerance percentage? Who is alerted? Does an approved change order raise the baseline by its
cost or its price?
**Changes:** steps 4, 12, 13; KAFF-709 AC1-AC2, KAFF-710 AC1, AC3; screens 12, 13. Rebuild cost: medium. No
purchase order or supplier invoice exists in `src/Domain`, so the source of "committed" does not exist yet.
**Default:** committed = approved subcontractor extracts plus approved supplier invoices and orders once
Release 2 builds them; tolerance 10% (invented placeholder, say so); alert to the Owner. Spec §6.8 line 317
(tolerance is 🟡, spec §16 #9).

### Q7. تفاصيل الإهلاك
**Arabic:** الإهلاك قسط ثابت (النسبة على التكلفة كل سنة) ولا قسط متناقص؟ بيبدأ من شهر الشراء ولا اللي بعده؟
لو بعنا أصل، أو اتشطب، نعمل إيه؟
**English:** Straight-line or declining balance? Which month does depreciation start? What happens when an
asset is sold or scrapped?
**Changes:** step 15; KAFF-700 AC2-AC3, AC7; screen 3. Rebuild cost: medium.
**Default:** straight-line (cost times rate divided by 12), full month from the purchase month, no disposal in
this release. Spec says only that the defaults are straight-line or declining and that further schedules are
out of scope (spec line 36); disposal is not mentioned.

### Q8. هل عليكم قرض بنكي أو معدات بتتقسّط؟
**Arabic:** عليكم قرض بنكي، أو معدات بتدفعوا تمنها بالتقسيط؟
**English:** Any bank loan or financed equipment? (Backlog Q24.)
**Changes:** step 11; KAFF-711; screen 14. Rebuild cost: medium, or zero if no.
**Default:** none; story 711 and screen 14 are not built. Spec §6.6 line 279, §16 #16 line 637.

### Q9. التوزيع الشهري للمصاريف العامة على المشروعات
**Arabic:** المصاريف العامة بتتوزع على المشروعات بنسبة الحجم. الحجم يعني قيمة العقد، ولا الأعمال المنفذة في
الشهر؟ وهل المشروعات المتوقفة أو المنتهية تدخل؟
**English:** Basis of project size for the overhead spread: contract value or executed value that month?
Include stopped and closed projects?
**Changes:** step 22; KAFF-708; screen 11. Rebuild cost: low (a report).
**Default:** contract value of active projects. Spec §6.10 line 325, §16 #8 line 629.

### Q10. شكل ملف ميزان المراجعة ولمن يُرسل
**Arabic:** ميزان المراجعة يتصدّر بأي شكل: إكسل، ولا PDF؟ وهل بيروح لمحاسب خارجي؟
**English:** Export format of the trial balance, and who receives it.
**Changes:** KAFF-704 AC3; screen 8. Rebuild cost: low.
**Default:** CSV that opens in Excel. The spec says "export" only (§6.6 line 277).

### Q11. بنود المصروفات الست
**Arabic:** بنود المصروفات: إيجار ومرافق، أجور إدارية ومكتبية، أصول وسيارات، مواد عامة، مصاريف بنكية، أخرى.
كفاية؟ وغلط التصنيف (مشروع أو شركة) بيتصحح إزاي؟
**English:** Are the six categories enough, and is a wrong tag fixed by reversal plus a new entry?
**Changes:** step 5-6; KAFF-707 AC2-AC3; screen 5. Rebuild cost: low.
**Default:** the six, correction by reversal. Spec §6.10 line 323, §6.1 line 216.

### Q12. عقود التكلفة زائد والتصميم والإيراد بالمنجز
**Arabic:** نفس طريقة الإيراد بالمنجز تتطبّق على عقود التكلفة زائد والتصميم؟ عندهم إيراد المشروع هو الأتعاب
أو مراحل التصميم.
**English:** Does percentage-of-completion apply to Cost Plus (fee revenue) and Design (phase revenue)?
**Changes:** KAFF-706 AC5; screen 7. Rebuild cost: medium. Folded into Q1 if answered together.
**Default:** Lump Sum only. Not in the spec.

## Part B, for Nabil and the Architect

### A1. How does profit reach `CurrentYearProfit`?
Revenue and expense accounts are per project, and balances are never stored. Two routes: (a) each month close
writes `PeriodCloseTransfer` postings from every revenue and expense account into `CurrentYearProfit`, which
zeroes project accounts and breaks any report that reads a project's cost from its account balance; (b) profit
is a derived sum by date range, `CurrentYearProfit` holds only what year close moves. Recommended: (b), with
reports filtering by fiscal year. Changes KAFF-702, 705. Rebuild cost: high if chosen wrongly, because postings
are append-only. The spec names the posting types (§6.6 lines 276-278) but not the pair
(`PeriodCloseTransfer`, `YearEndProfitTransfer` are in `PendingQ91`).

### A2. Account pairs for the nine pending posting types
`OpeningBalance` has no counter-account, cheque types have no cheque-in-transit account, and
`AssetPurchase`, `LoanDrawdown`, `SiteExpensePayment`, `PeriodCloseTransfer`, `YearEndProfitTransfer` have no
ruled pair. The balance sheet cannot balance until each has one. Recommended: add an opening-balance equity
account, and a cheque-in-hand account, then rule the pairs. Changes KAFF-712 AC3 and KAFF-705 AC1. Source:
`src/Domain/Treasury/PostingAccountLegality.cs`, `PendingQ91`.

### A3. Where does the expense category live?
The posting shape adds nothing to the spec's list (§6.1 line 212). Recommended: a field on the expense source
document. Changes KAFF-707.

### A4. Who builds the safe and bank screens
No treasury screen exists. Screens 5 and 8 assume Release 2 or this release builds a minimal safe balance
view. Changes screen 5.

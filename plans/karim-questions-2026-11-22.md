# أسئلة لكريم، عن طريق نبيل — 22 نوفمبر 2026

Ordered by rebuild cost across the whole project, highest first. Each: Arabic, one English line, the default we build on if no answer. Source ids in brackets: R2 to R6 are the release folders in `plans/`, so [R2-Q3] is Release 2 question 3. Same question in two releases is one entry here with both ids. Nothing here is decided. Defaults are recommendations; where the spec does not lean, it says "none".

---

## الأغلى لو اتبنت غلط

**1. فرق المنفذ والمستخلص في الدفاتر** [R4-Q1, R4-Q12]
آخر الشهر، لو المنفذ أكتر من المستخلص نسجّل الفرق أصل عقد؟ ولو أقل نسجّله التزام؟ لعقود المقطوعية بس ولا التكلفة زائد والتصميم كمان؟
EN: Post executed-versus-billed as contract asset or liability, and for which contract types?
Default: post it, Lump Sum only (spec leans).

**2. بداية السنة المالية وأرصدة أول المدة** [R4-Q2]
السنة المالية بتبدأ إمتى؟ وإيه رأس المال، والأرباح المحتجزة، والخزنة والبنوك والعملاء والموردين يوم بدء النظام؟ من غيرها الميزانية مش هتتوازن.
EN: Fiscal year start and opening balances at go-live.
Default: calendar year; figures from Karim's records.

**3. فلوس المعاينة تتسجل فين؟** [R3-Q1, R3-Q2]
العميل بيدفع قبل ما يبقى فيه مشروع. مش إيراد ومش في أي دفتر من الخمسة. تتسجل فين؟ لو ما وقّعش نرجّعها؟ ولما يوقّع تتخصم من إيه؟ وهل المعاينة ممكن تتم من غير ما يدفع؟
EN: Which account holds the visit fee, refund on loss, and where the credit lands at signing; may the visit happen unpaid?
Default: none (visit may go ahead unpaid, shown as "not paid").

**4. هل المالك يعتمد كل صرف فلوس؟** [R2-Q3]
المالك بيعتمد كل مستخلص عميل. هل بيعتمد كمان طلب العهدة، تأكيد مصروف الموقع، مستخلص مقاول الباطن، سداد المالك؟ في حد أدنى؟ ومين يتبلّغ بعد الاعتماد؟
EN: Does the Owner approve every money act, with any threshold, and who is told?
Default: yes, all, no threshold (spec §9).

**5. العهدة والمصروف: صرف واحد ولا اتنين؟** [R2-Q1, R6-Q6]
المهندس بيصرف من العهدة وعنده فواتير، وكمان بيسجّل «مصروف» والمحاسب يأكده. هل ده نفس الصرف مرتين؟ التكلفة تدخل المشروع من تسوية العهدة ولا من كل فاتورة؟ لو دفع من جيبه يرجعله إزاي؟ وصورة الفاتورة تتربط بإيه؟
EN: Are a عهدة settlement and a confirmed expense draft the same spend? How is a pocket-paid engineer repaid? What does an invoice photo attach to?
Default: none.

**6. مستخلص مقاول الباطن** [R2-Q2, R2-Q10]
بيتحاسب على إيه (جدول كميات وأسعار للمقاول على المشروع)؟ مين يشهد بالكميات؟ فيه دفعة مقدمة؟ خصومات غير الـ٥٪؟ وهل تقرير الجودة الأسبوعي ليه كمان؟
EN: What a subcontractor extract certifies, who certifies, any advance or extra deductions, and whether the QC report applies to it.
Default: certifier is the supervising engineer; QC report on client extracts only.

**7. عرض السعر: على الفرصة ولا مقايسة على مشروع لسه ما اتوقعش؟** [R3-Q7, R3-Q8]
المواصفات: التقدير على الفرصة ومرجع بس. اتفاق ٩ نوفمبر: عرض السعر هو المقايسة نفسها على مشروع غير موقّع. أنهي نكمل بيه؟ والتعديلات أثناء التفاوض نسخ مرقمة ولا الأحدث يحل محل القديم؟
EN: Spec model (estimate on the opportunity) or D-208 model (BOQ on an unsigned project)? Versions or replace?
Default: none for the model (spec stands); dated versions.

**8. مين يقفل الشهر والسنة؟** [R4-Q3]
المحاسب لوحده، ولا المالك لازم يوافق بعده؟
EN: Who closes month and year, and does the Owner approve?
Default: Finance prepares, Owner approves (spec leans).

**9. ضريبة الخصم والإضافة على المقاول والمورّد** [R2-Q4]
النسبة على إيه (قبل الخصومات ولا الصافي)؟ المورّد مش مربوط بمشروع، فالنسبة تتحدد على إيه؟ مورّد فرد مش بيتخصم منه؟ ونسجّل توريدها للمصلحة ولا بس نعرف المستحق؟
EN: Withholding base, category for a supplier with no project, individual payees, and remittance.
Default: none for the base; record and show the balance owed, no remittance.

**10. السلفة على العميل (سلفة الشركة)** [R2-Q5]
إيه اللي بنصرفه بالنيابة عنه؟ الحد الأقصى لكل مشروع ولا عميل ولا الشركة؟ مين يحدد الرقم؟ العميل يسدّدها بخصم من مستخلص ولا يدفعها لوحدها؟
EN: What counts as a firm advance, the cap's scope and owner, and how it is repaid.
Default: none.

**11. العهدة: الحد وقواعدها، وأوفلاين** [R2-Q6, R6-Q5]
(١) المحصّل والمصروف وهامش أمان المالك في الحد، كام؟ (٢) ١٠٬٠٠٠ أكبر طلب؟ (٣) «واحدة مفتوحة» لكل مهندس ولا مشروع ولا موقع؟ (٤) مين بيستلمها، المشرف ولا الصغير؟ (٥) الصغير يكتب المسودة أوفلاين والباقي أونلاين والفحص عند الوصول؟
EN: Ceiling terms, the 10,000 cap, "one open" scope, holder, and offline draft rule.
Default: cap 10,000, formula as written, one open per holder per project, draft offline and checked on arrival.

**12. حساب المالك الجاري** [R2-Q7]
مين يسجّل الضخ، والمالك يعتمد؟ يدخل الخزنة ولا بنك؟ سداد الشركة للمالك مين يعتمده؟ الدفع المرفوض لعدم كفاية الخزنة يفضل معلّق ولا يتكرر؟
EN: Who records an injection, which cash account, who approves repaying the Owner, and whether a refused payment stays pending.
Default: the user repeats the payment after the injection.

## متوسط

**13. تليفونات المهندسين** [R6-Q1]
أندرويد ولا آيفون؟ الآيفون بيمسح اللي اتحفظ لو ما فتحوش 7 أيام، فشغل ممكن يضيع.
EN: Android or iPhone? Safari clears saved data after 7 days idle.
Default: browser app; mix to be confirmed.

**14. شغل بالليل بعد نص الليل، وأوفلاين أكتر من ٣ أيام** [R6-Q2, R6-Q3]
الكمية تتحسب على يوم ما اتعملت ولا يوم ما وصلت؟ ولو التليفون بعيد أكتر من ٣ أيام: نرفض ونحذّر ولا المشرف يقبلها؟
EN: Which day an offline entry counts for, and what happens past 3 days.
Default: the day it was made, if in the window; refuse and warn on day 2.

**15. فوترة مرحلة التصميم** [R5-Q2]
مفيش مستخلص في التصميم. موافقة العميل تطلّع إيه؟ الـ٣٠٪ مقدم وقت التوقيع ولا موافقة الـ Concept؟ و«معتمد» يعني موافقة العميل ولا الداخلية؟
EN: What bills a design stage, when the 30% deposit is due, and whose approval "Approved" means.
Default: client approval; deposit at signing; Accounts collect normally.

**16. تعديل تصميم بعد الجولات المتفق عليها** [R5-Q5]
المواصفات متعارضة: «أمر تغيير مصغّر بيتحاسب» ولا «بيتسجل بس من غير سعر». أنهي صح؟
EN: Billable mini change order or logged only, once rounds are used?
Default: logged only, priced outside the system.

**17. العميل بيوافق بنفسه على أمر التغيير** [R5-Q3]
لما البوابة تشتغل، العميل هو اللي يوافق ويتشال زر المهندس؟ ورفضه بسبب ينهي الأمر؟
EN: Does the client's own tap replace the engineer recording approval; does his rejection end the order?
Default: replace; rejection needs a reason and ends it.

**18. "الملتزم به" في تنبيه الموازنة** [R4-Q6]
إيه اللي يتحسب ملتزم (فاتورة مورد، أمر شراء، مستخلص باطن معتمد)؟ نسبة السماح؟ مين يتنبه؟ وأمر التغيير يرفع الموازنة بتكلفته ولا سعره؟
EN: What counts as committed, the tolerance, who is alerted, cost or price for change orders.
Default: approved sub extracts and supplier invoices; 10% (placeholder); Owner.

**19. العلامة المائية: امتى "اتدفع"؟** [R5-Q6]
لما الفلوس تتسجل ولا لما الشيك يتصرف؟ ودفع جزئي؟ والملف النهائي قبل آخر ١٠٪؟
EN: Which state counts as paid; partial payment; final deliverable timing.
Default: fully collected and cheque cleared; partial stays marked; final released clean.

**20. أسماء المراحل والحالات** [R3-Q3]
جاهز: محتمل ← اجتماع ← معاينة ← عرض سعر ← تفاوض ← عقد؟ و«متوقف، معلّق، مفقود، أعيد فتحه» حالات؟ «عقد» هو «تم الفوز»؟ تخطي ورجوع؟
EN: Confirm the six stages, statuses, whether Contract equals ClosedWon, and legal moves.
Default: as built; forward one stage at a time.

**21. مصاريف قبل العقد** [R3-Q4]
المواصفات: يا مشروع يا شركة. تتسجل مصروف شركة بعلامة الفرصة؟ ولو نجحت تدخل تكلفة المشروع؟
EN: Company expense carrying an opportunity reference, and does it join project cost on a win?
Default: company expense with reference; never joins project cost.

**22. سحب المالك النهائي آخر السنة** [R4-Q4]
يتخصم من الأرباح المحتجزة ولا يفضل لوحده؟
EN: Do final drawings reduce retained earnings at year close?
Default: stay on their own line.

**23. هل كاف مسجلة في ضريبة القيمة المضافة؟** [R4-Q5]
EN: Is Kaff VAT-registered? Default: no.

**24. الإهلاك** [R4-Q7]
قسط ثابت ولا متناقص؟ من شهر الشراء ولا بعده؟ لو بعنا أو شطبنا أصل؟
EN: Method, start month, disposal. Default: straight-line, full month from purchase month, no disposal.

**25. قرض بنكي أو معدات بالتقسيط؟** [R4-Q8]
EN: Any loan or financed equipment? Default: no; loans story is dropped.

**26. العميل يشوف إيه في المستخلص وأوامر التغيير؟** [R5-Q8, R5-Q14, R5-Q9]
المستخلصات المصدّرة والمدفوعة بس؟ «المتبقي» = قيمة العقد ناقص المحصّل؟ أوامر التغيير من مرحلة موافقة العميل؟ والجدول الزمني مين يدخله؟
EN: Extract detail, how "remaining" is worked out, which change orders show, source of the time plan.
Default: Issued and Paid only; remaining as stated; change orders from client approval on; start and end dates per project and باب.

**27. مراجعة التصميم داخلياً، التداخل، الجولات** [R5-Q4]
مين يراجع قبل العميل؟ مرحلة تبدأ قبل ما اللي قبلها تتدفع؟ كام جولة مجانية؟
EN: Internal reviewer, overlapping stages, free rounds.
Default: Owner; may overlap; rounds typed at signing.

**28. مين يشوف التكلفة والهامش؟** [R3-Q6]
مسؤول المبيعات ولا المالك بس؟
EN: Does Marketing see cost and margin? Default: none (if no, margin block is Owner-only).

**29. مين يسجّل "تم الفوز" ومين يفتح المشروع؟** [R3-Q11]
EN: Marketing requests, Technical Office or Owner converts? Default: yes.

**30. قفل التصميم والفرش** [R5-Q10, R5-Q11]
مين يقفل التصميم والملكية الفكرية تتسجل إزاي؟ الفرش مين يفتحه ومن إمتى، وقفل الأصل يستناه؟
EN: Who closes design and records IP transfer; furnishing creator, timing, and whether parent closure waits.
Default: Owner both; furnishing by Owner or Technical Office, parent not held.

**31. الـ٥٪ محتجز مقاول الباطن** [R2-Q11]
على كل المقاولين؟ ممكن صفر؟ قبل ولا بعد الضريبة؟
EN: Is 5% universal, can it be zero, which base? Default: 5% of certified value, released at warranty end.

## تفاصيل صغيرة (جاوب بكلمة)

32. **ساعة الحضور** [R6-Q4]: نصدّق ساعة التليفون؟ للمرتب؟ EN: Trust device clock; pay effect? Default: keep both times, flag gaps, no pay.
33. **مين ينشر الصور للعميل** [R5-Q12, R6-Q9]: المهندس المشرف والمكتب الفني، أونلاين، وممكن نسحبها؟ EN: Who publishes and may unpublish. Default: supervising engineer and Technical Office, online, unpublish allowed.
34. **تنبيه العميل وتنبيه المبيعات** [R5-Q13, R3-Q5]: مفيش رسائل. تكفي قائمة على الشاشة ومكالمة؟ النشاط = أي حركة؟ EN: No messaging; on-screen list. Default: list, activity is any move.
35. **أول كلمة سر للعميل** [R5-Q7]: المالك يسلّمها. EN: Default: Owner hands over, forced change.
36. **تشوين المواد** [R2-Q8]: المهندس في تقرير الجودة. EN: Who marks stockpiled. Default: supervising engineer at the QC report.
37. **مبلغ مصروف الموقع** [R2-Q9]: المهندس يكتبه؟ EN: Default: engineer types it.
38. **تقرير المعاينة وأسباب الخسارة** [R3-Q9, R3-Q10]: ملاحظات وصور حرة؛ قائمة أسباب. EN: Default: free notes and photos; short list plus note.
39. **توزيع المصاريف العامة** [R4-Q9]: بقيمة العقد للنشطة. EN: Default: contract value, active only.
40. **تصدير ميزان المراجعة وبنود المصروفات الست** [R4-Q10, R4-Q11]: CSV؛ الستة كفاية، التصحيح بعكس قيد. EN: Default: CSV; six categories, fix by reversal.
41. **عامل مكرر أوفلاين، تليفون ضايع، تعديل أوفلاين** [R6-Q7, R6-Q8, R6-Q10]: EN: Default: register offline and resolve on arrival; no remote wipe, refuse on arrival; correct pending entries only.

---

# For Nabil and the Architect (not Karim)

N1. **Account pairs for pending posting types (PendingQ91)** [R2-Q12, R4-A2]. `SiteExpensePayment`, `OpeningBalance`, `AssetPurchase`, `LoanDrawdown`, `PeriodCloseTransfer`, `YearEndProfitTransfer` and the cheque types have no ruled pair. One ruling unblocks R2 KAFF-608 and R4 KAFF-712. Recommended: add an opening-balance equity account and a cheque-in-hand account, rule `SiteExpensePayment` as safe to `ProjectCost`.
N2. **How profit reaches `CurrentYearProfit`** [R4-A1]. (a) monthly transfers that zero project accounts, or (b) derived sum by date range. Recommended: (b). Wrong choice is costly because postings are append-only.
N3. **Portal deployment** [R5-Q1]. Separate deployment or same API on a second hostname. Recommended: same process, second hostname, own cookie.
N4. **Expense category location** [R4-A3]. Recommended: a field on the expense source document.
N5. **Who builds the safe and bank screens** [R4-A4]. R2 or R4 must supply a minimal safe-balance view.
N6. **Browser app on iPhone** [R6-Q1]. A native app breaks the pinned stack and needs a `decisions.md` entry.

Counts: 57 Karim-directed questions across the five files, 41 entries after merging; 5 for Nabil and the Architect from the plans (6 raw ids, R2-Q12 and R4-A2 merged), plus N6, a decision note raised by R6-Q1.

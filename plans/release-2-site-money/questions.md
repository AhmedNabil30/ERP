# Release 2, site money: questions for Karim

Date: 2026-11-22. Ordered by rebuild cost, highest first. Q1 to Q12 are numbers local to this plan, used in
`journey.md`, `screens.md` and `stories.md`. Numbers like Q22, Q26, Q58, Q91, Q114 are the existing register
(`stories/questions-for-karim.md`) and keep their meaning. Nothing here is decided. A recommended default is
given only where `spec.md` leans one way, and it is a default to build on, not a ruling.

---

## Q1. عهدة and expense draft: one spend or two? (highest)

**Arabic:** المهندس صرف من العهدة وعنده فواتير. وفي نفس الوقت المهندس يسجّل «مصروف» في اليومية والمحاسب يؤكده.
هل ده نفس الصرف مسجّل مرتين؟ يعني: لما أقفل العهدة بالفواتير، التكلفة تدخل على المشروع من التسوية؟ ولا كل فاتورة
تتسجّل كمصروف منفصل والمحاسب يؤكدها؟ ولو المهندس دفع من جيبه، يرجّعله المحاسب إزاي؟

**English:** An engineer spends from a عهدة and holds receipts. Separately, he drafts a "site expense" that Accounts
confirms. Are these the same spend recorded twice? Does the cost reach the project through the عهدة settlement, or
through a confirmed expense draft per receipt? How is an engineer repaid if he paid from his own pocket?

**Steps it changes:** Flow 1 step 10, Flow 5 steps 5 and 7; stories KAFF-610, KAFF-608; screens S3, S4.
**Why first:** merges or splits two flows. A wrong guess means one posting path is built twice and one deleted.
**Spec:** [spec §6.4 line 250] and [spec §8 line 355] describe both; neither names the other.
**Default:** none. The spec does not lean.

## Q2. Subcontractor extract: what is certified, and by whom

**Arabic:** مستخلص مقاول الباطن الأسبوعي: بيتحاسب على إيه؟ على بند اتفقتوا عليه مع المقاول في المشروع (جدول
كميات وأسعار للمقاول)؟ مين اللي بيشهد بالكميات: مهندس الموقع المشرف؟ هل في دفعة مقدمة للمقاول؟ هل في استقطاعات
غير الـ٥٪ (مثلاً خصم عيوب)؟ والدفع للمقاول جزء من نفس الدورة ولا خطوة لاحقة؟

**English:** What does a weekly subcontractor extract certify against: a per-project sub-BOQ of agreed quantities and
rates (D-139 §4 says rates live there; no such record exists)? Who certifies: the supervising engineer? Is there any
advance to a subcontractor? Any deduction beyond 5% (for instance debit notes)? Is payment part of the cycle?

**Steps it changes:** Flow 6 steps 0, 2, 4, 5, 9; stories R2-16, KAFF-510a, b, c; screens S10, S11.
**Why second:** a new record (the subcontract) and a new certifier role may be needed.
**Spec:** [spec §7 line 346] "certify → Technical Office gate → Accounts → Owner → post" names no certifier.
[spec §5.1 line 171] "one weekly extract each", 5% retained.
**Default:** none for the certifier. Spec leans: the supervising engineer confirms field quantities
[spec §7 line 341], so a recommended default is the supervising engineer.

## Q3. Does the Owner approve every money act: عهدة, expense confirmation, subcontractor extract, owner repayment?

**Arabic:** المالك بيعتمد كل مستخلص بدون حد أدنى. هل بيعتمد كمان: طلب العهدة؟ تأكيد مصروف الموقع؟ مستخلص مقاول
الباطن؟ ولو الإجابة «كله»، فيه مبلغ صغير يعدّي من غير اعتماده (مثلاً عهدة تحت ١٠٠٠ جنيه)؟ ومين يتبلّغ لما المالك يعتمد؟

**English:** The Owner approves every client extract with no threshold. Does he also approve a عهدة request, a
site-expense confirmation, a subcontractor extract, an owner repayment? If all, may a small amount pass without
him? Who is told once he approves?

**Steps it changes:** Flow 1 step 8, Flow 5 step 6, Flow 6 step 6, Flow 4 step 8; stories KAFF-311, 608, 510c, R2-09;
screens S4, S5.
**Spec:** [spec §9 line 371] "Owner approves all financial movements" against [spec §6.4 line 250]
"accounts pays", and [spec §7 line 336] "no threshold" written for client extracts.
**Default (spec leans):** yes, the Owner approves every one, no threshold, because §9 states no exception. This adds
a step to every عهدة. Karim may well want a threshold.
**Rebuild cost:** high. Approval is a state, a permission row, and a screen per act.

## Q4. Withholding on subcontractor and supplier payments

**Arabic:** لما نخصم ضريبة الخصم والإضافة من مقاول باطن أو مورّد: النسبة على إيه (قيمة المستخلص قبل الخصومات؟
الصافي؟)؟ والمورّد مش مربوط بمشروع، فالنسبة تتحدد على إيه؟ هل فيه مورّد فرد مش بيتخصم منه؟ وبعد ما نخصم، هل
البرنامج يسجّل توريدها للمصلحة ولا بس نعرف الرصيد المستحق؟

**English:** For a subcontractor or supplier payment: what does the percentage apply to? A supplier has no project, so
what carries the category? Does an individual payee escape withholding? Does the app record remittance to the tax
authority, or only the balance owed?

**Steps it changes:** Flow 7 steps 4, 5, 6; story KAFF-318; screens S11, S12.
**Spec:** [spec §6.7 lines 284-288, 308-310]; [D-139 §5] says "per contract/job"; `WithholdingBaseRule` is written for
client extracts only. Remittance sits close to the out-of-scope tax module [CLAUDE.md].
**Default:** none for the base. For remittance, spec leans "carry the liability" only [spec §6.7 line 287], so a
default is: record and show the balance, build no remittance.

## Q5. Firm advance: what counts, how big the cap, how repaid

**Arabic:** السلفة على العميل: إيه اللي بنصرفه بالنيابة عنه (يعني نوع المصروفات)؟ الحد الأقصى: لكل مشروع؟ لكل عميل؟
على الشركة كلها؟ ومين بيحدد الرقم؟ والعميل بيسدّدها إزاي: تتخصم من مستخلص ولا يدفعها لوحدها؟

**English:** A firm advance is Kaff spending for a client. What spending counts? Is the hard cap per project, per
client or company-wide, and who sets the figure? Is it repaid through an extract deduction or a separate payment?

**Steps it changes:** Flow 2 steps 1, 2, 6; story KAFF-310; screens S6, S9.
**Spec:** [spec §6.4 line 249]. The worked example [spec §15] has no firm advance column, so no extract formula exists.
**Default:** none.

## Q6. عهدة: ceiling, 10,000 cap, "one open at a time", who is the holder

**Arabic:** (١) الحد الأقصى للعهدة المفتوحة على الموقع = المحصّل − المصروف − هامش أمان للمالك. المحصّل من مين؟
والهامش كام؟ (٢) هل ١٠,٠٠٠ هي أكبر طلب؟ (٣) «مفيش عهدة جديدة قبل ما القديمة تتقفل» ـ لكل مهندس؟ لكل مشروع؟ لكل موقع؟
(٤) مين اللي بيستلم العهدة: المهندس المشرف أم الصغير اللي طلبها؟ (علشان النظام محتاج سجل موظف للشخص ده.)

**English:** (1) Define "collected", "spent" and the owner safety margin in `collected − spent − margin`. (2) Is 10,000
the most in one request? (3) "No new عهدة before the previous is cleared": per engineer, per project, or per site?
(4) Who holds the عهدة, the supervisor or the junior who asked? (The account is per employee; no link from user to
employee record was found.)

**Steps it changes:** Flow 1 steps 2, 3, 4 and the account note; stories KAFF-609a, 311, 610; screens S1, S3.
**Spec:** [spec §6.4 line 250] 🟡; [spec §16 row 4 line 625]; existing Q22 asks only (1) and (2).
**Default (spec leans):** cap 10,000 and the formula as written; "one open" per holder per project. These are
spec's own words, but marked 🟡.

## Q7. Owner current account: who records, which cash account, repaying the Owner, the refused payment

**Arabic:** (١) ضخ المالك: المحاسب يسجّله والمالك يعتمد؟ أو المالك يسجّله بنفسه؟ (٢) المبلغ يدخل الخزنة ولا
حساب بنك؟ (٣) سداد الشركة للمالك: مين يعتمده، والمالك هو المستفيد؟ (٤) لما الدفع يترفض عشان الخزنة ما تكفيش:
نسيبه «معلّق» لحد ما المالك يضخ، ولا المحاسب يكرره بعد الضخ؟

**English:** (1) Injection: recorded by Finance and approved by the Owner, or by the Owner? (2) Into the safe or a bank?
(3) Repaying the Owner: who approves when the Owner is the payee? (4) A refused payment: keep it pending until the
Owner injects, or the user repeats it?

**Steps it changes:** Flow 4 steps 4, 5, 6, 8; stories KAFF-312, 313; screens S7, S8.
**Spec:** [spec §6.1 line 218] "prompts an owner injection"; [spec §9 line 376] separation of duties; no line on a
pending payment.
**Default (spec leans):** the user repeats the payment after the injection, because §6.1 says only "prompts" and
no spec line stores a refused payment.

## Q8. Marking material as stockpiled from site (existing Q-225-1)

**Arabic:** مين يحدد إن المواد تشوين على الموقع (اللي بيبدأ بها تشوينات ٧٥٪)؟ المهندس في اليومية؟ ولا المحاسب لما
يجهّز المستخلص؟

**English:** Who marks material as stockpiled, which starts the 75% تشوينات: the engineer on the daily log, or Accounts
when the extract is prepared? Already open as `Q-225-1` (`stories/questions-for-karim.md` line 541). Today the
supervising engineer enters the stockpiled value at the QC report [D-235 item 4].
**Steps it changes:** Flow 3 step 4; story KAFF-309r.
**Default:** keep D-235 item 4 (the supervising engineer enters it at the QC report).

## Q9. Does the engineer type an amount on an expense draft? (`SiteExpenseDraftAmount`, ASSUMED)

**Arabic:** المهندس لما يسجّل مصروف، يكتب المبلغ؟ ولا يوصف المصروف بس والمحاسب يكتب المبلغ؟ (القاعدة بتقول إن
شاشة المهندس ما تعرضش مبالغ.)

**English:** When the engineer drafts an expense, does he type an amount, or only describe it and Accounts enters the
figure? `spec.md` §8 and `AC-600-J` collide (`SiteExpenseDraftAmount.cs`).
**Steps it changes:** Flow 5 step 2; story KAFF-608 AC7; screen S1 note, S4.
**Default:** engineer types the amount, as built.

## Q10. Weekly QC report: only on client extracts, or also on subcontractor extracts?

**Arabic:** تقرير الجودة الأسبوعي: هل هو خطوة في مستخلص العميل بس؟ ولا مستخلص مقاول الباطن كمان لازم له تقرير؟

**English:** Is the weekly QC report a step of the client extract only? Does a subcontractor extract also need one?
**Spec:** [spec §7 lines 332-334, 346]: the client chain lists QC, the subcontractor chain does not.
**Default (spec leans):** client extracts only.

## Q11. Subcontractor retention: zeroable, and the base (existing Q26)

**Arabic:** الـ٥٪ على كل المقاولين؟ في مقاول ممكن نخلّيه صفر؟ تتحسب على قيمة المستخلص قبل ولا بعد خصم الضريبة؟
**English:** Is 5% for every subcontractor, can any be zero, and is it taken on the extract value before or after
withholding? Existing `Q26`; the release timing is `ASSUMED` (`SubcontractorRetentionTiming`).
**Default:** 5% of the certified value, release at warranty end, as built [spec §5.1 line 171].

## Q12. Which accounts a confirmed site expense moves between (existing Q91 item)

**Arabic:** مصروف الموقع المؤكد يتسجّل على تكلفة المشروع ومن أنهي حساب (الخزنة؟ بنك؟ عهدة؟).
**English:** A confirmed `SiteExpensePayment` is in `PostingAccountLegality.PendingQ91`: the pair is not ruled.
`ProjectCost` is plausible but unconfirmed. This is for the Architect or the accountant, not Karim.
**Default:** safe to `ProjectCost`, which is the plausible target in the code's own note.

---

## Top five, for the cover note

1. Q1: عهدة and expense draft, one spend or two.
2. Q2: subcontractor extract, what is certified and by whom.
3. Q3: Owner approval on every money act.
4. Q4: withholding on subcontractor and supplier payments.
5. Q5: firm advance, what counts, the cap and repayment.

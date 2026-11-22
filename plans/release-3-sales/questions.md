# Release 3, sales spine: open questions for Karim

Ordered by what it costs to rebuild if we guess wrong. Arabic first for Karim, English below. "Default" is the recommended answer only where the spec leans; otherwise none is offered. Journey step numbers in brackets.

## Q1. فلوس المعاينة تتسجل فين؟ (steps 9, 10, 28, 34) — highest cost, touches treasury

**عربي:** العميل بيدفع رسوم المعاينة قبل ما يبقى فيه مشروع. الفلوس دي مش إيراد، ومش في أي دفتر من الخمسة (دفعة العميل، المحجوز، سلفة الشركة، العهدة، جاري المالك). تتسجل فين؟ ولو العميل ما وقّعش، نرجّعها له ولا تبقى إيراد للشركة؟ ولما العقد يتوقع، هل تتخصم من الدفعة المقدمة ولا من أول مستخلص ولا من قيمة العقد؟

**English:** The visit fee arrives before any project exists, so no project account can hold it, and spec §6.4 forbids netting it into the five ledgers. Which account holds it? On a lost deal is it refunded or kept as revenue? At signing, does the credit reduce the advance, the first extract, or the contract value? Spec §3 line 99 says only "deposit" and "credit against the new contract".
**Default:** none. Spec leans to a liability held on the opportunity; the credit step has no worked example in §15.

## Q2. هل ممكن المعاينة تتم من غير ما العميل يدفع؟ (step 11)

**English:** Spec §3 says the visit is billable but does not say it may not happen unpaid. May Marketing move to SiteVisit without a recorded fee, and may the fee be waived?
**Default:** the stage may be reached unpaid; the opportunity page shows "not paid". Changes the stage guard only; cheap.

## Q3. مراحل المبيعات والحالات (steps 4, 29) — spec §16 assumption 6, D-017

**عربي:** المراحل: عميل محتمل ← اجتماع ← معاينة ← عرض سعر ← تفاوض ← عقد. هل دي الأسماء الصح؟ وهل "متوقف" و"معلّق" و"مفقود" و"أعيد فتحه" حالات مش مراحل؟ وهل "عقد" مرحلة ولا هو "تم الفوز" (وده هو اللي بيحوّل لمشروع)؟

**English:** Confirm the six stage names; confirm Stalled, OnHold, ClosedLost, Reopened are statuses, not stages; and whether "Contract" (§3) and "ClosedWon" (§13) are one thing. Which stage moves are legal (skip, go back)?
**Default:** the current code: six stages, five statuses, Contract and ClosedWon merged, forward one at a time, going back allowed to any earlier stage (this last part is a PROPOSAL).

## Q4. مصاريف قبل العقد (steps 15, 16, 35)

**عربي:** المواصلات والمعاينة والطباعة قبل العقد: هتتسجل على مين؟ المواصفات بتقول كل مصروف يا مشروع يا شركة، ومفيش حاجة تالتة. هل المصروف ده يبقى "مصروف شركة" بعلامة الفرصة؟ ولو الفرصة نجحت، هل التكلفة دي تدخل في تكلفة المشروع ولا تفضل برّه؟

**English:** Spec §6.10 line 323 allows only project or company tags; §3 says track against the opportunity and do not recover from the client. Reconcile: company expense carrying an opportunity reference? On a win, does it join project cost?
**Default:** company expense with an opportunity reference; never enters project cost (spec §3 line 100 "not recovered"; §6.10 line 325 keeps project cost clean).

## Q5. التنبيهات وإيه اللي يعتبر "نشاط" (steps 5, 7)

**عربي:** تنبيه اليوم ٢ واليوم ٤: يوصل لمين؟ مندوب المبيعات ولا المالك ولا الاتنين؟ وبأي طريقة؟ وإيه اللي يوقف العدّاد: مكالمة مسجلة؟ تغيير مرحلة؟ أي تعديل؟

**English:** Recipient and channel are unstated, and no notification mechanism exists in the code. Which events count as activity?
**Default:** recipient is the opportunity's owner; no push, a "needs follow-up" list shown on screen; activity is any stage move, logged note, deposit, expense or quotation change.

## Q6. مين يشوف التكلفة والهامش؟ (step 21)

**عربي:** الأسعار والتكلفة والهامش في عرض السعر: هل مسؤول المبيعات يشوفها ولا المالك بس؟

**English:** Spec §4.2 requires the margin view; D-177 §3 lets internal staff read catalogue cost, but Marketing is not named. Does Marketing see cost and margin, or only the Owner?
**Default:** none. If Marketing may not see cost, Marketing prices with sell rate only and the margin block is Owner-only.

## Q7. عرض السعر مكانه فين؟ (steps 23, 33) — second highest rebuild cost

**عربي:** المواصفات بتقول التقدير على الفرصة وبيبقى "مرجع" للمكتب الفني. لكن اتفاق ٩ نوفمبر قال عرض السعر هو جدول الكميات نفسه على مشروع لسه ما اتوقعش. أيهما الصح بعد ما نبني المبيعات؟ ولو الأول، العميل بيتسجل مشروع قبل العقد ولا بعده؟ وفترة المتابعة (٣٠ يوم) تعمل مع عدّاد الأيام ٢/٤/٧ إزاي؟

**English:** Spec §3 line 102: estimate lives on the Opportunity, reference only. D-208: the quotation is the BOQ on an unsigned project, archived after 30 days. Two models, two clocks. Which wins once Sales exists? If the spec model, KAFF-417's unsigned-project quotation path is retired or kept for jobs that skip Sales.
**Default:** none. The spec stands unless Karim rules otherwise (CLAUDE.md); D-208 was a client ruling made while the module was missing, so it needs an explicit answer.

## Q8. تعديل عرض السعر أثناء التفاوض (step 25)

**English:** Are changes kept as numbered versions or does the latest quotation replace the old one? Spec silent.
**Default:** keep dated versions; the printout is the latest.

## Q9. تقرير المعاينة فيه إيه؟ (step 12)

**English:** Spec §3 says the site-visit report travels to the project, but not what it holds. Free notes and photos, or a form (area, access, conditions)?
**Default:** free notes and photos.

## Q10. أسباب الخسارة (step 27)

**English:** Free text (as built) or a short list for reporting (price, competitor, client delayed, other)?
**Default:** list plus optional note; spec §3 requires only "a reason".

## Q11. مين يضغط "تم الفوز"؟ (step 30)

**عربي:** المسؤول عن إنشاء المشروع هو المكتب الفني أو المالك فقط، لكن المبيعات هي اللي عارفة إن العميل وافق. مين يسجّل الفوز، ومين يفتح المشروع؟ ولو الصلاحية لسه معلّقة (قرار D-052: شركة كلها ولا مشروع)، تتحل قبل ما نبني التحويل؟

**English:** Marketing knows the client agreed but cannot open a project (D-052). Does Marketing mark "agreed" and the Technical Office or Owner converts? D-052 still leaves open whether create becomes company-wide.
**Default:** Marketing requests; Technical Office or Owner converts and picks the contract type.

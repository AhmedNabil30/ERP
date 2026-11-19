# Arabic copy review, 2026-11-18

**Scope.** The 12 keys added to `src/Web/public/locales/ar.json` between `9ef3e2f` and `HEAD` (`33ff701`),
plus the three keys the brief named that the diff did not change: `errors.master.individual_does_not_withhold`,
`errors.adjustment.exceeds_extract_net` and `users.confirm.change_role.reassign`. Meaning was taken from `en.json`,
`spec.md` §6.7 and §14, decisions D-235 to D-238, and the domain code that returns each key.

The proposals are the reviewer's suggestions, not Kaff's rulings. `ar.json` was not edited.

**Kaff's status vocabulary.** None of these keys uses or replaces لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها. No defect of that type.

## 1. One word, two meanings

| Key | Current | Proposed | Why |
|---|---|---|---|
| `errors.collection.exceeds_expected` | هذا المبلغ أكبر من المتبقي على المستخلص بعد ما تم تحصيله والشيكات تحت التحصيل. | Split into two keys. (a) Over-payment, `Extract.cs:577`: «المستلم مع المخصوم من المنبع أكبر من المتبقي على هذا المستخلص بعد ما تم تحصيله والشيكات التي لم تُصرف بعد. أدخل مبلغاً أقل.» (b) Nothing left, `Extract.cs:555`, a new key such as `errors.collection.nothing_remaining`: «لا يتبقى شيء على هذا المستخلص: ما تم تحصيله والشيكات التي لم تُصرف بعد يغطيان المستحق كله.» | The same key is returned in two cases. In `Extract.cs:555` no amount has been typed yet, because nothing remains, so «هذا المبلغ أكبر» tells the user about a figure they never entered. In `Extract.cs:577` the comparison is against received plus withheld together, but «هذا المبلغ» reads as the received amount only. «تحت التحصيل» usually means a cheque that has been deposited at the bank, but the count also includes cheques received and not yet deposited ("cheques in hand", D-238 §3). Splitting the key needs a code change, not only a copy change. |
| `errors.adjustment.exceeds_extract_net` | إشعارات الدائن ستتجاوز صافي المستحق على المستخلص. | قيمة إشعار الدائن أكبر من المتبقي على المستخلص بعد ما تم تحصيله وإشعارات الدائن السابقة. أدخل قيمة أقل. | The check in `Adjustment.cs:211` is amount + earlier credit notes + amounts already paid > net payable. The text names only credit notes against net payable, so after a part payment the refusal shows up when the credit notes alone are well under the net. It also does not say what to do next. |

## 2. One meaning, two words

| Key | Current | Proposed | Why |
|---|---|---|---|
| `errors.master.individual_does_not_withhold` | العميل الفرد لا يخصم ضريبة تحت الحساب. | العميل الفرد لا يخصم من المنبع، فلا تُحدَّد فئة خصم لعقده. | This is the only key that uses «ضريبة تحت الحساب». Every other key uses «الخصم من المنبع» / «خصم من المنبع» (`billing.component.TaxAtSource`, `errors.billing.tax_at_source_account_required`, the new collection keys). Since D-238 §5 it is also returned when Finance sets a category on an individual client's contract (`Project.cs:347`), so the text should tell Finance why the category cannot be set. |
| `errors.treasury.flow_source_not_allowed` | هذا النوع من المستندات يُسجَّل من شاشته فقط، لا كحركة خزينة عامة. | هذه الحركة تخص مستخلصاً أو تسوية أو تسليماً، وتُسجَّل من شاشة ذلك المستند. | «خزينة» appears only here. Everywhere else, and in the §14 glossary, it is «خزنة» (15 uses). «حركة خزينة عامة» is the build team's name for the generic movement route, not a screen the user knows. The proposal names the documents (Extract, Adjustment, ProjectHandover, SubcontractorRetentionRelease, D-238 §1) and the place to go instead. |
| `errors.treasury.hold_release_in_full` | المحجوز يُصرف كاملاً، لا جزء منه. | يُفرَج عن الحجز كاملاً مرة واحدة. أدخل كامل رصيد الحجز. | The posting type is already named «إفراج الحجز» (`proof.type.HoldRelease`), and the ledger is «الحجز» (`proof.ledger.Hold.title`). «يُصرف» is the verb used for the subcontractor's retention (`errors.closure.warranty_not_ended`), which is a different thing. The proposal also says what to enter. |
| `errors.treasury.hold_posting_not_reversible` | لا يجوز عكس قيد على المحجوز. | لا يجوز عكس قيد على الحجز. | Uses the ledger's displayed name «الحجز». See question Q-A on «المحجوز» against «الحجز». |
| `errors.extract.material_values_required` | أدخل قيمة الخامات الموردة للموقع وقيمة الخامات المركبة مع تقرير الجودة. | أدخل قيمة التشوينات بالموقع وقيمة الخامات المركبة قبل إرسال تقرير الجودة. | Material delivered to site and not yet installed is Kaff's «تشوينات» (spec §14, `billing.component.MaterialAdvance`). «الخامات الموردة للموقع» is a second name for the same thing. «مع تقرير الجودة» can read as "values that come with the QC report". The engineer needs to know that he must enter both values before he submits it (D-238 §4). |

## 3. Developer talk on a customer screen

| Key | Current | Proposed | Why |
|---|---|---|---|
| `errors.treasury.posting_date_out_of_range` | تاريخ القيد لا يكون قبل عام 2000 ولا بعد اليوم. | تاريخ القيد غير صحيح. أدخل تاريخاً من 1/1/2000 حتى اليوم. | The year 2000 is a typo guard the lane chose, not a business rule. Phrased as an instruction, it tells the accountant what to type. The text also says «بعد اليوم», but the server accepts one day past the site day (D-236 §2, which the lane chose and Nabil has not confirmed). The proposal keeps «اليوم», which is stricter than the rule and therefore safe, but the text and the rule will disagree until Nabil rules. |
| `errors.treasury.flow_posting_not_reversible` | لا يُعكس هذا القيد إلا من المستند الذي أنشأه. | هذا القيد يخص مستخلصاً أو تسوية، ولا يُعكس إلا من شاشة ذلك المستند. | «المستند الذي أنشأه» describes how the system is built. The accountant needs to know which screen to open. A handover or retention-release posting is never reversed (D-237 §3), so the proposal names only the two documents that can reverse their postings. |

## 4. Says what is wrong, not what to do next

| Key | Current | Proposed | Why |
|---|---|---|---|
| `errors.collection.withholding_not_applicable` | هذا العقد ليس عليه خصم من المنبع. | هذا العقد ليس عليه خصم من المنبع. اترك المبلغ المخصوم صفراً، أو اطلب من المالية تحديد فئة الخصم للعقد. | The refusal comes when a withheld amount is typed on a contract with no withholding category. The user has two ways out: leave the withheld amount at zero, or get Finance to set the category (D-238 §5). |
| `errors.collection.amount_invalid` | المبلغ المستلم والمبلغ المخصوم من المنبع لا يكونان بالسالب، ومجموعهما أكبر من صفر. | أدخل المبلغ المستلم والمبلغ المخصوم من المنبع بأرقام موجبة، ولا يكون الاثنان صفراً معاً. | The current text states a rule with a double negative. The proposal tells the user what to type. |
| `errors.project.withholding_category_required` | اختر فئة الخصم من المنبع للعقد. | اختر نوع الخصم من المنبع لهذا العقد: مقاولات وتوريدات 1%، خدمات 3%، أتعاب مهنية 5%. | Reviewer's suggestion. Finance staff know these categories by the activity and the rate (spec §6.7), not by an abstract «فئة». This is acceptable as it stands if the picker next to it already shows the rates. |

## 5. No change proposed

| Key | Current | Note |
|---|---|---|
| `errors.collection.amount_required` | أدخل المبلغ المستلم. | Clear, and says what to do. |
| `errors.treasury.posting_date_required` | تاريخ القيد مطلوب. | Matches the sibling «… مطلوب» keys. See Q-B on «قيد» against «حركة». |
| `users.confirm.change_role.reassign` | تستطيع الموارد البشرية إسناده إلى المشاريع من جديد بصفته الجديدة. | It matches `en.json` and the «الصفة» used for a role everywhere in `users.*`. It is rendered in `user-form-page.html:312` under the revoked-projects list. It reads correctly. The only issue is a masculine pronoun for a person who may be a woman, which is the convention used across the file. |

## Noticed outside scope

| Key | Current | Proposed | Why |
|---|---|---|---|
| `errors.extract.dispute_revision_unruled` | حل النزاع بمستخلص معدّل غير متاح بعد: لم يُقرَّر مصير المستخلص الأصلي. لم يتغير شيء. | حل النزاع بمستخلص معدّل غير متاح حالياً. استخدم إشعار دائن، أو اترك المستخلص متنازعاً عليه. لم يتغير شيء. | This key was not in the diff. «لم يُقرَّر» is the build team telling the user that it has not decided something. It is the same pattern this review exists to catch. |

## Wording invented by a lane, never confirmed by Kaff

These keys carry rules or words that came from a lane, not from Nabil or Karim:

- `errors.treasury.posting_date_out_of_range`: the 2000 floor and the one-day tolerance (D-236 §2, "the lane's choice, for Nabil to confirm").
- `errors.adjustment.exceeds_extract_net`: the whole guard is the lane's own (D-238 "Open, for Nabil").
- `errors.collection.exceeds_expected`: the wording «تحت التحصيل» for cheques in hand.
- `errors.treasury.flow_source_not_allowed` and `errors.treasury.flow_posting_not_reversible`: «حركة خزينة عامة» and «المستند الذي أنشأه» are the lane's terms.
- `errors.project.withholding_category_required` and `errors.master.individual_does_not_withhold`: «فئة الخصم» and «ضريبة تحت الحساب». The category itself is open (Q99, Q114, Q30).
- `errors.extract.material_values_required`: «الخامات الموردة للموقع» in place of «تشوينات».

## Term questions for Nabil

- **Q-A. «الحجز» or «المحجوز» for the hold?** Spec §14 allows both. `ar.json` has 27 uses of «المحجوز» and 12 of «الحجز», and the ledger, the rate and the release are all titled «الحجز». The reviewer suggests «الحجز» for the ledger and the rate, and «المحجوز» only for an amount held.
- **Q-B. «قيد» or «حركة» for a posting?** The treasury errors mix them: «نوع الحركة مطلوب» beside «تاريخ القيد مطلوب» and «طرفاً للقيد». Accountants say «قيد». The safe screen may say «حركة الخزنة». One word should be chosen per meaning.
- **Q-C. «الخصم من المنبع», or Egyptian practice's «الخصم تحت حساب الضريبة» (نموذج 41)?** The file uses the first everywhere but one key. The reviewer suggests keeping «الخصم من المنبع» unless Kaff's accountant says otherwise.

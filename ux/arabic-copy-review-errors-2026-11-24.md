# Arabic copy review: 33 new error texts, 2026-11-24

Scope: keys `errors.sales.*` (7), `errors.petty_cash.*` (4), `errors.accounting.*` (22) added in `git diff 70fc958 a32b42b`. Checked against existing `ar.json` usage and D-240 (حركة, not قيد), D-263 (إرجاع / اعتماد). Corrections below are the reviewer's suggestions, not Kaff's ruling. `ar.json` was not edited.

Result: 33 reviewed, 13 corrections, 20 pass. No developer talk found. No Kaff status word touched.

## One word, two meanings

| Key | Current | Proposed | Reason |
|---|---|---|---|
| errors.accounting.prepayment_not_found | هذه الدفعة المقدمة غير موجودة. | هذا المصروف المدفوع مقدمًا غير موجود. | «الدفعة المقدمة» already means the client's contract advance (`AdvanceRecovery`, `proof.check.advanceEndsAtZero`, `errors.project.signing_terms_required`). A prepaid expense is a different thing; one word, two meanings. |
| errors.accounting.prepayment_funding_mismatch | الحركة المذكورة لا تطابق هذه الدفعة المقدمة: يجب أن تكون حركة قائمة إلى حساب المدفوعات المقدمة بنفس المبلغ. | الحركة المذكورة لا تخص هذا المصروف المدفوع مقدمًا: يجب أن تكون حركة مدينة على حساب المصروفات المدفوعة مقدمًا وبنفس المبلغ. | Same collision. «حركة قائمة إلى» is unclear Arabic; «مدينة على» is the usual accounting phrase (assumed meaning: debit; confirm with the lane). Also the text calls the item «الدفعة» and the account «المدفوعات»: two words for one thing. |
| errors.accounting.prepayment_month_outside_schedule | هذا الشهر خارج جدول الدفعة المقدمة. | هذا الشهر خارج جدول توزيع المصروف المدفوع مقدمًا. | Same collision. |
| errors.accounting.accrual_already_released | تم إقفال هذا الاستحقاق من قبل. | تمت تسوية هذا الاستحقاق من قبل. | «إقفال» is used for closing a project (`errors.closure.not_settled`). Accrual release is settlement of the accrued expense. Needs Kaff or the accountant to confirm the word. |

## Label that misleads or does not match existing wording

| Key | Current | Proposed | Reason |
|---|---|---|---|
| errors.petty_cash.not_draft | طلب العهدة ليس مسودة، فلا يمكن تقديمه أو إعادته. | طلب العهدة ليس مسودة، فلا يمكن تقديمه أو إرجاعه. | D-263: staff send-back is «إرجاع». «إعادته» reads as "do it again". |
| errors.accounting.request_not_waiting | تم البت في هذا الطلب من قبل. | هذا الطلب لم يعد بانتظار الاعتماد. | «تم البت» is legal register. Existing wording is «بانتظار اعتماد المالك» (`errors.closure.*`). Says what the user can do next: nothing to approve. |
| errors.accounting.request_not_found | هذا الطلب غير موجود. | طلب الاعتماد غير موجود. | Bare «الطلب» could be petty cash, hold release or an extract. Name it. |
| errors.accounting.baseline_needs_signed_boq | خط أساس الموازنة يحتاج مشروعًا موقّعًا ومقايسة مجمّدة. | حدّد خط أساس الموازنة بعد توقيع التعاقد وتجميد المقايسة. | Matches `errors.daily_log.boq_not_frozen` («توقيع التعاقد وتجميد المقايسة»). Says the action, not a project attribute. |
| errors.accounting.asset_rate_locked | لا يمكن تغيير النسبة بعد تسجيل الإهلاك. | لا يمكن تغيير نسبة الإهلاك بعد تسجيل الإهلاك. | «النسبة» alone is ambiguous beside `tolerance` and hold percentages. |
| errors.accounting.account_wrong_type | هذا الحساب ليس من النوع المناسب لهذه الحركة. | نوع هذا الحساب لا يناسب هذه الحركة. | Shorter; «من النوع المناسب» is vague and clumsy. Low priority. |
| errors.sales.opportunity_not_live | الفرصة معلّقة أو مغلقة ولا يمكن نقلها. | الفرصة معلّقة أو مغلقة، فلا يمكن نقلها إلى مرحلة أخرى. | Says what is being moved. Low priority. |

## Spelling consistency

Existing file writes tanween as «ـاً» (e.g. `تاريخاً`, `سبباً`). Two new keys use «ـًا».

| Key | Current | Proposed | Reason |
|---|---|---|---|
| errors.accounting.description_too_long | الوصف طويل جدًا. | الوصف أطول من الحد المسموح. | Existing phrase: `errors.extract.reason_too_long`. |
| errors.accounting.month_invalid | اختر شهرًا صحيحًا. | اختر شهراً صحيحاً. | Tanween style. |

(Also «مقدمًا» and «مشروعًا» in rows above follow the proposed «ـاً» form if the file style is kept: مقدماً.)

## Checked and passed (20)

errors.accounting: month_in_future, report_range_invalid, accrual_not_found, prepayment_months_invalid, asset_not_found, asset_name_required, asset_rate_invalid, asset_month_before_start, baseline_not_set, baseline_already_set, tolerance_invalid. errors.petty_cash: request_not_found, amount_invalid, purpose_required. errors.sales: opportunity_not_found, already_at_last_stage, opportunity_not_on_hold, opportunity_already_closed, opportunity_not_closed_lost, opportunity_list_filter_unknown (matches the 7 existing «فلتر غير معروف»).

Notes on passes: «حركة» used correctly, no «قيد». «العهدة» matches `proof.ledger.PettyCashAdvance.title`. «الإهلاك», «خط أساس الموازنة», «الاستحقاق» are new in this file, with no earlier use to clash with.

## Wording invented by a lane, not confirmed by Kaff

No ruling found for these terms in D-240 or D-263. Confirm with Nabil or Karim:

- الإهلاك (depreciation) and أصل (asset) in asset_* keys
- خط أساس الموازنة (budget baseline) in baseline_* keys
- نسبة السماح (tolerance) in tolerance_invalid
- الاستحقاق (accrual) and the release word in accrual_already_released
- المصروف المدفوع مقدمًا (my proposal for prepayment, also unconfirmed)
- معلّقة (on hold) for a sales opportunity, beside «الجزء المعلّق» and «الحجز» elsewhere

## Brief check

Brief said "new today". The diff is 33 keys; count matched (7+4+22). Nothing wrong.

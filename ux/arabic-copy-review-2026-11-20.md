# Arabic copy review: extract chain screens, 2026-11-20

**Scope.** Keys added or changed in `src/Web/public/locales/ar.json` by b8e8ccf and 5a6c89a
(`git diff 655329f..5a6c89a`): 231 added, 11 changed (plus one comma-only change), 1 removed. The strings the build added beyond
the mock, the 7 `waiting.task.*` words and the 4 new error keys come first, then the rest of the diff.

**Rulings applied.** D-240 («الحجز», «حركة», «تشوينات», «الخصم من المنبع» kept until ruled). The
words in `.design/v2/team-a/README.md` "Extract chain" → "New words" are approved (D-242 item 1). I
flag them only where they really clash with something.

**Kaff's five status words.** None of them appear in this diff, and none were changed. No rows.

Every "proposed" below is my suggestion. Kaff decides the words. `ar.json` was not edited.

## 1. A label that says something other than what the screen does

| Key | Current | Where it shows | Problem | Proposed |
|---|---|---|---|---|
| `waiting.line.nobody` | «بانتظار العميل: {task}» | The waiting line on the extract, cheque, credit note and change order (`extract-display.ts` `waitingText`) | The code picks this line whenever the holder list is **empty**, for any step. When nobody holds the role on the project, a cheque deposit reads «بانتظار العميل: إيداع الشيك» and a credit note reads «بانتظار العميل: اعتماد الإشعار الدائن». The client is not involved in either step. Since D-241 item 4 a person records the client's approval of a change order, so the client may never be the right holder for this line now. | For an empty holder list: a new key, «لا يوجد من يتولى هذه الخطوة: {task}». Keep «بانتظار العميل» only for a step that really waits on the client. This needs a code change in `waitingText`, so it goes to the lane. |
| `change_order.action.client_rejection` | «رفض العميل» | The button on the change-order detail where the supervisor records the client's answer | This is a mock word, but on a button it reads as "reject the client". The button beside it is «تسجيل موافقة العميل», so the pair is not parallel. | «تسجيل رفض العميل» |
| `extract.new.to_before_from` | «نهاية الفترة بعد بدايتها.» | The "to" date error on the new-extract form | The code refuses only `to < from`, so a period that starts and ends on the same day is accepted. The text states a stricter rule than the one enforced. It also reads as a statement, not as something to fix. | «نهاية الفترة لا تسبق بدايتها.» |
| `extract.rejected_by` | «رُفض في {gate}» | The rejection banner on the extract detail, filled with the gate name | When the Owner rejects, {gate} is «المالك», so the banner reads «رُفض في المالك» ("rejected in the Owner"), which is wrong Arabic. The approved mock wording says what the contractor needs to know, the error and where the extract went back to: «رُفض: الكميات، إلى مهندس الموقع» / «رُفض: التشوينات، إلى المشرف». | Now: «رُفض من {gate}». Once the read sends the rejection kind (D-243 item 10): the mock's form, «رُفض: {kind}، إلى {role}». |

## 2. One meaning, two words

| Key | Current | Where it shows | Problem | Proposed |
|---|---|---|---|---|
| `waiting.task.adjustment_approval` | «اعتماد إشعار الدائن» | The credit-note waiting line, «بانتظار …: {task}» | «إشعار الدائن» means "the creditor's notice", which is wrong. Every new screen and the mock say «الإشعار الدائن»; `credit_note.approve` says «اعتماد الإشعار الدائن». The same wrong form is in `errors.adjustment.extract_collected`, `target_account_not_allowed` and `source_not_creditable`, which are outside this diff. | «اعتماد الإشعار الدائن» |
| `errors.adjustment.source_not_creditable` (outside the diff, but it collides with a new key) | «… مستخلص صادر أو متنازع عليه …» | The refusal when a credit note is raised on the wrong extract | The new `extract.status.Disputed` is «معترض عليه», and the whole dispute flow uses «اعتراض». «متنازع عليه» is a second word for the same state. | «… مستخلص صادر أو معترض عليه …» (and «الإشعار الدائن», as in the row above) |
| `waiting.task.change_order_owner_approval` | «اعتماد المالك» | The change-order waiting line, «بانتظار أحمد نبيل: اعتماد المالك» | The Owner's name already sits in the line, so the line says "owner" twice. The extract's own Owner task (`extract.task.owner`) is «الاعتماد». Minor. | «الاعتماد» |
| `errors.extract.credit_note_open` (outside the diff) | «يوجد إشعار دائن مفتوح …» | The server refusal of a collection | The new `collection.refused.credit_note`, which is in the mock, says «لم يُحسم» for the same condition. Minor. | «يوجد إشعار دائن لم يُحسم على هذا المستخلص، فلا يُحصَّل الآن.» |

## 3. One word, two meanings

| Key | Current | Where it shows | Problem | Proposed |
|---|---|---|---|---|
| `cheque.released_hold` | «الحجز» | The cheque list, in the column that names a cheque's source: «مستخلص 3» for an extract, «الحجز» for the released hold | On a cheque row, «الحجز» alone reads as the cheque being held or seized, not as the released retention it pays. The error keys for this flow already say «الحجز المُفرج عنه». | «الحجز المُفرج عنه» |

## 4. Wording that does not tell the user what to do

| Key | Current | Where it shows | Problem | Proposed |
|---|---|---|---|---|
| `errors.closure.hold_not_released` | «لم يُفرج عن الحجز بعد، فلا يوجد ما يُحصَّل منه.» | The refusal when the hold is collected too early | «ما يُحصَّل منه» ("what is collected from it") is awkward, and the message does not say when collection becomes possible. | «لم يُفرج عن الحجز بعد. يُحصَّل بعد الإفراج عنه عند التسليم.» |
| `waiting.barred` | «لا يمكنه الاعتماد: {who}» | The dim second line under the waiting line | This is a mock word. But {who} can be several names joined with «أو», and «يمكنه» is singular, so a line with two names reads wrong. It also leaves out why the person is barred. | «لا يعتمده من شارك فيه: {who}» (works for one name or several, and matches `extract.barred`) |
| `collection.refused.status`, `dispute.refused` | «مستخلص {number} {status}» | The refusal state on the collect page and the dispute page | The English has a colon. The Arabic has none, and it says only the state, not why the action is refused. «مستخلص 4 مسودة» states a fact but does not tell Finance that payments are recorded only on an issued extract. Low. | Collect: «مستخلص {number} {status}. تُسجَّل الدفعة على المستخلص الصادر فقط.» Dispute: the same pattern, once the lane confirms which states accept a dispute. |

## 5. Construction practice (my suggestion, not Kaff's ruling)

| Key | Current | Where it shows | Note | Proposed |
|---|---|---|---|---|
| `extract.stopped` | «المشروع متوقف، فلا يصدر المستخلص.» | The extract detail banner | «متوقف» is how people on site say it, and spec §13 keeps `Stopped` separate from «متعثرة»/«تم تأجيلها» (D-044 §7), so this does not clash with Kaff's words. But the word for the `Stopped` state is not ruled: `closure.status.stopped` is still «[PLACEHOLDER Stopped - awaiting Nabil] متوقف». This banner fixes the word before the ruling. | No change. This is a term question for Nabil. |
| `collection.method.Cash` | «نقدًا» | The payment-method picker | This is a mock word. It is an adverb beside two nouns («شيك», «تحويل»). On site and in accounts, «نقدي» or «كاش» is what people say. Low. | «نقدي» |

## Rest of the diff: reviewed, no change proposed

`extract.list.empty_title`, `extract.number`, the 7 rewritten `extract.action.*`, `extract.document.pending`,
`extract.history.title`, the removal of `errors.extract.revision_hold_accrued` (D-244 item 1; no code
still references it), the remaining `waiting.task.*` (`change_order_submit`, `change_order_technical_review`,
`change_order_client_approval`, `cheque_deposit`, `cheque_settlement`), `errors.closure.released_hold_nothing_remaining`,
`errors.closure.released_hold_exceeded` (same shape as `errors.collection.exceeds_expected`),
`errors.collection.bounce_reason_required`, `extract.history.rejected` / `reversed`, `credit_note.status.Reversed`
(«معكوس», in line with «عكس الدفعات»), `credit_note.approved_on` / `rejected_on`, `change_order.rejected_on`
(both nouns are masculine, so «اعتُمد/رُفض» agree), `collection.withheld_invalid` (an empty field already
counts as zero, so the error only appears for text that is not a number), `collection.no_payments`,
`change_order.title_required`, `change_order.line.*_required`, `closure.hold_collection.title` / `done`,
`extract.failed`, and the other added keys from the mock.

## Outside the diff: leftovers found while checking

- **D-240 «الحجز» was missed in three keys.** `errors.closure.not_applicable_to_design` («ولا محجوز»),
  `errors.closure.nothing_to_release` («لا يوجد رصيد محجوز»), `closure.design_none` («ولا محجوز»). Proposed:
  «ولا حجز», «لا يوجد حجز للإفراج عنه». D-243 item 4's catalogue test does not catch the bare form «محجوز».
  (`errors.closure.warranty_not_ended`'s «محجوزات المقاول» is the subcontractor's retention, a different
  ledger, so it is correctly left as it is.)
- `errors.extract.material_values_required` says «قيمة التشوينات بالموقع وقيمة الخامات المركبة» and «إرسال تقرير الجودة».
  The screens say «قيمة التشوينات المركّبة» and «تسليم تقرير الجودة». The mock README already asked for this key to change.
- `errors.adjustment.termination_settlement_unruled`: «لم يُقرَّر ما الذي يُسوّى» is the build team speaking
  to a customer. Proposed: «تسوية فسخ التعاقد غير متاحة حالياً. لم يتغير شيء.» (the same as `errors.closure.termination_settlement_unruled`).
- `closure.status.*`: five values still carry «[PLACEHOLDER … - awaiting Nabil]» in the text that shows on screen.

## Wording the lane invented and Kaff never confirmed

These keys are in the diff but appear neither in the approved mock nor in a ruling:

- `waiting.task.adjustment_approval`, `waiting.task.change_order_submit`, `waiting.task.change_order_technical_review`,
  `waiting.task.change_order_client_approval`, `waiting.task.change_order_owner_approval`, `waiting.task.cheque_deposit`,
  `waiting.task.cheque_settlement`. Most reuse mock words; `cheque_settlement` («تسجيل صرف الشيك أو ارتداده») is new.
- `extract.rejected_by`, `extract.history.reversed`, `credit_note.status.Reversed`, `credit_note.approved_on`,
  `credit_note.rejected_on`, `change_order.rejected_on`
- `collection.withheld_invalid`, `collection.no_payments`, `collection.refused.status`, `dispute.refused`
- `extract.new.to_before_from`, `change_order.title_required`, `change_order.line.description_required`,
  `change_order.line.unit_required`, `change_order.line.price_required`
- `closure.hold_collection.title`, `closure.hold_collection.done`, `cheque.released_hold`
- `errors.closure.hold_not_released`, `errors.closure.released_hold_nothing_remaining`,
  `errors.closure.released_hold_exceeded`, `errors.collection.bounce_reason_required`

These are in the brief's "invented" list but **are in the approved mock**: `waiting.barred` («لا يمكنه الاعتماد: سارة عادل»),
`extract.history.rejected` («المالك: رفض»), `extract.failed` («تعذّر تحميل المستخلص»), `change_order.line.quantity_required`
(«الكمية أكبر من صفر.»).

## Term questions for Nabil

1. **The word for a stopped project** (`Stopped`): is it «متوقف»? Three keys already use it, and `closure.status.stopped` is still a placeholder.
2. **The released hold in the cheque list and on the closure page:** «الحجز المُفرج عنه», or is «الحجز» alone enough?
3. **The waiting line when no one holds the role:** what should it say? Today it says «بانتظار العميل».
4. **The client's rejection of a change order:** «تسجيل رفض العميل» on the button?
5. **The cash method:** «نقدًا», «نقدي» or «كاش»?

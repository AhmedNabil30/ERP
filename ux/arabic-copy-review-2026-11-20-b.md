# Arabic copy review (b): NCR, partial approval, quantities, withholding row, closure, 2026-11-20

**Scope.** `git diff 7a8eb99..HEAD -- src/Web/public/locales/ar.json`, read against the templates in
`features/extracts/extract-detail`, `extract-new`, `extract-list`, `shared/kaff-quantity-table`,
`features/projects/project-workspace` (withholding row and sheet) and the closure keys. Every "proposed"
is my suggestion. Kaff decides the words. `ar.json` and `en.json` were not edited.

**Kaff's five status words.** None changed. Only `closure.stage.Active` carries one («جاري العمل»), unchanged. No defect.

## A. The NCR ruling: one term, one meaning

**Finding.** Three words are in use for the NCR, and two of them already mean something else.

- Screens: «الملاحظة». But «الملاحظة» is the snag (`closure.snag.*`, `closure.done.snag_*`, «قائمة الملاحظات») and the daily-log note (`daily_log.form.note`). A site engineer reading «إغلاق الملاحظة» on an extract cannot tell it from closing a snag. One word, three meanings.
- Errors: «بند عدم مطابقة». «بند» is the BOQ line («البند» in `boq.column.item`, `extract.qty.item`). Two meanings again.
- `en.json` also says "note" for the screen keys (`extract.ncr.close` = "Close the note"), so the English intent file carries the same drift.

**Proposal (suggestion, needs Nabil's word).** One term everywhere: **«تقرير عدم المطابقة»** (the literal NCR, a site and QA phrase). No «بند», no «ملاحظة». The close-note field becomes «ما تم تصحيحه», which is what the existing error already asks for. If Nabil wants it shorter, «عدم المطابقة» alone also works, but then use it in all keys below, never mixed.

| Key | Current | Proposed | Why | Severity |
|---|---|---|---|---|
| `extract.ncr.close` | إغلاق الملاحظة | إغلاق تقرير عدم المطابقة | Button and sheet title. Collides with snag. | High |
| `extract.ncr.closed` | أُغلقت الملاحظة. | أُغلق تقرير عدم المطابقة. | Success banner. Same. | High |
| `extract.ncr.note` | ملاحظة الإغلاق | ما تم تصحيحه | Field label. «ملاحظة» is a third use of the word. Matches `errors.extract.ncr_close_note_required`. | Medium |
| `extract.ncr.note_required` | اكتب ملاحظة الإغلاق. | اكتب ما تم تصحيحه. | Follows the label. | Medium |
| `extract.portion.held` | معلّق حتى إغلاق الملاحظة | معلّق حتى إغلاق تقرير عدم المطابقة | Row label in the work-value list. Wraps on mobile but is clear. | High |
| `errors.extract.held_portion_needs_ncr` | الجزء المعلّق يحتاج إلى بند عدم مطابقة مفتوح يُعلَّق عليه. | لا يُعلَّق جزء من المستخلص إلا على تقرير عدم مطابقة مفتوح. | Term, and the current sentence is clumsy («يُعلَّق عليه»). | High |
| `errors.extract.ncr_already_closed` | بند عدم المطابقة مُغلق من قبل. | تقرير عدم المطابقة مُغلق من قبل. | Term. | Medium |
| `errors.extract.ncr_close_note_required` | اكتب ما تم تصحيحه قبل إغلاق بند عدم المطابقة. | اكتب ما تم تصحيحه قبل إغلاق تقرير عدم المطابقة. | Term. | Medium |
| `errors.extract.ncr_not_found` | بند عدم المطابقة غير موجود في هذا المشروع. | تقرير عدم المطابقة غير موجود في هذا المشروع. | Term. | Medium |
| `errors.closure.ncr_open` | لا يمكن التسليم وفيه بند عدم مطابقة مفتوح. | لا يمكن التسليم وفيه تقرير عدم مطابقة مفتوح. | Term. Parallel to `errors.closure.major_snags_open`, which is good. | High |
| `en.json` same keys | "Close the note", "The note is closed.", "Held until the note is closed" | "Close the NCR", "The NCR is closed.", "Held until the NCR is closed" | The English intent file should say NCR, as the errors already do. | Low |

## B. A second collision: the held portion is "released" like the hold, and "محجوز" like the hold

D-240 fixes «الحجز» as the retention hold, released once at handover. The held portion of a split extract
(a different thing, billed on the next extract) borrows both its verbs.

| Key | Current | Proposed | Why | Severity |
|---|---|---|---|---|
| `extract.ncr.consequence` | يُفرج عن {amount} ج.م. | يُعتمد {amount} ج.م للدفع. | «يُفرج عن» is the hold's verb. «للدفع» matches the row «المعتمد للدفع» beside it. Check with the lane that the money joins the next extract, then it could add «في المستخلص القادم». | High |
| `errors.closure.released_remainder_unbilled` | لا يمكن التسليم قبل إدراج الجزء المعلّق المُفرج عنه في مستخلص. | لا يمكن التسليم قبل إدراج الجزء المعتمد بعد التصحيح في مستخلص. | Designer-invented. «المُفرج عنه» sits one word from «الحجز المُفرج عنه» (`closure.unmet`, `cheque.released_hold`), so a reader may think the hold is meant. It does tell the contractor what to do (bill it), which is right. | High |
| `errors.extract.held_portion_already_released` (outside the diff) | تم الإفراج عن الجزء المعلّق من قبل. | اعتُمد الجزء المعلّق للدفع من قبل. | Same verb clash. | Medium |
| `errors.extract.no_held_portion` (outside the diff) | لا يوجد جزء محجوز في هذا المستخلص. | لا يوجد جزء معلّق في هذا المستخلص. | «محجوز» is the hold's word; the screens say «معلّق». One meaning, two words. | Medium |

## C. Designer-invented words, judged

| Key | Current | Proposed | Why | Severity |
|---|---|---|---|---|
| `extract.qty.heading` | الكميات | no change | Clear. | none |
| `extract.qty.item` | البند | no change | Same meaning as `boq.column.item` and `extract.figures.item`: the BOQ line. | none |
| `extract.qty.period` | هذه الفترة | no change | Matches `extract.input.hold_this_period`. | none |
| `extract.qty.cumulative` | حتى تاريخه | no change | Matches `extract.hold_to_date` and `cumulative_certified_work`. | none |
| `extract.work.heading` | قيمة الأعمال (ج.م) | no change | Reads right above «المعتمد للدفع» and «معلّق». | none |
| `extract.action.approve_portion`, `extract.portion.title` | اعتماد جزء | اعتماد جزء من الأعمال | D-254: the split scales work only, not the whole extract. «جزء» alone can read as part of the money total. Optional. | Low |
| `extract.portion.percent` | النسبة المعتمدة | no change | Matches `extract.history.portion`. | none |
| `extract.portion.error` | النسبة من 1 إلى 99. | no change | States the rule. | none |
| `extract.portion.submit` | اعتماد النسبة | no change | | none |
| `extract.portion.approved` | المعتمد للدفع | no change | | none |
| `extract.list.column.in_cheques` | في شيكات (ج.م) | شيكات لم تُصرف (ج.م) | «في شيكات» does not say whether the cheques are cashed. The error texts say «الشيكات التي لم تُصرف بعد». Same fix for `collection.in_cheques` (outside the diff). | Medium |

## D. Withholding row on the project workspace

| Key | Current | Proposed | Why | Severity |
|---|---|---|---|---|
| `projects.withholding.label` | الخصم من المنبع | no change | D-240 word. | none |
| `projects.withholding.field` | فئة الخصم | no change | «فئة» is the word in `errors.collection.withholding_not_applicable` and `errors.master.*`. | none |
| `errors.project.withholding_category_required` (outside the diff) | اختر نوع الخصم من المنبع … | اختر فئة الخصم من المنبع … | «نوع» here, «فئة» on the sheet. One meaning, two words. | Medium |
| `projects.withholding.edit`, `.save`, `.failed` | تعديل · حفظ · تعذّر الحفظ. | no change | | none |
| `projects.withholding.saved` | حُفظ الخصم من المنبع: {category}. | no change | | none |
| `projects.withholding.category.None` | لا يخصم | بدون خصم | «لا يخصم» is a verb with no subject on a choice button; it reads as "does not deduct" about whom. | Low |
| `projects.withholding.category.*` (3 rates) | مقاولات وتوريدات 1% · خدمات 3% · أتعاب مهنية 5% | no change | Matches the error text. The rates are a tax fact, not mine to confirm. | none |

## E. Closure and other keys changed in the diff

| Key | Current | Proposed | Why | Severity |
|---|---|---|---|---|
| `errors.extract.material_values_required` | … قيمة التشوينات بالموقع وقيمة التشوينات المركّبة … | … قيمة التشوينات بالموقع وقيمة الخامات المركّبة … | A stockpile is not installed, so «التشوينات المركّبة» says two opposite things. The earlier wording was «الخامات المركبة». The builder changed it, so ask. | Medium |
| `errors.extract.awaiting_evidence_revision` vs `material_values_required` | «يُرسل تقرير الجودة» vs «تسليم تقرير الجودة» | one verb: «إرسال» | One action, two verbs. | Low |
| `closure.status.*` (5 keys) | the `[PLACEHOLDER … awaiting Nabil]` prefix was removed | see section F | The placeholders are now final text on screen. | see F |
| `errors.closure.not_applicable_to_design`, `closure.design_none`, `closure.unmet.HoldReleased`, `errors.closure.nothing_to_release`, `errors.closure.hold_not_released` | «محجوز» changed to «الحجز» | no change | Good, aligns with D-240. | none |
| `errors.closure.released_hold_carries_no_withholding` | لا يُخصم من المنبع على الحجز المُفرج عنه في هذا العقد. أدخل المخصوم صفراً. | no change | Tells the user what to enter. | none |
| `errors.adjustment.termination_settlement_unruled` | تسوية فسخ التعاقد غير متاحة حالياً. لم يتغير شيء. | no change | Developer talk removed. | none |
| `collection.refused.status`, `dispute.refused` | «… على المستخلص الصادر فقط» | verify | The credit-note source accepts issued or disputed. Confirm a dispute really takes issued only. If not, the text is wrong. | Medium |
| `waiting.line.nobody` | بانتظار التكليف: {task} | no change | Fixes the "waiting for the client" error from the earlier review. | none |

## F. Needs Nabil's word (not mine to settle)

1. **The NCR term**: «تقرير عدم المطابقة» (proposed), or «عدم المطابقة» alone. Replaces «الملاحظة» and «بند عدم المطابقة» in the 10 Arabic keys in section A.
2. **The held-portion vocabulary** (section B): «معلّق» for the held portion, «يُعتمد للدفع» when the NCR closes, and «الإفراج» kept for the hold only.
3. **`closure.status.stopped` «متوقف»**: the placeholder tag is gone, but no ruling is in this diff. Spec keeps Stopped apart from «متعثرة», so it is allowed, but it is one idea beside «متعثرة» and Nabil should confirm.
4. **`closure.status.terminated` «تم الإنهاء» against «فسخ التعاقد»** in `errors.closure.termination_settlement_unruled` and `errors.adjustment.termination_settlement_unruled`. One state, two words. «إنهاء المشروع» once performed the termination, so «إنهاء» is a risky word. My suggestion: «تم فسخ التعاقد» for the status.
5. **Handover words** (construction practice, my suggestion): Egyptian sites say الاستلام الابتدائي and الاستلام النهائي, with فترة الصيانة between. The app says «الاستلام الابتدائي» (good), then «التسليم» for the final step (`closure.status.handover` «تم التسليم») and «الضمان» for the period (`closure.status.under_warranty` «تحت الضمان»). Usual: «تم الاستلام النهائي» and «فترة الصيانة». These words run through many keys, so this is a ruling, not a quick fix.

## Invented by a lane, never confirmed by Kaff

`extract.qty.*`, `extract.portion.*`, `extract.work.heading`, `extract.ncr.*`, `extract.list.column.in_cheques`, `extract.action.approve_portion`, `projects.withholding.*`, the three `errors.extract.ncr_*`, `errors.closure.ncr_open`, `errors.closure.released_remainder_unbilled`, `errors.closure.released_hold_carries_no_withholding`, and the five `closure.status.*` words (placeholder tags stripped with no ruling in this diff).

## What was wrong in the brief

- The diff from 7a8eb99 also holds about 40 keys that the first 2026-11-20 review already proposed (`waiting.*`, `collection.method.Cash` as «كاش» rather than «نقدي», `errors.adjustment.*`). They are applied. I flagged only the rows above.
- The brief framed the clash as «الملاحظة» against «بند عدم المطابقة». The errors say «بند عدم مطابقة». More important, «الملاحظة» is not free: it already means the snag and the daily-log note, so choosing it would make the clash worse.

# Arabic copy review, project module (KAFF-418, 419, 420)

Date 2026-11-11. Head 44de80d. Base c58cf94. Scope: keys added or changed in `projects.frame.*`, `boq.*`, `projects.create.*`, `projects.quick.*`, plus existing keys those screens reuse. Daily log and crew not covered.
Nothing under `src/` was edited. Suggested Arabic is my suggestion. Kaff decides.
Mock = `.design/v2/team-a/project-create.html`, `boq.html`, `project.html`.

## Result

- Kaff's five status words (`status.kaff.*`) are intact and used verbatim. No defect under flag 1.
- `en.json` has no missing key for the reviewed groups and no Arabic left in it by these lanes. The Arabic in `en.json` is deliberate glossary words (باب, تشوينات, يومية).
- Corrections by kind: wrong term 1, mock mismatch 3, explanatory text 3 live (plus 1 mock-approved), punctuation 4 (plus 2 optional), dead key 86 in the groups checked (one of them hides a missing screen state), questions for Nabil 10.

## A. Wrong term or mock mismatch (fix these first)

| Key | Current | Where it shows | Problem | Suggested |
|---|---|---|---|---|
| `errors.project.contract_type_required` | اختر نوع العقد. | Project create, under the contract-type radio group | Field legend `projects.field.contract_type` and the mock error both say التعاقد. Same screen, two words. | اختر نوع التعاقد |
| `errors.project.client_required` | العميل مطلوب. | Project create, under the client combobox | Mock error is «اختر العميل» (project-create.html line 380). The client field is a pick, not a typed field. | اختر العميل |
| `boq.column.total` | الإجمالي | BOQ lines header (boq-page.html lines 105 and 168) | Mock header is «الإجمالي (ج.م)». The built header lost the currency. Confirm the rows show ج.م, or restore it. | الإجمالي (ج.م) |
| `boq.start.projects` | مشاريع سابقة | BOQ start sheet, source choice | Mock sheet title is «مشروع سابق» (singular, one is chosen). Low risk. Check the template. | مشروع سابق |

## B. Explanatory text on a customer screen (breaks "screen explains itself")

| Key | Current | Where it shows | Problem | Suggested |
|---|---|---|---|---|
| `projects.sign_design.lead` | اكتب مساحة التصميم. تُحسب قيمة التعاقد من المساحة، ولا يمكن تغييرها بعد التوقيع. | Overview, design project, above the area field (project-workspace-page.html line 133) | Tells how the screen works. The field label already says مساحة التصميم (م²). The irreversible part is already on the confirm step. | Delete the line |
| `projects.convert.lead` | عند قبول العميل التصميم، حوّله إلى مشروع تنفيذ جديد لنفس العميل. يبقى مشروع التصميم كما هو. | Overview, design project (line 117) | Two sentences of instruction above a button that already reads تحويل إلى تنفيذ. | Delete the line |
| `boq.sign.lead` | التوقيع يثبّت قيمة التعاقد على إجمالي المقايسة، وتُجمَّد البنود بعده فلا تُضاف ولا تُعدَّل ولا تُحذف. | BOQ sign sheet, above the buttons (boq-page.html line 293) | Explanatory. But it is in the approved mock (boq.html line 560), so it is approved text that conflicts with the standing rule. Question for Nabil, not a defect. | Keep until Nabil rules. If he drops it, delete the line. |
| `boq.drop` | اسحب بنداً من الكتالوج | BOQ empty state on desktop | Describes how to use the screen. In the mock (boq.html line 547), so approved. Same question. | Keep per mock |

## C. Punctuation (field errors)

Mock field errors have no final period: «اسم المشروع مطلوب», «يوجد مشروع بهذا الاسم لهذا العميل», «رقم الهاتف مطلوب», «اختر نوع العميل».

| Key | Current | Problem | Suggested |
|---|---|---|---|
| `errors.project.name_required` | اسم المشروع مطلوب. | Period; mock has none | اسم المشروع مطلوب |
| `errors.project.client_required` | (see A) | Period | اختر العميل |
| `errors.project.contract_type_required` | (see A) | Period | اختر نوع التعاقد |
| `errors.master.name_required` | الاسم مطلوب. | The quick-add client panel shows «الاسم مطلوب.» beside «رقم الهاتف مطلوب» (no period): two styles in one form. The key is shared with other master screens whose errors all end in a period. | Nabil rules once whether field errors carry a period; then the master errors follow. |

Optional: `projects.frame.team_failed` and `projects.frame.progress_failed` end in a period, while `projects.create.clients_failed` does not. The mock mixes these too. Low priority.

## D. One meaning, two words (all questions for Nabil)

| Keys | Current | Problem | Suggestion |
|---|---|---|---|
| `projects.field.contract_value` (create, BOQ frozen total) vs `projects.frame.hero.contract_value` (overview hero) | قيمة التعاقد vs قيمة العقد | One figure, two names. The mocks differ the same way (boq.html says التعاقد, project.html says العقد). Also نوع التعاقد vs نوع العقد vs شروط العقد vs تاريخ توقيع العقد. | Nabil picks one. |
| `boq.sign.field.hold` vs `projects.workspace.vocab.fact.hold_rate` | نسبة المحجوز (sign sheet) vs نسبة الحجز (contract terms) | Same rate, two words. Both in the mock. spec §14 lists «محجوز / حجز». | نسبة الحجز in both |
| `boq.sign.field.advance` vs `projects.workspace.vocab.fact.advance_rate` | نسبة الدفعة المقدمة vs نسبة استرداد الدفعة المقدمة | If the same rate, two labels. If different, the sign sheet should say which one it sets. | Ask which rate is set at signing |
| `boq.follow_up` vs `projects.follow_up.line` | تنتهي المتابعة {date} vs المشروع غير موقّع. تنتهي فترة المتابعة في {date}. | Same fact, two wordings (both match their own mock). | Use «تنتهي فترة المتابعة في {date}» in both |
| `projects.workspace.vocab.status.stopped` (live, shown in the frame header via enum-keys.ts) | متوقف | Beside Kaff's متعثرة for a stuck project. Q18 in questions-for-karim.md is still open. | Nabil rules whether Stopped is متعثرة |
| `projects.workspace.vocab.action.open_boq` (dead) | فتح جدول الكميات | جدول الكميات vs المقايسة for the same thing. Dead, so only a trap if revived. | Delete (see F) |

## E. Construction practice (my suggestion, not Kaff's ruling)

| Key | Current | Where it shows | Note | Suggested |
|---|---|---|---|---|
| `projects.workspace.vocab.status.under_warranty` | في فترة الضمان | Frame header status | Site usage between the two handovers is فترة الصيانة. ضمان is the guarantee itself. | في فترة الصيانة |
| `projects.workspace.vocab.status.handover_pending` / `handover` | في انتظار التسليم / تم التسليم | Frame header status | From the contractor's side التسليم is fine; the client says استلام. Site usage has الاستلام الابتدائي then الاستلام النهائي. One «تم التسليم» hides which. | Ask: does Kaff track الابتدائي and النهائي as two states? |
| `projects.frame.stage.handover` | التسليم | Frame stage strip | Same point. | Keep unless Nabil wants الاستلام |
| `boq.qty.surveyed` | معاينة | BOQ line, quantity type beside تقديري | A noun meaning "an inspection", not a quantity type. A measured quantity on site is مقاسة or «بالمعاينة». | مقاسة (or بالمعاينة) |
| `projects.workspace.waiting.step.DeductionsApplied` | الاعتماد النهائي | Waiting line when it is the owner's turn | Mock says «اعتماد المالك» (project.html line 467). Question only, I do not pick. | Nabil: اعتماد المالك (mock) or الاعتماد النهائي |

## F. Dead keys (grep of `src/**/*.ts|html`, specs excluded)

`projects.frame.tab.*` look dead but are live (built with `'projects.frame.tab.' + tab`), so they are excluded. 86 keys remain in the groups checked.

- `projects.frame.money.failed` is dead, and its state is missing. The mock has a «فشل جزء المال» state with «تعذّر تحميل بيانات المال.», but no template shows a money failure. That is a missing screen state, not only a dead key. Needs a lane.
- `boq.sign.hint.hold`, `boq.sign.hint.advance`, `boq.sign.error.rate_format`.
- Intent and group vocabulary under `projects.workspace.vocab.group.*` and `.intent.*` (all `ls`, `cp`, `ds`, `closure`, `understand_progress`, `org_who`, `ds.understand_contract`), `vocab.jump_label`.
- `projects.workspace.vocab.fact.boq_lines|boq_total|extracts_count|extracts_latest|team`; `vocab.action.open_boq|open_extract`.
- `projects.workspace.group_failed`, `value_null.title`, `value_null.body`, `boq_total_pending`, `boq_none`.
- `projects.workspace.waiting.line|you|holders|barred` (replaced by `projects.frame.waiting.*`).
- `projects.workspace.progress.executed_of|active_bab|active_babs|no_active_bab|unavailable|cost_plus_note`; `site.no_log|active_bab`; `pay.certified|billed`.
- Older BOQ builder keys: `boq.not_frozen`, `boq.total.pending`, `boq.line.sell_rate`, `boq.empty_editable`, `boq.add.title|search|searching|more_results|change_item|submit`, `boq.field.quantity_surveyed|quantity_estimated`, `boq.hint.quantity_surveyed`, `boq.error.quantity_required|quantity_format`, `boq.line.edit`, `boq.remove.question|detail|confirm`, `boq.note.line_added|line_saved|line_removed|signed`, `boq.sign.ask|confirm_question|confirm_detail`, `boq.credit.raised`.
- `projects.follow_up.next`.

Still live under `projects.workspace.*` (do not delete): `vocab.status.*`, `waiting.step.*`, `waiting.supervisor`, `loading`, `team_none`, `date.signed|started`, `site.last_log`, `progress.done|cost_to_date|supervision_to_date`, `pay.collected|outstanding`, `vocab.fact.hold_rate|advance_rate`, `vocab.action.new_daily_log`. The extract screens use some of these too.

Note: dead `boq.note.signed` and `boq.sign.confirm_detail` still say «توقيع التعاقد», against D-217 («توقيع المقايسة»). `projects.sign_design.*` saying «توقيع التعاقد» is correct for a design project (no BOQ).

## G. Developer talk and contradicting labels

None found in the reviewed keys. The new BOQ and frame strings are short and plain.

## H. Wording invented by a lane and not confirmed by Kaff

Mock-verbatim keys are excluded. Everything below is not in the mocks or differs from them.

- `projects.create.client_clear` مسح العميل
- `projects.create.client_none` لا يوجد عميل بهذا الاسم (mock has «لا نتيجة» as a state label only)
- `projects.frame.stage.label`, `projects.frame.tabs.label` (aria labels)
- `projects.frame.title_neutral` المشروع
- `projects.frame.value_unset` لم تُحدَّد بعد
- `projects.frame.waiting.mine` and `.line` (shape follows the mock, step words come from old keys)
- `projects.frame.action.open_extract` فتح المستخلص, `projects.frame.action.reopen` إعادة فتح
- `projects.frame.team_failed`, `projects.frame.progress_failed`
- `boq.qty.surveyed` معاينة, `boq.qty.estimated`, `boq.qty.none`
- `boq.start`, `boq.start.bases` مقايسات جاهزة, `boq.start.use` استخدام, `boq.start.lines`
- `boq.lines_count`, `boq.missing`, `boq.copy.skipped`, `boq.catalogue.title`, `boq.add.line`, `boq.add.ticked`, `boq.custom.*`
- `boq.archived_badge` مؤرشف (a feminine مقايسة would be مؤرشفة, but the mock uses «مؤرشف» at lines 516, 517 and 533, so a question only)
- `money.currency_short` ج.م (duplicates `catalogue.currency.affix`; not an Arabic issue)
- `projects.workspace.vocab.status.stopped|handover_pending|handover|under_warranty|terminated`: working text with no Kaff ruling (stated in the `enum-keys.ts` comment)

## Brief corrections

- Point 4: confirmed. The mock phrase is «عندك: اعتماد المالك». The built line takes the step from the old key `DeductionsApplied` = «الاعتماد النهائي».
- Point 6: `boq.sign.hint.*` and `projects.frame.money.failed` are dead, as stated. The `projects.workspace.*` groups are not all dead: about 20 keys are live (list in F), and `projects.workspace.vocab.status.*` drives the project state in the header. Deleting the group wholesale would break that.
- Point 7: the label «المشروع» on project create matches the mock (project-create.html line 387). No defect.
- Points 1 and 2: right, with a precision. The mock's error text is «اختر العميل» and «اختر نوع التعاقد», so they are mock mismatches, not only style.
- Point 5: right. Both leads are explanatory. `boq.sign.lead` (not in the brief) is the same kind but approved in the mock.
- "78 added lines": I did not recount. The diff also removes keys and edits others (for example `boq.sign.confirm` changed from «نعم، وقّع وثبّت القيمة» to «توقيع المقايسة»), so changed keys exceed the added count.
- The brief did not mention that `projects.frame.tab.*` look unused to grep but are built dynamically. Do not delete them.

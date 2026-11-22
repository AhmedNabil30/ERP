# Arabic copy review: Release 2-6 mocks, one term one meaning (2026-11-22)

Scope: `.design/v2/team-a/` site-money (R2), sales (R3), accounting (R4), portal (R5), offline (R6), their notes files and the Sales section of README.md. Compared with `src/Web/public/locales/ar.json` and spec.md section 14. Nothing was edited (no mocks, no src, no ar.json). Not committed.

Method: strings read from the notes lists and grepped in the HTML; screen ids are the ones each mock uses (S1..S13, A1..A14, P1..P8, offline views). "Proposed" terms are my suggestions, not Kaff's rulings. "Needs Karim" = yes where the term is a Kaff or construction-practice decision, or where the built app would have to change.

Short answer: worst clashes first: (1) three words for one gate approval, (2) three meanings of "refuse", (3) withholding has two names, (4) الضمان means two things, (5) pay/spend verbs, (6) wait phrases.

## A. Kaff status vocabulary

Spec and ar.json agree on the five words (`status.kaff.*`). Mock use is mostly verbatim; problems are borrowed words and look-alikes.

| Term | Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|---|
| متعثرة | portal P3 section pill, derived (planned end passed and under 100%) | Word is correct, the rule that triggers it is invented. A client reads it as "Kaff is failing". | Keep the word verbatim. Karim rules whether lateness by date means متعثرة (portal-notes question 4). | yes |
| متوقف beside متعثرة | ar.json `projects.workspace.vocab.status.stopped`, `closure.status.stopped`, `daily_log.stopped` "المشروع متوقف", `extract.stopped`. Mocks use only متعثرة. | Two words for a project that is not going well, if "stopped" is meant as a Kaff status. If it is an admin stop (a different state), nothing says so. | Decide whether "stopped" is a sixth state. Same idea: use متعثرة. Different (work halted by Kaff): keep متوقف and never show beside متعثرة. | yes |
| جاري العمل | accounting project header pill; portal P2/P3/S2 stage status; sales S1/S3 opportunity status | Correct on project and stage (accounting, portal). On sales it is borrowed for an opportunity (next row). | Accounting, portal: keep. | no |
| جاري العمل / تم تأجيلها on opportunities | sales S1 and S3 pills; README Sales words | Two project states borrowed for a pipeline object, then two invented ones added (مفقودة, تم الفوز). The button that causes "تم تأجيلها" is «تعليق», a different word from its own result. | Keep تم تأجيلها as the state; rename the button «تأجيل». For the pipeline either use plain non-Kaff words (نشطة / مؤجلة) or Karim confirms Kaff's words may apply. مفقودة: suggest «خسرناها» (Kaff's choice). | yes |
| لم تبدأ | portal P3/P6 upcoming section or stage | verbatim | none | no |
| انتهت | portal: only in a preview label «انتهت الجولات» (rounds used up) | Not on screen, but never reuse Kaff's "finished" word for rounds. A finished stage on the portal should read انتهت, not تمت الموافقة. | as left | no |
| قيد المراجعة | site-money S10 extract pill (new) | Invented state beside built `extract.list.waiting` «بانتظار {step} من {who}». Two phrases for "extract is in somebody's hands". | Use «بانتظار …» everywhere. Drop قيد المراجعة. | yes |
| قيد التنفيذ | removed from portal in round 2 | correctly gone | none | no |

## B. One word two meanings, one meaning two words

### B1. Gate approval: اعتماد / تصديق / قبول / موافقة (worst)

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| ar.json: «اعتماد الكميات» (`extract.action.approve_technical`, `extract.task.technical`, `projects.workspace.waiting.step.QcSubmitted`), «اعتماد» (Owner), «اعتماد المراجعة» (change order), «اعتماد الإشعار الدائن» | Built app uses one word for every gate. | **اعتماد** for every Kaff staff gate (Technical Office, Finance, Owner). | yes |
| site-money S11 and notes: «تصديق الكميات», «القيمة المصدّقة» (Technical Office); «قبول التسوية» (Finance, S4); «اعتماد» only for Owner | The mock splits one act into three words by role and contradicts built ar.json for the same extract step. | Revert to اعتماد الكميات, القيمة المعتمدة, اعتماد التسوية. Show the role in the label, not a different verb. | yes |
| accounting A6/A10 «اعتماد الإقفال»; A13 «المعتمد للصرف» (committed) | اعتماد right for the close. «المعتمد للصرف» reuses اعتماد and صرف for a budget figure; ar.json `errors.extract.approved_portion_out_of_range` «النسبة المعتمدة للصرف» means an extract portion approved for payment. | A13: «الملتزم به» (alternative the notes already list). | yes |
| portal P5/P6: «تمت الموافقة», «موافقتك», «موافقة كف», «تأكيد الموافقة»; sales «العميل وافق» | Client says موافقة, staff say اعتماد: a good split. «موافقة كف» is a Kaff gate shown to a client, so it breaks the split. ar.json says اعتماد المالك for the same change-order step. | Rule: موافقة = client agrees; اعتماد = Kaff gate. Portal step labels «موافقتك» then «اعتماد كف». | yes |

### B2. Send back or refuse: رفض / إرجاع / مرفوض / تعذّر

| Meaning | Current uses |
|---|---|
| Staff send a document back to origin with a reason (CLAUDE.md: rejections return to origin) | ar.json «رفض», «رفض المستخلص», «سبب الرفض», `credit_note.reject_*`. site-money S4/S5/S11 «رفض», «سبب الرفض», «تأكيد الرفض». site-money S2 «إرجاع», «تأكيد الإرجاع» (same act, other word). accounting A6/A10 «إرجاع» (round 2, deliberate). portal S3 staff «إرجاع للتنفيذ بسبب». |
| Client declines a change order | portal P5 «رفض», «سبب الرفض», «مرفوض», «تأكيد الرفض». ar.json `change_order.action.client_rejection`. |
| Server did not accept an offline line | offline «مرفوض» mark, group header, and the text «ويُرفض غداً». |
| Client sends a design stage back for changes | portal P6 «إرجاع للتعديل», «طلب تعديل إضافي». |

Problem: one family of screens says رفض for "back to the person to fix and resend" and also for "the client said no". Offline مرفوض is a third thing (a rule, not a person).

Proposal:
- **إرجاع** (+ سبب الإرجاع) = staff send a document back to origin. Changes ar.json `extract.action.reject`, `extract.reject.*`, `extract.history.rejected`, `credit_note.reject_*`, `errors.extract.reason_required`, `errors.change_order.reason_required` (about 10 keys) and the site-money S4/S5/S11 buttons. Alternative: keep رفض in the built app, revert accounting A6/A10 to رفض, and give the client a different word («عدم الموافقة»).
- **رفض / مرفوض** = a final no; the client declining a change order only.
- **لم يُقبل** for the offline server refusal, so the mark never reads as a person's decision; text «ويُرفض غداً» becomes «ولن يُقبل غداً».
- P6 «إرجاع للتعديل» fits; keep.

Needs Karim: yes (changes built keys).

### B3. Money going out: دفع / صرف / مصروف

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| site-money «دفع» (S4, S11 buttons), «مدفوع» (S10); accounting A5 «دفع», «الدفع من» | Round 2 chose دفع; consistent. | **دفع** = money leaves safe or bank. | no |
| accounting A13 «المعتمد للصرف», A14 «تاريخ الصرف» (loan), cheque pill «تم صرفه» | صرف still used for committed, loan payment date, cheque cashed. | Committed «الملتزم به». Loan «تاريخ الدفع» or «السداد» (accounting already says «سداد القرض»). Cheque «تم صرفه»: keep only if that is how Kaff says it; else «تم تحصيله». | yes |
| ar.json `proof.doc.Payment` «صرف», `proof.type.MaterialAdvanceIssue` «صرف تشوينات», `errors.closure.warranty_not_ended` «تُصرف محجوزات» | صرف = issue. Fine for material issue; clashes with دفع for a payment document. | Payment document «دفع». Keep صرف only for issuing تشوينات. | no |
| site-money «مصروف»; accounting «مصروف جديد», «مصروفات عامة»; sales «مصاريف ما قبل العقد», «إضافة مصروف» | Singular/plural mix: مصروف / مصروفات / مصاريف. One idea. | **مصروف** singular, **المصروفات** plural. Sales «مصاريف» becomes «المصروفات قبل العقد». | no |

### B4. Withholding and deductions

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| site-money S11 chain step, row, button, S12 title: «الخصم والإضافة» (replaced «الاستقطاعات») | Spec says "withholding"; ar.json uses «الخصم من المنبع» in 12+ keys (`billing.component.TaxAtSource`, `projects.withholding.label`, `collection.withheld`, `errors.collection.*`) and never «الخصم والإضافة». spec section 14 lists neither. | One term. Unless Karim confirms «الخصم والإضافة» is what Kaff calls it, use built **الخصم من المنبع**. | yes |
| portal P4 «الخصومات» (one total: hold, advance recovery, withholding) | Fine as a client-facing plain total. Collides with ar.json line 485 «نسبة الخصم من كل مستخلص» which is advance recovery. | Portal keep «الخصومات». Built 485 becomes «نسبة الاسترداد» (matches `billing.component.AdvanceRecovery`). | yes |
| «المخصوم» (site-money S12), «المخصوم من المنبع» (ar.json) | consistent | keep | no |

### B5. Hold vs warranty: الضمان / الحجز

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| spec: محجوز/حجز = retention hold; ضمان = warranty (`WarrantyPeriod`). ar.json: الحجز, بدء فترة الضمان | clear | keep | no |
| site-money S11 «الضمان هذا الأسبوع ٥٪», «الضمان حتى الآن» (subcontractor retention) | Same word as the warranty period but means retention. A contractor reads it as warranty. | **الحجز** for money held back (S11, S12); **الضمان** only for the period. Spec assumption 19: sub retention is released at warranty end, so it is a hold. | yes |
| sales/accounting stage line المقايسة · التنفيذ · التسليم · الضمان · الإقفال | Skips الاستلام الابتدائي, which ar.json already has (`closure.action.practical_completion`). | Suggest (construction practice, not Kaff's ruling): المقايسة · التنفيذ · الاستلام الابتدائي · فترة الضمان (or الصيانة) · الاستلام النهائي · الإقفال. | yes |

### B6. Remaining balance: باقي العقد / المتبقي

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| portal P2/P3 «باقي العقد» (renamed from المتبقي) | ar.json has «المتبقي على العميل» (`projects.workspace.pay.outstanding`), «المتبقي» (collection), «المتبقي (ج.م)» (`extract.list.column.remaining`). Different quantities: contract value minus billed vs amount due. | Portal keeps «باقي العقد» (how a client speaks of it). Staff screens keep «المتبقي على العميل» for the due amount; use «باقي العقد» only for contract minus certified work. | yes |
| site-money «المتبقي للإرجاع»; accounting «المتبقي» (accrual), «المتبقي من القرض»; ar.json «تشوينات المتبقية» | «المتبقي» for 5 quantities. Fine only with a qualifier. | Always qualify; never a bare «المتبقي». | no |
| site-money S13 «رصيد التشوينات» (renamed) vs ar.json `proof.side.material.title` «تشوينات المتبقية» | Two names. | «رصيد التشوينات» both places. | no |

### B7. Money into the safe: ضخ / إيداع

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| site-money «إيداع» (round 2). accounting A5 still «ضخ من المالك». ar.json `errors.treasury.negative_balance` «يلزم إيداع من المالك». ar.json cheque «إيداع الشيك». | Accounting not updated: two words for one act. إيداع also means cheque-to-bank in ar.json, a different act, but with its object it is unambiguous. | **إيداع من المالك**; change accounting A5 button. | no |
| site-money S12 «توريد» | Kept for «المستحق توريده». | keep | no |

### B8. بند: five meanings

| Current uses | Proposed | Needs Karim |
|---|---|---|
| ar.json «البند» = BOQ or catalogue line, many keys | **بند** = BOQ or catalogue line only | no |
| site-money «بنود المصروف», «إضافة بند مصروف» (expense line on a settlement); accounting «بند، بندان، 3 إلى 10 بنود» (accrual line); sales «بند مخصص», «حذف البند» (quotation line, same as a BOQ line, fine) | Expense lines: «إضافة مصروف». Accrual lines: «قيد» or «مستحق». Quotation line: keep «بند». | no |
| daily-log entry type (the original clash) | «النوع», never بند | no |

### B9. Phone and password

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| ar.json «كلمة المرور» x16, «الهاتف» x17. Portal «كلمة السر» x7, «رقم الموبايل» x3. Offline «التليفون» x3, «الهاتف» x2. | Three words for the phone, two for the password. | **كلمة المرور** everywhere (built). Phone: one word per screen at least; Karim says whether site and client screens say «الموبايل» / «التليفون» or built «الهاتف». | yes |

### B10. Waiting family

| Current uses | Problem | Proposed | Needs Karim |
|---|---|---|---|
| ar.json `waiting.line` «بانتظار {who}: {task}»; `errors.auth.forbidden` starts «دورك أو تكليفك» (your role). site-money «دورك», «بانتظار غيرك», «بانتظار اعتمادك». portal «بانتظارك», «عند كف», «عند العميل». offline «عند المشرف». sales «بانتظار المكتب الفني». | «دورك» means "your role" in ar.json and "your turn" in site-money. «عند X» is a third form of "waiting on X". | Pattern **بانتظار {who}** only. Site-money group headers «بانتظارك» and «بانتظار غيرك». Portal «عند كف» becomes «بانتظار كف». Offline «عند المشرف» is a place reached, not a wait; keep and have Nabil confirm (already open in offline-notes). | yes |

## C. Developer talk on the customer (portal) screens

Nothing reads as the build team speaking. Items to change or confirm:

| String | Screen | Problem | Suggested |
|---|---|---|---|
| «بعلامة مائية» / «نسخة نظيفة» | P6, P7 | Design-trade words; a client may not read them as "not yet released for payment". | «نسخة للمعاينة» / «النسخة النهائية» |
| «التعديل خارج الجولات المتفق عليها ويُسعَّر من كف» | P6 limit | Reads fine; rule invented (Q5 unanswered). | Karim wording |
| ISO dates 2026-11-22 | P3, P5 | Developer format; Egyptians expect day/month/year. | dd/mm/yyyy |
| «كلمة السر المؤقتة» | S5 | see B9 | «كلمة المرور المؤقتة» |
| «مستخلص» (not «دفعة»), «أمر تغيير» | P3, P4, P5 | Match spec section 14 and ar.json. | keep |

## D. Label contradicts its action

| String | Where | Contradiction | Proposed |
|---|---|---|---|
| «تعليق» button, result «تم تأجيلها» | sales S3 | suspend vs postpone | «تأجيل» |
| «العميل وافق» | sales S3 | Marketing stage move; reads as the client's own approval, which portal P5 records separately | «العميل وافق على العرض» |
| «ضخ من المالك» | accounting A5 | If it only asks the owner, the label names the act itself | «طلب إيداع من المالك» if it asks; confirm who presses it |
| «إضافة رغم ذلك» | offline | reads as a retry | Karim wording |

## E. Construction practice (my suggestion, not Kaff's ruling)

| Term | Current | Usual on site |
|---|---|---|
| Hand-over stages | mocks التسليم; built الاستلام الابتدائي and تم التسليم | الاستلام الابتدائي, فترة الصيانة (or الضمان), الاستلام النهائي |
| Client retention | الحجز | site men also say «الضمان» or «المحجوز» for it; that is the root of B5, ask Karim |
| Tax withheld by client | الخصم من المنبع | «الخصم والإضافة» is the form's name; ask Karim (B4) |
| Net amount | «الصافي المستحق» (site-money) and «صافي المستحق» (sales) | one form: «صافي المستحق» |
| تشوينات, مستخلص, باب, عهدة | consistent in all mocks | keep |

## F. Wording invented by a lane, not confirmed by Kaff

Combined from the notes files. * = flagged above.

- **site-money**: طلب عهدة جديدة · مسوداتي · عهد بانتظار الإرسال · تأكيد الإرجاع · إرسال للمحاسبة · بنود المصروف* · المستلم · المصروف · أُعيدت التسوية · بانتظار المحاسبة · بانتظار المالك · معتمد من المالك · قبول التسوية* · تصديق الكميات* · القيمة المصدّقة* · قيد المراجعة* · دورك* · بانتظار غيرك · عند المشرف · بلا صورة فاتورة · المتبقي للإرجاع · مدفوع · سلفة على عميل · تجهيز سلفة · الحد · الحد الأقصى · الخارج حالياً · إجمالي المعرّض · الناقص · إيداع · تسجيل إيداع من المالك · الإيداع يسجّله المالك · حساب المالك الجاري · مستحق للمالك · سلفة قابلة للرد · سحب نهائي · الخصم والإضافة* · الضمان هذا الأسبوع* · الأساس · النسبة · المخصوم · تقرير الجودة · رصيد التشوينات · مواد مشوّنة بالموقع · مواد مركّبة · محدود بالمتبقي · قريب من الحد.
- **sales**: مفقودة* · تم الفوز · تحتاج متابعة · آخر نشاط · العميل وافق* · إرجاع إلى «تفاوض» · فتح المشروع · استئناف · إعادة فتح · تعليق* · إغلاق كخسارة · استخدام أحمد سمير · إنشاء عميل جديد بنفس الرقم · هذا الرقم مسجّل للعميل … · رسوم المعاينة · بانتظار تأكيد المالية · تسجيل الرسوم · تسجيل نشاط · مصاريف ما قبل العقد · الغرض · إجمالي المصاريف · نسبة الزيادة · الزيادة الافتراضية · إجمالي الباب · نسبة الربح · إجمالي التكلفة / البيع · بانتظار مراجعة المكتب الفني · فتح العروض · بانتظار فتح المشروع. Stage names (عميل محتمل · اجتماع · معاينة · عرض سعر · تفاوض · عقد) are spec assumption 6 (Karim), still unconfirmed.
- **accounting**: the long list in `accounting-notes.md`; notably الأصل (needs الأصل الثابت), مستحق من العميل / مقدّم من العميل (versus ar.json «دفعة العميل المقدمة», and near «مدفوع مقدمًا» for prepayment), المعتمد للصرف*, ضخ من المالك*, لم يتم, تعذّر, ضمن الحد / قريب من الحد / تجاوز. ميزان المراجعة and الميزانية العمومية are accountants' standard words and likely fine; still unconfirmed.
- **portal**: stage names (المفهوم الأولي · المخطط المبدئي · التصور ثلاثي الأبعاد · تطوير التصميم · الوثائق النهائية), بانتظارك, عند كف*, عند العميل*, تمت موافقتك, فريق كف, بعلامة مائية*, نسخة نظيفة*, سحبه كف, الأتعاب, باقي العقد*, جولات التعديل, إرجاع للتعديل, طلب تعديل إضافي, اعتماد وإرسال للعميل, إرجاع للتنفيذ بسبب, رفع المسلَّم, تسجيل تحصيل, تسجيل النقل, سحب النشر, نشر المحدد, كلمة السر المؤقتة*, النهاية المخططة.
- **offline**: لم يُرسل · جارٍ الإرسال · أُرسل · مرفوض* · إرسال الآن · تراجع · إضافة رغم ذلك* · خروج · التقاط صورة · إعادة التقاط · حفظ المسودة · مسودة جديدة · إضافة عامل · حفظ العامل · الإرسال · المسودات · أُرسل اليوم · عند المشرف · طلب عهدة · وصف الصورة · and all reason sentences (مرّت أكثر من 3 أيام ..., لم يعد هذا الحضور ..., مساحة التليفون ممتلئة ..., الصورة كبيرة جداً, سيُحذف هذا السطر ..., لم تُسوَّ العهدة السابقة, هذا الهاتف مسجَّل ...).

## G. Things in the brief that were wrong or not available

- The brief names "الضمان" as a term to align across mocks. The mocks mostly use it correctly (warranty period); the clash is one use in site-money S11 where it means subcontractor retention (B5).
- spec.md section 14 has no entry for the five status words, withholding, or subcontractor retention. They live in `CLAUDE.md` and `ar.json` only, so B4 and B5 cannot be settled from the spec.
- `accounting-notes.md` still lists «رفض» in its invented list while its own round 2 says the Owner button became «إرجاع». I used the HTML, where accounting has no «رفض».
- `accounting.html` still has «ضخ من المالك», «المعتمد للصرف», «تاريخ الصرف». The round-2 word fixes in site-money did not reach it.
- Portal strings were checked from the HTML and notes; the visual screens were not opened, so on-screen text built at runtime from variables may differ.

## H. Order to put to Karim

1. إرجاع vs رفض for staff gates (B2), changes about 10 built keys.
2. اعتماد for every gate or three words (B1), mock reverts, built stays.
3. الخصم من المنبع vs الخصم والإضافة (B4).
4. الحجز vs الضمان for subcontractor retention (B5).
5. Hand-over stage names (B5, E).
6. Is «متوقف» a state beside متعثرة (A).
7. Do Kaff's status words apply to opportunities (A).
8. Phone and password words (B9).

# Arabic word proposals: sales status, hold names, client balance (2026-11-24)

Source: findings 6, 9, 10 of `ux/consultant-review-app-r2-r5-2026-11-24.md`; CLAUDE.md vocabulary; spec §14; D-240; D-263.
Every proposed Arabic word is a suggestion. Kaff has not ruled on any of them. `ar.json` was not edited.

## 1. Opportunity status (finding 6)

Kaff's five words (لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها) belong to projects. Sales gets its own four.

| Key | Current | Proposed | Reason |
|---|---|---|---|
| sales.status.Active | جاري العمل | مفتوحة | Project word removed. "Open" says what a lead is, not that site work started. |
| sales.status.OnHold | تم تأجيلها | معلّقة | Project word removed. Matches the existing button `sales.action.hold` "تعليق". Root differs from الحجز, so no clash with the client hold. |
| sales.status.ClosedLost | مفقودة | خاسرة | "مفقودة" means missing. Feminine to agree with فرصة. Close to the button "إغلاق كخسارة". |
| sales.status.ClosedWon | تم الفوز | رابحة | Pairs with خاسرة. A short adjective, like the other three. |
| `en.json` sales.status.* | In progress / Postponed / Lost / Won | Open / On hold / Lost / Won | Keep the English meaning in step. |

Notes:
- The lead at stage 1 shows both the stage "عميل محتمل" and a status. With "مفتوحة" the two no longer contradict.
- «معلّق» is also used for the NCR held portion (D-243 §4, «الجزء المعلّق»). That is a different screen and a different noun, so I left it.

## 2. One name per money (finding 10)

| Money | Word | Where | Current state | Reason |
|---|---|---|---|---|
| Client hold (Hold, spec §14) | **الحجز** | closure, extract, proof, trial balance | Keys already say الحجز. The account name in `ProjectAccountSetFactory.NameAr` is "محجوز". | D-240 §1: «المحجوز» is replaced everywhere. This is the one leftover. |
| Subcontractor retention | **ضمان المقاول** (always with المقاول, never bare «الضمان») | closure panel, requests, errors | Keys say "محجوزات المقاولين" / "صرف محجوزات". The rate field says "نسبة الاستقطاع". Three words. | D-263 §4 rules «الضمان» with no separate word. The root differs from الحجز, so the two ledgers no longer read as one. |
| Warranty period | **فترة الضمان** (and «طلبات الضمان») | closure | Already in use. | Keep as is. |

| Key | Current | Proposed | Reason |
|---|---|---|---|
| (code) ProjectAccountSetFactory.NameAr, AccountType.Hold | محجوز | الحجز | D-240 §1. See the data warning below. |
| closure.retention.title | محجوزات المقاولين | ضمان المقاولين | D-263 §4. |
| closure.retention.loading | جارٍ تحميل محجوزات المقاولين | جارٍ تحميل ضمان المقاولين | Same. |
| closure.retention.none | لم يُفرج عن محجوزات أي مقاول بعد. | لم يُفرج عن ضمان أي مقاول بعد. | Same. The panel should also list contractor, amount held and the release date (finding 10). That is a build item, not a word. |
| errors.closure.retention_release_already_requested | طلب صرف محجوزات هذا المقاول… | طلب صرف ضمان هذا المقاول… | Same. |
| errors.closure.retention_release_not_requested | لا يوجد طلب صرف محجوزات… | لا يوجد طلب صرف ضمان… | Same. |
| errors.closure.retention_release_posting_not_found | حركة صرف المحجوزات… | حركة صرف ضمان المقاول… | Same. |
| errors.closure.retention_release_already_reversed | حركة صرف المحجوزات هذه… | حركة صرف ضمان المقاول هذه… | Same. |
| errors.closure.retention_reversal_already_requested | …لصرف المحجوزات… | …لصرف ضمان المقاول… | Same. |
| errors.closure.retention_reversal_not_requested | …لصرف المحجوزات… | …لصرف ضمان المقاول… | Same. |
| errors.closure.warranty_not_ended | تُصرف محجوزات المقاول عند انتهاء الضمان فقط. | يُصرف ضمان المقاول عند انتهاء فترة الضمان فقط. | The two senses of ضمان sit in one sentence, so the sentence says «فترة الضمان» for the warranty. |
| subcontractor.field.retention_rate | نسبة الاستقطاع % | نسبة ضمان المقاول % | One word for the one thing. Mirrors «نسبة الحجز» on the client side. |
| errors.master.retention_rate_required | نسبة الاستقطاع مطلوبة. | نسبة ضمان المقاول مطلوبة. | Same. |
| closure.retention.* (release_waiting, reversed, ask…) | الإفراج | keep | These say «الإفراج» without the noun, so no change. |

Left alone because they mean something else: errors.identity.username_taken and username_reserved (محجوز = taken).

Warnings:
- **Data.** The account name «محجوز» is stored per project when the accounts are created. Changing the factory fixes new projects only. Existing projects need a data fix (a migration or a rename). That is a code task, not a copy edit.
- **Collision to put to Nabil.** D-263 §4 says «الضمان» for subcontractor retention. The same word is the warranty (spec §14, about 20 keys). I kept it and added «المقاول» or «فترة». If it still reads badly on site, the fallback is «استقطاع المقاول» (already the rate label). That is my suggestion only, and it needs a new ruling.
- **Brief check.** D-263 §4 says retention "stays «الضمان»". The built closure keys use «محجوزات», so the ruling was never applied there.

## 3. What the client still owes (finding 9)

Two figures need two names. The staff word and the portal word must be the same word for the same figure.

| Key | Current | Proposed | Reason |
|---|---|---|---|
| portal.home.remaining | باقي العقد | المتبقي من العقد | Contract value minus paid. «المتبقي» is the word staff screens already use (collection.remaining, extract.list.column.remaining). «باقي» is spoken language. |
| projects.workspace.pay.outstanding | المتبقي على العميل | المتبقي من العقد, if the figure is contract value minus collected | One idea, one name, on both sides. I could not find where `m.outstanding` is computed, so check this first. If it is only issued extracts not yet collected, use «المستحق الآن» instead. |
| portal.home.money | المدفوع وباقي العقد | المدفوع والمتبقي من العقد | Follows the rename. |
| (new) portal home headline | none | المستحق الآن | Total of issued, unpaid extracts. Shown first, with the extract number it belongs to. Uses the same root as «الصافي المستحق» on the extract card and the account «مستحق من العميل». |
| portal.extracts.net | الصافي المستحق | keep, add the currency (ج.م) | Finding 9: the work value has no currency and the net has one. Both get one. |
| portal.extracts.unpaid | غير مدفوع | keep | The status of one extract. |

Reason for the split: «المتبقي من العقد» includes work not yet billed, so it is not an amount to pay today. «المستحق الآن» only counts extracts already issued. A client who reads the first as the second pays the wrong sum.

## All proposals, one list

1. Sales status: مفتوحة · معلّقة · خاسرة · رابحة.
2. Client hold: الحجز everywhere, including the stored account name (needs a data fix).
3. Subcontractor retention: ضمان المقاول, never the bare word. The rate label is «نسبة ضمان المقاول». Warranty stays «فترة الضمان». Put the collision to Nabil.
4. Contract balance: المتبقي من العقد, one word on staff and portal.
5. New portal figure: المستحق الآن.

## Brief corrections

- Finding 10 says the trial balance shows «محجوز». That string is the stored account name in `ProjectAccountSetFactory.cs` line 147, not an ar.json key.
- Nothing in `ar.json` beyond the keys above matched محجوز/الحجز/محجوزات as a problem.

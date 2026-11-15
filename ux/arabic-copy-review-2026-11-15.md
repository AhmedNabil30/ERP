# Arabic copy review, site day (KAFF-614 daily log, KAFF-615 crew)

Date 2026-11-15. Head b905e81. Base 1a0ba86. Scope: keys added or changed in `daily_log.*`, `crew.*`, `errors.daily_log.*`, plus existing keys those two screens reuse (`action.save|edit|cancel`, `catalogue.search.clear`, `hr.worker.engagement.engage_action`, `hr.worker.pool.never_engaged`). Templates read: `daily-log-page.html`, `crew-page.html`, and the shared parts `kaff-attendance-strip`, `kaff-link-row`, `kaff-log-group`, `kaff-log-entry`, `kaff-kind-row`, `kaff-figure-pair`, `kaff-day-switch`, `kaff-hours-stepper`.
Nothing under `src/` was edited. Suggested Arabic is my suggestion. Kaff decides.
Mocks = `.design/v2/team-a/daily-log.html` (commit b0066c6) and `.design/v2/team-a/day-labour.html` (commit ecb140d), the ones named in the two stories.
Already ruled and not re-asked: D-223 (التعاقد, نسبة الحجز, مقاسة). Neither screen uses those words, so D-223 changes nothing here.

## Result

- Kaff's five status words (`status.kaff.*`) do not appear on either screen. No defect under flag 1.
- Every wording the lanes reported as invented is in the approved mocks word for word, except one: `daily_log.quantity.no_lines`. The built Arabic matches the mock everywhere I compared (about 85 strings). There is no "restore the mock wording" fix to make. See "Brief corrections".
- `en.json` has the same key count as `ar.json` in all three groups (62, 16, 28) and no Arabic in these groups. No key is used in a template and missing from either file.
- Corrections by kind: wrong term or mock mismatch 2, explanatory text on screen 3 (2 are in the mock), punctuation 1 question, one meaning two words 3, construction practice 4, dead keys 0 in `daily_log.*` and `crew.*` (and 15 server-keyed error keys whose text never shows), developer talk 11 error keys, plural-sensitive keys 4, mock states missing from the build 2, questions for Nabil 12.
- Lines in `ar.json` between base and head: 69 added, 37 removed. The removed keys are gone from `src/` (checked: no reference left).

## A. Wrong term or mock mismatch

| Key | Current | Where it shows | Problem | Suggested |
|---|---|---|---|---|
| `daily_log.quantity.measured` (label of the figure `lineTotal`) | الكمية المقاسة ({unit}) | Daily log, quantity form, first figure above the entry box | The template fills it from `quantitySurveyed ?? quantityEstimated` (`daily-log-page.ts` line 340). With no survey recorded it shows the estimated quantity under a label that says measured. D-217 item 21: a quantity counts as estimated until a survey is recorded. D-223 item 7 makes مقاسة the survey type. A label that contradicts its number. The same figure is the «من {total}» in the picker row. | Question 1. Not a mock restoration: the mock always had a surveyed value. |
| `crew.row_failed` | تعذّر حفظ هذا السطر | Crew, under a failed row, bold, followed by the server reason | In the mock the bold sentence ends the line and the retry button follows. The build puts the server's sentence right after it with no separator, so it reads «تعذّر حفظ هذا السطر يجب أن تكون الساعات…». | تعذّر حفظ هذا السطر. (add the period; still the mock's words) |

Mock-matching checks that passed: `daily_log.kind.*` order and words (ملاحظة, كمية, خامات, سبب التوقف), `daily_log.group.*`, `daily_log.form.*`, `daily_log.section.*`, `daily_log.day.*`, `crew.*` (all 16), the seven units, `daily_log.location.refused_title` and `.refused`, `daily_log.picker.*`, `daily_log.failed`, `daily_log.closed`.

## B. Explanatory text on a customer screen

Standing rule: screens explain themselves. Sentences on screen:

| Key | Current | Where it shows | In the mock? | Problem | Suggested |
|---|---|---|---|---|---|
| `daily_log.quantity.no_lines` | المقايسة لا تحتوي على بنود لتسجيل كمية عليها. | Daily log, quantity kind, when the BOQ has no lines | No. Invented by the lane. | A sentence that explains the screen to an engineer who cannot act on it. The state title should name the state. | لا بنود في المقايسة (question 2) |
| `daily_log.location.refused` | الموقع (GPS) مغلق أو غير مسموح. شغّل الموقع من إعدادات الجهاز ثم اضغط الحضور. | Daily log, banner after a refused check-in | Yes, verbatim | Instruction sentence, but it tells the engineer the one thing he can do, and it is approved. | Keep per mock |
| `daily_log.failed.nothing_changed` | لم يتغير شيء. | Both screens, after a failed load | Yes | Reassurance sentence on a banner. Approved. | Keep per mock |
| `daily_log.empty` | لم يُسجَّل شيء في يومية {day} بعد. | Daily log, empty list | Yes | A sentence where the crew link says only «لم يُسجَّل أحد بعد». Approved. | Keep per mock |
| `crew.max_hours` | الحد الأقصى {max} ساعة | Crew, under a refused value | Yes | A rule shown as a message, short. Fine. | Keep |

The three mock-approved sentences (location, nothing_changed, empty) are the same kind as `boq.sign.lead` on 2026-11-11: approved text against the standing rule. Question 3 asks Nabil once.

Other sentences reach the screen from the server (`errors.daily_log.*`). They are not in the mocks. See G.

## C. Punctuation

| Key | Current | Problem | Suggested |
|---|---|---|---|
| (whole module) | Banner sentences carry a period (`daily_log.failed`, `crew.failed`, `daily_log.location.refused_title`, every `errors.*`). State titles and row messages carry none (`daily_log.before`, `daily_log.closed`, `crew.empty`, `crew.max_hours`, `crew.row_failed`). | The mock mixes them the same way: a period where a bold title sits beside a second sentence, none where the text stands alone. Consistent with the mock, so no defect. The only join that breaks is `crew.row_failed` (A). | Nabil rules once: period on every sentence or on none (question 12; the same question stayed open on 2026-11-11 for `errors.master.*`) |

## D. One meaning, two words (all questions for Nabil)

| Keys | Current | Problem | Suggestion |
|---|---|---|---|
| `daily_log.title` and `daily_log.empty` vs spec §14 | اليومية = the daily log. Spec §14: يومية = day labour (`DayLabour`). `errors.daily_log.worker_not_found` says «عامل اليومية». | One word, two things. On site يومية usually means the day's wage. Nabil himself wrote «Daily log (اليومية)» in D-217 and the mock title is اليومية, so this is a question, not a defect. | Ask: keep اليومية, or «التقرير اليومي» / «يومية الموقع» for the log. If kept, the crew link «العمالة» under it keeps the two apart. |
| `daily_log.stopped` (project banner) vs `daily_log.kind.stoppage` / `.group.stoppage` / `.form.stoppage` | المشروع متوقف vs سبب التوقف | Two ideas share a root. The banner is the project status Stopped. The log entry is a day's stoppage that does not change the status (D-217 item 14). Kaff's own word for a stuck project is متعثرة. Same open question as 2026-11-11 section D (Q18). | Nabil rules whether the Stopped banner reads متعثر. If not, the two uses stay apart. |
| `errors.daily_log.not_found` «سجل يومي», `errors.daily_log.worker_not_found` «عامل اليومية», screen «اليومية» | | Three names for the log. The not_found text never reaches the screen (404 is swallowed by `getMine`). | Align when the key is next touched. |

No البند clash: «البند» is the BOQ line only (`daily_log.quantity.line`, `picker.title`, `picker.search`). The entry type is «نوع الإدخال». `errors.daily_log.boq_line_not_found` says «بند المقايسة», same meaning.

## E. Construction practice (my suggestion, not Kaff's ruling)

| Key | Current | Where it shows | Note | Suggested |
|---|---|---|---|---|
| `daily_log.back_dated` | بأثر رجعي | Pill on a past-day entry, daily log and crew, for the supervising engineer and owner | An accounting phrase (retroactive effect). On site the same idea is «مُسجَّل بعد اليوم» or «سُجّل لاحقاً». In the mock. | سُجّل لاحقاً (question 6) |
| `daily_log.unit.metre` | متر | Material unit list | On site a linear metre is «متر طولي» (م.ط) and an area «متر مربع»; a bare «متر» is ambiguous beside م². Karim confirms the list (D-217 item 17). | م.ط or متر طولي |
| `daily_log.unit.bag` | كيس | Material unit list | Cement on Egyptian sites is «شكارة» (also كيس). Karim's list. | Ask Karim with the rest of the unit list |
| `hr.worker.engagement.engage_action`, `crew.sheet.once`, `crew.sheet.times` | تشغيل, شُغّل مرة واحدة, شُغّل {count} مرات | Crew add-worker sheet | The foreman says «اشتغل معانا». «شُغّل» is clear and in the mock. Plural issue in I. | Keep; fix only the count wording (I) |
| `daily_log.field.check_in` / `check_out` | الحضور / الانصراف | Check-in button, attendance strip | Matches how crews are spoken about on site. | Keep |
| `daily_log.group.quantities` | الكميات التقديرية | Log group heading | Site says «الكميات المنفذة». Nabil chose التقديرية (D-217 item 21, approved mock). | Keep |

## F. Dead keys (grep of `src/**/*.ts|html`, specs excluded)

- `daily_log.*` (62 keys) and `crew.*` (16 keys): no dead key. Every key is used as a quoted literal in a template or `.ts`. Dynamic building checked: `daily_log.unit.*` is a literal array (`UNIT_KEYS`), `daily_log.day.switch` is a default input, `daily_log.day.today|yesterday` are literals in `site-days.ts`, `daily_log.title` is used by `kaff-project-links.ts`. No `'daily_log.' + x` pattern found.
- The 29 `daily_log.*` keys removed in this range are not referenced anywhere in `src/`.
- `errors.daily_log.*` (28 keys): the server emits all 28 (`DailyLogErrors.cs`), so none is dead. The key reaches the screen through the server's `messageKey`. But the text of these never shows on these two screens:
  - Intercepted before display: `project_state_closed_to_logs` (becomes the closed state), `location_required` (becomes the location banner), `not_found` (404 swallowed by `getMine`).
  - Not reachable from the built forms: `entry_outside_window`, `check_in_only_today` (day switch allows today and 3 days back, check-in only today), `entry_id_taken`, `entry_id_required`, `occurred_at_required`, `entry_on_another_day` (the device generates these), `unknown_field`, `check_in_uses_check_in_route` (no free-field form), `file_reference_required` (no photo form), `expense_must_be_positive` (no expense form), `weather_retired` (the weather kind is gone from the screen), `cannot_correct_a_correction` (the edit button shows only when `canCorrect`).
  Leave them: another client may reach them. Do not spend Nabil's time on their wording.

## G. Developer talk and contradicting labels

Server messages that can show on screen and read as the build team speaking, or are heavy for a site engineer. None is in a mock.

| Key | Current | Problem | Suggested |
|---|---|---|---|
| `errors.daily_log.boq_not_frozen` | لا تُسجَّل الكميات المنفذة إلا بعد توقيع التعاقد وتجميد المقايسة. | «تجميد» is system talk. D-217 says the BOQ is what is signed. | لا تُسجَّل الكميات قبل توقيع المقايسة |
| `errors.daily_log.check_in_required` | سجّل حضورك اليوم في هذا المشروع أولاً، ثم أدخل البيانات. | The screen already shows only the check-in first (AC-1). Reachable only when the check-in happened on another device. Long. | سجّل حضورك أولاً |
| `errors.daily_log.location_required` | تسجيل الحضور يحتاج إلى مكانك. فعّل إذن تحديد المكان للمتصفح من إعدادات الهاتف ثم حاول مرة أخرى. | Two wordings of one instruction. The screen shows `daily_log.location.refused` instead. Text never shows. | Align to `daily_log.location.refused` |
| `errors.daily_log.check_in_uses_check_in_route` | سجّل الحضور والانصراف من زر الحضور والانصراف، فهو يسجّل مكانك ووقتك. | «route» speaking. The built buttons are «الحضور» and «الانصراف», two buttons, not one «الحضور والانصراف». A label that contradicts the screen. | Unreachable. Delete the sentence or use «سجّل الحضور من زر الحضور» |
| `errors.daily_log.entry_id_taken` | معرّف هذا الإدخال يخص سجلاً آخر. | Developer talk (identifier). Unreachable. | تعذّر حفظ الإدخال |
| `errors.daily_log.entry_id_required` | الإدخال يحتاج إلى معرّف يولّده الجهاز. | Developer talk. Unreachable. | تعذّر حفظ الإدخال |
| `errors.daily_log.entry_on_another_day` | وقت هذا الإدخال يقع في يوم غير يوم السجل. | Developer talk. Unreachable. | هذا الإدخال لا يخص هذا اليوم |
| `errors.daily_log.file_reference_required` | الصورة تحتاج إلى مرجع ملف. | Developer talk («مرجع ملف»). The photo form does not exist yet. | الصورة لم تُرفع |
| `errors.daily_log.unknown_field` | حقل غير معروف في اليومية. | Developer talk. Unreachable. | تعذّر حفظ الإدخال |
| `errors.daily_log.occurred_at_required` | الإدخال يحتاج إلى وقت حدوثه. | Developer talk. Unreachable. | تعذّر حفظ الإدخال |
| `errors.daily_log.hours_out_of_range` | يجب أن تكون الساعات أكبر من صفر وحتى 16 ساعة، بخطوة نصف ساعة. | Reaches the screen (crew row failure, after the bold `crew.row_failed`). The limit 16 is typed into the sentence while `crew.max_hours` takes it from a constant: two copies of one rule. Plain wording, only long. | الساعات من نصف ساعة إلى 16 ساعة |

Reachable server errors that read fine: `quantity_must_be_positive`, `text_required`, `text_too_long`, `correction_not_allowed`, `correction_window_closed`, `nothing_to_correct`, `entry_not_found`, `worker_not_found`. `executed_quantity_below_zero` is slightly technical: «هذا التعديل يجعل الكمية المنفذة أقل من صفر» could be «الكمية المنفذة لا تقل عن صفر».

No built button contradicts its action: الحضور performs check-in, الانصراف check-out, حفظ / حفظ التعديل save, تعديل edit, تشغيل engage, أضف عاملاً opens the add sheet. The one label that contradicts its value is in A.

## H. Wording invented by a lane and not confirmed by Kaff

Mock-verbatim keys are excluded. This is the whole list for the two screens:

- `daily_log.quantity.no_lines` المقايسة لا تحتوي على بنود لتسجيل كمية عليها. (not in the mock)
- All 28 `errors.daily_log.*`: server text, in no mock, never reviewed by Nabil; 11 are in G with a suggestion.
- The English equivalents of the above.

The brief's list (b) is therefore mostly confirmed by the mock: `daily_log.location.refused*`, `daily_log.picker.done`, `daily_log.section.*`, `daily_log.photo`, the seven `daily_log.unit.*`, `daily_log.loading` and all sixteen `crew.*` are the mock's own words. They were built from an approved mock, not invented. Karim still has to confirm the unit list (D-217 item 17).

## I. Counts and plurals (known item a, sized)

`I18nService.t` replaces `{name}` only. There is no plural mechanism. Arabic forms: 1 singular, 2 dual, 3 to 10 plural, 11 and up singular accusative.

| Key | Current | Count value | 1 | 2 | 3-10 | 11+ | Where it breaks |
|---|---|---|---|---|---|---|---|
| `daily_log.crew.summary` {count} | {count} عمال · {hours} ساعة | men on the day | عامل واحد | عاملان | عمال | عاملاً | crew link on the daily log, any day with 1 or 2 men |
| `daily_log.crew.summary` {hours} | … ساعة | summed hours, halves allowed | ساعة واحدة | ساعتان | ساعات | ساعة | 3 to 10 hours total reads «8 ساعة». A one-man day of 8 hours reads «1 عمال · 8 ساعة», wrong twice. Decimals (7.5) are fine with ساعة. |
| `crew.men` | عمال | count under the figure on the crew totals | عامل | عاملان | عمال | عاملاً | Label under a figure, so it reads as a column title. Tolerable and in the mock; wrong for 1 and 2 if read as a unit. |
| `crew.hours` | ساعات | hours figure and list header | n/a | n/a | n/a | n/a | Used as a header too, so it must stay generic. No change. |
| `crew.sheet.times` | شُغّل {count} مرات | times a man was engaged | `crew.sheet.once` already handles 1 | شُغّل مرتين | مرات | شُغّل 12 مرة | add-worker sheet. 2 reads «شُغّل 2 مرات» (the mock has the same fault); 11+ should be «مرة». |
| `crew.max_hours` {max} | الحد الأقصى {max} ساعة | constant 16 | n/a | n/a | n/a | ساعة | Correct today. Breaks only if the limit drops below 11. |
| `crew.hours.minus|plus|input` | … ساعات {name} | name, not a count | | | | | No problem |
| `daily_log.group.*` count | badge number beside the name | | | | | | Number only, no noun, no problem |

English has the same problem: `daily_log.crew.summary` («1 men · 1 hours»).

Smallest repair without a plural mechanism: give each form its own key (`crew.men.one|two|few|many`) and pick the key in code. One small mechanism for all keys is cleaner, and only two sentences use it today. Mock-approved wording stays as the 3-10 form.

Also noted, not copy: the unit the daily log stores for a material is the localised label (`unitOptions` uses `i18n.t(key)` as the value), so an entry made in English stores «Bag», in Arabic «كيس». Worth a ticket.

## Mock states with no built key (not covered by copy)

- Photo kind: the mock has a fifth kind «صورة» with «التقاط صورة», «الوصف», «إزالة». The build has four kinds and shows existing photos only. Ask the Scrum Master whether AC-11 wants it.
- Crew «غير مُسنَد»: the mock has «هذا المشروع خارج تكليفك» and «أنت غير مُسنَد إلى هذا المشروع.» No `crew.*` key exists. I did not check whether the project frame handles it.

## Questions for Nabil

1. Quantity figure when no survey exists: label it «الكمية (تقديرية)» until a survey is recorded, or show a dash?
2. BOQ with no lines, in the daily log: «لا بنود في المقايسة», or keep the sentence?
3. Keep the three approved sentences on the daily log (location, nothing_changed, empty) or cut them to titles?
4. Does the «المشروع متوقف» banner read متعثر?
5. Keep «اليومية» for the daily log although spec §14 gives يومية to day labour?
6. «بأثر رجعي» or «سُجّل لاحقاً» for the back-dated pill?
7. Plural forms: dual and 11+ for crew count, hours and times engaged. Approve building them?
8. Unit list with Karim: متر vs م.ط, كيس vs شكارة.
9. Photo kind in the daily log form: in this release or later?
10. Crew «غير مُسنَد» state: confirm it is the frame's job.
11. Server error wording (G): may a lane replace the 11 developer-talk sentences?
12. Period or no period on sentences, once for the whole app.

## Brief corrections

- List (b): only `daily_log.quantity.no_lines` is invented. The rest are in the mock verbatim (checked against `daily-log.html` and `day-labour.html`).
- Known (a): confirmed and sized in I. Four keys are affected, two of them real sentences (`daily_log.crew.summary`, `crew.sheet.times`).
- "Sixteen `crew.*`" is right: 16 keys in both languages.
- Item (c) asked whether the mock has each sentence: answered in B.
- Safe fixes that only restore mock wording: none found. The one text change inside mock wording is the period after `crew.row_failed` (A), which adds a character the mock did not have.

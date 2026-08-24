# RTL and i18n — the rules, concretely

**Audience:** the Frontend agent, in a fresh session, with no memory of the session that wrote this.
**Authority:** `CLAUDE.md` ("RTL is the primary direction, not a mirror"), `decisions.md` D-036,
D-044 rulings 6 and 7, and `src/Web/src/app/core/i18n/i18n.service.ts`, which already implements most
of what follows. **Read the service before writing a component; do not reimplement any of it.**

---

## 0. The one-sentence version

The document opens `<html lang="ar" dir="rtl">`, `I18nService` flips that attribute when the locale
changes, and **that single attribute must be the only thing that flips the layout** — because every
directional property in the codebase is logical. If you ever need to write `[dir=rtl] .thing { … }`
to fix a layout, the layout is wrong, not the direction.

---

## 1. Logical properties — the substitution table

There is no `left` and no `right` in this codebase. Adding one breaks Arabic **silently** rather than
loudly, which is why this is a prohibition and not a preference.

### Box model and position

| Never write | Always write |
|---|---|
| `margin-left` / `margin-right` | `margin-inline-start` / `margin-inline-end` |
| `margin-top` / `margin-bottom` | `margin-block-start` / `margin-block-end` |
| `margin: 0 1rem` | `margin-inline: 1rem` |
| `padding-left` / `padding-right` | `padding-inline-start` / `padding-inline-end` |
| `padding: 0.5rem 1rem` | `padding-block: 0.5rem; padding-inline: 1rem` |
| `border-left` / `border-right` | `border-inline-start` / `border-inline-end` |
| `border-top` / `border-bottom` | `border-block-start` / `border-block-end` |
| `border-top-left-radius` | `border-start-start-radius` |
| `border-top-right-radius` | `border-start-end-radius` |
| `border-bottom-left-radius` | `border-end-start-radius` |
| `border-bottom-right-radius` | `border-end-end-radius` |
| `left:` / `right:` | `inset-inline-start:` / `inset-inline-end:` |
| `top:` / `bottom:` | `inset-block-start:` / `inset-block-end:` |
| `inset: 0 1rem` | `inset-block: 0; inset-inline: 1rem` |

### Size

| Never write | Always write |
|---|---|
| `width` / `min-width` / `max-width` | `inline-size` / `min-inline-size` / `max-inline-size` |
| `height` / `min-height` / `max-height` | `block-size` / `min-block-size` / `max-block-size` |

`min-block-size: 2.75rem` on every interactive control is how the 44px tap target is expressed today
(see `app.css`, `status-page.css`). Keep that.

### Alignment and flow

| Never write | Always write |
|---|---|
| `text-align: left` / `right` | `text-align: start` / `end` |
| `float: left` / `right` | Don't float. Use flex or grid. `float: inline-start` if you truly must |
| `justify-content: flex-start` | `justify-content: start` (identical, but say what you mean) |
| `flex-direction: row-reverse` "to fix RTL" | **Nothing.** `row` already flows right-to-left under `dir=rtl` |
| `background-position: left` | `background-position-x: start` is *not* widely safe — use a flex/grid slot instead |
| `scroll-padding-left` | `scroll-padding-inline-start` |
| `overflow-x` | `overflow-inline` where supported; `overflow-x` is acceptable for a scroll container because a scroll axis is physical |

### The properties that are **not** logical — handle by hand

These have no logical form and will point the wrong way in RTL if you use them naively:

| Property | What to do |
|---|---|
| `transform: translateX(…)` | Multiply by `-1` in RTL, or avoid it: animate `inset-inline-start` or use `margin-inline-start`. A drawer that slides in from the inline-start must slide from the **right** in Arabic. |
| `box-shadow: 4px 0 …` | Two rules under a `:dir(rtl)` guard, or use a symmetric shadow. Prefer symmetric. |
| `background-image` gradients with a direction | `linear-gradient(to inline-end, …)` is not universal; prefer vertical gradients or a symmetric one. |
| `clip-path`, SVG path data | Physical by nature. See §5 on icon mirroring. |
| `text-shadow` offsets | Same as `box-shadow`. Prefer symmetric or none. |
| `cursor: e-resize` etc. | Physical, and correct as physical — a resize handle points where it points. |

If you genuinely need a direction-conditional rule, the modern selector is `:dir(rtl)` — but **treat
reaching for it as a signal you chose a physical property one step earlier**, and fix that instead.

### Keyboard, which is also directional

Arrow keys in a horizontal widget (tabs, a segmented control, a stepper) are **visual**, not logical.
In RTL, `ArrowLeft` moves to the *next* item and `ArrowRight` to the *previous* one. Any keyboard
handler on a horizontal widget must read the effective direction:

```ts
private readonly i18n = inject(I18nService);
protected readonly isRtl = computed(() => this.i18n.direction() === 'rtl');
// …then map ArrowLeft/ArrowRight through isRtl() before deciding next/previous.
```

`Home` and `End` are logical already (first/last item) and need no adjustment.

---

## 2. Bidi isolation — Latin runs inside Arabic sentences

**This is D-036, and it was a shipped defect.** Without isolation, the Unicode bidi algorithm moves
the trailing punctuation of a Latin run to the wrong visual end: the project code `KF-2026-014`
renders as `014-2026-KF` inside an Arabic sentence. A phone number, an amount with a minus sign, an
email address and a code are all Latin runs.

### It is already solved for interpolated parameters — use it

`I18nService.t()` wraps **every** substituted `{param}` in U+2068 FIRST STRONG ISOLATE and U+2069 POP
DIRECTIONAL ISOLATE. So this is correct with no further work:

```html
{{ i18n.t('client.created', { name: client.name, phone: client.phoneEntered }) }}
```

The fix lives inside `t()` on purpose: plain characters survive text interpolation where a `<bdi>`
element would not, and putting it in one place means it cannot be forgotten per template.

### It is **not** solved for values you render directly

A bare interpolation of a value is not isolated:

```html
<!-- WRONG — the code's hyphens will migrate -->
<span>{{ project.code }}</span>

<!-- RIGHT — element-level isolation -->
<bdi>{{ project.code }}</bdi>
```

**Rules:**

1. Any standalone value that is or may be a Latin run — code, phone, email, IBAN, amount, username,
   file name, URL — is wrapped in `<bdi>`, or in an element carrying `unicode-bidi: isolate`.
2. Table cells holding figures use the existing `.figure` class from `styles.css`, which already sets
   `unicode-bidi: isolate`, `font-variant-numeric: tabular-nums`, `white-space: nowrap` and
   `text-align: end`. **Do not write a second one.**
3. Never build a sentence by concatenating in the template
   (`{{ i18n.t('x') }} {{ value }} {{ i18n.t('y') }}`). Put the placeholder in the catalogue string
   and pass it through `t()` — that is what gives you the isolate and what lets Arabic reorder the
   sentence.
4. Never wrap a whole paragraph of Arabic in `dir="ltr"` to "fix" a stray character. Isolate the
   character.

### Free-text the user typed

An Arabic notes field may legitimately contain an English sentence, and vice versa. Render user free
text with `dir="auto"` so the first strong character decides — never with a hardcoded direction.

---

## 3. Numbers, money, dates, phones

### The locale is pinned, and it is load-bearing

`ar-EG-u-nu-latn`. The default numbering system for `ar-EG` is `arab`, which renders `1,234.50` as
`١٬٢٣٤٫٥٠` with U+066B as the decimal separator. Kaff's staff read money in Western digits. A CSS
`font-variant-numeric` **cannot** change which digits `Intl` emits — an earlier version of
`styles.css` claimed it did and it did nothing. See D-036.

**Consequence for you:** never construct `Intl.NumberFormat` or `Intl.DateTimeFormat` yourself.
Always go through `I18nService`, which holds the pinned locale.

```ts
// WRONG — reintroduces Arabic-Indic digits
new Intl.NumberFormat('ar-EG').format(v);

// WRONG — a float formatted by hand, and money arithmetic in the client
(v / 100).toFixed(2);

// RIGHT
this.i18n.formatMoney(v);
this.i18n.formatNumber(v, { maximumFractionDigits: 3 });
this.i18n.formatDate(d, { dateStyle: 'medium' });
```

### Money — 4 stored, 2 displayed, 0 arithmetic

| Rule | Source |
|---|---|
| The backend stores `decimal(18,4)`. Display is **exactly** 2 decimals, minimum and maximum. | D-044 ruling 6 |
| Rounding happens at the last possible moment — in `formatMoney`. Rounding earlier lets display precision leak back into arithmetic. | D-044 ruling 6 |
| **The frontend performs no money arithmetic, ever.** Every subtotal, total, hold, recovery and net payable comes from the server, already computed. | Kickoff 2026-08-18 §3 |
| Never sum a column in the template to produce a footer total. Ask the API for it. | same |
| Currency is EGP and there is no conversion. A currency field exists; conversion logic does not. | `spec.md` §1, §16 assumption 12 |

The wire format for money is **still open** — see `questions.md` Q-UX-15. Until it is settled, treat any
money value arriving from the API as an opaque display value, not as a number you may operate on.

Negative money (a credit note, a deduction line) renders with the minus sign kept adjacent to the
digits by the `.figure` isolation. Do not render a negative as `(1,234.00)` parentheses — that is an
Anglo-accounting convention nobody has asked for.

### Dates — Gregorian, pinned

`formatDate` pins `calendar: 'gregory'`. `ar-EG` already defaults to Gregorian; it is pinned so a
contractual document cannot silently switch to Hijri after a browser or ICU update. **Do not remove
the pin and do not add a Hijri toggle** — nobody has asked for one.

| Use | Options |
|---|---|
| A date in a list or a form | `{ dateStyle: 'medium' }` |
| A date on a document (extract, contract) | `{ year: 'numeric', month: '2-digit', day: '2-digit' }` — unambiguous, and `-u-nu-latn` keeps it in Latin digits |
| A timestamp in the audit trail | `{ dateStyle: 'short', timeStyle: 'medium' }` |

Timestamps arrive as `DateTimeOffset`. Render them in the browser's zone; do not attempt a timezone
picker.

### Phone numbers

`PhoneNumber` (Domain) carries two fields: `Entered` — exactly what the user typed, kept for display
and for support calls — and `Normalised` — digits only, national form, the deduplication key.

| Rule |
|---|
| **Display `PhoneEntered`.** Never display the normalised form; it is an index key, not a presentation. |
| Wrap it in `<bdi>` and set `dir="ltr"` on the input. |
| Do not format, mask or group the digits in the UI. Normalisation belongs to the Domain, which folds Arabic-Indic digits, strips non-digits, and reduces `+20`/`0020` to national form. |
| `tel:` links are fine on mobile and should use the normalised form. |

---

## 4. Input direction — per field, deliberately

The **form** is RTL. Individual fields are not, and getting this wrong makes a phone number
unreadable.

| Field | `dir` | `inputmode` | Notes |
|---|---|---|---|
| Arabic name (client, project, person) | `auto` | — | `auto` so a Latin company name still reads correctly |
| Free-text notes, reasons, descriptions | `auto` | — | |
| Username | `ltr` | — | `autocomplete="username"` |
| Password | `ltr` | — | `autocomplete="current-password"` / `"new-password"` |
| Email | `ltr` | `email` | `autocomplete="email"` |
| Phone | `ltr` | `tel` | `autocomplete="tel"` |
| Code (client code, project code, catalogue code) | `ltr` | — | `spellcheck="false"`, `autocapitalize="characters"` if the API uppercases |
| Money amount | `ltr` | `decimal` | see `components.md` → money input |
| Quantity / integer | `ltr` | `numeric` | |
| Date | `ltr` | — | native `<input type="date">` is LTR by definition |
| Search box | `auto` | `search` | staff search Arabic names and Latin codes in the same box |

Two supporting rules:

- A field with `dir="ltr"` still sits in an RTL form: its **label is above or to its inline-start
  (right)**, and the field's own text runs left-to-right inside its box. Do not mirror the field's
  position to "match" its content direction.
- Placeholders are never a substitute for a label. Every input has a real `<label for>`.

---

## 5. Icon mirroring — which flip and which must not

The test is not "does it look symmetrical". The test is: **does the icon depict direction of
movement/reading, or does it depict an object or a convention?** Direction-of-reading flips. Objects
and universal conventions do not.

### Flip in RTL

| Icon | Why |
|---|---|
| Back / forward arrows | Back means "toward where I came from", which is the reading origin |
| Next / previous, pagination chevrons | reading order |
| Breadcrumb separator (`›`) | reading order |
| Tree expand/collapse chevron (collapsed state) | points along the reading direction |
| List indent / outdent | reading order |
| Undo / redo curved arrows | they mean "backwards/forwards through time", mapped to reading order |
| Drawer and sheet slide-in direction | a side panel opens from the inline-start edge = the **right** in Arabic |
| Progress bar fill, stepper, and any "% complete" meter | fills from inline-start = right |
| Reply / forward / send arrows | reading order |
| Tab order and the active-tab indicator | reading order |
| Trend arrows on a comparison ("up from last month") — *only the horizontal component* | vertical trend arrows do not flip at all |

### Do **not** flip in RTL

| Icon | Why |
|---|---|
| **Clock / time** | A clock face runs clockwise in every locale. Flipping it produces a clock that does not exist. |
| Checkmark ✓ | A convention, not a direction |
| Media playback: play, fast-forward, rewind, skip | Playback controls follow the media timeline, which is not the reading direction. Flipping play makes it read as "rewind". |
| Camera, paperclip, printer, folder, trash, building, person, calendar page | Objects |
| Numerals and glyph shapes | See §3 — the numbering system is pinned |
| The Kaff logo / wordmark | A mark is a mark |
| Volume, wifi, battery | Conventions |
| Magnifier (search) | Cosmetic either way — pick one and keep it consistent. **Do not** flip it in half the components |
| Vertical arrows: sort ascending/descending, up/down, chevron for a `<select>` | Vertical, so direction-neutral |

### Undecided, and worth naming before the first one is built

- **Time-series charts.** Should a time axis run right-to-left (reading order) or left-to-right
  (near-universal chart convention)? No chart exists before slice 7. Raised as `questions.md` Q-UX-13 —
  do not decide it in a component.

### How to implement a flip

Use one mechanism throughout: a CSS class that scales the X axis, applied by direction.

```css
.icon-directional:dir(rtl) { transform: scaleX(-1); }
```

Never ship two SVG files for the same icon, and never flip an icon by swapping the component's
`name` input in the template — a reader cannot tell that from a bug.

---

## 6. i18n key naming

The catalogues live at `src/Web/public/locales/ar.json` and `en.json`, are loaded at bootstrap before
the first render (`provideAppInitializer`), and are resolved through `I18nService.t()`. A missing key
renders as the key itself and warns once in the console — that is deliberate, so a gap is visible
rather than blank.

### The convention already in the file, extended

`<namespace>.<subject>.<element>` — lowercase, dot-separated, `snake_case` inside a segment.

| Namespace | For | Example |
|---|---|---|
| `app.*` | shell chrome | `app.name`, `app.language` |
| `nav.*` | navigation labels, one per destination | `nav.clients`, `nav.users`, `nav.audit` |
| `<feature>.title` / `.subtitle` | page headings | `clients.title`, `users.create.title` |
| `<feature>.field.*` | form field labels | `clients.field.phone`, `users.field.role` |
| `<feature>.hint.*` | helper text under a field | `clients.hint.phone_is_the_key` |
| `<feature>.action.*` | buttons scoped to a feature | `clients.action.create` |
| `action.*` | generic buttons reused everywhere | `action.save`, `action.cancel`, `action.retry`, `action.back` |
| `<feature>.empty.*` | empty states | `clients.empty.title`, `clients.empty.body` |
| `<feature>.confirm.*` | confirmation dialogs | `users.confirm.deactivate.title` |
| `enum.<Type>.<Member>` | server enums rendered as text | `enum.Role.Owner`, `enum.AssignmentLevel.Supervisor` |
| `errors.<domain>.<code>` | **server-returned** message keys — mirror `Error` codes exactly | `errors.identity.username_required` |
| `validation.*` | client-side validation before a request is sent | `validation.required`, `validation.email_invalid` |
| `a11y.*` | screen-reader-only text and ARIA labels | `a11y.close_dialog`, `a11y.sort_ascending` |
| `status.kaff.*` | Kaff's verbatim vocabulary — see §7 | `status.kaff.in_progress` |

### Hard rules

1. **`errors.*` keys are owned by the backend.** A `ProblemDetails` carries `messageKey`, never
   prose — `CLAUDE.md` forbids the server sending user-facing sentences. When a backend error key
   appears, add it to **both** catalogues; do not rename it to fit a UI convention.
2. **Every key exists in both `ar.json` and `en.json`.** English is a development convenience, but a
   missing English key renders as a raw key in review and wastes a reviewer's time.
3. **Never compose a sentence from two keys.** Use `{placeholders}` — that is also what gives you the
   bidi isolate (§2).
4. **Never key on a value.** `clients.kind.corporate` is right; `clients.kind.{{ kind }}` built by
   string concatenation in a template is not — build the key in the component with an exhaustive
   `switch` so a new enum member is a compile error under `strictTemplates`.
5. Pluralisation: Arabic has six plural forms. **No screen in slices 1–8 needs a pluralised count
   string.** Prefer `{count}` beside a static noun (`clients.count_label` → `العملاء: {count}`) over
   inventing a plural mechanism. If a genuine plural requirement appears, raise it — do not add a
   pluralisation library.

---

## 7. The Kaff status vocabulary — how it is keyed, and what it is not

`CLAUDE.md`: these five words appear **verbatim** in the UI. No translations, no substitutes. They
already exist in both catalogues, in Arabic in both, with a `_note` explaining why English is not
translated:

```
status.kaff.not_started   لم تبدأ
status.kaff.in_progress   جاري العمل
status.kaff.finished      انتهت
status.kaff.troubled      متعثرة
status.kaff.postponed     تم تأجيلها
```

### They are not one thing — D-044 ruling 7

**متعثرة and تم تأجيلها are health tags, not states.** A struggling project stays structurally
`Active`. This matters more than it looks: the obvious mapping was onto `Stopped`, and `spec.md` §7
says a stopped project MUST NOT issue extracts — so flagging a project as متعثرة would have frozen
the material purchases and subcontractor payments meant to unstick it. The flag would have caused the
problem it describes.

Design consequence:

- **Two distinct components** (`components.md` §7): a *status chip* rendering one of the three, and a
  *health tag* rendering zero or one of the two. They are visually different and never occupy the
  same slot.
- A project may carry **a status and a health tag at the same time**: جاري العمل + متعثرة is a
  legitimate, expected combination.
- Do not sort, filter or group a list as if the five were one enum.

### What is still unknown

Whether the three map onto `ProjectStatus` (`Setup → Active → HandoverPending → Handover →
UnderWarranty → Closed`) at all is **slice 4's question and it is open** — kickoff question 7 asks
Karim what انتهت means (site-finished, or file-closed-and-money-collected). Until it is answered:

> **Nobody puts a project status chip on any slice-1 screen "because it's useful."**
> — slice 1 kickoff, §7. A guessed mapping born on an assignment screen would be indistinguishable
> from a decision.

The chip component may be *built* in slice 1 (it is a presentational component with a fixed
vocabulary). It may not be *placed* on a slice-1 screen.

### One known defect in the continuity files

`CLAUDE.md` writes تم تأجيلها; `agents.md` writes متأجلة. Two spellings of a word required "verbatim"
is a defect in the continuity files, tracked as action A1b, awaiting Karim's word. **The catalogues
currently use تم تأجيلها, which matches `CLAUDE.md` — the authoritative file for conventions. Do not
change it on your own initiative.** (`questions.md` Q-UX-12.)

---

## 8. Fonts, line height and the things `styles.css` deliberately does not do

Already decided in `src/Web/src/styles.css`; do not relitigate:

- `--font-body` puts system Arabic faces first with Latin fallbacks after, so mixed content stays
  legible on Windows.
- `--line-height: 1.7` — Arabic needs more leading than Latin at the same size.
- **Numerals are not set in CSS.** Which digits appear is decided by the numbering system `Intl` is
  given. See §3 and D-036.
- `font-feature-settings: "ss01"` was removed: a stylistic set means something different in every
  font, and applying an unnamed one to all Arabic text in a production system is a gamble on
  whichever family resolves.
- Arabic text must never be styled with `text-transform: uppercase`, `letter-spacing`, or
  `font-synthesis` bolding — the first two are meaningless-to-harmful for Arabic script, and
  synthesised bold destroys the join.
- Do not use `<i>` or `<em>` for emphasis on Arabic. Use weight or colour.

---

## 9. The checklist before you say an RTL screen is done

- [ ] `grep -n "margin-left\|margin-right\|padding-left\|padding-right\|text-align: *left\|text-align: *right\|float:\|\bleft:\|\bright:" ` over your CSS returns nothing.
- [ ] Every user-facing string is `i18n.t('key')`, in both catalogues.
- [ ] Every standalone code / phone / email / amount is inside `<bdi>` or `.figure`.
- [ ] Every input carries the `dir` from §4.
- [ ] Every interactive control is at least `2.75rem` on its block axis.
- [ ] At 390px there is **no horizontal overflow on the page body** — wide tables scroll inside their
      own container (`components.md` §8).
- [ ] The English locale still renders correctly. Switching to `en` is the cheapest way to catch a
      hardcoded Arabic string.
- [ ] No directional icon is flipped that §5 says must not be.

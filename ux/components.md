# Shared components

**One of each. Do not build a second form field.** Every component below is standalone, signal-based,
`OnPush`, and lives under `src/Web/src/app/shared/`. Feature folders own screens; they do not own
input controls.

---

## The Angular 22 shape — the same in every component

`CLAUDE.md` names mixing Angular eras as the main frontend risk on this project. The existing files
(`app.ts`, `status-page.ts`) are the reference; match them exactly.

```ts
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';

@Component({
  selector: 'kaff-thing',
  // No `standalone: true` — standalone is the default in this Angular version, and the existing
  // components omit it. Adding it back is era-mixing noise.
  imports: [/* only what the template uses */],
  templateUrl: './thing.html',
  styleUrl: './thing.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Thing {
  protected readonly i18n = inject(I18nService);   // inject(), never constructor injection

  readonly labelKey = input.required<string>();     // input(), never @Input()
  readonly disabled = input(false);
  readonly value = model<string>('');               // model() for two-way
  readonly confirmed = output<void>();              // output(), never @Output()/EventEmitter

  private readonly touched = signal(false);         // signals, never BehaviorSubject
  protected readonly showError = computed(() => this.touched() && this.errorKey() !== null);
}
```

**Prohibitions, restated because they are cheap to violate:**

| Never | Always |
|---|---|
| `NgModule` | standalone components |
| `*ngIf` / `*ngFor` | `@if` / `@for` — and `@for` **requires** `track` |
| `@Input()` / `@Output()` / `EventEmitter` | `input()` / `input.required()` / `model()` / `output()` |
| `BehaviorSubject` for component state | `signal()` / `computed()` |
| constructor injection | `inject()` |
| Zone.js, `ngZone.run`, `setTimeout` to force a render | signals; the app is zoneless |
| a hardcoded string in a template | `i18n.t('key')` |
| `margin-left`, `text-align: right`, `left:` | logical properties — `rtl-and-i18n.md` §1 |

`strictTemplates` is on. If a template needs `$any(...)`, the component's types are wrong — narrow a
discriminated union into `computed` signals instead, the way `status-page.ts` does.

---

## Baseline requirements for every component

### Tap targets — 44px, everywhere, not only on mobile

Every interactive element has `min-block-size: 2.75rem` (44px). That includes rows in a list that act
as links, chips used as filters, icon-only buttons, and radio/checkbox **labels** — the label is the
target, not the 16px box.

Add the token once and use it:

```css
:root { --tap-target: 2.75rem; }
```

Icon-only buttons additionally need `min-inline-size: var(--tap-target)`. Two adjacent targets need at
least `--space-2` between them; a mis-tap on a "revoke assignment" button costs somebody an afternoon.

### Accessibility — the non-negotiable set

- Every input has a real `<label for>`. A placeholder is not a label; a `title` is not a label.
- Errors: `aria-invalid="true"` on the control, message in an element referenced by
  `aria-describedby`, and the message is `role="alert"` only for form-level banners (a per-field alert
  on every keystroke is a screen-reader machine gun).
- Async results announce through a single `aria-live="polite"` region in the shell, not per component.
- Dialogs: `role="dialog"`, `aria-modal="true"`, `aria-labelledby` pointing at the title, focus moves
  into the dialog on open and **returns to the trigger on close**, `Escape` closes, focus is trapped.
- Icon-only buttons carry `aria-label` from an `a11y.*` key.
- Never convey state by colour alone — the inactive chip carries the word "inactive", not just grey.
- Focus is visible: `styles.css` already sets `:focus-visible { outline: 2px solid var(--color-accent) }`.
  Do not remove it, and do not replace it with a box-shadow that disappears on a coloured background.
- Contrast: 4.5:1 for text, 3:1 for a control boundary, in **both** the light and dark palettes
  already in `styles.css`.

---

## 1. Form field (`kaff-field`)

The wrapper every input sits in. It owns the label, the hint, the error, and the wiring between them.
Nothing else may render a label.

```ts
readonly labelKey     = input.required<string>();
readonly hintKey      = input<string | null>(null);
readonly errorKey     = input<string | null>(null);   // an i18n key, from the server or validation.*
readonly required     = input(false);
readonly controlId    = input.required<string>();
```

```
                                          Label *      ← inline-start (right), `*` after the label
┌────────────────────────────────────────────────────┐
│  <ng-content> — the control                        │
└────────────────────────────────────────────────────┘
  Hint text, or the error when there is one            ← muted; error colour when errorKey is set
```

- The error **replaces** the hint rather than stacking, so the field does not reflow and push the
  submit button under the user's thumb mid-tap.
- `aria-describedby` points at whichever of the two is rendered.
- The required marker is `*` plus `a11y.required` for screen readers — never colour alone.
- Layout is always label-above-control. A two-column label/control layout on desktop is a later
  refinement and must not be the first thing built; it complicates RTL for no slice-1 gain.

## 2. Money input (`kaff-money-input`)

**4 decimals stored, 2 displayed, and no arithmetic in the client** — `decisions.md` D-044 ruling 6,
kickoff 2026-08-18 §3.

```ts
readonly value = model<string>('');   // a STRING. Not a number.
readonly currencyKey = input('currency.egp');
```

```
                                          Amount *
┌────────────────────────────────────────────────────┐
│ 1234.50                                     EGP    │   dir=ltr, inputmode=decimal
└────────────────────────────────────────────────────┘   suffix at the field's LEFT
```

| Rule | Why |
|---|---|
| The bound value is a **string**, and it is never `parseFloat`ed. | A `decimal(18,4)` does not survive a round trip through a JavaScript `double`. |
| The component performs no arithmetic — no totals, no percentages, no VAT, no net. | Every total comes from the server. |
| On blur, display formats to 2 decimals via `i18n.formatMoney`; the **submitted** value is the raw entry, unformatted, without group separators. | Display precision must not leak into arithmetic. |
| `dir="ltr"`, `inputmode="decimal"`, `autocomplete="off"`. | `rtl-and-i18n.md` §4. |
| Accept `.` as the decimal separator, and fold Arabic-Indic digits on input the way the Domain does for phones. | A phone keyboard in Arabic can emit `٫` and `٠`–`٩`. |
| Never a spinner (`<input type="number">`). | Arrow keys silently changing an amount is a defect waiting for a witness. |
| A read-only money **display** is not this component — it is `.figure` from `styles.css`. | One class already does isolation + tabular numerals. |

**The wire format for money is open** (`questions.md` Q-UX-15). Until it is settled, treat every money
value from the API as an opaque string for display.

## 3. Phone input (`kaff-phone-input`)

```
                                          Phone *
┌────────────────────────────────────────────────────┐
│ 0101 234 5678                                      │   dir=ltr, inputmode=tel
└────────────────────────────────────────────────────┘
  Clients are matched by phone.                          hint, on the client form
```

- Submits **exactly what the user typed**. `PhoneNumber` in the Domain does the normalising — folding
  Arabic-Indic digits, stripping non-digits, reducing `+20`/`0020` to national form. **Do not
  reimplement that in TypeScript**; two normalisers that disagree is a duplicate master record.
- No input mask and no auto-formatting. A mask fights `+20`, a landline, and a paste.
- `autocomplete="tel"`, `dir="ltr"`, `<bdi>` when the value is rendered read-only.
- Optional duplicate check on blur (`slice-1-flows.md` S-013), emitted as an output — the component
  does not know what a duplicate means.

Errors it renders: `errors.phone.required` · `errors.phone.too_long` · `errors.phone.too_short`.

## 4. Date picker (`kaff-date-field`)

**Gregorian, pinned.** `I18nService.formatDate` pins `calendar: 'gregory'` deliberately: `ar-EG`
already defaults to it, and a contractual document silently switching to Hijri after a browser or ICU
update is not a risk worth carrying to save one property. **Do not add a Hijri toggle. Nobody asked.**

- Use the **native `<input type="date">`** as the control. On a building site the OS date picker is
  faster, larger, familiar, and already localised; a custom calendar grid is an RTL keyboard-navigation
  project nobody scheduled.
- The native control is LTR by definition. That is correct — see `rtl-and-i18n.md` §4.
- Read-only display uses `i18n.formatDate(value, { dateStyle: 'medium' })`, never `toLocaleDateString`
  called directly (that would lose the pinned locale and reintroduce Arabic-Indic digits).
- A date range is two fields with `from`/`to` labels, and the component validates only that `from ≤ to`
  (`validation.date_range_invalid`). Any other date rule is a business rule and belongs to a feature.

## 5. Status chip (`kaff-status-chip`) — the three Kaff states

```
[ لم تبدأ ]    [ جاري العمل ]    [ انتهت ]
```

```ts
type KaffStatus = 'not_started' | 'in_progress' | 'finished';
readonly status = input.required<KaffStatus>();
// label = i18n.t(`status.kaff.${status()}`) — built in the component with an exhaustive switch,
// never by concatenating in the template.
```

- Renders the vocabulary **verbatim**, in Arabic, in both locales. The catalogues already carry the
  five keys and `_note`s explaining why English is not translated.
- Colour is supporting, never the message: neutral / accent / success, and the word is always present.
- **It may be built in slice 1. It may not be placed on a slice-1 screen.** Slice-1 kickoff §7:
  "Nobody puts a project status chip on any slice-1 screen 'because it's useful'." The mapping onto
  `ProjectStatus` is open (kickoff question 7), and a guessed mapping born on an assignment screen
  would be indistinguishable from a decision.

## 6. Health tag (`kaff-health-tag`) — the two that are **not** states

```
[ متعثرة ]    [ تم تأجيلها ]
```

`decisions.md` D-044 ruling 7: **متعثرة and تم تأجيلها are health tags, not states.** A struggling
project stays structurally `Active`. The obvious mapping was onto `Stopped`, and `spec.md` §7 says a
stopped project MUST NOT issue extracts — so the flag would have frozen the material purchases and
subcontractor payments meant to unstick the project. The flag would have caused the problem it
describes.

- A **separate component** from the status chip, visually distinct (outlined, warning-toned), and it
  never occupies the same slot.
- A project may show a status **and** a health tag simultaneously: جاري العمل + متعثرة is expected.
- Zero or one tag. Whether both can apply at once is not stated anywhere; do not build for two until
  somebody says so.
- The tag itself is **slice 4's** — `Project` is deliberately thin and no column exists.

## 7. Button (`kaff-button`)

Not glamorous, but it is where the tap target and the RTL action order get decided once.

```ts
readonly variant = input<'primary' | 'secondary' | 'danger' | 'ghost'>('secondary');
readonly busy    = input(false);
readonly type    = input<'button' | 'submit'>('button');
```

- `min-block-size: var(--tap-target)`, `touch-action: manipulation` (already on `button` globally).
- `busy` sets `disabled` **and** `aria-busy`, and keeps the label visible — a spinner that replaces the
  label makes the button change width mid-tap.
- **Action order in RTL**: the primary sits at the **inline-start (right)** of the row, the secondary
  toward the inline-end. Because the row is a flex container in a `dir=rtl` document, this is simply
  the DOM order primary-then-secondary with `flex-direction: row`. **Do not use `row-reverse`.**
- On mobile, actions stack full-width with the primary on top.
- A destructive action is never the default focus target in a dialog.

## 8. Data table (`kaff-table`) — with RTL column order

```
Desktop, dir=rtl — reading right to left:

│  Name          Code        Kind        Phone        State     [actions] │
│  ───────────────────────────────────────────────────────────────────────│
│  Client name   KF-C-0042   Corporate   0101…        Active    [ … ]     │
   ↑ first column = inline-start = RIGHT              actions at inline-end = LEFT
```

| Rule | Detail |
|---|---|
| Column order is **DOM order**. In `dir=rtl` the first `<th>` renders at the right. Do not reverse the array. |
| Identity first | The name / primary identifier is the first column, at the inline-start. |
| Actions last | At the inline-end, so a mis-tap while scanning identity does not hit "delete". |
| Figures | `class="figure"` — `text-align: end`, `tabular-nums`, `unicode-bidi: isolate`. Already in `styles.css`. |
| Codes, phones, ids | `<bdi>`. |
| Overflow | The table scrolls inside its own `overflow-x: auto` container. **The page body must never scroll horizontally at 390px.** |
| Mobile | Below ~640px the table becomes the card list shown throughout `slice-1-flows.md`. Same data, same DOM order, no horizontal scroll for the primary reading path. |
| Sorting | A `<th>` sort control carries `aria-sort` and an `a11y.sort_ascending` / `_descending` label. The arrow is **vertical**, so it does not flip. |
| Selection | Only if a feature needs bulk actions. Slice 1 does not. Do not build it. |
| Paging | Cursor-based (`action.load_more`) for anything append-only, like the audit trail. |

`@for` over rows **must** `track` a stable id — never `$index`, which re-renders the world when a row
is inserted.

## 9. Empty state (`kaff-empty-state`)

```
┌────────────────────────────────────────────────────┐
│                                                    │
│                  (optional icon)                   │
│               Nothing here yet — title             │
│        One sentence saying what would put          │
│        something here.                             │
│                 ┌──────────────┐                   │
│                 │   Action     │                   │
│                 └──────────────┘                   │
└────────────────────────────────────────────────────┘
```

```ts
readonly titleKey  = input.required<string>();
readonly bodyKey   = input<string | null>(null);
readonly actionKey = input<string | null>(null);
readonly action    = output<void>();
```

Three distinct empty states, and conflating them is the usual mistake:

| State | Message | Action |
|---|---|---|
| **Nothing exists yet** | `<feature>.empty.title` / `.body` | the create action, if the user holds it |
| **Nothing matches the filter** | `<feature>.empty.filtered.title` / `.body` | `action.clear_filters` |
| **You may not see this** | Not an empty state at all — it is a refusal. Use the error banner / S-016. | `action.back` |

**An empty state never renders sample or placeholder rows.** `spec.md` §4.5 makes this explicit for
the BOQ — "Empty BOQ shows an explicit empty state. Never phantom pre-filled rows" — and the reason
generalises: a phantom row is indistinguishable from real data at a glance.

## 10. Error banner (`kaff-error-banner`)

One component, the three modes of S-016 plus the inline form-level case.

```ts
readonly messageKey = input.required<string>();
readonly tone       = input<'error' | 'warning'>('error');
readonly retry      = output<void>();      // rendered only when retryable
readonly retryable  = input(false);
```

```
┌────────────────────────────────────────────────────┐
│ (!)  Message resolved from an i18n key      [Retry]│   icon at inline-start (right)
└────────────────────────────────────────────────────┘   action at inline-end (left)
```

| Rule |
|---|
| The input is a **key**, never a sentence. The API returns `messageKey` because `CLAUDE.md` forbids the server sending user-facing prose; `toProblem()` already extracts it. |
| `role="alert"` so it announces. |
| **A 403 is never retryable.** Retrying a refusal is theatre and teaches users to hammer a button. |
| A 5xx or network failure **is** retryable. |
| Never render a raw status code, a stack trace, or a correlation id as the primary message. The correlation id may appear as small copyable secondary text — support needs it. |
| Colour plus icon plus text. Never colour alone. |

## 11. Confirmation dialog (`kaff-confirm-dialog`)

```
┌────────────────────────────────────────────────────┐
│                          Deactivate this user?     │  title, aria-labelledby
│                                                    │
│   What happens, in one or two sentences, in the    │  body — says the CONSEQUENCE
│   user's language.                                 │
│                                                    │
│  ┌───────────────────┐  ┌───────────────────┐      │
│  │    Deactivate     │  │      Cancel       │      │  primary at inline-start (RIGHT)
│  └───────────────────┘  └───────────────────┘      │
└────────────────────────────────────────────────────┘
```

```ts
readonly titleKey   = input.required<string>();
readonly bodyKey    = input.required<string>();
readonly confirmKey = input('action.confirm');
readonly destructive = input(false);
readonly requiresReason = input(false);          // see below
readonly confirmed  = output<{ reason?: string }>();
```

| Rule |
|---|
| The body states the **consequence**, not the mechanic. "This user will not be able to sign in" beats "Set IsActive = false". |
| Focus moves to the dialog on open, is trapped, and **returns to the trigger** on close. `Escape` cancels. |
| The destructive button is **not** the initially focused element. |
| On mobile the dialog is a full-width sheet entering from the block-end (bottom), with the actions stacked and the primary on top. |
| **`requiresReason` is not decoration.** `spec.md` §7: "Any rejection at any gate returns the extract to Draft with a written reason… Never a silent step-back." §3: Closed Lost MUST record a reason. When a reason is required the confirm button stays disabled until the field is non-empty, and the reason is submitted with the action so the audit record carries the *why*. |
| Never use a confirmation dialog as a substitute for a permission. The server still refuses. |

## 12. Search input (`kaff-search-input`)

- `dir="auto"` — staff search Arabic names and Latin codes in the same box.
- `inputmode="search"`, `type="search"`, debounced ~300ms, and the debounce is **in the component**, not
  duplicated per screen.
- The magnifier icon is cosmetic; whichever way it is drawn, it is drawn the same way everywhere
  (`rtl-and-i18n.md` §5).
- A clear (`×`) affordance at the field's inline-end, labelled `a11y.clear_search`, at full tap size.
- **Never normalise a phone query in the client** — send the raw text and let the server do it, or the
  frontend acquires a second, divergent copy of `PhoneNumber.Normalise`.

---

## 13. Duplicate warning (`kaff-duplicate-warning`)

**New on 2026-08-21, and it exists because a rule reversed.** D-049 ruling 8: a client phone that is
already on file **warns and does not block the save**, naming the record that already holds it.

> *"A corporate client and its CEO might be registered as two separate entities sharing the same
> contact number."* — Karim, D-049 ruling 8

This is a different interaction from anything else in this inventory: **it names an existing record,
and its primary action is to proceed anyway.** A confirmation dialog states a consequence; this one
presents evidence and asks a question the user is expected to be able to answer either way.

```
┌────────────────────────────────────────────────────┐
│                This number is already registered   │  titleKey
│                                                    │
│   This number is already registered to Hassan      │  bodyKey, {name} bidi-isolated
│   Farouk. Do you want to proceed?                  │
│                                                    │
│   ┌────────────────────────────────────────────┐   │  the matched record — read-only,
│   │                        Hassan Farouk       │   │  never a link inside the dialog
│   │              C-10001 · 0101 234 5678       │   │  <bdi> on code and phone
│   │              [ Archived ]                  │   │  neutral chip, when applicable
│   └────────────────────────────────────────────┘   │
│                                                    │
│  ┌───────────────────┐  ┌───────────────────┐      │  primary at inline-start (RIGHT)
│  │      Proceed      │  │  Open that record │      │
│  └───────────────────┘  └───────────────────┘      │
│                                    Cancel          │  tertiary
└────────────────────────────────────────────────────┘
```

```ts
readonly titleKey  = input.required<string>();
readonly bodyKey   = input.required<string>();          // resolved with { name }
readonly match     = input.required<DuplicateMatch>();  // id, displayName, code, phone, isArchived
readonly noticeKey = input<string | null>(null);        // e.g. clients.duplicate.matched_archived
readonly proceed      = output<void>();
readonly openExisting = output<string>();               // the matched id
readonly cancelled    = output<void>();
```

| Rule |
|---|
| **Proceed is a real outcome and it is the primary action.** A warning whose only paths are "open the other one" and "go back" is a refusal wearing softer words, and the ruling reversed a refusal. |
| **The match must be named.** KAFF-119 rule 4: *"a warning that says only 'this number exists' is not what was ruled."* If the API returns a count without a name, **this component cannot be rendered as ruled** — raise it, do not ship the anonymous version. |
| **Cancel returns to the form with every field the user typed still in it.** The dialog is an interruption, not a submission boundary. |
| `openExisting` **navigates away and discards the draft**, so it confirms first if the form is dirty. It is the one path that loses work. |
| **No action inside the dialog edits the matched record** — no un-archive, no merge, no "use this one instead". Changing somebody else's master record while creating a new one is two changes behind one button. Un-archiving lives on the record's own screen. |
| Focus is trapped and returns to the trigger; `Escape` cancels, which is the safe direction here because cancelling loses nothing. |
| **The comparison is the server's.** The client sends the raw value and renders what comes back. Never normalise a phone in the component — that is a second copy of `PhoneNumber.Normalise` waiting to drift, and after ruling 8 a missed match is a warning nobody sees rather than a save the index refuses. |
| It is **not** `kaff-confirm-dialog` with extra inputs. That component's body states a consequence of what you are about to do; this one shows a record you did not know existed. |

**Used by** S-013 (client create), S-014 (editing a phone into a collision — KAFF-121 rule 4). **Not
by S-007**: whether two *users* may share a phone is still unruled (`questions.md` Q-UX-7), and
copying this dialog there would be assuming Karim's client answer applies to users.

| Key | For |
|---|---|
| `clients.duplicate.warning_title` · `.warning_body` | S-013 / S-014 |
| `clients.duplicate.matched_archived` | the archived notice |
| `clients.duplicate.proceed` · `.open_existing` · `action.cancel` | the three actions |

---

## Removed, and one that was never there

**`components.md` never carried a withholding component**, so the instruction to remove anything that
existed only for client-level withholding has nothing to delete. Checked deliberately rather than
assumed: the withholding category was a plain `select` inline on S-012's form, not a shared component,
and it left with the field when D-049 rulings 9 and 10 moved the rate onto the contract. **The
inventory is clean.** Whatever Finance needs in slice 4 (KAFF-416) is that slice's to design, and the
rate belongs to a contract there, not to a client.

**One key set is retired** with the refusal it belonged to: `clients.duplicate.title` / `.body` /
`.action.open_existing` / `.action.edit_phone`, replaced by the `§13` keys above.

## The session-expiry dialog is not a component

`slice-1-flows.md` S-016a specifies a re-authentication dialog over the current screen. It is **one
dialog used in exactly one place — the shell** — so it lives there, not here. A shared component for a
single caller is an abstraction with one implementation, and it would invite a second caller.

## What is deliberately **not** in this inventory

Building any of these now means designing against rules nobody has been asked for:

- A **toast / snackbar system.** Slice 1 has four success messages; an inline confirmation on the
  screen is enough, and toasts that carry errors are how errors get missed.
- A **charting library.** No screen before slice 7 needs one, and the RTL time-axis question is open
  (`questions.md` Q-UX-13).
- A **rich-text editor.** Every free-text field in slices 1–8 is plain text.
- A **file uploader.** Slice 5 attaches documents and slice 6 attaches photos; the mobile capture flow
  is the harder half and belongs to whoever designs slice 6.
- A **generic CRUD scaffold.** Client master and User master look similar and are not the same: one is
  deduplicated by phone with a business rule attached, the other is the most privileged screen in the
  system. A shared scaffold would flatten that distinction.
- **A permission directive** (`*kaffCan="…"`). Tempting, and dangerous: it reads as enforcement. If one
  is ever built, it must be named for what it does — hiding — and documented as convenience, because
  the server decides.

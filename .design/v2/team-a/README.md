# KAAF sign-in: design handoff

The single source for the sign-in screen is `direction-1.html` in this folder. It is a static mockup: open it directly or over the existing static server. Everything below is read from that file. Where this README and the file disagree, the file is the truth, and this README should be fixed.

Screenshots of every state are in `shots/` (`d1-{desktop,mobile}-{light,dark}-{idle,refusal,busy}.png`, plus `-en-refusal` for English). The coloured strip at the top of the mockup (state buttons and a light/dark toggle) is preview chrome. Do not build it.

## Brand assets (`../brand/`)

| File | Use it on | Notes |
|---|---|---|
| `kaaf-wordmark.png` | Any surface that is exactly `#09221e` (the green panel and the mobile header) | 519 x 155. Its background is the exact green, so it is seamless with no cutout. Always set `width="519" height="155"` so nothing reflows while it loads. |
| `kaaf-wordmark-alpha.png` | Any other background (cream, dark-mode surfaces that are not `#09221e`, email, print) | Transparent, mark flattened to `#e6d9d1`. |
| `kaaf-logo.jpg` | Nowhere in the UI | 1280 x 1280 with the wordmark in the middle 16 percent and baked padding. Source file only. |

Do not redraw, recolour or restyle the wordmark. Do not use a كف letterform anywhere. Keep the aspect ratio 519:155.

## Colour tokens

Only two colours come from the logo: green `#09221e` and cream `#e6d9d1`. Everything else is a step derived from them. There is no yellow and no accent colour. The only other hue is the semantic red.

Green steps:

| Token | Value | Used for |
|---|---|---|
| `--g900` | `#09221e` | Brand green. Brand panel and mobile header background (must stay exactly this so the wordmark is seamless). Light mode: text, field borders, primary button background. Dark mode: brand panel only. |
| `--g800` | `#0f2e29` | Dark mode only: input field fill. |
| `--g300` | `#9fb3ad` | Dark mode only: secondary text (subtitle, owner-reset note). 7.57:1 on `#09221e`. |
| dark page | `#061815` | Dark mode form side and mobile page background. A shade darker than the brand panel, so the green panel still reads as a surface. |
| dark border | `#7f9a92` | Dark mode input borders (needs to be visible against `#0f2e29`). |

Cream steps:

| Token | Value | Used for |
|---|---|---|
| `--c200` | `#e6d9d1` | The logo cream. Text on green (tagline, header controls). Dark mode: body text and the primary button background. |
| `--c100` | `#f4eee9` | Light mode page background (form side, mobile page). |
| field | `#fbf8f5` | Light mode input fill. |
| dim text | `#4b5f59` | Light mode secondary text. 4.95:1 on `#e6d9d1`, so at least that on the lighter page. |

Contrast, computed from WCAG luminance: cream `#e6d9d1` on green `#09221e` is 12.08:1, and the same ratio holds for green on cream, which is the primary button in both schemes. I did not separately measure the red pairs below or the dark border against the dark field fill.

Semantic red (the only non-brand hue, used only by the refusal message):

| | Light | Dark |
|---|---|---|
| fill | `#f9e4dd` | `#3b1612` |
| text | `#7a1a0e` | `#ffd0c6` |
| left bar | `#c2321c` | `#ff7b63` |

Light and dark are both required. Follow `prefers-color-scheme`, and provide a user-visible override in the product (the mockup's toggle only demonstrates the two palettes). Focus ring: 3px, `#09221e` in light, `#e6d9d1` in dark, offset 3px, on every input and button.

## Type

Two faces, nothing else.

| Face | Role |
|---|---|
| Noto Kufi Arabic (500/600/700) | Display: the heading, the tagline and the button. |
| IBM Plex Sans Arabic (400/500/600) | Body: subtitle, owner-reset note, refusal message, input text, labels, and the Latin fallback for English strings. |

Reem Kufi was dropped completely. The client chose Noto Kufi because Reem Kufi stretches the Arabic letter joins (`الدـخول`). The KAAF lockup is an image, so no Latin display face is needed and none is loaded. Noto Kufi Arabic has no Latin glyphs, so English strings fall through to Plex (this is visible in the English shots).

Ramp as built:

| Role | Face / weight | Desktop | Mobile (up to 820px) | Line-height |
|---|---|---|---|---|
| Heading (`تسجيل الدخول`) | Noto Kufi 700 | `clamp(1.75rem, 2.6vw, 2.25rem)` | 3.5rem Arabic, stacked one word per line (3rem English, one line); hidden when keyboard-up | 1.4 desktop, 1.3 phone |
| Tagline | Noto Kufi 600 | `clamp(1.25rem, 1.9vw, 1.7rem)` on the green panel | not shown (client decision) | 1.7 |
| Primary button | Noto Kufi 700 | 1.15rem | 1.25rem | 1 |
| Subtitle | Plex 400 | 1rem | 1rem | 1.6 |
| Field label | Plex 600 | 1rem | 1.0625rem | 1.6 |
| Input text | Plex 400 | 1.0625rem | 1.125rem | n/a |
| Owner-reset note | Plex 400 | .9rem | .9rem | 1.6 |
| Refusal message | Plex 500 | .96875rem | .96875rem | 1.5 |

On phone the Arabic heading is stacked (`تسجيل` over `الدخول`) with `inline-size: min-content`, so it wraps at the space; the widest word is about 170px in a 358px column, so there is real margin at 360px. The English heading overrides this to one line (`:lang(en)`), because stacking `Sign / in` looks wrong.

## Layouts and breakpoint

One component, one DOM, two layouts. The breakpoint is `max-width: 820px`; at 821px and up it is the desktop split.

Desktop split (default): two columns, 1.1fr brand panel and 1fr form. In RTL the brand panel is on the right (reading start) and the form on the left. Brand panel: exact `#09221e`, the wordmark centred at 440px wide (shrinks only to the panel width), and the tagline at the bottom, start-aligned. Form side: the AR/EN switch at the top end corner, the form block vertically centred, 384px max width. Dark mode keeps the panel at `#09221e` and darkens only the form side.

Phone (up to 820px): a stack, no panel. Two groups on one cream page, under one slim header.
1. Green header, 72px tall: the wordmark (96px wide, `kaaf-wordmark.png`) at the start edge and the AR/EN switch at the end edge, both vertically centred on ONE line, 16px side margin. The header is always 72px; it never grows or shrinks. The switch is the same single element as on desktop, restyled cream-on-green. Do not add a second language button.
2. The tagline is not shown on a phone (client decision). The header carries the brand.
3. Top group, the statement: the heading (3.5rem, stacked) and the subtitle, starting 24 to 40px under the header (`clamp(24px, 4.5vh, 40px)`).
4. Bottom group, the action, packed to the bottom edge for one-handed reach: label and 60px user name field, label and 60px password field, the refusal slot, then the sign-in bar. The split between the two groups is a single rule, `margin-block-end: auto` on the subtitle.
5. The sign-in bar is full width with no radius: `72px + env(safe-area-inset-bottom)` tall, flush to the bottom edge. Horizontal padding of the form is 16px, and the bar uses `margin-inline: -16px; width: calc(100% + 32px)` to run edge to edge.

Positions as built, on a 900px-tall viewport, measured from the bottom edge: bar 0 to 72px; password field about 158 to 218px; user name field about 270 to 330px. The empty band between the subtitle and the first label is about 200px at that height. It shrinks on shorter screens (about 100px at a 744px visible height) because the groups themselves do not scale.

Why the empty band is accepted: it is the deliberate negative space between the statement and the action. Moving the fields up would trade away the thumb reach the client chose this layout for. Filling the space with the tagline, a taller green header, or a repeated logo was tried and rejected (a void in cream, a void in green, a changed approved header). The band is made quieter by scaling the heading up into one big typographic moment, so the top group has weight and the space reads as a pause between two things instead of a hole. If a later design needs that band for something real (a notice, for example), it is the place for it.

Control heights: inputs 56px on desktop and 60px on phone (2px border, 6px radius), desktop button 58px (6px radius), phone bar 72px, language switch 44px minimum height. Do not shrink touch targets below these.

Implementation hint from the mockup: on phone the form side uses `display: contents` so the switch and the form sit directly in the stage grid. Rows are `72px minmax(0,1fr)`. The green header is a grid pseudo-element in row 1, holding the logo and the switch; the form is in row 2 as a flex column. In Angular a plain two-row grid on the host does the same.

## The refusal slot

The contract: ONE refusal message region, between the last field and the submit button, never beneath an individual input. Wrong password, unknown user name and locked account all show the identical text, so the screen never reveals whether an account exists.

Mechanism:
- A fixed-height region (min-height 4.25rem, about two lines of message plus padding) sits between the password field and the button. Its height never changes, so the page does not shift when a refusal appears or goes away: no layout shift at all.
- The same grid cell holds two stacked children: the owner-reset note ("forgot your password? ask the owner") and the refusal box. Idle and busy show the note, which makes the reserved space read as intentional instead of as an empty gap. A refusal hides the note (`visibility: hidden`) and shows the message in the same cell.
- Only the refusal element carries `role="alert"` and `aria-live="assertive"`. The note is outside the live region, so it is never announced.
- The refusal text, identical for every failure case: Arabic `لم يتم الدخول. تأكد من اسم المستخدم وكلمة المرور، أو تواصل مع المالك.` and English `Sign-in failed. Check your user name and password, or contact the owner.` Do not vary it by failure reason, and do not add field-level errors.
- Style: 6px left bar in the red (logical start), icon, red-tinted fill. Colour is never the only signal (the icon and the text carry it too).

Spacing: 0 below the last field, then .5rem above and .75rem below the slot (.4rem and .75rem on phone).

## Sign-in contract (what the screen does and does not have)

- Exactly two fields: user name (text, `autocomplete="username"`, autocapitalize off, spellcheck off) and password (`autocomplete="current-password"`, `minlength="8"`). Both are `dir="ltr"` and start-aligned, so Latin credentials read correctly inside the RTL page. Paste must work.
- No password strength meter, no pattern hint, no requirements checklist, no remember-me, no social login, no sign-up link, no email field. Accounts are created by the owner.
- One primary button. Busy state: spinner (20px, 3px ring, reduced-motion slows it) plus `جارٍ التحقق…` / `Checking…`, button disabled with `aria-busy="true"`, opacity .88. The button stays the same size.
- A user landing on a temporary password is sent onward to a change-password screen. This screen is not that one, but both layouts leave room: a second form of the same shape fits in the same stack and bottom bar.
- Language switch: Arabic and English. Switching sets `lang` and `dir` on the document (`rtl` for Arabic, `ltr` for English); the layout uses logical properties so it mirrors correctly. Arabic is the default.

Colour of primary button: green with cream text in light mode, cream with green text in dark mode (both 12.08:1).

## Keyboard-up rule (phone)

When the on-screen keyboard shrinks the visible area (`max-width: 820px` and `max-height: 620px`):
- hide the heading and the subtitle (the statement group), so nothing but the action remains;
- the form block keeps the 72px header above it and stays packed to the bottom;
- input height drops from 60px to 56px, the field gap to .75rem, the slot to 3.9rem.

The header, both fields, the refusal slot and the full-width bar all stay on screen. The viewport meta in the production page should keep `interactive-widget=resizes-content` so the bar rides on top of the keyboard instead of sitting under it. This rule was checked in a simulated viewport only. Test it on a real iOS and Android device before you call it done; the mockup was not tested with a real keyboard.

## What the mockup does not specify

- Real validation behaviour (the demo only plays idle, then busy for 1.5 s, then refusal).
- Motion beyond the spinner and the 2px press translate on the button.
- The wordmark size on the desktop panel at narrow desktop widths (821 to 1000px); it is `min(440px, 100%)`.
- Anything about the change-password screen.

---

# The application shell (`layout.html`)

The shell is the frame every other screen lives inside. The mockup is `layout.html`; screenshots are `shots/layout-*.png`. It inherits everything above (colours, faces, the 820px breakpoint, 44px minimum targets). Below are only the shell's own decisions. The strip at the top of the mockup (preview toggles) is not part of the design. Every corner in the shell is `--radius` (6px), the same token the sign-in screen and the kit use; the only round shape is the user avatar disc.

## What the shell holds

Ten destinations in two groups, in this order, labels verbatim:

- `البيانات الأساسية`: المشاريع, كتالوج الأسعار, الأبواب, الموظفون, مقاولو الباطن, الموردون, العملاء.
- `الإدارة`: المستخدمون, الأقسام, سجل التدقيق.

Rules the design is built around:
- Every row is permission-gated. A user without the permission does not see the row at all (not greyed, not locked).
- A group with no visible rows disappears with its heading. Check both: the first group alone (`layout-*-5rows-core-only`) and the second group alone (`layout-*-3rows-admin-only`).
- The sidebar is short for most people and long only for the Owner. It was designed and shot at 10 rows, 5 rows, 3 rows and 2 rows. Nothing depends on the list being long: there are no fixed gaps that only work at ten rows, and the user block is pinned to the bottom of the sidebar, not to the end of the list.
- The signed-in user's name and role, the language switch and the theme control live in the shell on every screen.
- Which role sees which row is a business decision that is not made here. The role names and row counts in the mockup's preview strip are samples only.

## Desktop (821px and wider)

Two parts: a green sidebar and the content frame. There is no top bar. A top bar would spend 56 to 64px of height on a screen that is a dense table 90 percent of the time, and it would have nothing to hold: identity and controls live in the sidebar footer, the title and actions live in the page header.

Sidebar:
- 256px wide, always exactly `#09221e` (so `kaaf-wordmark.png` is seamless), cream text, in both light and dark.
- Fixed to the viewport height, with the navigation scrolling inside it if there are more rows than fit. Header block 72px (wordmark 104px wide at the start edge, 44px collapse button at the end edge). Footer pinned to the bottom: avatar (40px, cream disc with the first letter of the name), name, role, then two 44px buttons side by side (language, theme).
- Navigation rows: 44px tall, 12px side padding, 22px icon, 14px gap, Plex 500 .96875rem. Group headings: Noto Kufi 600 .8125rem in `#9fb3ad`, with a hairline rule above the second group. No uppercase, no numbering.
- Icons are one stroke family (24px grid, 1.8px stroke, round caps). Each destination has one fixed icon. They are inline paths in the file and may be redrawn as long as the one-family rule holds.
- Collapsed rail: 76px wide. Labels, group headings, name and role hide; icons stay centred; a divider replaces the group gap; the footer buttons stack vertically (the language button shows `EN` or `ع`). The sidebar collapses automatically at 1100px and narrower (between 821 and 1100px), and by hand with the collapse button at any width. The button's chevron points at the edge the sidebar collapses into and reverses when it is a rail. A tooltip for each icon is required in the build. The mockup only sets a native `title`, because a CSS tooltip would be clipped by the scrolling navigation area. Build it as a non-clipped overlay.

Row states (sidebar):
- Default: transparent.
- Hover: `rgba(230,217,209,.09)` fill. It must not look like the active row.
- Active: `rgba(230,217,209,.15)` fill, weight 600, `aria-current="page"`, and a 5px cream marker bar on the inline-start edge of the sidebar (the right edge in Arabic, the left edge in English). The marker is placed with `inset-inline-start`, so it flips with the document direction. It sits flush with the sidebar edge, not inside the row.
- Keyboard focus: 3px cream outline drawn inside the row (`outline-offset:-3px`), so it is never clipped by the sidebar edge.

Content frame:
- Page header: 28px top, 32px sides, 20px bottom. The page title sits at the start edge: Noto Kufi 700, 2rem, line-height 1.3, with an optional count line (Plex, .9375rem, dim colour) under it. A back link or breadcrumb for deeper screens goes above the title; this slot is reserved but not drawn.
- Page actions sit at the end edge of the same header: search first (290px wide, 48px tall, same field styling as the sign-in screen), then the one primary action (48px tall button, green with cream text, cream with green text in dark mode). At most one primary action per screen.
- Below it, the content frame has 32px side padding and 32px bottom padding. The table has no card shadow: a 1px border, `--radius`, `--surface` fill.

The table (the shell's real content):
- Header row 44px, `--head` fill, Plex 600 .8125rem in the dim colour. Body rows 48px with a hairline between them. Total row 52px, `--head` fill, weight 600.
- Twelve rows plus header, total row and pager fit on a 900px-tall screen without scrolling the page.
- Money: a `bdi` element with `dir="ltr"`, `font-variant-numeric: tabular-nums`, Western digits, thousands separators. The currency is stated once in the column header (`(ج.م)`), not repeated on every figure. Money columns align to the end edge. The same applies to percentages. Codes (`KF-2401`) are `bdi` too.
- Status uses Kaff's words verbatim (لم تبدأ, جاري العمل, انتهت, متعثرة, تم تأجيلها) inside a pill that also carries a shape, so status never relies on colour alone: hollow dot (not started), dot (in progress), check (finished), triangle (stalled), pause bars (postponed). The stalled pill is the only red on the screen. Pills are 28px tall.
- Below 1100px the table scrolls horizontally inside its frame instead of squeezing. For the build, make the first two columns (code and name) sticky at the start edge so a row stays readable while scrolled. The mockup does not do this.

## Phone (820px and narrower)

Same system, composed for a thumb. The whole app is exactly one screen tall: the header and the action bar stay put and only the page scrolls between them.

- Header: 56px, `#09221e`, the 88px wordmark at the start edge and the language switch (44px minimum) at the end edge, on one line, as on the sign-in screen. The user and the theme control are not here; they are in the destinations sheet.
- Page: title (Noto Kufi 700, 1.875rem), count, a full-width 52px search field, then the list. A table does not fit on a phone, so each record is a row of 76px minimum with a hairline, no cards: name and status pill on the first line, client and contract value on the second. The whole row is the tap target.
- Action bar at the bottom edge: 80px tall plus the safe-area inset (10px padding, 60px controls), `--surface` fill, hairline above. Two controls: the menu button (116px minimum width, outlined, icon and label `القائمة`) at the start edge, and the screen's one primary action filling the rest (`مشروع جديد`). A screen with no primary action shows the menu button alone. Both are in the lower part of the screen, reachable by one thumb.
- Navigation is a bottom sheet, not a side drawer and not a tab bar. Reason: ten destinations cannot be tabs (five is the ceiling), and the list is a different length for every person, so a bar that changes shape by role would be unpredictable. The sheet works the same for two rows and ten. The menu button opens it; the dimmed area, the close button or Escape closes it; choosing a row closes it and goes there.
- The sheet: grab bar, then a top row (avatar, name, role, 48px theme button, 48px close button), then the two groups with their headings, then nothing else. Rows are 52px (larger than desktop), 1.0625rem, same icons, same marker on the inline-start edge. Maximum height 90 percent of the screen, scrolling inside if needed (the ten-row sheet is about 710px). The destinations are packed toward the bottom of the screen where the thumb is; the user block at the top is the rarely used part.
- A short sidebar (two rows) is a short sheet. It does not fill with empty space.
- Keyboard up (`max-height: 620px`): the count line is hidden, the title shrinks to 1.5rem, and the action-bar controls drop from 60px to 52px, so a search being typed keeps its results visible. The sheet is not used while typing.

## States the shell owns

- Loading: the shell does not change. Sidebar, title, search and the primary action stay visible; only the content area goes to skeleton rows (12 on desktop, 8 on phone) with a 3px indeterminate line along the top of the content frame. The count line becomes a skeleton bar. The content frame has `aria-busy="true"` and a visually hidden "جارٍ التحميل" status. Skeleton rows are the same height as real rows, so nothing jumps when data arrives. Reduced motion stops the shimmer and makes the line static.
- Empty or not-yet-designed screen: the shell keeps the title and shows a dashed box with a short sentence. The mockup uses this for screens that are not designed yet.
- Language: switching sets `lang` and `dir` on the document. The sidebar moves to the other side because the layout is a grid in logical order, and the active marker follows. Figures stay Western digits in both languages.
- Theme: light and dark. The sidebar is `#09221e` in both; only the page, the table surface and the controls change. Shell tokens: light page `#f4eee9`, surface `#fbf8f5`, table head `#ebe0d8`, hairline `rgba(9,34,30,.16)`, row hover `rgba(9,34,30,.055)`; dark page `#061815`, surface `#0a1f1b`, table head `#0f2e29`, hairline `rgba(230,217,209,.18)`, row hover `rgba(230,217,209,.06)`.
- Status pill colours (light / dark): in progress `#d3e3db` on `#0b3b32` / `#17403a` on `#e6d9d1`; finished `#09221e` on `#e6d9d1` / `#e6d9d1` on `#09221e`; stalled `#f9e4dd` on `#7a1a0e` / `#3b1612` on `#ffd0c6`; postponed `#e6d9d1` on `#43392f` / `#1d2b27` on `#c9bfb7`; not started: transparent with a 1.5px outline in `#4b5f59` / `#9fb3ad`. The contrast of these pairs was not measured.

## Not decided here (business questions)

- Which roles see which destinations. The mockup shows 10, 5, 3 and 2 rows as samples, with role names attached for display only.
- Whether a role may see a destination read-only or must not see it at all (the shell assumes "not at all", as the brief states).
- What each destination's primary action is called, and whether a screen may have none.
- Whether the list is paged (12 per page with page numbers, as drawn) or loads more.
- The project columns shown (code, name, client, status, contract value, approved extracts, progress). They are sample data for a realistic table, not a decided column set.
- Role names beyond the six in the brief, and how a long name or role wraps in the sidebar footer. Test it with real data.

## What the mockup does not do

- It does not implement the sidebar tooltips, sticky first columns on the narrow table, swipe-to-close on the sheet, or the back/breadcrumb slot.
- It was not tested on a real phone with an on-screen keyboard.
- The hover and focus states in `layout-*-hover-and-focus-rows.png` are forced with classes for the screenshots. Real `:hover` and `:focus-visible` rules are in the CSS.

---

# The component kit (`components.html`)

Every screen in the ERP is built from these parts and from nothing else. The mockup is `components.html`; screenshots are `shots/kit-*.png` (each section at 1440 and 390, light and dark, plus the English/LTR checks, the narrow-desktop check and the open-menu and inline-confirmation states). A part that a screen needs and the kit does not have is added to the kit first, then used. A screen never styles its own version of a kit part.

The strip at the top of the kit page (links, a theme button, and a direction-test button) is not part of the design. The direction-test button flips the page to left-to-right so the layout of each part can be checked; it does not translate the Arabic copy.

## Rules that apply to every part

- Arabic RTL is the primary direction. All spacing and positions use logical properties (`inline-start`, `inline-end`, `padding-inline`, `inset-inline-*`). Never `left` or `right`, with two narrow exceptions already in the sidebar and the popup placement code. In English every part mirrors: the start edge becomes the left edge. Each component below says what flips.
- Touch targets are never smaller than 44 by 44 px. The kit has no 36 or 40 px controls. On a phone, the controls in the bottom bar and in a bottom sheet are 56 to 60 px.
- Colour is never the only carrier of meaning. Every colour distinction is paired with a shape, an icon, a weight or a word (status pills have a shape, errors have an icon and text, a selected segment is filled and bold, a toggle moves and shows a check and says its state in words).
- Money is a `<bdi dir="ltr">` with `font-variant-numeric: tabular-nums`, Western digits, thousands separators, aligned to the end edge of its column, and the currency named once in the column header or the field affix, never repeated per row or per figure. The same applies to codes (`KF-2401`) and percentages.
- No toasts, no snackbars, no popups that disappear on their own. A confirmation happens in place, where the action was taken (see "Feedback"). Nothing in the product is announced by a notification that leaves the screen.
- Focus: every interactive part shows a 3px outline (`--focus`) with 3px offset on keyboard focus, drawn inside the part (offset `-3px`) when it sits against an edge that would clip it (sidebar rows, menu items, segmented-filter buttons use a negative offset). Never remove it.
- Disabled: 45 percent opacity and `cursor: not-allowed`. A disabled control that the user may need an explanation for must carry the reason as text next to it (in the hint, or in the menu item), because opacity alone explains nothing.
- Busy: the part keeps its exact size. A 18px spinner appears inside the control, the label changes to the progressive form ("جارٍ الحفظ…"), `aria-busy="true"`, and clicks are ignored. Reduced motion slows the spinner but does not remove it.
- Motion: none except the spinner, the skeleton shimmer, the toggle thumb (150ms) and the sidebar chevron. All stop or shorten under `prefers-reduced-motion`.

## Tokens added by the kit

Everything in the login and shell sections still applies. The kit adds only these:

| Token | Light | Dark | Used for |
|---|---|---|---|
| `--btn-h` | `#25544b` | `#fbf6f2` | primary button hover fill |
| `--danger` / `--danger-ink` | `#a42a16` / `#ffffff` | `#ff9d8a` / `#2a0d08` | the loud destructive confirm button only (7.19:1 light, 9.01:1 dark) |
| `--dh` | `#8a2211` | `#ffb9ab` | destructive confirm hover fill |
| `--err-ink` | `#7a1a0e` | `#ffd0c6` | destructive trigger text and border, error text, destructive menu item |
| `--err-bar` | `#c2321c` | `#ff7b63` | the 6px start bar of refusal banners, error field border |
| `--note-bg` | `#ebe0d8` | `#0f2e29` | neutral notice banner |
| `--ok-bg` / `--ok-ink` | `#d3e3db` / `#0b3b32` | `#17403a` / `#e6d9d1` | saved-in-place banner (same pair as the in-progress status pill) |
| `--radius` | `6px` | `6px` | every corner of every surface (see "Corner radius" below); the same in light, dark and both directions |
| `--scrim` | `rgba(3,13,11,.55)` | same | dimming behind dialogs, panels and sheets |
| `--shadow` | `0 12px 36px rgba(9,34,30,.22)` | `0 12px 36px rgba(0,0,0,.6)` | overlays and popups only; nothing else has a shadow |

Corner radius: ONE token, `--radius: 6px`, defined once and used for every surface that has a corner: fields, buttons (including the large bottom-bar button), the icon button, the segmented filter, the search field, badges, status pills, popups and menus, dialogs, side panels, bottom sheets (top corners), banners, cards, table frames, list frames, the choice boxes (checkbox) and the tab tops. The sign-in, layout and kit screens all use it (the sign-in screen already did; layout and kit were 8 to 22px and were moved to 6px by client decision). The build reads this one value. Do not invent a second radius.

Kept different on purpose (there are only these): (1) fully round (`50%`): the avatar disc, spinners, the toggle thumb and the radio button, because a circle is the shape that carries their meaning (a radio that is not round stops reading as a radio); (2) the switch track is fully round (16px on a 32px track) because a switch whose track is square cannot show a thumb travelling along it; (3) small structural slivers that are not surfaces: the 4px selected-tab bar and the 3px active-row marker use 2 to 3px so their ends do not look chewed; (4) skeleton placeholder bars use 4px; (5) the 24px frame drawn around the phone mockups in the kit sheet is a picture of a device, not part of the product UI.

## Buttons

Sizes: compact 44px high (table toolbars, dialog actions on a phone are 56), default 48px (pages and forms), large 60px (the phone bottom action bar). Padding 22px (16px compact, 26px large). Font: Noto Kufi Arabic 700, 1rem (.9375rem compact, 1.0625rem large). Border 2px always (transparent where there is no visible border) so a variant swap never shifts layout. A button's width follows its label; a phone-bar button may fill.

Variants and when to use each:
- Primary: solid green with cream text (cream with green in dark). One per screen section; the answer to "what do I do next here".
- Secondary: transparent, 2px field-colour border. Cancel, the alternative path.
- Quiet: transparent with no border. A third-level choice, such as skip or close in a banner.
- Destructive trigger (IRREVERSIBLE actions only): transparent with a 2px border and text in `--err-ink`, with a trash icon. The first click of a permanent delete. It stays quiet; the loudness belongs to the confirm. For a recoverable action (archive, deactivate) do not use it: use the secondary button with the action's own icon (see "Two confirmations").
- Destructive confirm (IRREVERSIBLE actions only): solid `--danger`. Appears only inside the red confirmation of a permanent action, never anywhere a user could press it without having seen the question. It is the only loud red in the product. A recoverable action never uses it; its confirm is the ordinary primary button.
- Icon button: 44 by 44, `--radius`, no border (a 2px border variant exists for a lone button that needs to look pressable, like the row menu in the kit demo). It always has an accessible name.

States for every variant: default, hover (fill step, not an opacity change), keyboard focus, disabled, busy. Hover is never the only affordance (touch has none).

RTL: icons sit at the start of the label inside the button and flip sides with the direction. Do not mirror the icon glyphs themselves, except directional ones (back arrows, chevrons).

Never use: a primary and a destructive confirm in the same row at rest; a destructive trigger styled loud; a button for navigation (that is a link); a disabled primary with no reason shown.

## Links, back link, badge, avatar

- Link: ink colour, always underlined (1.5px, offset 4px; 3px on hover). Colour is not the cue.
- Back link: 44px high, chevron plus label, the chevron points to the start edge in RTL (toward the previous screen) and flips in English. It sits above the page title and is the only way back that is not the browser's.
- Badge: 24px high, `--radius`, 1.5px border, tabular figures. A count or a neutral tag (`جديد`). Outlined or filled for emphasis. A badge is never a status; status always uses the status pill.
- Avatar: a disc with the first letter of the name, in Noto Kufi 700: 32, 40 or 56px. Ink disc with page-colour letter on the page; cream disc with green letter on the green sidebar and header.

## Status pill

Kaff's five words, verbatim and never abbreviated: لم تبدأ, جاري العمل, انتهت, متعثرة, تم تأجيلها. 28px high, `--radius` corners (not fully round), 13px icon at the start of the label, Plex 600 .8125rem, a 1px inner ring at 30 percent of the text colour. The shape is part of the meaning:

| Status | Shape | Light (text on fill) | Dark (text on fill) |
|---|---|---|---|
| لم تبدأ | hollow circle, 1.5px outline, no fill | `#4b5f59` on `#fbf8f5`: 6.44:1 | `#9fb3ad` on `#0a1f1b`: 7.78:1 |
| جاري العمل | solid dot | `#0b3b32` on `#d3e3db`: 9.36:1 | `#e6d9d1` on `#17403a`: 8.31:1 |
| انتهت | check mark | `#e6d9d1` on `#09221e`: 12.08:1 | `#09221e` on `#e6d9d1`: 12.08:1 |
| متعثرة | warning triangle | `#7a1a0e` on `#f9e4dd`: 8.66:1 | `#ffd0c6` on `#3b1612`: 11.52:1 |
| تم تأجيلها | pause bars | `#43392f` on `#e6d9d1`: 8.16:1 | `#c9bfb7` on `#1d2b27`: 8.14:1 |

All ten text pairs are above 4.5:1 (the lowest is 6.44:1). The pill's fill against the table surface is low (1.07 to 1.5:1 for the tinted ones), so the fill alone does not carry the meaning: the word and the shape do, and the ring keeps the edge visible. A pill is a label, not a control, so the 3:1 boundary rule for controls does not apply. Never put a status in a pill that is not one of the five words. Never colour a row by status.

RTL: the icon is at the start edge of the pill, the label follows. In English, same order, mirrored.

## Form fields

The field wrapper owns the label, the control and one footer line. Layout: label (Plex 600, 1rem), control, footer slot. The footer slot has a reserved height of 24px (one line) and holds either the hint or the error, never both: the error replaces the hint in the same place with an alert icon and the error text, so the form never reflows when validation runs. A longer error wraps inside the slot and is allowed to push content down by the extra lines only (this is the one permitted shift).

- Required: a `*` after the label, plus a visually hidden word "مطلوب". Optional fields carry no marker. The asterisk is a shape, so colour is not involved.
- Control height 48px on desktop and 56px on a phone, 2px border in the field colour, `--radius`, text 1.0625rem. Hover darkens the border to ink. Keyboard focus draws the 3px outline outside the border.
- Disabled: dashed border, 45 percent opacity, the hint says why. Read-only is not disabled.
- Error: a 2px border in `--err-bar` plus an inner 1px ring, `aria-invalid="true"`, the footer text in `--err-ink` with the alert icon, `aria-describedby` pointing at the footer.
- Validating: an 18px spinner in the trailing slot, the footer says what is being checked.
- Read-only and generated (the project code): filled with the table-head tone, 2px border in the hairline colour, a lock icon at the end, text in ink at weight 500, focusable and selectable. It must look settled, not greyed, and it carries a hint ("يُولَّد تلقائياً عند الحفظ"). Never use disabled for a value the user is meant to read and copy.
- Number: text aligned to the end edge, tabular figures, `dir="ltr"` and `inputmode="decimal"`, an optional unit affix (such as م³) in the trailing slot, separated by a 2px rule.
- Money: same as number, with the currency (`ج.م`) as the trailing affix inside the field. The figure sits next to the currency. One currency affix per field; the table never repeats it. Money is typed and stored as a decimal; the design allows exactly the number of decimals the business defines and always shows them.
- Text with leading Latin (user names, codes, e-mails): `dir="ltr"`, aligned to the start edge.
- Select: the closed control looks like a field with a down chevron at the trailing end, and the option list is a popup (below). On a phone use the platform's native select.
- Textarea: 112px minimum height, vertical resize, 12px padding, 1.6 line height.
- Date: a text field in `DD/MM/YYYY` with Western digits (`dir="ltr"`, `inputmode="numeric"`), and a 44px calendar button at the trailing end that opens the date popup. The browser's own date input is not used, because its digits and format follow the device locale.
- Toggle: a 56 by 32px switch inside a 56px-high row. The label is at the start, the switch at the end. The state is carried by position (the thumb moves to the end edge when on), by a check mark in the thumb, by the track being filled, and by a text line under the label (`مفعّل` or `متوقف`). A toggle applies immediately only when its label says so; otherwise it is part of a form that has a save button.

Form footer (action row): a hairline above, then primary, secondary and, separated by free space, the destructive trigger at the far end. On a phone the row sits at the bottom of the form: primary fills the available width, secondary beside it, and the destructive trigger is placed as its own full-width row above the bar, outside it.
- Saving: the primary becomes its busy form, same size; the secondary is disabled.
- Saved: the action row is replaced in place by a "تم حفظ التغييرات" banner with a check (green pair), then returns to the buttons after about three seconds. There is no toast.
- Destructive in a form: the trigger stays in the row. Pressing it replaces the action row with an inline confirmation (the question, the loud confirm, cancel). It never opens a dialog unless the form is already inside a dialog or panel (then a confirmation dialog is allowed, see "Overlays").

RTL: label and hint align to the start edge; inputs with Latin content align start; number and money figures align to the end edge; the affix is at the end edge; a trailing icon (calendar, lock, chevron) is at the end edge.

Never use: placeholder as a label; an error under the field and a hint above it at the same time; red as the only error cue; a field narrower than 160px; two required markers styles.

## Lists and tables

Anatomy of the desktop table (frame: 1px hairline border, `--radius`, surface fill):
- Header row: 44px, table-head fill, Plex 600 .8125rem in the dim colour, text aligned to the start edge (numeric columns to the end edge).
- Body row: 48px, 1px hairline between rows, hover fills with a 5.5 percent ink tint across all cells (including the pinned cell).
- Group row: 44px, table-head fill, the section name (a باب in the catalogue) in Noto Kufi 600 .9375rem and a count badge. The group row spans all columns. It is not collapsible in this version.
- Identity cell (always the first column): the NAME leads, bold (Plex 600), and the code follows it, demoted: dim, .8125rem, tabular, in a `bdi dir="ltr"`. In the DOM the name comes first so that reading order, copy and screen readers all lead with the name. Never lead with the code and never show the code as the primary text. The name truncates with an ellipsis, the code never truncates.
- Money cell: aligned to the end edge, `bdi dir="ltr"`, tabular numerals, 500 weight, Western digits, no currency symbol (the column header says `(ج.م)`).
- Status cell: the status pill.
- Actions cell: the last column, 64px wide, end-aligned, a 44px icon button that opens the row menu. The header cell is empty (with a hidden "إجراءات" label).
- Totals row: 52px, table-head fill, weight 600, a 2px field-colour rule above it, labels in the identity column and totals in the money columns, aligned to the same edge as the figures above.
- Pagination: below the table, outside the frame: the count line ("يعرض 7 من 48 مشروعاً") at the start edge, the pager at the end edge. Buttons are 44px, 2px border. The current page is filled and bold with `aria-current="page"`, the disabled end is 40 percent opacity. On a phone the numbered buttons collapse to "السابق | صفحة 1 من 4 | التالي" with the two buttons at the opposite ends.

The sticky first column (solution for narrow laptops): the identity column is `position: sticky` at the start edge (`inset-inline-start: 0`), 260 to 340px wide, with a solid surface fill (so the scrolling columns pass under it without showing through), a 1px hairline on its inline-end side as the divider, and no moving shadow. The header cell for it is sticky too and sits above (higher z-index). The table has a minimum width, around 880 to 960 px, and its frame scrolls horizontally when the available width is smaller. The identity column is the only pinned column: the name must always be readable, everything else may scroll. When a row hover or an inline confirmation is shown the pinned cell takes the same fill. The inline confirmation text is itself pinned at the start edge so the question is readable whatever the scroll position. At 1000px the pinned column stays on screen and the money column is reached by scrolling; this is why the identity column is capped at 340px. In English the pin moves to the left and the hairline to its right side. Both directions were checked scrolled (`shots/kit-desktop-light-s-lists.png` for Arabic, `kit-desktop-light-s-lists-english-ltr.png` for English): in both, the name column stays at the start edge while the status pills pass under it. In the mockup the scrolled position is held with CSS scroll-snap so a screenshot can show it; that snapping is for the demo only and must not be built (real scrolling is free).

Phone row (820px and narrower): the table is not used. Each record is a stacked row of at least 80px with a hairline below and no card around it: line one is the name (bold) at the start and the status pill at the end; line two is the code (dim) and the client (dim) at the start and the money figure at the end; a 44px actions button at the very end spans both lines. The whole row is the tap target. A group heading becomes a full-width band. The total is a band at the bottom of the list, ("إجمالي الصفحة"). Money aligns to the end edge in every row.

The four list states (they must read differently):
1. Loading: skeleton rows at exactly the real row height (48px) and the real header. No spinner, no overlay. Status: `role="status"`, `aria-busy="true"`, hidden text "جارٍ التحميل".
2. Empty (no records exist): a 56px round icon (open folder), a title ("لا توجد مشاريع بعد"), one sentence, and the primary action. The box is solid.
3. No results after a filter or search (records exist, none match): a search icon, the title "لا نتائج مطابقة", a sentence that names what was searched or filtered, and a secondary button "مسح التصفية". The box has no fill, so it reads as part of the list.
4. Refused by permission: a lock icon in an outlined disc, a title ("هذه الشاشة خارج صلاحياتك"), a sentence that says what is missing and who can grant it, a back link, and no action that tries again. The box has a dashed border. It is not an error and not red.

Never use: a spinner over a table; "no data" without a next step; a table on a phone; horizontal scroll as the phone solution; colouring rows by status; more than one pinned column.

## Filters and search

- Segmented filter: 2px border, `--radius`, segments at least 44px high with 18px side padding and 2px dividers. The selected segment is filled with ink and bold (`aria-pressed="true"`); the others are transparent. Hover tints; keyboard focus draws an inset outline. It filters one dimension with three or fewer options (such as نشط · مؤرشف · الكل). More options mean a select. The default selection is the unfiltered, common case (نشط), and it counts as the "off" state: the clear link does not appear.
- Search field: 48px high, 2px border, a search icon at the start (20px), a 44px clear button at the end that appears only when there is text, and a busy spinner at the end while a search is running. Width 300px on desktop and full width on a phone. Search is by name and code, and it never changes the segmented filter.
- Result count line, under the toolbar: says what is shown, not what exists ("يعرض 12 من 48 مشروعاً نشطاً"). With a filter or search on, it describes the result and shows an underlined link "مسح التصفية" ("3 نتائج لـ «فيلا» في المشاريع المؤرشفة").

RTL: the segmented filter order reads right to left (the first option is at the start edge); the search icon is at the start edge and the clear button at the end edge.

## Row actions and confirmation

- Row menu: opens from the actions icon button, 240px wide, 44px items, `--radius`, popup shadow. Items have an icon at the start and a label. A disabled item stays visible and its reason is given elsewhere (a tooltip is not enough on a phone). The destructive item is last, after a hairline, in `--err-ink` with a trash icon and an ellipsis ("حذف…", the ellipsis says there will be a question). The menu is placed with fixed positioning at the button, aligned to the button's edge on the side where there is room, and flips above the button when there is no room below. It closes on Escape, on outside click and on scroll. It must never be clipped by a scrolling table frame: render it in a layer above the page, not inside the table. On a phone the same actions appear as a bottom sheet with 56px rows.
- Inline confirmation (delete): choosing the destructive item does not delete. The row is replaced in place by a 64px row tinted with the error fill, with a 1px error line above it, saying "حذف «اسم» ؟ لا يمكن التراجع." with the loud confirm ("تأكيد الحذف") and a secondary cancel. The row grows from 48 to 64px; this is the one allowed shift and it is user-initiated. Cancel restores the row. Confirm goes busy ("جارٍ الحذف…", cancel disabled). If the server refuses, the same place shows the reason with an alert icon ("تعذّر الحذف. المشروع مرتبط بمستخلصات معتمدة، فيمكن أرشفته فقط.") and offers the next best action ("أرشفة بدلاً من الحذف") and "إغلاق". When the reason is a business rule do not offer "حاول مجدداً". Only one inline confirmation is open at a time.
- The destructive treatment, as a rule: the trigger is quiet and bordered, the confirm is loud and appears only after the question has been read. A destructive button is never the default-focused button of a dialog; the focus starts on cancel.

### Two confirmations: recoverable and irreversible

The kit has two destructive confirmations and they must look different at a glance. The rule for choosing is one sentence: **if the user can get it back, it is the quiet one.** Red is reserved for what nothing can bring back. When in doubt ask "is there a restore button somewhere?" If yes, quiet.

| | Recoverable (the default for anything reversible) | Irreversible |
|---|---|---|
| Examples | archive, deactivate, hide, revoke access that can be granted again | permanent delete of a draft, a purge, anything with no way back |
| Trigger | secondary button (2px ink border) with the action's icon (for archive: the archive box), labelled with the verb ("أرشفة") | destructive trigger: red 2px border and text, trash icon, label says permanence ("حذف نهائي") |
| Menu item | ink colour, the verb plus an ellipsis ("أرشفة…") | `--err-ink`, trash icon, "حذف نهائي…" |
| Inline confirmation | neutral note fill (`--note-bg`), 1px ink line above, ink text, the action icon at the start; the message says it can be undone and where ("يختفي من القائمة النشطة، ويمكنك استرجاعه من «مؤرشف» في أي وقت.") | red error fill, red line above, red text, trash icon; the message says "لا يمكن التراجع" |
| Confirm button | the ordinary primary button, labelled with the verb ("أرشفة") | loud `--danger` button, labelled "تأكيد الحذف" |
| Confirmation dialog | neutral icon tile, the primary button, initial focus on the confirm (it can be undone) | red icon tile, red button, initial focus on "إلغاء" |
| Busy / refused | same busy state (the confirm keeps its size, cancel disabled); a refusal appears in place with the reason | same |

Everything else about the confirmation is shared: it happens in place, never a toast, one open at a time, cancel restores. The recoverable one must also provide the way back: an "إلغاء الأرشفة" item on the archived item's menu, which needs no confirmation and answers in place with the restore band.

Existing kit usages and what changed: (1) the destructive trigger and confirm are now defined for irreversible actions only, and the red examples in the kit sheet (delete a project, delete a draft extract) remain red because they are permanent; (2) nothing in the kit sheet that was an archive was red before, so no archive example changed colour in the kit; (3) the catalogue was the first screen with a recoverable action and moved to the quiet pattern: its row-menu item, the inline confirmation, the phone confirmation and the panel's footer trigger. A builder adding a new destructive action must classify it with the rule above before choosing a component.


## Overlays

Four kinds:
- Dialog: 520px wide, maximum width the screen minus the gutters, `--radius`. Header (title in Noto Kufi 700 1.25rem, a 44px close button at the end edge), body, footer with the primary first (start edge), then secondary. Used for a short form or a decision that needs the user's full attention (create a record).
- Confirmation dialog: 420px wide. A 48px round icon in the error tint, a title that repeats what will be affected, one sentence of consequence, then the loud confirm and "إلغاء". Initial focus on "إلغاء". Escape and the scrim cancel.
- Side panel (desktop): 440px wide, from the end edge of the screen (the left edge in Arabic, the right edge in English), full height, hairline on its inline-start side, popup shadow. Header (title, close), a scrolling body, a footer with the edit action at the start and the destructive trigger at the end. Used to read a record's detail without leaving the list. The body uses the key-value list: label column 130px in dim, value in ink, 48px rows with hairlines, money and codes as `bdi`.
- Bottom sheet (phone): the same content as the side panel. It rises from the bottom edge, `--radius` on the top corners, a grab bar, a header, a scrolling body, and a footer pinned at the bottom with a 56px primary and a 56px destructive icon button. Maximum height 92 percent of the screen. It replaces the side panel at 820px and narrower. The navigation sheet from the shell is the same component.
- Popup: the select list and the date picker. 6px padding, `--radius`, 1.5px field-colour border, the popup shadow. Select options are 44px; the selected option has a check, a bold label and a filled row; a disabled option stays visible with its reason in the label. The popup opens below the control and flips above when there is no room. Date picker: 296px wide, a month header with 44px previous and next buttons, a 7-column grid of 38px days with Western digits, the selected day filled, today outlined, days outside the month dimmed and not selectable. Week starts on Saturday.

Closing and focus: every overlay closes with its close button, the scrim and Escape. Focus moves into the overlay on open and returns to the control that opened it on close. Focus is trapped inside while it is open. The page behind does not scroll. The scrim is 55 percent of the deep green in both themes.

Stacking rules:
- Allowed: one confirmation dialog on top of a dialog, a side panel or a bottom sheet (to confirm a delete or to discard edits). A popup (select, date picker, row menu) inside any overlay or on the page. Maximum depth: page, one surface (dialog, panel or sheet), one confirmation, one popup.
- Not allowed: a dialog over a dialog, a panel over a panel, a panel over a dialog or a dialog over a panel, any surface over a confirmation, a confirmation over a confirmation, two popups open at once. If a flow needs a second step, replace the content of the open surface (with a back link in its header) instead of stacking.
- A confirmation closes before anything behind it changes; a popup always closes first on Escape (the topmost layer closes first).

## Feedback

- Refusal banner: the shape of the sign-in screen's refusal. 6px bar on the start edge in `--err-bar`, an alert icon, the text in `--err-ink` on the error fill, 6px radius, padding 12 by 16px, text weight 500. It sits where the refused action is, between the thing that was done and its button, not at the top of the page. It may hold one secondary action at the end edge (for example "طلب إيداع"). It says what failed and what to do, never apologises, and a refusal that must not reveal information (sign-in) keeps the same text in all cases.
- Notice banner: neutral fill, a dark 6px start bar, an info icon. For facts about the surrounding content (a locked period, read-only mode). It does not ask for action.
- Saved banner (in place): the green pair with a check, replaces the action row for about three seconds. Not an alert; announced politely (`role="status"`).
- Permission-refused page: the fourth list state above, used for a whole screen too.
- Field-level error: see the form field section.

RTL: the bar is on the start edge in both directions, the icon next to it, the action at the end edge.

## Checkbox, radio and tabs

The kit includes all three because this product will need them and a screen must not invent them: the user and permission screens assign many roles and projects at once (checkbox lists with a select-all parent), choices like a payment method are single choice from a short list (radio), and a project's detail has several sections of one record (tabs). Where the real screen needs them is decided by the business; the parts are fixed here.

Checkbox:
- A 24px box with a 2px border inside a row that is at least 44px high; the whole row (box and label) is the target. Radius `--radius`.
- Selected: filled with ink and a check mark. Partly selected (a parent whose children are mixed, or a select-all header): filled with a dash. Unselected: empty field fill. Colour is never the cue: the check and the dash are shapes.
- States: default, hover (a 4px halo and a darker border), keyboard focus (3px outline around the box, not the row), checked, partly, disabled (45 percent, and the reason belongs in the label or a hint), error (a red border plus a message under the group with an alert icon).
- Optional second line under the label in the dim colour for what it grants. Children of a group are indented 36px at the start edge.
- RTL: the box is at the start edge and the label follows; in English the box is at the left. Never put the box at the end.
- Use for: multiple choice and for selecting table rows. Do not use for a setting that takes effect immediately (use the toggle), and not for a single yes/no that is a consequence of the form (use a toggle or a radio pair).

Radio:
- A 24px circle, 2px border, a filled ring with a centre when selected (the ring and centre are the shape cue), a row of at least 44px, label and optional description. States as for the checkbox. One option is always preselected unless "no choice yet" is a valid state, in which case a required error appears under the group.
- Use for 2 to 5 mutually exclusive options that the user should see at a glance. For more options or a list that changes, use the select. For one option on or off, use the toggle, never a radio.
- Keyboard: arrow keys move the selection within the group; tab leaves the group.

Tabs:
- A strip of tabs with a 2px hairline under it. Each tab is 48px high on desktop and 56px on a phone, 18px side padding, Noto Kufi 600 (700 when selected), `--radius` on the top corners.
- Selected: bold, ink text, and a 4px bar along the bottom edge. Others: dim text. The bar and the weight are the cues, not the colour.
- States: default, hover (a tint), keyboard focus (an inset 3px outline), selected, disabled (45 percent), and a count badge inside a tab when the count matters.
- Use for sections of one record (items, extracts, expenses, files). Exactly one is selected at all times and shows its content. The selected tab is part of the page address so it can be shared. Not for filtering a list (use the segmented filter), not for steps in sequence (separate pages), not for navigation between screens (the sidebar does that).
- Phone: the strip scrolls horizontally if it does not fit and keeps the selected tab in view. With more than five tabs on any width, replace the strip with a select.
- RTL: the first tab is at the start edge (right in Arabic); in English the first tab is at the left.

## What the kit deliberately does not contain

- Toasts, snackbars, banners that disappear on their own, tooltips that carry required information (a tooltip may repeat a label, never replace one), and carousels.
- Cards as a general container. A group of fields in a form may sit in a bordered surface; lists, tables and detail views do not use cards.
- Gradients, decorative shadows and illustration. The only shadows are on overlays and popups.

## Beyond the inventory (and why)

- Saved-in-place banner and the "saving" form state: there are no toasts, so a save needs a place to say it succeeded.
- Notice banner: a neutral variant, so a refusal is never used for information that is not a failure.
- Server-refused state of the inline confirmation, with a better next action: a business rule that blocks a delete (approved extracts exist) needs a way forward, and the user must see it in place.
- A date picker popup and the key-value list: the detail panel and the date field need them.
- Validating state of a field: usernames and codes are checked by the server.
- The reason on a disabled menu item.
- Not built, flagged: a native select on phones is recommended in place of the custom popup, and the popup's keyboard behaviour (arrow keys, type-ahead) is specified here but not scripted in the mockup.

---

# The catalogue list (`catalogue.html`)

Screen: كتالوج الأسعار, the price book. It is composed from the kit inside the approved shell. Screenshots: `shots/cat-*.png` (desktop 1440, phone 390, one 1000px check, light and dark; states: list, no results, archive confirmation, detail panel as the Owner, panel with an unsaved cost edit, detail panel as the Technical Office (read-only shape), archived view, restore in place, loading; plus English/LTR). The preview strip in the mockup switches states; `?view=list|empty|archive|panel|edit|panel-ro|archived|unarch|loading` opens one directly, and `?role=to` shows the Technical Office.

## The data this screen shows (and one thing it does NOT)

A catalogue item is `code · description · unit · bab · costPrice · baseSellRate · status` (`spec.md` §4.1). Both prices are STORED figures. `costPrice` is internal. `baseSellRate` is the item's sell rate. They are two independent money columns and neither explains the other.

**An earlier version of this design showed a derived sell price (cost plus the باب's markup), an "overridden" tag, a derivation card in the panel and a "custom sell price only" filter. None of that exists in the domain.** It came from a wrong brief and was drawn faithfully, then removed. Do not reintroduce it from the older shots or from memory: there is no derivation, no override field, no "back to the calculated price" action, and no marker on a sell figure. The باب markup is `lineMarkup` (`spec.md` §4.2) and applies to a BOQ line on a project, not to the catalogue.

## What it is made of (unchanged from the kit)

Page header and actions, the primary button, the secondary button, the segmented filter (نشط · مؤرشف · الكل), the search field, the result count line, the table frame with its pinned identity column, the row menu (fixed-position), the inline confirmation, the empty-result list state, the side panel (desktop) and bottom sheet (phone), form fields (money with the currency affix, read-only generated field with a lock), the neutral notice banner, the skeleton loading rows, the badge and the avatar. Counts, money and codes follow the kit rules: `bdi`, tabular numerals, Western digits, the currency named once in the column header.

## Page structure

- Title "كتالوج الأسعار" with the count of everything ("612 بنداً في 8 أبواب"). Actions at the end edge: search (by code or description), "استيراد من Excel" (secondary, upload icon, the entry point to the import flow, which is not designed here), and the one primary action "بند جديد".
- Toolbar: the segmented filter (نشط is the default and counts as "off") and a باب select ("كل الأبواب", narrows the list to one باب). Under it the result count line says what is shown ("يعرض 598 بنداً نشطاً"); with a search or a باب chosen it describes the result and shows "مسح التصفية". There is no price filter.
- The list is grouped under 8 أبواب. Items are never shown ungrouped, including search results (matching items stay under their باب, and a باب with no match disappears).

## Designing for 612 items

Nothing here paginates by page. A page boundary would cut a باب in two. Instead:
- Each باب is a collapsible group. A fresh visit shows the groups the user left open last time (the mockup shows two open and six collapsed), so eight groups are at worst eight rows. A search, a باب choice, or the archived view opens every group that has matches.
- An open group shows its first items and ends with a "عرض 111 بنداً أخرى في هذا الباب" row. The build may virtualise rows behind it.
- The group heading is sticky: while a باب's items scroll, its name and count stay at the top of the table.

## The باب group heading (and the margin that is deliberately NOT on it)

The heading carries exactly two things: the باب's name and the count of its items ("118 بنداً"). The name sits at the start edge and the count at the end edge as quiet dim text (the name takes the slack, so there is no boxed badge stranded at one end), so the row reads as a section label with its size, and no gap is left where anything else used to be. Phone: the same two things in the sticky 56px band.

**The باب's default markup percentage is deliberately absent from this screen.** The client ruled it out: this screen cannot change it, and a number a user cannot act on here is clutter. The margin is `lineMarkup` on a BOQ line (`spec.md` §4.2), defaulting from the باب and edited on the project. Do not add it back as a helpful touch, as a tag on the heading, as a tooltip, as a column or inside the panel. An earlier version showed it ("هامش المشاريع الافتراضي 15%"); it was removed on purpose.

## Kit additions (the kit was missing these; each is now part of it)

1. Collapsible, sticky group row. A 52px button (the kit's static 44px group row was not interactive), chevron at the start, the باب name in Noto Kufi 600, and the item count ("118 بنداً") as quiet dim text at the end edge. `aria-expanded` is on the button. The chevron points to the end edge when closed (left in Arabic) and down when open, and flips with the direction. Rule: the group row is a button only when the group can collapse; otherwise it stays the static kit row.
2. In-group "load more" row. A quiet 48px button spanning the group at the end of its visible items. Never use it for page-level paging (the kit's pager remains for tables with no groups).
3. Internal-column marker. The cost column carries a faint tint in the header and cells (3.5 percent ink), an eye-off icon and a "داخلي" badge in its header, and the same badge and icon on the cost field label in the panel, plus the hint "داخلي: لا يظهر في أي مستند يراه العميل." Use it for any figure that must never reach a client. The tint plus icon plus word is the cue, not colour alone. The sell column is untinted: it is the ordinary figure.
4. Archived-row marker. A dashed 22px tag "مؤرشف" with an archive icon beside the code, and the row text drops to the dim colour. Never strikes the text through, because the item is not wrong, only retired.
5. Restore in place. The archive's opposite needs no confirmation: choosing "إلغاء الأرشفة" turns the row into a one-line "تمت استعادة «…» إلى القائمة النشطة." band in the green pair for about 2.5 seconds, then the row returns in its new place. No toast.
6. Dirty field. A field whose value differs from the saved one gets the ink border at 2px plus an inner ring, and the panel shows a notice banner at the top ("تعديل غير محفوظ في سعر التكلفة.") and enables Save. Save stays disabled while nothing has changed.
7. Recoverable confirmation. The quiet destructive confirmation (neutral fill, ordinary primary confirm, "you can undo this and here is where"), specified side by side with the irreversible one in "Two confirmations" and shown in the kit sheet (actions section). The catalogue's archive uses it.

(The price-origin marker, the derivation card and the phone filter disclosure that an earlier version of this section listed were removed with the derived sell price; the phone filter now fits in one row, see below.)

## Row, detail and actions

- Row (1440): identity cell leads with the Arabic description (bold) and demotes the code beside it (dim, small, tabular, `bdi`); then the English description (dim, left-to-right text, truncated), the unit (م³ and م² are written with superscript digits), the cost (internal tint), the sell price (plain), and the 44px actions button. Rows are 48px. The two money columns are end-aligned figures like any other money column.
- Below 1100px the shell collapses to the rail and the English description column drops out of the table (it is in the panel), so cost and sell are visible at 1000px; the identity column stays pinned. Below 820px the table becomes the stacked row.
- Phone toolbar: the full-width search, the full-width segmented filter (52px segments), then one row with the باب select and the import button side by side (52px). Nothing is hidden behind a disclosure.
- Phone row (at least 84px, no card): Arabic description at the start and the sell price at the end; code, unit and the archived tag at the start of the second line and the internal cost at the end (dim, eye-off icon, the word "تكلفة", so cost is never mistaken for sell); the 44px actions button spans both lines. A باب is a sticky 56px band with a chevron, the name and the count.
- Row menu: عرض التفاصيل, تعديل, then after a hairline "أرشفة…" for an active item (ink colour with the archive icon, not red, because archive is recoverable; the ellipsis says a question follows), or "إلغاء الأرشفة" for an archived one. Opening a row (a click anywhere on it) opens the detail panel.
- Archive confirmation, the RECOVERABLE pattern (inline, in place of the row, 64px): neutral note fill with an ink line above, the archive icon at the start, "أرشفة «اسم البند»؟" and, under it, "يختفي من القائمة النشطة، ويمكنك استرجاعه من «مؤرشف» في أي وقت." The confirm is the ordinary primary button "أرشفة" (not red); cancel is secondary. Busy state is the kit's. On a phone it is a full-width neutral block with two 52px buttons. Archive has no red anywhere on this screen.
- Detail panel (440px from the end edge; bottom sheet on a phone). Order: the prices first (it is why most people open an item), then the item's data. Header: the Arabic description as the title, the code demoted beside the باب name. Body: the cost price (money field, internal marker), the sell price (money field), then "بيانات البند": Arabic description, English description (left-to-right), the unit, and the code (read-only, generated, lock icon). Footer: Save (disabled until something changes), Close, and at the far end "أرشفة" as a secondary button with the archive icon (recoverable, so not the red trigger); it opens the recoverable confirmation as a confirmation dialog because the panel is already an overlay (kit stacking rule). The panel has two shapes by role (below). With the derivation card gone the panel is shorter: the two prices sit one above the other and the whole panel fits at 900px without scrolling for the Owner.
- Loading: the shell stays, the toolbar stays, and the table becomes 10 skeleton rows at real height.
- Empty states: no matches after search or filter uses the kit's no-results state (and the clear link); an empty archived view says "لا توجد بنود مؤرشفة" with an archive icon.

## The two shapes of the detail panel

`costPrice` and `baseSellRate` are guarded by `Permission.CataloguePriceEdit` (company-wide, Owner only), enforced server-side. `CatalogueManage` is unchanged and still covers everything else for the Technical Office: create, descriptions, unit, moving between أبواب, archive and unarchive. The panel therefore has two shapes, built from the same parts.

- Owner: both prices are editable money fields. Editing one turns that field's border dark, shows the unsaved-edit notice, and enables Save. The two figures never affect each other.
- Technical Office (read-only shape): both prices keep their place and their numbers. The heading "الأسعار" carries a quiet "للاطلاع فقط" badge with a lock, and one plain line under it says who changes these: "يعدّل المالك وحده سعر التكلفة وسعر البيع. بقية بيانات البند تعدّلها أنت." Each price is the kit's read-only field (the settled head-tone fill, the lock at the end, the text in full ink and selectable, NOT greyed out or dashed, so it never looks disabled or failed). No disabled buttons, no red, no error banner, no tooltip: it is a normal state, explained once in words.
- Both shapes keep the "داخلي" marker on cost. The rest of the panel (the item's data, Save, archive) is identical.
- Rule for the kit: a field a user may see but not edit is a read-only field with a lock and a one-line reason placed where the user looks, never a disabled field and never a hidden one.

## Rulings this screen is built on

These came from the client and `spec.md`, not from the design:
- Follow the spec: the catalogue item stores `costPrice` and `baseSellRate`. There is no derived sell price and no override on the catalogue. The باب markup is `lineMarkup` on a BOQ line (`spec.md` §4.2), defaulting from the باب and overridable per line on the project. This screen does not show or edit it at all (see the heading section).
- No silent re-pricing, and it does not belong on this screen. Signed BOQs stay frozen by copy; open estimates raise a manual review alert and a human decides per offer; a new price applies to future lines only. That behaviour belongs to the project's BOQ, so no sentence about it appears here.
- `Permission.CataloguePriceEdit` (Owner only) guards cost and sell price; `CatalogueManage` covers the rest. See the panel shapes above.
- Left to the builder's discretion: rounding, moving an item between أبواب (the panel shows the باب and offers no move control), and whether archiving an item in use shows a warning.
- The Excel import warns on an existing code and updates it on confirm. That is the import screen, designed later; this screen only has its entry button.

## Still open

- The 8 أبواب, the item names and the figures in the mockup are samples.


---

# The أبواب screen (`babs.html`)

Screen: الأبواب, the trade tree. It is the sibling of the catalogue and is composed from the same kit inside the same shell, so the two read as one product. Screenshots: `shots/bab-*.png` (desktop 1440 and phone 390, light and dark; states: the tree with one parent open and one closed, the margin editor open, after saving a margin, the move-to picker, the archive confirmation, the detail panel, the archived view, restore in place, no search results, empty, loading; plus one 1000px check and English/LTR). `?view=list|markup|saved|move|archive|panel|archived|unarch|nores|empty|loading` opens one state directly.

## What it holds

A باب is a trade section. About 40 of them, forming a tree. Each has a typed Latin uppercase code (`CON`, globally unique, immutable after create), an Arabic name, an English name, a sort order, a default markup percentage, and a count of the items it holds. This screen is the home of the default markup and the only place that edits it: a BOQ line on a project inherits it as its starting `lineMarkup` and the project may override it per line (`spec.md` §4.2). A باب is archived, never deleted, and can be restored. It can be moved to a different parent.

## Reused from the catalogue, unchanged

Page header and actions (search, one primary "باب جديد"), the segmented filter (نشط · مؤرشف · الكل) and the result count line, the table frame with its pinned identity column and the 1100px rail behaviour, the load-more row ("عرض 26 باباً أخرى"), the row menu (fixed-position), the recoverable archive confirmation with its "restore in place" counterpart, the archived-row marker, the no-results, empty and loading states, the side panel and bottom sheet, read-only generated code field with a lock, the neutral notice and the saved-in-place band. The identity cell leads with the Arabic name and demotes the code, exactly as in the catalogue.

## The tree

- Desktop rows are 48px. Each row has a 44px disclosure button at the start edge (a spacer of the same width for a leaf, so names stay aligned), then the Arabic name (bold), the code (dim, tabular) and, for a parent, "3 فروع" in dim text.
- Each depth indents 24px on the start edge, a logical offset that flips in English, and carries a 1px vertical guide that sits under the parent's disclosure button, so a parent and its children read as a group. The chevron points to the end edge (left in Arabic) when closed and down when open, and flips with the direction.
- Parents show their own item count and, dim beside it, the count with their children ("17 (118 مع الفروع)"). Leaves show one count.
- Columns: الباب (identity), English name, الترتيب, البنود, الهامش الافتراضي (%), and the 44px actions button. Below 1100px the shell collapses to the rail and the table keeps the identity column pinned. Under 820px the table is replaced by tree rows (below).
- "فتح الكل" and "طيّ الكل" are quiet buttons on the count line (hidden while searching, in the archived view and while loading). A search or the archived view flattens the tree into a result list and each row carries a quiet "ضمن: أعمال خرسانة" parent path, because a flattened row has lost its place in the tree. Archived children of an active parent also appear in the full "الكل" tree dimmed with the archive tag.
- Depth limit: the design is drawn for three levels. Beyond three, the indent stays 24px on desktop and 20px on a phone and the name wraps, but no new treatment is invented; ask before allowing deeper trees.

## The tree at 390

A tree is not shrunk. A row is a flex line of at least 72px: indent guides (20px per level), a 44px disclosure button, the name (bold, wrapping) with a second line "CON · 17 بنداً · 3 فروع" in dim text, then the markup as a labelled chip button ("12%" with a pencil, 44px high, 1.5px border) and the 44px actions button. Nothing is hidden behind a hover. The disclosure, the chip and the menu are three separate 44px targets; the rest of the row opens the details sheet. English name and sort order are in the sheet, not in the row. The toolbar is the full-width search, the full-width segmented filter, and the count line with "فتح الكل / طيّ الكل". The primary "باب جديد" is in the bottom action bar as everywhere. An open parent's children are plain rows below it, not a drill-down, because the real depth is two levels; if deeper trees ever become normal, a drill-down with a back link is the fallback to design.

## The markup percentage: a deliberate edit, without ceremony

The percentage is the only editable number here and it has consequences downstream, so it is treated as a deliberate act that stays cheap:

1. In the row it is a figure (tabular, Western digits, bold) with an explicit pencil button ("تعديل الهامش الافتراضي لـ …"), never a field that can be changed by accident, never an always-on input. On a phone the figure is itself a labelled chip button. The pencil is also in the row menu and in the details panel.
2. Choosing it opens the row into a bordered editor in place (2px ink border, no modal): the title "الهامش الافتراضي لـ «أعمال خرسانة»", the number field with a "%" affix, the current value, the explanation "يبدأ به كل بند جدول كميات جديد في المشاريع، ويمكن تغييره لكل بند هناك. تغييره هنا لا يعيد تسعير أي بند قائم.", and the buttons. While the value is unchanged Save ("حفظ الهامش") is disabled; once it differs the field's border darkens and a before-to-after line appears ("15% ‹ 18%" in reading order). There is no second confirmation, because the editor already states the consequence and shows both numbers.
3. Saving turns the editor into a green in-place band: "تم حفظ الهامش الافتراضي لـ «أعمال خرسانة»: من 15% إلى 18%. يبدأ به كل بند جدول كميات جديد في المشاريع، ولم يتغيّر أي سعر قائم." Cancel restores the row. Only one editor is open at a time.
4. The relationship is stated where the person is deciding, in plain words, not in a permanent banner and not in a tooltip: the percentage is a starting point for future BOQ lines, it is not a price and it re-prices nothing. The column header says only "الهامش الافتراضي (%)".
5. The audit trail records who changed it and the before and after (the kit's rule for every state change); the panel footer shows who last changed the باب and when.

## Move to…

Not dragging: the row menu and the details panel offer "نقل إلى…", which opens a picker. Desktop: a 520px dialog (kit stacking rules apply: it may be opened from the panel, and a confirmation may not stack on it). Phone: a bottom sheet with 56px rows. The picker is the tree as a radio list (indented, codes dim): at the top "بلا أب (باب رئيسي)", then every active باب. The current parent is marked "الحالي"; the باب itself and all of its descendants are disabled with the reason ("هذا الباب" / "من فروعه") so a cycle cannot be built, and the footer says so in a sentence. Choosing a target shows a plain consequence line ("سيصبح «خرسانة مسلحة» فرعاً من «أعمال تشطيبات»، وتنتقل معه بنوده وفروعه.") and enables "نقل". The move happens in place and the new parent opens to show the result.

## Archive (see the corrected section "Archive: a refusal when the باب holds items..." below)

The first version of this section said the confirmation names the subtree count and proceeds. That was wrong and is superseded by the corrected section further down: own active items refuse, no own items confirm quietly.

## The details panel

Side panel (440px) or bottom sheet. Header: Arabic name, code demoted, "17 بنداً و3 فروع". Fields: Arabic name, English name, sort order (number field), code (read-only, lock), "يتبع" (read-only path, with a secondary "نقل إلى…" button), and the default margin shown as a read-only field with a lock and a line saying it is edited from the table in a separate step, plus a quiet "تعديل الهامش" button that closes the panel and opens the row editor. Footer: Save (disabled until something changes), Close, and at the far end "أرشفة" (secondary, recoverable). The markup has exactly one edit path (the row editor), so there are not two ways to change it.

## Kit additions (the kit was missing these; each is in the kit sheet, section "إضافات من شاشة الكتالوج")

1. Tree row: indentation on the start edge, per-level guides, a 44px disclosure button with a same-width spacer for leaves, expand-all and collapse-all as quiet buttons, and the flattened parent-path line for result lists. Rules: indent with logical properties; never rely on indentation alone to carry the hierarchy for assistive technology (use `role="treegrid"` or `aria-level` and `aria-expanded` in the build).
2. Deliberate value editor: the row opens into a bordered editor with the explanation of consequence, a before-to-after line, a disabled-until-changed Save, and an in-place saved band. Use it for any single number whose change has downstream meaning. Do not use it for ordinary text fields.
3. Tree picker: a radio list with indentation, the current choice marked, and the item itself plus its descendants disabled with a reason; used for "move to…" and any "choose a parent" decision. No drag and drop anywhere in the product for this.
4. The before-to-after saved band is a variant of the in-place band.

## Not decided here (business questions)

- Who may edit the default markup. The mockup shows the Owner shape; if other roles can read but not edit, the margin follows the kit's read-only rule (a lock and a one-line reason where the editor would be) and the pencil disappears. The permission name is not given here.
- What archiving a parent does to its children and to its items (the confirmation currently states the counts and says it can be restored; it does not claim the children are archived with it), and whether a child can be restored while its parent is archived.
- Whether a child inherits its parent's default markup when created, or starts from its own.
- Whether the parent's item count rolls up the children (shown as "مع الفروع" for information, not as a rule).
- Maximum tree depth, whether sort order is per parent (the sample uses per-parent numbering for children), and whether codes of children carry the parent's code.
- Whether the markup has limits (a range, decimals); the field accepts a plain number.
- The 41 أبواب, their names and figures in the mockup are samples.


---

## The أبواب module, finished (`babs.html`, extended: create, edit and first run)

The module stays one file. The tree, the margin editor, move, archive, create, edit and the first-run state are one flow around one tree, and the user needs the tree in view while doing any of them, so a second file would only duplicate the shell. `?view=create|createerr|createchild|createpick|createsave|created|editdirty|editerr|empty` opens each new state.

### Create is a panel over the tree, not a page

A user creating أعمال كهرباء is looking at where it belongs: the siblings, the sort numbers, the parent. A full page would remove that context and add a route, a back button and a lost scroll position for a form of five fields. So creating uses the kit's side panel (desktop) and bottom sheet (phone), the same surface the edit uses, which makes create and edit one component with two headings. The tree stays visible behind the scrim. After saving, the new row appears in place, highlighted in the saved-band tint, scrolled into view, with its parent opened if it is a child.

Entry points: the page's primary "باب جديد" (and the phone bar's primary), and "إضافة باب فرعي" in a row's menu, which opens the same panel titled "باب فرعي جديد", with "ضمن: أعمال خرسانة" in the subtitle and the parent filled in.

### The create form

Fields in order (the code input sits after the English name), all in the kit's field wrapper (label, control, one footer line where the error REPLACES the hint, so nothing reflows):
1. الاسم بالعربية (required).
2. الاسم بالإنجليزية (optional, hint "اختياري.").
3. يتبع: a field-shaped button showing "بلا أب (باب رئيسي)" or the parent's name. It opens a SECOND STEP inside the same panel (not a dialog on top, which the stacking rules forbid): the header becomes a back link "رجوع إلى الباب الجديد" plus the title "اختيار الأب", the body is the tree picker (the kit's radio tree, "بلا أب" first), and the footer says in a sentence what will happen ("سيكون الباب الجديد فرعاً من «أعمال بناء».") with "تم" and "إلغاء". Choosing returns to the form with the parent set and the sort order re-suggested.
4. الترتيب: a number, NOT prefilled (a suggested "next free" number would presuppose per-parent numbering, which is not ruled), placeholder "مثال: 90", hint "رقم يحدّد موضع الباب في القائمة." It exists on create only.
5. الهامش الافتراضي: a number with a "%" affix, REQUIRED, empty, with the placeholder "مثال: 15" and the hint "يبدأ به كل بند جدول كميات جديد." It is a value the user types. There is no inherited or suggested value, because whether a child inherits its parent's margin is not ruled. The placeholder is dim example text, not a default.
6. رمز الباب: a typed, required, unique code (see below); it sits right after the English name in the form.

Footer: "إنشاء الباب" (primary) and "إلغاء". Busy: the primary keeps its size, shows the spinner and "جارٍ الإنشاء…", cancel is disabled.

### The code is TYPED on create, immutable afterwards (corrected)

An earlier version of this design showed the باب code as generated ("يُولَّد عند الحفظ"). That was wrong: `CreateBab` takes a `Code` the user supplies and `KAFF-204` has `bab.field.code` as an input. So on create the code is a real input: label "رمز الباب" with the required marker, Latin and uppercase (`text-transform: uppercase`, `dir="ltr"`, no autocapitalize guessing), placeholder "SCF", hint "حروف لاتينية كبيرة. فريد في النظام كله، ولا يمكن تغييره بعد الإنشاء." Its error replaces the hint: "رمز الباب مطلوب.", "حروف لاتينية فقط.", and on collision "الرمز CON مستخدم لباب آخر. الرموز فريدة في النظام كله." (the only uniqueness rule in the system is the code, globally and case-insensitively, `ux_babs_code`). On EDIT the code is shown read-only with a lock and "لا يُغيَّر بعد الإنشاء." because that is true there. The mockup uses the seeded codes (`D-145 §1`): CON, MAS, PLU, ELE, HVA, FIN, CAR, MET. The child codes (CNR, CNP, CNS, TIL, PNT, GYP) and OLD are sample data only. (The catalogue ITEM code is still shown as generated; verify that against `CreateItem` the way this one was verified, see the catalogue section.)

### Validation, shown

- Required: "اسم الباب بالعربية مطلوب." and the code errors above. The error sits in the footer slot in place of the hint, with an alert icon and a red border plus inner ring. There is NO duplicate-name error: no rule, ruling or constraint says two أبواب cannot share an Arabic name (an earlier version invented one; this is the same mistake as D-179, a plausible rule born inside a design). Whether names must be unique, and at what scope, is open (see "Still open").
- Margin: "الهامش الافتراضي مطلوب." when empty and "أدخل رقماً، مثل 15." when not a number. No range is stated, because none is ruled.
- A failed save also shows one banner at the top of the form ("لم يُنشأ الباب. صحّح الحقول المعلَّمة." or "تعذّر الحفظ...") in the refusal shape; the field errors stay next to their fields. On a phone the first error should be scrolled into view.
- Errors appear on save, not while typing.

### Edit (corrected)

The details panel is the edit form and `EditBab` takes only `(NameAr, NameEn, DefaultMarkup)`: so the panel edits the Arabic name and the English name, and nothing else. The sort order is shown READ-ONLY with a lock and "لا يُغيَّر بعد الإنشاء." (not removed: the user needs to see why the tree is ordered the way it is, and a locked field says that it cannot be changed, while an absent one would suggest it is changeable somewhere else). The code is read-only for the same reason. The parent is a read-only path with "نقل إلى…" (move has its own endpoint and picker). The margin is read-only here with a pointer to the row editor, which is its one edit path. Save is disabled until something changes; an unsaved-change notice appears; errors replace hints. Only the name error exists (no sort error).

### First run: an empty tree that teaches

A fresh install has no أبواب and the catalogue cannot take items without one. The empty state is a bordered panel, not an icon and "no data": the title "ابدأ بأول باب", two sentences saying what a باب is and that the catalogue needs at least one, the single primary "باب جديد" (the page header's and the bar's primaries are hidden in this state so there is only one), then three numbered steps (this one really is a sequence): create أبواب here, add items and prices in the price catalogue, and each BOQ line then starts from its باب's default margin. Beside it, a dashed "شكل الشجرة" box with the tag "مثال، وليس بيانات" shows a two-level example (أعمال خرسانة with two children, أعمال كهرباء) so a person sees the shape before making one. On a phone the example stacks below. The count line says "لا توجد أبواب".

### Archive: a refusal when the باب holds items, a quiet confirmation when it does not (corrected)

`D-130 §5` and `KAFF-213` rules 3 to 5: a باب holding ACTIVE ITEMS cannot be archived, the refusal names the count, and cascading was rejected. The count is the باب's OWN active items, not its subtree. Active CHILD أبواب do not block archiving, and archiving does not touch them. The earlier design drew a confirm-and-go for every باب with a subtree count; that flow would be rejected by the server. Now:
- Choosing "أرشفة…" on a باب with own active items shows a REFUSAL in place of the row (no confirmation step, because the answer is already known): the kit's refusal shape (red fill, red bar, alert icon): "تعذّرت أرشفة «أعمال خرسانة». فيه 17 بنداً نشطاً. انقل هذه البنود إلى باب آخر أو أرشفها أولاً، ثم أعد المحاولة." with a way forward ("عرض البنود (17)", a link to the catalogue) and "إغلاق". On a phone it is a full-width block under the row with two 52px buttons. The mockup shows the own count (17), not the subtree total; the row still shows "(118 مع الفروع)" as information only.
- Choosing it on a باب with NO own active items shows the quiet recoverable confirmation: "أرشفة «أعمال تشطيبات»؟ لا بنود نشطة فيه، ولا تتأثر أبوابه الفرعية (3). يمكنك استرجاعه من «مؤرشف» في أي وقت." The sentence about the children states what the code does (nothing happens to them); it does not say what an active child under an archived parent MEANS (open).
- The menu item stays enabled and leads to the refusal; it is not disabled, because a disabled item explains nothing on a phone. The details panel's "أرشفة" behaves the same.
- No new kit part: the refusal reuses the kit's server-refusal-with-a-next-step shape. Rule added: when the precondition is already known on the client, show the refusal immediately instead of an unneeded confirmation.

### Unarchive is designed ahead of the backend

"إلغاء الأرشفة" for a باب (the row menu item and the in-place restore band) is designed, but no unarchive endpoint exists for a باب: only `ArchiveBab` exists. `D-130 §4` rules that archived records can be un-archived in general, and a backend story is being cut. Do not build the button against an API that is not there; build it when the endpoint ships. (Items already have unarchive.)

### Child inheritance: confirmed none

`KAFF-204` rule 2: every باب carries its own default markup. There is no inheritance and no null. The create form asks for the margin outright, which was right. (This was confirmed, no longer pending.)

### Holes found in the module (neither of us had named them)

1. The catalogue's own first run: with no أبواب, the catalogue's "بند جديد" has nothing to attach to. The catalogue should show a pointer ("أنشئ باباً أولاً" with a link here) instead of a form that cannot be saved. Not drawn yet; it is a change to the catalogue screen.
2. Items and أبواب are coupled in three ways (move, archive, rename), which is why the list always shows the item count; this is why the count is on the archive message.
3. Duplicate names: no rule exists (removed from the design). It is an open question below.
4. Deleting does not exist (archive only), so a باب created by mistake lives in the archive. The recoverable pattern makes this cheap, but a "created by mistake" cleanup is a business question.
5. The sort order is typed. If the real need is only "put this before that", a move up and move down control would be kinder; not designed, because the screen is touched rarely.
6. Permissions: who creates, edits and archives أبواب is not stated. The panel has one shape (editable) in the mockup; if a role may only read, the kit's read-only rule applies (lock plus one reason line).

### Kit additions from the module

- Two-step panel: a back link replaces the heading and the body is replaced; no stacking. Shown in the kit sheet.
- The field-shaped button that opens a picker. (A "generated field with no value yet" was part of an earlier version of this list and is withdrawn: the باب code is typed.)
- First-run state pattern: title, two sentences, one primary, the sequence as numbered steps (only when it truly is a sequence), and a labelled example that is visibly not data. One primary only: hide the page-level primary while the first-run state is shown.
- Created-row highlight: the new row takes the saved-band tint and the band follows it, scrolled into view.


### Still open for the أبواب module (the BA pass refused to guess, and so does the design)

1. What an ACTIVE child under an ARCHIVED parent means. Nothing checks it today: a child can be created, or an item filed, under an archived باب. The mockup avoids drawing the case (the archived sample has no children), and the tree code that hides archived roots also hides their children only because there is nothing else to show; this is not a decision. Needs a ruling before the tree shows such a pair.
2. Whether changing a باب's default markup is Owner-only like catalogue cost (`D-177 §3`) or stays with the Technical Office as it is today. The mockup shows the editor to the user in the frame; if Owner-only, the kit's read-only shape applies (lock and one reason line, pencil removed).
3. Whether two أبواب may share an Arabic name, and if not, tree-wide or per parent.
4. Whether sort order is per parent or global, and what two siblings with the same number do (today a silent tie-break by code, which is behaviour, not a decision). The sample numbers (10, 20... for roots and 1, 2, 3 for children) are not a rule.


---

## The catalogue module, finished (`catalogue.html` extended; `catalogue-import.html` new)

The module is two files. Create, edit and the first-run states are one flow around the list and live in `catalogue.html` (same panel, same page). The import is a task of its own (a file, a preview of hundreds of rows, a decision, a result) that needs the full page width and its own back link, so it is a separate page, `catalogue-import.html`, reached from the list's "استيراد من Excel" button (and from the "no items" first-run state). `?view=...` opens each state in both files.

### Create a catalogue item (a panel over the list, like the باب)

The same reasoning as the أبواب: the list is the context, the form is five fields and a price section. It is the existing detail panel / bottom sheet, titled "بند جديد". Fields: الوصف بالعربية (required), الوصف بالإنجليزية (optional), رمز البند (required, typed), الباب (required; a field-shaped button that opens a second step in the same panel with the أبواب as a radio list, back link on top), الوحدة (required; a field-shaped button that opens the kit's select popup with م³ م² م.ط عدد طن كجم مقطوعية, which is a sample list), then the two prices (both required). Errors replace hints. The create form shows only errors that are real rules: required fields and "not a number" on a price; there is no duplicate-description rule (this screen first showed one and it was removed for being invented, the same mistake as D-179).

The code is TYPED, not generated (corrected). `CreateCatalogueItem/Request.cs`: "The code is typed, not generated. spec.md §4.1 makes the item's code one of its imported fields." Uniqueness is the database index `ux_catalogue_items_code`, normalised trimmed and upper-invariant. So create has a required Latin-uppercase input "رمز البند" (placeholder "CON-1030", hint "فريد في النظام كله، ولا يمكن تغييره بعد الإنشاء.") with the collision error "الرمز CON-1014 مستخدم لبند آخر. الرموز فريدة في النظام كله." that replaces the hint; no format rule is stated because none is ruled. On EDIT the code stays read-only with a lock (true there). The created band says "رمزه CON-1030" (not "المولَّد").

### Create is ONE form for the Owner and the Technical Office (corrected)

An earlier version drew a second shape for the Technical Office: no price fields, a "بلا سعر" item, a "2 بنود بلا سعر" count link. That flow cannot exist and must not be redrawn: `CreateCatalogueItem/Handler.cs:65-72` refuses a null cost price (`CostPriceRequired`) and a null sell rate (`SellRateRequired`) under defect rule **V-36-H** ("an omitted price is refused, not defaulted to 0: a money value nobody entered"; an explicit 0 is legal, absent is not; V-36-H got a story REJECTED). An unpriced item cannot be created, so there is no unpriced state anywhere: no "—" in a price cell, no "بلا سعر" tag, no filter or count for unpriced items.

What is true today: `CataloguePriceEdit` (D-177) guards EDIT and the re-import confirm, NOT create. So the Technical Office creates an item with BOTH prices typed and required, exactly like the Owner, and only later changes to a price are the Owner's. The form is identical for both roles (cost marked داخلي, errors for empty and not-a-number). The asymmetry (cannot change a price, can set it at birth) has been put to the client as a question; if he closes it, create gains a role shape and gets redrawn then. Design today's truth.

The EDIT panel keeps its two shapes exactly as designed (Owner edits cost and sell; everyone else reads them with a lock and one reason line): that one is real and tested.

### Unpriced items: not a state (removed)

See the record above (V-36-H). Nothing in the list, the panel, the filters or the count line represents an unpriced item.

### Edit

The existing details panel covers editing completely: Arabic and English description, unit, the code (read-only), cost and sell (Owner edits, others read), archive. Nothing new is designed and there is no second edit path. One gap, named and not built: moving an item to another باب. If wanted, reuse the create form's second step (the باب radio list) behind a "نقل إلى باب…" button in the panel; it is a CatalogueManage action.

### The catalogue's first run (a change to the screen already built)

Two empty states, drawn as changes to the list screen. They hide the page's other primaries so there is one:
- No أبواب (`nobabs`): the list is replaced by "أنشئ الأبواب أولاً": two sentences saying every item belongs to a باب and none exists yet, one primary "الانتقال إلى الأبواب" (a link to the أبواب screen), two numbered steps (create the أبواب there, then come back to add items or import), and a labelled example ("مثال، وليس بيانات") of how an item sits under a باب. "بند جديد", the import button and the search are hidden. Who may create أبواب is not stated, so the Technical Office text would need "اطلب من المالك" if they cannot; drawn for the Owner.
- أبواب exist, no items (`noitems`): "لا توجد بنود في الكتالوج", two paths side by side: the primary "بند جديد" for a few items and the secondary "استيراد من Excel" for a batch, with a one-line explanation of each (the import is a single batch with a preview, not a sync).

### Import from Excel (`catalogue-import.html`)

Ruled and not changed by the design: a bulk load, not a sync (`spec.md` §4.1); an existing code warns first and updates on confirm (Nabil, 2026-10-06); the Technical Office cannot move a price through an import, and the server refuses the WHOLE confirm if any existing price would move without `CataloguePriceEdit` (D-177).

The page is a sequence, so it has a numbered stepper (the one place numbering is true): 1 اختيار الملف، 2 المعاينة، 3 النتيجة. Back link "كتالوج الأسعار" above the title.

1. Choose the file. A dashed drop zone with "اختيار ملف" and a card "ما الذي سيحدث" that states the three rules in plain words (a row without a code is a new item and its code is generated; a row with an existing code updates that item after the preview warns; it is a single batch, not a sync), the required columns as chips (الرمز، الوصف بالعربية، الوصف بالإنجليزية، الوحدة، الباب، سعر التكلفة (داخلي)، سعر البيع) and a link to a template file. For the Technical Office the page says up front, before they pick a file, that they cannot change existing prices by import (new items with their prices are fine). An unreadable file gives an error banner naming the file and the problem (wrong format, missing columns); nothing changes. Parsing shows the file chip, a spinner, "لم يتغير شيء في الكتالوج بعد".
2. Preview (the important screen). A file chip (name, size, row count) with "اختيار ملف آخر". Three summary blocks that already separate the kinds of row by word, shape and border: جديدة 150 (plus icon, green pair), ستُحدَّث 62 with "48 منها تتغير أسعارها" (swap icon, ink outline), أخطاء 3 (warning triangle, red border, "لن تُطبَّق"). Under them a notice that states what will be overwritten ("62 بنداً موجوداً سيُستبدل ببيانات الملف... دفعة واحدة وليس مزامنة"). A segmented filter (الكل / جديدة / ستُحدَّث / أخطاء with counts) and the table. Every row has a status pill with a shape, the file's row number, the identity cell (description leads, code dim, or "الرمز يُولَّد" for new), باب, unit, cost (internal marker kept) and sell, and a last column "ما يتغير" (names the fields that change) or "سبب الخطأ". Rows that UPDATE show the new value bold with the old value struck beneath it, only on the cells that change, and a description change shows "كان: …" under the name. New rows show their figures plainly; error rows are tinted and say why ("الرمز CON-9999 غير موجود. الرموز تُولَّد ولا تُدخل: اترك الخلية فارغة لإضافة بند جديد."، "الباب «أعمال حديد» غير موجود"، "سعر التكلفة ليس رقماً"). On a phone each row is a stacked block with the same pill, the same struck-through before and after, and the same reasons.
3. The apply bar (sticky on desktop; the primary in the bottom action bar on a phone): "سيُطبَّق: إضافة 150، وتحديث 62 (48 بتغيير سعر)" and "تُتخطّى 3 صفوف بها أخطاء. لا يمكن التراجع عن الاستيراد بعد تطبيقه.", with the primary "تطبيق الاستيراد" and "اختيار ملف آخر".
4. The confirmation, the LOUD one. An import that rewrites 62 items and 48 prices cannot be undone, so it uses the irreversible pattern: a dialog (a bottom sheet on a phone) with the red-tint icon tile, "تطبيق الاستيراد؟", the numbers again ("سيُضاف 150 بنداً ويُحدَّث 62 بنداً موجوداً، منها 48 تتغير أسعارها"), the skipped count, "لا يمكن التراجع عن الاستيراد. تُسجَّل التغييرات في سجل التدقيق، ولا يوجد زر استرجاع.", the red "تأكيد الاستيراد", and "إلغاء" with initial focus. No type-to-confirm ceremony: the numbers in the dialog are the check. Busy: the button keeps its size, "جارٍ التطبيق…", cancel disabled.
5. The result. All three steps checked. A green in-place banner "تم الاستيراد. أُضيف 150 بنداً، وحُدِّث 62 (48 منها بتغيير سعر)، وتُخطّي 3 صفوف. كل التغييرات مسجّلة في سجل التدقيق.", three count blocks, "عرض الكتالوج" (primary), "تنزيل تقرير الاستيراد", a link to the audit log, and the skipped rows with their reasons below so the person can fix the file.

### The refusal path (Technical Office)

The refusal is shown BEFORE the confirm, because the rule is knowable from the preview: the server rejects the whole confirm if any existing price would move without `CataloguePriceEdit`, so offering "apply" would be offering something that must fail.
- In the preview, rows that would move a price are tinted and carry a lock pill "سعر لا تملك تغييره" instead of "تحديث"; the old and new figures stay visible.
- A refusal banner in the page's red refusal shape heads the preview: "هذا الاستيراد مرفوض لحسابك. 48 صفاً يغيّر سعراً قائماً، وتغيير الأسعار للمالك وحده، والخادم يرفض الملف كله إن وُجد فيه تغيير سعر. أزل التغييرات السعرية من الملف ثم ارفعه من جديد، أو اطلب من المالك تطبيقه. الصفوف المعلَّمة بقفل هي التي تغيّر السعر."
- The apply bar has no apply button at all (not a disabled one): it says "لا يمكنك تطبيق هذا الملف" with the reason, "اختيار ملف آخر", and "تنزيل قائمة الصفوف المرفوضة". There is no "apply the rest" option, because the server refuses the whole confirm.
- If the server still refuses at confirm (the file changed or the role changed after the preview): a separate state, "رُفض التطبيق ولم يتغيّر أي شيء في الكتالوج", naming the cause and the count, confirming that nothing was added or updated, with "اختيار ملف آخر" and a way back.

### Assumptions in the import that are NOT in the spec (flagged, adjust if wrong)

1. A row with no code is a new item; a row with a code that does not exist is an error (codes are generated, not typed). If the real rule creates an item with the typed code, the error row becomes a new row.
2. Rows with errors are skipped and the rest is applied; the alternative is to block the whole file on any error. The apply bar says how many are skipped, and the dialog repeats it.
3. For the Technical Office, new rows in the file are imported WITH the prices in the file (create is not guarded by `CataloguePriceEdit`); only a row that would change an existing price is refused. (An earlier version said new rows import unpriced; withdrawn with the unpriced state.)
4. Whether unchanged rows (same data) appear in the preview at all; the mockup lists only rows that do something.
5. The file layout (one sheet, the seven columns) and the template file are placeholders.

### Holes found in the module

1. The Technical Office can set a price at creation but not change it later (D-177 guards edit and import, not create). A question for the client; the design shows today's truth.
2. An import that rewrites prices is, on the Owner's side, a pricing decision: nothing shows the Owner the total effect before applying (for example the average change). The preview shows each row; a one-line aggregate ("48 أسعاراً تتغير، أكبر زيادة …") was not drawn because it is a business calculation.
3. The result is the only place the skipped rows live; the report download is the lasting record. Whether the audit log records an import as one entry or one per item was not stated; the design only says "كل التغييرات مسجّلة".
4. Moving an item between أبواب has no designed screen (named under Edit).
5. Archiving an item that is used in an open BOQ was left to the builder earlier and still has no design.
6. A very large file (thousands of rows): the preview shows a sample ("يعرض 9 من 215 صفاً") and a load-more; real paging and filtering of the preview is implied by the segmented filter, not drawn.

### Kit additions from the catalogue module

- Import diff cell: new value bold, old value struck beneath, only on cells that change; status pills for new, update and error with shapes.
- Stepper for a true sequence, summary count blocks, a sticky apply bar that carries what will happen and "can't undo", and the irreversible confirmation used for a bulk action.
- First-run and no-items states (the same pattern as the أبواب first run).

---

# The project workspace (`project.html`)

**PROPOSAL, not approved; r3 2026-11-11.** Nabil saw r2 (`5ac8028`) and ruled that the project page and its tabs must be better, and that the tabs are the project's real screens: the user stays inside the project and always sees its name and its stage. r3 draws the **project frame** (header, stage line, tabs) and the tab **نظرة عامة**. The earlier versions are in git history.

Route `/projects/:projectId` (and, later, `/projects/:projectId/<tab>`). Open `project.html` directly, no build step. `?view=normal|turn|site|sitedone|nolog|setup|archived|loading|failed|partial|refused`, `?type=ls|cp|ds` (default `ls`), `?tab=team` shows the team tab, `?terms=1` opens the contract-terms disclosure, `?embed=1` hides the preview strip, `?flow=1` lets a screenshot take the whole page. The strip and `flow` are review chrome: do not build them.

## What r2 got wrong, and what changed

- Anchor links jumping down one long page, no active mark, a 2900 px phone page: now real tabs, one active, and the overview is one short screen and a half at 390.
- Nothing said where the project is in its life: now a stage line in the frame.
- Nine cards of equal weight: now one start point (the waiting line for the owner, the large percent for everyone) and a calm list under it.
- Headings "الفهم" and "الكميات" meant nothing to a site man: gone. The overview has no section names except "الأبواب". The BOQ line count is off this tab.
- Dates printed as `2026-11-10`: now written as the daily log writes them, "الثلاثاء 10 نوفمبر" (weekday, day, month, no year).
- Lump Sum is written "مقاولة بالمقطوعية", the wording of `project-create.html` (r2 used "مقاولة بمبلغ مقطوع"). Cost Plus is written "التكلفة زائد نسبة إشراف", also from `project-create.html`. Both are wordings to settle (see not decided).

## The project frame (one named part: put it in the kit as `project-frame`)

The frame sits above the content of every tab of a project and does not change when the tab changes. It replaces the page header each project screen draws today (back link + title + status pill + meta line), the group jump line of the old workspace, and, on screens that were a long page of sections, the sections themselves. `boq.html`, `daily-log.html` and `day-labour.html` adopt it in later runs: each keeps its own content under the tab bar and loses its own project title block.

**Anatomy, top to bottom.**
1. **Back link** "المشاريع" (kit back link, 44px).
2. **Title row**: project name (display font, 2rem on desktop, 1.625rem on a phone, wraps) and the status pill. The pill carries only the five Kaff status words (لم تبدأ in Setup, جاري العمل while running). A project that is archived shows no pill (archived is not one of the five).
3. **Meta line** (owner and roles who see money or the client only): client name (link) and the contract-type badge. The junior site engineer's frame has no meta line.
4. **Stage line** (`stl`): five stages from `ProjectStatus` in `src/Domain/Projects/ProjectStatus.cs`: Setup is المقايسة, Active is التنفيذ, HandoverPending and Handover are together التسليم, UnderWarranty is الضمان, Closed is الإقفال. Extracts are not a stage; they run during التنفيذ. Each stage is one of three shapes: **done** (filled disc with a check, ink label), **current** (ring with a centre dot, bold ink label), **coming** (hollow ring, dim label). A rule joins the markers: solid up to and including the current stage, dashed after it. A coming stage has no sentence and no lock (D-217 item 38). The first stage sits at the start edge (right in Arabic, left in English; the rule mirrors). Five equal columns, labels centred under their markers; at 360 px each column is about 65 px and the longest label (المقايسة) fits on one line, so the line never scrolls. Maximum width 640 px on desktop. The line is a labelled `ol`; the current stage has `aria-current="step"`. Not drawn on an archived project (nothing is current).
5. **Primary action slot**: unchanged from the shell rule. The frame never renders the primary itself; the shell puts it at the end of the title row on desktop and in the bottom bar on a phone. One per screen.
6. **Tab bar** (`tabs`, the kit's tabs part used as the screen switcher): نظرة عامة · المقايسة · اليومية · المستخلصات · الفريق. Each tab is a link to the project's screen of that name (`<a>`, `aria-current="page"` on the active one). 48 px high on desktop, 56 px on a phone, the 4 px ink bar and the bold weight mark the active tab (colour is never the cue). The bar is **sticky** at the top of the scrolling area, under the frame. On a phone it scrolls sideways inside itself (scrollbar hidden), keeps the active tab in view, and the page never scrolls sideways. First tab at the start edge.
   - A tab the viewer may not open, or that belongs to a stage not reached, is **absent**, never greyed. Owner of a running Lump Sum project: all five. Unsigned or archived Lump Sum: نظرة عامة, المقايسة, الفريق. Junior site engineer: نظرة عامة, اليومية, الفريق (no المستخلصات, D-217 item 37; no المقايسة, a guess, see not decided). Cost Plus: نظرة عامة, اليومية, الفريق. Design: نظرة عامة, الفريق.
   - This is a departure from the kit's tabs rule "not for navigation between screens": a project is a second level of navigation inside the sidebar's المشاريع, and Nabil ruled the tabs are the project's screens. The kit rule should be amended to say so.

**States.** Loading: the frame keeps its shape (title bar and stage line as skeletons), the tab bar is absent, the pane shows skeletons. Failed to load and refused: only the back link and the neutral title "المشروع"; no stage line, no tabs, no project detail.

## Tab: نظرة عامة (the overview)

One short pane under the tab bar. Order and content by role:

- **Owner, running Lump Sum.** (1) The waiting line: "المستخلص 3 عند المكتب الفني: اعتماد الكميات" (on the owner's turn: "المستخلص 3 عندك: اعتماد المالك", and the primary becomes "فتح المستخلص"). (2) The **hero**: the percent done, large (3rem, 2.5rem on a phone), a 14 px bar, and under a hairline three figures: قيمة العقد (unit ج.م once), منفَّذ في الموقع, معتمد في المستخلصات. (3) "الأبواب": one row per باب with its status pill and a percent bar. (4) Three money figures: المطالَب به (unit once), المحصَّل, المتبقي على العميل. (5) The disclosure "شروط العقد" opens hold rate, advance rate, signing date, start date.
- **Junior site engineer.** The hero with the percent only (no figures, no money), "الأبواب", then one row "آخر يومية" with the date and one tag, "سُجِّلت اليوم" or "لم تُسجَّل اليوم" ("لا يوجد" when no log). Primary "سجل يومي جديد". The crew link of r2 is off this tab (the crew screen is reached from the daily log).
- **Setup (BOQ unsigned).** The state line "المشروع غير موقّع. تنتهي فترة المتابعة في {date}." and the contract value "لم تُحدَّد بعد" (never 0.00). Stage line: المقايسة current, the rest coming. Tabs: نظرة عامة, المقايسة, الفريق. Primary "فتح المقايسة". There is no separate "empty BOQ" view: the BOQ's empty state lives on the المقايسة tab (not drawn here).
- **Archived.** The line "المشروع مؤرشف", the contract value row, primary "إعادة فتح". No stage line.
- **المال fails alone** (`partial`): the refusal banner "تعذّر تحميل بيانات المال." with retry replaces the three money figures; the rest stays.
- **Cost Plus** (`?type=cp`): supervision rate, cost to date, supervision to date. **Design** (`?type=ds`): the hero (percent of stages approved), area, rate and design value, and "المراحل" with a percent bar each. Their waiting line, money group and primary actions are not drawn.

## Tab: الفريق

Drawn as its own tab view (`?tab=team`): the team by name and role, one list, nothing else. The other tabs (المقايسة, اليومية) are links to `boq.html` and `daily-log.html`; المستخلصات is inert (no mock).

## Reused from the kit, unchanged

Status pill, badge, back link, link, buttons, refusal banner with retry, notice banner, the refused state, skeleton, `fig` (money, percent as LTR isolates), list (`lst`), the shell, the sidebar and bottom bar with the role label under the user name, the tabs part (with `aria-current` for links).

## New in the kit (added to `components.html`, section «إضافات من سلسلة المستخلص», `#s-extracts`)

- **Project frame**: the named part above (header, meta line, stage line, tab bar). Specified so three other screens can adopt it. Its stage line is also a new part, **stage line (`stl`)**, usable alone.
- **Hero**: the large percent, a 14 px bar and an optional row of up to three figures.
- **Fact list (`kv`)**, **row percent bar (`lst .bd`)**, **disclosure link (`disc`)**: as in r2.

Removed from r2: the group section and its heading column, the group jump line, the coming-step group, the closing group, the count of BOQ lines, executed and certified figures as a separate pair (now inside the hero).

## Left off the screen, on purpose

Project code; any question or explanation line; the BOQ line count; chips of any kind; hold and advance rates, signing date and start date on the first view; the holder's personal name (the waiting line names the role once); the client and type for the junior site engineer; the crew link on the overview; the stop control; the ledger strip, waterfall and extract chain (they belong to the المستخلصات tab); `Stopped` and `Terminated` (not stages).

## Sentences left on the screen

- "المشروع غير موقّع. تنتهي فترة المتابعة في {date}." A state line (D-208).
- The waiting line: who it waits on, the one fact the owner opens the project for.
- "المشروع مؤرشف", "تعذّر تحميل المشروع.", "تعذّر تحميل بيانات المال.", "هذا المشروع خارج تكليفك" with "اطلب الإسناد ممن يملك صلاحية إسناد المشاريع.". States and one refusal reason.

No code or id is left on the screen. Dates are shown where a date is the fact (last log, unsigned deadline, terms).

## Not decided here

1. **What the supervising site engineer sees** (item 37 rules the junior only).
2. **Stop and restart**, and the states Stopped and Terminated: not stages, not drawn.
3. **Who reopens an archived project**: "إعادة فتح" is drawn for the owner.
4. **Closure words and stage labels.** التسليم, الضمان and الإقفال are the writer's words (from r2), not ruled. المقايسة and التنفيذ are also the writer's choice. The mapping of HandoverPending and Handover into one stage, التسليم, is a guess.
5. **Which tab opens first for each role.** Every view here opens on نظرة عامة.
6. **Whether the junior site engineer sees المقايسة** (it carries rates, so the mock leaves it off).
7. **Wording of the contract types** across screens: "مقاولة بالمقطوعية" (project-create, now here) against "مقاولة بمبلغ مقطوع" (r2); "التكلفة زائد نسبة إشراف" against "التكلفة زائد إشراف". One wording must be settled for the build.
8. **The stage line for Cost Plus and Design**: the label المقايسة does not fit a type that has no BOQ; the mock draws the same five labels.
9. **Cost Plus and Design** money, primary action and waiting line.
10. **Who prepares the monthly extract, what a terminated project settles, how a credit note is paid** (Karim): nothing drawn.

## What the mock does not draw

The tabs المقايسة, اليومية and المستخلصات (the first two are links to the existing mocks, the third is inert); the supervising engineer's view; the English/LTR text beyond the direction switch; a project in HandoverPending, Handover, UnderWarranty or Closed (the stage line shape for those is the same, with the current marker moved).

# Today's crew (`day-labour.html`)

Approved by Nabil, 2026-11-10, with these two fixes (D-213): the title is neutral and the date shows once. Revised under D-217, 2026-11-11: hours cap, who edits a saved day, days back, the back-dated tag, no phone in the sheet. Everything else is as approved.

Mock of the day-labour screen (route `/projects/:projectId/day-labour`), brief 2 of `ux/journey-project-module-2026-11-09.md`. Labour is hours, per man, per day (D-213); a man may work 4 hours one day and 8 another. Brief 1 (the daily log without labour) is `daily-log.html`. Open `day-labour.html` directly. `?view=normal|filled|saved|yesterday|backdated|flag|empty|sheet|loading|failed|refused|unassigned`; `embed=1` hides the preview strip (review chrome, do not build it). The stepper, the "same crew as yesterday" button, the day switch and the add-a-man sheet work.

## What it holds

For the entering site engineer and the supervising engineer, on a phone, one job: say how many hours each man worked on the chosen day, in one save.

- Back link carrying the project name (where am I), the title "العمالة" (neutral, so it never contradicts the selected day), and the day switch.
- The day switch is the one in `daily-log.html`, copied: اليوم, أمس, then the two earlier day names (الأحد, السبت), with the full date written once beside it. Today and up to 3 days back (D-217 item 7).
- Totals strip, sticky: men and hours. Before anything is entered it says "لم يُسجَّل أحد بعد".
- "نفس عمالة أمس" (outline button), present only on today while nothing is entered. It copies the men and their hours from yesterday (item 4); the engineer then adjusts.
- The crew: one row per engaged man, name and trade stacked on the start side, one hours stepper on the end side. A man with hours gets a 5px start bar. The unit is carried once, as the column head "ساعات", and again in the totals strip.
- Hours stop at 16 per man per day (item 1) and move in half-hour steps (item 2). Plus is disabled at 16. A typed value above 16 is kept and refused in place: error border and "الحد الأقصى 16 ساعة"; "حفظ" is disabled until it is corrected.
- "أضف عاملاً" (outline) at the foot of the list opens the add-a-man sheet.
- Primary action "حفظ", in the bottom bar on a phone and in the page header on desktop. Disabled until one man has hours, and while a row is refused.

## Who sees and edits what (D-217 items 3 and 8)

- **Today, saved**: the engineer who entered it sees "تعديل". Rows return to steppers.
- **An earlier day, saved** (view `yesterday`): the entering engineer sees it read-only: ticks and hours, no steppers, no "أضف عاملاً", no primary. The controls are absent, not greyed.
- **An earlier day, saved, as the supervising engineer** (view `flag`): rows are editable and "حفظ" is the primary. Beside the date sits the tag "بأثر رجعي", drawn as in `daily-log.html ?view=flag` (status pill, clock icon). The owner sees the same tag; no separate view is drawn.
- **An earlier day, nothing saved yet** (view `backdated`): the entering engineer enters hours as on today. There is no banner and no tag for him (item 8).
- The old "بأثر رجعي" note banner is removed.

## States

- **Normal**: crew listed, nothing entered. Yesterday is saved, so switching to أمس shows it read-only.
- **Filled**: hours entered, totals live.
- **Saved**: rows read-only with a tick and hours, "تم الحفظ", primary "تعديل". "أضف عاملاً" is absent.
- **Empty**: "لا عمالة مشغَّلة على المشروع". "أضف عاملاً" is the primary here.
- **Add-a-man sheet**: bottom sheet on a phone, side panel on desktop. Search "اسم أو مهنة", by name or trade. Each result: name, trade, times engaged, rating and one "تشغيل" button. No phone number (item 6). "تسجيل عامل جديد" link at the foot.
- **Loading**: skeleton rows. **Failed**: refusal banner with retry. **Unassigned**: the project-scoped refusal.
- **Row refused**: saved rows show a tick; one row refused above 16 hours; one row whose save failed, which keeps its hours and shows "إعادة المحاولة".

## Reused from the kit, unchanged

App shell and bottom bar, back link, buttons (outline `sec2`, small), the day switch of `daily-log.html` (`seg days`), status pill, search field, refusal banner, saved-in-place banner (`rec`, now defined in this file's stylesheet; it was used without a rule before), the four list states, skeleton, bottom sheet.

## New in the kit (put these in `components.html` before building)

- **Hours stepper** (`stp`): minus, typed value, plus; 48px field, 44px-plus buttons, no unit text inside. Minus disabled at zero, plus at the cap. In error the border takes the error colour and a line under the row gives the reason.
- **Crew row** (`man`): name over trade, one stepper on the same line. Variants: entered (start bar), saved (tick and hours, read-only), refused, failed (retry line). The column head above the first row carries the unit.
- **Totals strip** (`tot-in`): men and hours; the zero variant is one line.
- **Side panel on desktop, bottom sheet on a phone** (`sx`).

No new colour: everything is an existing token.

## Left off the screen, on purpose

Worker code, the day rate (D-139), phone number anywhere, times engaged and rating on the row (they show in the sheet), a link to past days for the supervisor (item 5), any explanatory sentence. No code of any kind is on the screen.

## Not decided here

1. The crew screen opened directly with no check-in today. The daily log hides its crew link before check-in (D-217 item 9); what this screen does when opened by its address with no check-in is not ruled. The mock draws no such state.
2. What a supervising engineer's edit of an earlier day leaves for the entering engineer to see. Nothing is drawn.
3. Whether the supervising engineer may edit today's saved day. Item 3 names only the entering engineer for today; the mock shows no "تعديل" to the supervising engineer on today.
4. Whether the supervising engineer sees an unsaved earlier day differently from the entering engineer. The mock draws one editable state.
5. Whether the owner may edit. The owner sees the tag; the owner's edit rights are not ruled.
6. The Supervisor's history link: dropped, not asked.

## What the mock does not draw

Weekly payroll; rate entry for a new man; registration (`.../day-labour/new`); closing an engagement and rating; the history route; the owner's view; the English/LTR text; how "نفس عمالة أمس" behaves on an earlier day (it appears on today only).

# The daily log (`daily-log.html`)

**Approved by Nabil, 2026-11-10, under D-217 (items 7 to 16 applied).** Nothing is built yet.

Mock of the daily-log screen (route `/projects/:projectId/daily-log`), brief 1 of `ux/journey-project-module-2026-11-09.md`, with Nabil's rulings D-212, D-213, D-214, D-215 and D-217 applied; where they differ, D-217 stands. Open `daily-log.html` directly. `?view=before|location|empty|saved|qty|edit|picker|mat|photo|past|flag|far|stoppage|stopped|stoppedsaved|closed|loading|failed`; `embed=1` hides the preview strip (review chrome, do not build it). Working in the mock: check-in and check-out, the four-day switch, the kind switch, the item picker with search by name, saving an entry (it joins "المسجَّل", the form clears, the executed figure moves), editing a saved quantity.

## What it holds

For the site engineer or Supervisor, on a phone, one job: put the day's site facts into the log, as quantities, materials, notes, photos and, when work stops, the reason. It is the same tool as the crew screen (`day-labour.html`): header, bottom bar with one primary and the date switch family are shared.

- **Check-in comes first (D-217 item 9).** Until the engineer has checked in today the screen holds only the back link, the neutral title "اليومية", one state block "لم يُسجَّل الحضور بعد" and one primary, "الحضور" (bottom bar on a phone, page header on desktop). No date switch, no crew link, no entry control of any kind: they are absent, not greyed. After check-in the entry screen replaces it.
- **Check-in strip (today only):** a tick, "الحضور" and the time, and an outline "الانصراف" button; after check-out, both times and no button. A far-from-site check-in (from 500 m, item 18) is accepted and carries the tag "بعيد عن الموقع" (shape and word) beside the time. No dropdown (D-207, D-208).
- **Day switch (item 7):** four buttons, today and up to three days back: اليوم · أمس · الأحد · السبت (weekday names for the older two), the full date written once beside it. It appears only after check-in, because a past day is entered by an engineer who is checked in today (item 10). A past day has no check-in strip and no check-in line.
- **Back-dated entries carry no banner or tag for the entering engineer (item 8).** Only the supervising engineer (and the owner) sees the flag: each back-dated entry in "المسجَّل" carries the tag "بأثر رجعي" (`?view=flag`, signed in as "مهندس مشرف"). The old banner "بأثر رجعي" of the first draft is gone.
- **Crew link:** one navigation row "العمالة" with the day's summary and a chevron, to the crew screen. A link, not a button: the page has one primary. Labour is not entered here (D-213).
- **The entry form**, first on a phone; five kinds in a segmented row (3 + 2): ملاحظة · كمية · خامات · صورة · سبب التوقف, each 64px high. Note: one text area. Quantity: the BOQ line picked by name, "الكمية المقاسة" and "المنفَّذ حتى الآن" side by side (unit in the label), then "تنفيذ اليوم (تقديري)" with the unit as the field affix. Materials: name, quantity, unit from a list. Photo: a large dashed "التقاط صورة" and an optional caption. Stoppage: one required text area "سبب التوقف".
- **"المسجَّل" for the chosen day**, below the form: grouped by kind (سبب التوقف, الكميات التقديرية, الخامات, الملاحظات, الصور), each group with a count badge, newest first inside a group. A second quantity for the same item on the same day is a second row (it adds, item 11). A second note is a second row beside the first (item 12). A saved quantity row carries "تعديل".
- **Edit a saved quantity (item 11):** "تعديل" loads that row into the form. The kind row is absent while editing, the line is shown read-only (settled style), the quantity field holds the saved figure, the row being edited gets a start bar, "إلغاء" (quiet) sits under the field, and the primary reads "حفظ التعديل". How the edit is stored is not ruled (see below).
- **Primary "حفظ":** bottom bar on a phone, page header on desktop. Disabled until the form is complete; required fields carry the `*`.

## States

- **Not checked in** (`before`): see above. **Location refused** (`location`): the refusal banner above the state block says why and how to switch location on; "الحضور" stays pressable.
- **Checked in, log empty** (`empty`), **entries saved** (`saved`: two quantities for the same item, two notes), **quantity form** (`qty`), **edit** (`edit`), **item picker** (`picker`: the add-a-man sheet part, search "اسم البند", results show line name, باب name, "المنفَّذ ≈ x من y unit" and "اختيار"; no code anywhere), **materials** (`mat`), **photo** (`photo`).
- **Past day** (`past`, as the entering engineer): switch on أمس, yesterday's own entries, no strip, no flag.
- **Supervising engineer** (`flag`): the same day with the flag on each back-dated entry.
- **Far from site** (`far`): tag beside the check-in time.
- **Stoppage on a running project** (`stoppage`): the "سبب التوقف" kind is available on a running project (item 14); selecting it shows the text area. The project status does not change and nothing about status is shown.
- **Stopped project** (`stopped`, `stoppedsaved`): a note banner "المشروع متوقف" (a state, not a sentence). A stopped project still takes quantities, materials and photos (item 15), so the kinds are unchanged; the stoppage kind is preselected. `stoppedsaved` shows the saved reason row.
- **Project does not take a log yet** (`closed`): refused state with "هذا المشروع لا يقبل اليومية بعد" and a link back to the workspace. No form, no date switch, no primary.
- **Loading**: skeleton of the strip and the list. **Failed**: refusal banner with retry; the strip stays, the form is not drawn (the item list may not have loaded; attendance does not depend on it).

## Quantity is an estimate (D-214, kept)

The input is labelled "تنفيذ اليوم (تقديري)" (a past day: "تنفيذ الأحد (تقديري)"); every executed figure carries "≈" inside it, left to right ("≈ 12.5"): the "المنفَّذ حتى الآن" cell, each saved quantity row, the picker result; the group is headed "الكميات التقديرية". The measured quantity has no "≈". Confirming the real quantity happens on the extract screen (item 13), not here: no confirm control is drawn.

## Reused from the kit, unchanged

App shell, bottom bar with one primary, back link, outline and quiet buttons (`sec2`, small), segmented control (day switch and kind row), form field with label / control / affix / `*`, text area, select, read-only field (`ro`), tot-in, badge, pill (`s-none`, for "بعيد عن الموقع" and "بأثر رجعي"), refusal banner, note banner, saved-in-place banner (`ban ok`; `day-labour.html` names its saved banner `rec` and never defines it, so it should switch to `ban ok` when built), state block, skeleton, bottom sheet / side panel (`sx`), search field, result list, text link.

## New in the kit (put these in `components.html` before building)

- **Check-in strip** (`att`): one bordered row, tick + label + time at the start, outline button at the end; variants in and out. Before check-in the same action is the page primary instead.
- **Link row** (`clink`): icon, bold name over a dim summary, chevron; 64px; hover tint. A navigation row inside a page.
- **Log group** (`lg`) and **log entry** (`ent`): a head row in the table-head tone with icon, name and count badge; entry rows with the text at the start, one figure and, for a quantity, a quiet "تعديل" button at the end; the edited row has a 5px start bar. The photo variant has a 48px thumbnail at the start.
- **Kind row**: the kit segmented control as equal buttons (icon over word, 64px, fills the width; five wrap 3 + 2). The day switch is the same control with four plain buttons.
- **Picker trigger**: a field whose whole 56px surface is a button with a chevron, opening the picker sheet.
- **Photo capture** (`shot`): a 72px dashed full-width button; once taken it becomes a row with a 56px thumbnail and "إزالة".

No new colour.

## Left off the screen, on purpose

The estimated (Sales) quantity, project code and contract value, line codes (picked and searched by name), entry ids, the photo's file reference, the المستخلصات link, the worker dropdown, hours box and labour kind (crew screen), the free "unit" box on materials, both hint sentences of the old screen, the weather row and any quick-choice chip (weather is written as the stoppage reason, D-217 item 16), the back-dated banner (the flag is the supervising engineer's, item 8).

## Not decided here (guesses in the mock)

1. **Offline.** Not drawn; whether the page queues drafts is not checked.
2. **How a corrected quantity is stored** (D-217 item 11). Daily-log entries are append-only deltas today. The mock shows the saved row changed in place and "المنفَّذ حتى الآن" recomputed; whether the edit is a reversing entry, a replacement or an in-place update is not ruled.
3. **Photo upload.** The API has `RecordPhoto` but no upload endpoint was found (journey break 13). Needs backend before the photo kind can be built.
4. **Materials unit list.** The seven units (كيس, م³, م², طن, متر, قطعة, لتر) stay for the mock; Karim confirms the list (item 17).
5. **Second stoppage reason on the same day.** The mock keeps one (saving replaces it and the form opens with the current text); notes are the kind ruled to stack.
6. **Check-in rules around the past days.** The mock offers the four-day switch only after check-in today. Whether a check-out is needed, and what happens to a past day if the engineer never checks in today, is not ruled.
7. **Edit permission.** Who may edit a saved quantity and until when (the crew screen rule of item 3 is the nearest, not applied here).
8. **Does an entry on a past day need a reason?** Not asked; none is drawn.
9. **The flag on the crew screen.** The crew mock still draws "بأثر رجعي" as a banner for everyone; item 8 and item 7 apply to it too. Not changed here.
10. **Confirmation of the real quantity** (item 13) lives on the extract screen; nothing is drawn here.

## What the mock does not draw

An offline state; the photo being taken or uploaded; a refused or failed save of one entry; a quantity over the surveyed total; check-out of a past day; the owner's view of the flag (the supervising engineer's view stands for it); the English/LTR text beyond the direction switch.

# The BOQ and quotation builder (`boq.html`)

Mock of the BOQ screen (route `/projects/:projectId/boq`), brief 3 of `ux/journey-project-module-2026-11-09.md`. First drawn after Nabil's review of 2026-11-10 (drag and drop from the catalogue, start from a base project); redrawn the same day to apply his rulings (D-217). Nabil called the first draft "much better". Open `boq.html` directly. `?view=empty|base|blank|building|edit|pending|sheet|dropfail|partial|sign|signerr|custom|customerr|review|frozen|archived|design|loading|failed`; `embed=1` hides the preview strip (review chrome, do not build it). Not approved by Nabil yet.

Drag, tick-to-add and start-from-a-base are Nabil's direction. The base start is ruled (D-217) but has no backend yet (see "What the backend lacks").

## What it holds

For the Technical Office (and the Owner), mostly on a laptop: build the priced list of work until it can be signed, then sign it.

- Header: back link with the project name, title "المقايسة", one quiet badge (follow-up end date while unsigned, "مجمّدة في ..." once frozen, "مؤرشف" when archived), "طباعة عرض السعر" (outline, shown once there are lines, any state), and the ONE primary button.
- Totals strip, sticky: the total with "ج.م" named once beside it, and the line count. Draft reads "الإجمالي", frozen reads "قيمة التعاقد". When any line has no quantity the total is "-" and a dashed tag says how many lines are missing one.
- The lines, grouped under the باب (name at the start, باب subtotal at the end). Columns on a wide screen: item, quantity (typed in place, with its unit), rate, total. Rate is on every line (D-217). Below 1100 px and on the phone a line is two rows: name and remove; then quantity, `x rate`, total.
- **Quantity state, no sentence.** One quantity box per line. A line whose quantity is an estimate carries the dashed tag "تقديري"; once the survey is recorded the surveyed quantity replaces it and the line carries the quiet check mark "معاينة". A line with no quantity carries the dashed "بلا كمية". Frozen and archived lines carry no mark (a signed BOQ has settled quantities).
- **Awaiting review.** A custom item shows the dashed tag "بانتظار المراجعة" beside its quantity tag.
- The catalogue pane at the inline-end (desktop only, 400 px, sticky, scrolls inside itself): "الكتالوج", the outline button "بند مخصص", a name search, the items under their باب (collapsible, the first three open). Each row: tick, grip, name over unit and rate, "+" (44 px). Items already in the BOQ show a tick mark and are not draggable. No codes anywhere (D-212).
- Primary action, one at a time: "ابدأ من مشروع سابق" in the empty state; "توقيع المقايسة" once lines exist (disabled while a line has no quantity; the dashed tag next to the total is the reason); none after signing.

## The ways to add a line

1. **Drag** a catalogue row into the lines (HTML5 drag). While dragging, the list shows a dashed slot at the end of the row's own باب section, and a ghost section with the باب name when that باب has no lines yet.
2. **"+"** on the row.
3. **Tick** several rows and press "أضف (n)" (outline button at the foot of the pane; a second filled button would break the one-primary rule).
4. **Custom item** (D-217, spec section 4.5): "بند مخصص" opens the side panel (bottom sheet at 390). Fields: "اسم البند", "الوحدة", "السعر" (with ج.م), "الباب" (select). Confirm is "أضف للمقايسة" (filled: the panel covers the page primary). A missing field shows the field error colour and one word, "مطلوب". The new line lands with no quantity, focus in its quantity box, and the "بانتظار المراجعة" tag. It goes to the Technical Office review queue and never writes to the catalogue.
5. **A base** (below).

Keyboard: every row's tick and "+" are tabbable; Space and Enter work. A new line takes focus in its quantity box. At 390 px the pane becomes a full-height sheet opened from "إضافة بنود": ticks only (no "+", no grip), search and "بند مخصص" on top, footer button "أضف (n)" (filled, because the sheet covers the page primary).

## Start from a base (ruled, D-217)

The Technical Office prepares named bases for quick quotation (example: "شقة 120 م، سوبر لوكس"). The empty state offers two buttons: "ابدأ من مشروع سابق" (the one filled button) and "مقايسة فارغة" (outline). "ابدأ من مشروع سابق" opens a side panel (bottom sheet at 390): a name search, then two groups, "مقايسات جاهزة" (the prepared bases, first) and "مشاريع سابقة" (any earlier project the user can open). Each result: name, line count (projects also the signing month), "استخدام". Choosing one fills items and quantities, priced at today's catalogue rates; all lines are editable and removable and read "تقديري". Hold and advance do not come across (they are typed at signature).

**The catalogue pane is not shown before the first choice.** The empty state has one decision to make, and the pane (a long list with its own search and ticks) beside it would offer a second, competing way to start and push the filled button off the centre. The pane appears as soon as the BOQ has a first line or "مقايسة فارغة" is chosen. This replaces the first draft, where the pane sat beside the choice so a drop could start the BOQ.

"مقايسة فارغة" leaves an empty list that is a drop zone (a plus icon and, on desktop, one line "اسحب بنداً من الكتالوج"; on a phone the button "إضافة بنود").

## States (each drawn)

- **empty**: the choice, full width. **base**: the picker open over it. **blank**: empty list as a drop zone, pane beside it.
- **building**, **edit** (the first quantity focused), **pending** (a line added with no quantity: dashed start bar, dashed "بلا كمية" tag, total "-", subtotal "-", overall total "-", sign disabled).
- **sheet**: the 390 add sheet (on a wide screen this view looks like building).
- **custom**: the custom item panel, empty. **customerr**: submitted with fields missing (unit, rate, باب). **review**: a custom line in the list awaiting review.
- **dropfail**: the item was archived meanwhile; the pane row turns to the error colour with one line, "تعذّرت الإضافة: البند مؤرشف", and nothing is added. Adding "سيراميك أرضيات 60×60" in any view reproduces it.
- **partial**: four ticked, two landed, the third refused. The banner says "أُضيف 2 من 4"; the lines that did not land stay ticked. The batch stops at the first failure, which matches one add call per line.
- **sign**: the signature panel (bottom sheet at 390): total (read-only, "ستصبح قيمة التعاقد"), "نسبة المحجوز" and "نسبة الدفعة المقدمة" both **empty and required**, the one freeze sentence, "توقيع المقايسة" (filled), "إلغاء". **signerr**: confirm pressed with the fields empty (or outside 0 to 100): each field turns to the error colour with "مطلوب" or "من 0 إلى 100"; nothing freezes. A valid confirm goes to the frozen state.
- **frozen**: lines read-only, no pane, no remove, no primary, the date as a badge, print stays.
- **archived**: as frozen but labelled "الإجمالي", a "مؤرشف" tag and one line with a link to the project.
- **design** (not for this type): the existing sentence "لا توجد مقايسة لهذا النوع من التعاقد." and a link back to the project.
- **loading** (skeleton strip, rows and pane), **failed** (refusal banner with retry, no pane).

## Rules from D-217 (Nabil, 2026-11-10)

1. Base start: prepared bases first, then earlier projects; search by name; fills items and quantities at today's catalogue rates; hold and advance do not come across.
2. Empty state: the filled button is the base start, "مقايسة فارغة" is outline.
3. One quantity box per line; estimated until the survey is recorded, then surveyed replaces it. The state is shown by a mark, not a sentence.
4. Signing is blocked while any line has no quantity.
5. Removing a line asks no confirmation.
6. The rate is on every line.
7. A custom item is allowed from the builder and goes to review (spec section 4.5).
8. Conditions and line markup are not on this screen.
9. Hold and advance percentages start empty, are required and are typed per project.
10. One name for the act: "توقيع المقايسة" on the button, the panel title and the confirm. "توقيع التعاقد" and "وقّع وثبّت القيمة" are gone.
11. The advance hint stays off.
12. An archived BOQ is read-only. Follow-up is 30 days.
14. The print title is "عرض سعر".

## Reused from the kit, unchanged

App shell and bottom bar, back link, buttons (filled, outline `sec2`, small), search field, checkbox (`ck`), refusal banner, note banner, empty and refused states, skeleton, the side panel and bottom sheet (`sx`, from the crew screen), read-only money field, the باب section band, the dashed tag (`atag`, from the catalogue's archived marker), the saved-in-place banner style.

## New in the kit (put these in `components.html` before building)

- **BOQ line** (`ln`): name, quantity box (`qi`: typed value with the unit as an affix), rate, total, remove, and a tag slot (`tags`) for the quantity state and the review mark. Wide grid of five columns; compact two-row shape below 1100 px. Variants: pending (dashed 5 px start bar, dashed tag, total "-"), read-only (plain quantity and unit, no remove, no tags).
- **Quantity state mark** (`qs`): quiet check plus "معاينة" for a surveyed quantity; the existing dashed tag carries "تقديري".
- **Totals strip with figure** (`tot2`), **catalogue pick row** (`ci`) with added, failed and phone (`sci`) variants, **drop slot and ghost section**, **full-height sheet** (`sx.full`), **collapsible باب band in a pane**, **neutral badge** (`bdg`).
- **Field error** (`ctl.err` plus `fmsg`): error-colour border and one short message under the field. Used by the sign panel and the custom item form.
- **Select in a field** (`ctl select`): the باب choice.
- **Grouped result list** (`res-g`): a quiet band heading inside the result list ("مقايسات جاهزة", "مشاريع سابقة").

No new colour: every value is an existing token.

## Left off the screen, on purpose

Item codes (lines and pane). The words "سعر البيع" on rows. Cost price and margin. A second quantity box. Line positions and ids. The advance hint. The sentence under the quantity field. "المبالغ بالجنيه المصري (ج.م)". Any explanatory text about drag, ticks or estimates: the grip, the dashed slot, the "+" and the tags carry it.

Sentences kept: the freeze sentence in the sign panel (irreversible, spec section 4.4); one empty-state line "اسحب بنداً من الكتالوج" (desktop only); the state sentences (failed, partial, drop failed, archived, not for this type).

## Not decided here (the mock runs on these defaults)

1. **Same item twice in one BOQ** (D-217: not ruled, goes to Karim). The mock shows an added item as added and offers no second add.
2. **Signing with a custom line still awaiting review.** The mock lets it be signed (the review concerns the catalogue, not the quantity). Not ruled.
3. **Which projects may be a base source** and what happens to a source item archived since the base was prepared (not drawn).
4. **Whether a custom item may be edited or removed after it goes to review.** The mock treats it like any line (editable quantity, removable).
5. **Quantity saves**: the mock saves as the number is typed. Saving on blur, with a failed-save state per line, is not drawn.
6. **Who sees "مقايسات جاهزة"**: shown to everyone who can open the BOQ screen in the mock.

## What the backend lacks for this design

- A base entity (a named, prepared BOQ template of items and quantities), and a source list that returns prepared bases first, then earlier projects the user may open.
- A copy route that fills a BOQ from a base or earlier project at today's catalogue rates (no hold or advance).
- A multi-line add (one `AddBoqLine` call per line today, so a batch can stop halfway, which the partial state draws).
- Estimated versus surveyed quantity on a line: the screen needs a flag per line saying the surveyed quantity has been recorded.
- The custom-item review queue view for the Technical Office, if it does not exist, and a per-line `pendingCatalogueReview` flag on the BOQ line response.
- The pane uses `ListCatalogueItems` (`search`, `babId`); drop and "+" use `AddBoqLine`. Reorder is not needed (lines sort by باب).

## What the mock does not draw

The screen where the Technical Office prepares a base (not in scope here). The printed quotation; the conditions and markup controls (ruled off this screen); reopening an archived project; per-line save failure; the English/LTR text; two people editing at once.

# Project create (`project-create.html`)

Mock of `/projects/new`, brief 4 of `ux/journey-project-module-2026-11-09.md`, revised after Nabil's rulings of 2026-11-10 (D-217). Open `project-create.html` directly. `?view=normal|filled|search|noresult|noclients|quickadd|quickerr|loading|failed|errors|duplicate|submitting|refused`; `embed=1` hides the preview strip (review chrome, do not build it). Not approved by Nabil yet.

**Proposals in this mock, not rules.** The client typeahead rests on Q-S (PROPOSAL). The quick-add client panel is drawn to D-217 ruling 4; its layout (side panel, bottom sheet) is my choice.

## What it holds

For the Owner and the Technical Office (`ProjectCreate`), mostly on a laptop: open a project with three inputs.

- Header: back link "المشاريع" and the title "مشروع جديد". No badge, no code.
- "المشروع": a text field, required.
- "العميل": a type-to-filter field (combobox), searched by name. Focusing or typing opens a popup list; a result is the client's name and nothing else, with the typed part underlined and bold (the match sits inside one text span, so "شركة النيل" keeps its space). Choosing a name fills the field and shows a 44 px clear button. Required.
- "نوع التعاقد": three radio rows, 56 px. Nothing is selected until the person chooses (D-217 ruling 1). Required. The three names only, no hint (ruling 2).
- The ONE primary "إنشاء المشروع": at the foot of the form on desktop (hairline above), in the bottom bar beside "القائمة" on a phone. After pressing, a Lump Sum project opens its BOQ and the other two types open the workspace (brief; navigation, not drawn).

## Quick add of a client (D-217 ruling 4)

From the no-result row and from the no-clients state the link "عميل جديد" opens a panel over the form: a side panel of 460 px on desktop, a bottom sheet at 390 (the kit's `sx`, as on the crew and BOQ screens). The typed text becomes the name. The fields are only those `CreateClient` requires (`src/Api/Features/Clients/CreateClient/Request.cs` and `Validator.cs`):

- "الاسم" (`Name`, required: `Client.Create` refuses a blank one)
- "رقم الهاتف" (`Phone`, required: it is the de-duplication key)
- "نوع العميل" (`Kind`, required: the validator refuses a missing kind; the two options are "فرد" and "شركة", nothing preselected)

Left out because the API treats them as optional: alternate phone, email, address, tax registration number. The code is generated server side and has no member on the request. The panel has one filled action "حفظ العميل" and "إلغاء". Saving closes the panel and the new client sits selected in the field. Errors sit under the field ("الاسم مطلوب", "رقم الهاتف مطلوب", "اختر نوع العميل"). The link to the clients screen is gone.

## States (each drawn)

- **normal**: empty form, no type chosen. **filled**: all three set.
- **search**: the popup open with the matches for "شركة". **noresult**: the popup shows "لا يوجد عميل بهذا الاسم" and the link "عميل جديد".
- **noclients**: in place of the field, "لا يوجد عملاء مسجّلون" and the link "عميل جديد". This is the way out.
- **quickadd**: the panel open from no-result with the typed name. **quickerr**: the panel after saving empty, three errors.
- **loading**: the client field is dashed, disabled, with a skeleton and spinner; the primary is disabled while it loads.
- **failed**: in place of the field, the refusal banner "تعذّر تحميل العملاء" with "إعادة المحاولة". The primary is absent: the form cannot succeed until the list loads, and retry is the only action.
- **errors**: a footer line under each unfilled field with the alert icon: "اسم المشروع مطلوب", "اختر العميل", "اختر نوع التعاقد". The footer slot is reserved at 24 px for the name and client fields, so they do not shift. Pressing the primary on an incomplete form reaches this state.
- **duplicate**: after pressing, the server refuses the name for this client. The name field takes the error border and the line "يوجد مشروع بهذا الاسم لهذا العميل". Two different clients may share a name (ruling 3), so the line names the pair.
- **submitting**: primary in its busy form ("جارٍ الإنشاء…", spinner, same size), name read-only, radios disabled.
- **refused**: the form and the primary are absent. The lock state says "لا تملك صلاحية إنشاء المشاريع": it names the missing permission, not a person.

## Reused from the kit, unchanged

App shell and bottom bar, back link, buttons (filled, outline small), form field with its label, required mark and reserved footer slot, radio (`ck rd`), link, refusal banner, refused state, skeleton, spinner, busy button, popup (`--shadow`), side panel and bottom sheet (`sx`, from the crew screen), icon button.

## New in the kit (put these in `components.html` before building)

- **Combobox (type to filter)** (`cb`, `pop`, `opt-c`): a kit field with a search icon at the start and a clear button at the end, a popup list below it (max 300 px, scrolls inside, options 48 px, 56 px on a phone), the match underlined, the first match highlighted while typing, and a no-result row with an action. Closed, it is an ordinary field.
- **Choice row** (`types label.ck`): a radio inside a 56 px bordered box, for a short list of mutually exclusive named choices. The border carries the selection, with the filled radio and bold text, so colour is not alone. A group error line sits under the last row.

No new colour: every value is an existing token.

## Left off the screen, on purpose

Contract value, hold, advance, the project code, the follow-up period, the start date, the base-project choice (it lives on the BOQ screen), type hints, the value-comes-from-the-BOQ sentence, the initial-status banner. Client codes: a client is found by name only.

Sentences kept: none beyond labels, values, actions and state lines (no result, no clients, failed to load, validation, name refused, refusal).

## Ruled (D-217, 2026-11-10)

1. No contract type preselected; a missing type is a validation error.
2. The three type names stay, no hint.
3. The same project name is refused for the same client; two different clients may share a name.
4. A client who is not registered is added inside this form; no link to the clients screen.
5. Client search is by name.

## Not decided here

1. **Q-S**: the typeahead is still a proposal. Whether the list needs server-side name search depends on how many clients Kaff has.
2. **Quick-add permission**: `CreateClient` requires `Permission.ClientManage` (CompanyWide, spec section 2), which may not be granted to the same roles as `ProjectCreate`. If it is not, the link must be absent for that person and "لا يوجد عملاء مسجّلون" needs a different state; the mock draws the link for everyone.
3. **Duplicate phone in quick add**: `CreateClient` returns the clients that already hold the number and asks for an acknowledgement (`AcknowledgedDuplicatePhone`). Not drawn. Which of the two clients to pick, or whether to proceed, is open.
4. **Archived clients**: whether they appear in the search is not drawn.
5. **Quick-add tax registration, address, email**: not asked here. Added later on the clients screen.
6. **Failed load**: the primary is absent and the retry button is the only action (my decision, report to Nabil). Alternative: leave the primary and refuse it.
7. **After pressing**: the destination by type is from the brief and `project-create-page.ts`; the mock does not navigate.

## What the backend lacks for this design

- Name check per client: no endpoint or constraint refuses a project name that already exists for the same client. The duplicate refusal needs a server rule (and an error code for the page to show).
- Quick add: `CreateClient` exists, but needs `ClientManage`. If that differs from `ProjectCreate`, either the permission is widened for this flow or a project-create user cannot add a client.
- The client list endpoint (`ListClients`) has no per-name search known to this mock; filtering runs in the page.

## What the mock does not draw

The destination after create; a server error other than the duplicate name; the duplicate-phone answer; the English/LTR text; the quick-add saving state.

# The owner's daily log (`daily-log-review.html`)

**APPROVED, D-226 (2026-11-15); r3 adds the other-day control and shows the distance by default.** From D-225 item 1: the owner's اليومية tab opens a read-only view of every engineer's log for the chosen day, with a link to the crew screen where he may edit. Same view serves any caller whose daily-log tab mode is `review` (today: the owner). Data: `GET /api/projects/{id}/daily-logs?date=`. Build: KAFF-420 AC-3 (the owner's اليومية tab).

Open `daily-log-review.html` directly. `?view=two|one|partial|idle|none|older|stopped|loading|failed`; `embed=1` hides the preview strip (review chrome, do not build it). The four-day switch works in every view.

## What it holds

One job: read what each engineer logged on a day, and who was on site. No entry form, no kind row, no primary action: the phone bottom bar holds the menu alone, the desktop header has no action slot.

- **Project frame** from `project.html` (back link, name, status pill, client and type, stage line, tabs), tab اليومية active.
- **Day switch**: اليوم · أمس · الأحد · السبت, each chip carrying the day number under the weekday (as far as 390 allows: four chips fit at 360 and 390); the full date is written once beside it.
- **Other day (D-226 item 4)**: one control «يوم آخر» beside the switch, a native date input under a button face (calendar icon), `max` = today. Any past day opens; the entry window of D-217 limits what may be recorded, not what may be read. While an older day is open the control takes the filled pressed tone, no chip is pressed, and the full date is written beside it. A future date cannot be chosen; the server returns an empty day for one anyway. The `older` view shows 2 نوفمبر.
- **Link row** "العمالة" to the crew screen, above the blocks. **Its total is the sum of the crew lines listed in the blocks below** (men counted once each, hours added): one truth, the two cannot disagree. A day with no crew line reads "لم يُسجَّل أحد بعد". The row stays on every day, because the owner edits crew on that screen.
- **One block per engineer**: his name (kit section heading), his attendance strip, then either his entries in the approved log groups (each with a count badge: سبب التوقف, الكميات التقديرية, الخامات, الملاحظات, الصور, العمالة) or, when he logged nothing, one dim line in the log frame.
- **Attendance strip** (the kit `att` without its button): الحضور and time, الانصراف and time, the "بعيد عن الموقع" pill and the "بأثر رجعي" pill. No check-out is named in the strip: "لم يُسجَّل الانصراف" with a clock icon in the dim tone (an engineer still on site and one who left without checking out look the same; the server decides which it is, see questions).
- **Far from site** shows the distance in metres (D-226 item 2): "بعيد عن الموقع · 650 متر". The server sends the distance; the screen does not compute it. When the server sends none the tag stands alone.
- **A corrected quantity** shows the new figure, the old figure struck through in the dim tone before it, and the pill "مصححة" on the same line as the figure; "بواسطة {name}" sits on a dim line under the row. At 390 the line name takes the first line of the row and the mark and figures the second: name, mark and both figures cannot share one 358 px line, so the mark stays with the figures, which is what it refers to. A quantity row shows its باب in the dim tone beside the line name.
- No money, no code, no id, no "تعديل" button anywhere.

## States

- `two`: today, two engineers. One far from site and with no check-out, one with a corrected quantity.
- `one`: أمس, one engineer, back-dated log.
- `partial`: السبت, two engineers present, one logged, the other has name and strip and the dim line only (an engineer who checked in and logged nothing).
- `idle`: today, check-ins exist, no entries from anyone: every block is name, strip, dim line. The crew row says "لم يُسجَّل أحد بعد".
- `none`: الأحد, nobody checked in and nobody logged: the state block with the approved sentence, without "بعد" (a past day). Today keeps "بعد" (`لم يُسجَّل شيء في يومية اليوم بعد.`), in both the dim line and the state block.
- `stopped`: note banner "المشروع متوقف" as `daily-log.html`. The status pill keeps its place and shows "جاري العمل", because `project.html` has no stopped view and no pill for one (its frame draws only جاري العمل and لم تبدأ). No status word is invented. See questions.
- `older`: 2 نوفمبر chosen through «يوم آخر»: two engineers, one logged, one checked in and logged nothing.
- `loading`: skeleton, tab bar absent (as `project.html`). `failed`: refusal banner "تعذّر تحميل اليومية." with retry; frame, tabs and day switch stay.

## Reused from the kit, unchanged

Project frame with stage line and tabs, status pill, badge, back link, text link, segmented control (day switch), check-in strip (`att`, without its button), link row (`clink`), log group (`lg`) and entry (`ent`) with the photo thumbnail, the log's empty line (`log-empty`), note banner, refusal banner with retry, state block, skeleton, section heading, shell, bottom bar with the menu only.

## New in the kit

The other-day control (`dpick`: a native date input behind a 52 px button face, the day switch's height, the switch's tones). Small additions to existing parts, all existing tones: a struck dim figure and a mark inside the entry's figure cell; a dim line under an entry; a dim secondary text inside the name (`tr`, as the picker result); a two-line day chip (weekday over day number) in the day switch; a dim variant of the strip's time item.

## New words (for Nabil to confirm)

1. "مصححة": corrected mark on a quantity, printed without tashkeel.
2. "بواسطة {name}": who corrected it.
3. "لم يُسجَّل الانصراف": a check-in with no check-out, patterned on the approved "لم يُسجَّل الحضور بعد". No approved mock has a word for "still on site".
4. "يوم آخر": the date control. "متر" is in the materials unit list.

Every other word is from the approved mocks.

## Not decided here

1. **Stopped project status word.** The frame mock has no pill for a stopped project; the mock keeps جاري العمل. Nabil names the word (متعثرة or تم تأجيلها are Kaff's, neither is chosen here).
2. **Arabic plural of workers and hours** (عمال, ساعة): no plural mechanism exists; the crew link row uses one pattern for every count.
3. **Block order.** One block per engineer, as D-225 words it. A by-work view for two engineers on the same trade is not drawn.

## What the mock does not draw

The native date picker's own popup (the browser draws it); a photo at full size; a log still being written live; English/LTR text beyond the direction switch; the supervising engineer's view of the same screen.

# D-227 proposals, revised for D-228: team add and remove, the third sign field, after signing, design project (`project-team.html`, `boq.html`, `project.html`)

**PROPOSAL, not approved; revised 2026-11-16 after the consultant review `ux/consultant-review-d227-mocks-2026-11-16.md` and D-228.** No new visual idea: every part is in the kit or an approved mock. Nothing here is built.

## A. The team tab with add and remove (`project-team.html`, new)

Open `project-team.html` directly. `?view=normal|noeng|add|adderr|addeng|addfail|addnone|confirm|added|removed|readonly|loading|failed`, `?embed=1` hides the preview strip. The frame is `project.html`'s, unchanged, with الفريق current. **The tab strip is scrolled so the current tab is in view** (consultant must-fix 5): the person reads where he is from the bold, barred tab, with no heading and no sentence. The build must scroll the strip to the current tab on load, as `project.html` already does.

**The list.** One `lst`: avatar disc, name, then the role as the app writes it (`enum.Role.*` plus the level: «مهندس الموقع · مشرف», «مهندس الموقع · مبتدئ», «المكتب الفني», «المالية»). One quiet badge after the role, only where it helps: «تلقائي» (Finance) and «أنشأ المشروع» (the creator). A person added by hand has no badge. The Owner is not listed. Phone: name and remove button on line one, role and badge on line two. Desktop: three columns.

**Primary action.** «إضافة شخص»: phone bottom bar beside «القائمة» (KAFF-936), desktop header slot. When the viewer may not add, the bar holds «القائمة» alone, filling the bar.

**Add.** Phone bottom sheet, desktop side panel. «الشخص»: a select of active users not on the project, "name — role", nothing preselected. Only for a site engineer, «الدرجة»: «مبتدئ» / «مشرف», nothing preselected. Field errors «اختر شخصاً», «اختر الدرجة». Server refusal: the refusal banner with the app's string «هذا الشخص مسند بالفعل إلى هذا المشروع.». Nobody left: «لا يوجد أحد متاح للإضافة» (was «…مستخدم…», the system's word) and «إغلاق». After adding: the saved banner «أُضيفت نورا سعيد» and the new row, tinted, scrolled into view (it was hidden behind the bar).

**Remove.** A bordered secondary button «إزالة» per removable row (KAFF-935). It opens the kit's recoverable confirmation dialog (neutral tile, ordinary primary as confirm): «إزالة {name} من المشروع؟ / يفقد وصوله إلى المشروع وتبقى أعماله السابقة. يمكنك إضافته من جديد.» No red. Finance has no button.

**States drawn.** normal, empty of engineers (notice «لم يُسنَد مهندس الموقع بعد»), add, add with a missing field, add with an engineer chosen, add refused, nobody to add, remove confirmation, just added, just removed, read-only, loading, **failed (now keeps the project name, the pill, the stage line and the tabs; only the pane carries «تعذّر تحميل الفريق.» and retry)**.

**Ruled (D-228 item 1), all as drawn:** the Owner cannot remove Finance; may remove the creator; any number of supervising engineers; a removed person's work stays under his name through the audit trail and the tab lists current members only; a removed person can be added back.

**Reused.** Project frame, stage line, tabs, `lst`, avatar, `badge q`, pill, buttons, refusal, notice and saved banners, field with select, radio, field error, bottom sheet, side panel, recoverable confirmation, empty state, skeleton. **New in the kit:** only `rdg` (the kit radio in two columns, 48 px targets) and the add-person and remove-person icons.

**New words** (guesses): «إضافة شخص», «إزالة», «تلقائي», «أنشأ المشروع», «الشخص», «الدرجة», «لم يُسنَد مهندس الموقع بعد», «لا يوجد أحد متاح للإضافة», «اختر شخصاً», «اختر الدرجة», «أُضيفت {name}», «أُزيل {name}», «إزالة {name} من المشروع؟», the consequence sentence, «تعذّر تحميل الفريق.». One term for the role everywhere: the app's «مهندس الموقع» (the notice and primary were «مهندس موقع»).

## B. The sign sheet (`boq.html`, changed; `?view=sign`, `?view=signerr`, `?view=pending`)

Three fields, empty, required, one field part: **«نسبة الحجز»** (was «نسبة المحجوز»; D-223), **«نسبة الدفعة المقدمة»**, and, full width under them, **«نسبة الخصم من كل مستخلص»** (D-228 item 3; «نسبة استرداد الدفعة المقدمة» is retired). The lead sentence is unchanged. No "(%)" in the labels: the percent affix says it; the app keys `boq.sign.field.*` must follow (build). At 360×640 the sheet fits with empty fields (about 553 of 589 px) and the body scrolls about 30 px only with all three errors showing; the footer stays pinned.

**Disabled primary (kit proposal, revised).** A required gap the user can fix on the screen never disables the primary. On the BOQ, «توقيع المقايسة» stays enabled; pressing it scrolls to the first line without a quantity and puts focus in its field (the line is already marked with the dashed bar and «بلا كمية»); only when none is left does the sheet open. Forms do the same: pressing marks the field with its own error. **Disabled** is kept only for a condition the user cannot fix on that screen, and then the reason sits beside the button as the `fmsg` line (not drawn: no such case in these screens). `?view=pending` now has one line without a quantity out of the same 8 lines as `?view=sign`, so the item counts agree (it was 9 against 8). Confirming the sheet leaves the BOQ for the signed overview.

## C. After signing and the design project (`project.html`, changed)

**Signed, Owner (`?view=signed`).** Lands on the overview. Pill **«لم تبدأ»** (D-228 item 4: «جاري العمل» only from the first daily log entry). Stage التنفيذ. **The three rates sit in a fact list straight under the notice, above the hero**, so they are on the first screen: «نسبة الحجز», «نسبة الدفعة المقدمة», **«نسبة الخصم من كل مستخلص»** (the «شروط العقد» disclosure is not drawn on this view; it stays on the others, with the renamed row). Notice «لم يُسنَد مهندس الموقع بعد»; primary **«إضافة شخص»** (the same label as the team tab's own primary; «إضافة مهندس الموقع» clipped the phone bar at 390) to the team tab.

**Signed, Technical Office (`?view=signedto`).** Same, with the notice and **no primary** (only the Owner manages the team; the Technical Office's first job is not on this screen). The sidebar names the Technical Office user.

**Design project, three states (D-228 item 2), stage line and primary agree.** The design stage line has three stages: **«التعاقد · التصميم · التسليم»** («التصميم» is new).
- `?type=ds&view=setup`, unsigned: pill «لم تبدأ», stage التعاقد, primary **«توقيع التعاقد»** alone. The notice is the date only: «تنتهي فترة المتابعة في السبت 5 ديسمبر.» (the first sentence is dropped: the pill and the primary carry it). The rate per م² shows; area and fee read «لم تُحدَّد بعد» with **no info icon** (also removed from the Lump Sum unsigned row).
- `?type=ds&view=normal`, signed, design in progress: pill «جاري العمل», stage التصميم, 50%, **no primary**.
- `?type=ds&view=designdone`, finished: pill «انتهت», stage التسليم, all phases 100%, primary **«تحويل إلى تنفيذ»** alone.
**Build waits** on the design phases of `spec.md` §13 (`NotStarted` to `Invoiced`): the server cannot yet say a design is finished. Until then the built behaviour is wider than this ruling (D-228); the third state is the ruled state, drawn.

**Consultant findings, design gap or build defect.** After signing: the landing and the stage line are build defects; the next step is drawn. Design project: empty BOQ tab and convert beside sign are build defects; the stage line and the three states are design, drawn. Disabled primary: design (rule above). The sidebar/role label «مهندس موقع» in `project.html`'s own mock data is now «مهندس الموقع».

## Still for Nabil

1. **The design stage words** «التصميم» and «التسليم» are guesses; a design project's end state may want «انتهت» only.
2. **The sign sheet lead sentence** («التوقيع يثبّت…») is kept as approved; the consultant calls it the longest sentence on the sheet and a candidate to cut. A ruling is needed to remove it.
3. **Technical Office after signing:** with no primary, what is its next job? Drawn: nothing on this screen.

## What these mocks do not draw

The design sign sheet and the convert sheet; busy states beyond the kit's; English/LTR copy; the daily log and crew «حفظ» reason lines; a disabled-primary case with an unfixable reason.


# People: salaried staff and day-labour workers (`people.html`)

**PROPOSAL, revised for D-232 (Nabil's rulings on the first draft, 2026-11-16) and the consultant review `ux/consultant-review-people-mock-2026-11-16.md`.** Draws D-231: salaried employees and day-labour workers (يومية) are separated on screen. Each has its own menu entry, its own list and its own add form, with only the fields that population has. Open `people.html` directly. `?view=staff|staff-create|staff-createerr|staff-taken|staff-dup|staff-edit|staff-menu|staff-archive|staff-archived|staff-none|staff-empty|staff-loading|staff-failed` and `?view=workers|workers-site|workers-create|workers-createerr|workers-dup|workers-edit|workers-menu|workers-archive|workers-archived|workers-unarch|workers-none|workers-empty|workers-loading|workers-failed|workers-ro|nav`. `embed=1` hides the preview strip (review chrome, do not build it). `role=owner` shows the Owner's full menu (the only thing that differs for the Owner here). Settled by D-232: screens only, labels «الموظفون» and «عمالة اليومية», no salary field, day rate stays per engagement, no move action.

The record underneath is unchanged: one `Employee` register, `EmployeeKind` fixed at creation, nobody in both (`spec.md` §10, `Employee.cs:82`, D-231 item 2). Only the screens split.

## What it holds

- **Menu.** The one row «الموظفون» becomes two rows, side by side in «البيانات الأساسية»: «الموظفون» (salaried) and «عمالة اليومية» (day labour). No new group heading. Both words already exist in `ar.json` (`nav.employees`, `hr.worker.pool_title`). View `nav` opens the phone menu sheet on the workers row.
- **Salaried staff list** (`staff`). Title «الموظفون». Columns: name (pinned), المسمى الوظيفي, القسم, تاريخ التعيين, رقم الهاتف, row menu. Filter strip: search by name or phone, the segmented filter نشط / مؤرشف / الكل, the count line. Primary «تسجيل موظف» in the header slot on desktop and in the bottom bar on a phone.
- **Day-labour list** (`workers`). Title «عمالة اليومية». Columns: name (pinned), الباب, التخصص, عدد مرات التشغيل, متوسط التقييم, رقم الهاتف, row menu. Same filter strip plus a الباب select. Primary «تسجيل عامل».
- **Register and edit** are the catalogue's side panel (bottom sheet on a phone), not a page. The primary is always enabled; a missing field is refused in place on press (D-229 item 3). Edit's «حفظ» is disabled only while nothing has changed.
  - Staff form: الاسم الكامل*, رقم الهاتف*, الرقم القومي (hint «14 رقماً»), المسمى الوظيفي, القسم, تاريخ التعيين. No kind field, no باب, no التخصص.
  - Worker form: الاسم الكامل*, رقم الهاتف*, الباب*, التخصص. No national id, no job title, no department, no hire date.
  - Worker edit opens with a figures strip: عدد مرات التشغيل and متوسط التقييم. **No rate appears anywhere on this register, for any role** (D-232 item 4: a worker has no standard rate, and an average of different rates would mislead). The Owner's panel is therefore identical to HR's, so the owner variant `workers-edit-owner` is dropped. The «سجل التشغيل» link is dropped with it: its route is per project and needs `DayLabourRateManage`, so it belongs to the project's day-labour screens, not this register.
- **The kind is not on any screen.** Today's form has a «نوع التكلفة» select and a line saying the kind is permanent. Both go: the menu entry the user came from is the kind.

## Fields per population, and where they come from

| Field | Salaried | Day labour | Source |
|---|---|---|---|
| Full name | yes | yes | `Employee.cs:75` |
| Phone | yes | yes | `Employee.cs:77` |
| الباب (trade) | no | yes, required | `Employee.cs:85`, guard in `Employee.Create` and `Edit` (`DayLabourRequiresTrade`) |
| التخصص | no (dropped, see Q8) | yes | `Employee.cs:88` |
| الرقم القومي, المسمى الوظيفي, القسم, تاريخ التعيين | yes | no | `Employee.cs:90-96`, written by `SetStaffDetails` (`Employee.cs:140`) |
| Times engaged, average rating | no | yes | `DayLabour/ListPool/Response.cs:26-37` (computed per worker) |
| Day rate | no | not on this register: it lives on each engagement (D-232 item 4) | `DayLabour/ListEngagements/Response.cs:6-24` |
| Code | generated, **not shown** | generated, **not shown** | `Employee.cs:73` |
| Salary | **none now** (D-232 item 3: salary, insurance number and pay method come with accounting and payroll) | n/a | no field on `Employee` |
| Day rate on the worker | **does not exist** on the worker | the rate lives on each engagement | there is no single rate to put on a form |

## Who sees what

- **HR** holds `EmployeeManage` (`PermissionCatalogue.cs:357`: Owner and HR, company-wide). Sees both lists and both forms, full edit and archive. **Never sees a rate**: HR holds no money permission (`Hr_holds_no_permission_that_touches_money`, `CatalogueCompletenessTests.cs:222`). The worker edit panel for HR has two figures and no rate and no history link.
- **Owner** holds `EmployeeManage`. Same screens as HR; no rate on them either.
- **Finance and the assigned site engineer** hold `DayLabourRateManage`, project-scoped. They reach a rate from the project's day-labour screens, never from this register. They do not get the menu rows (no `EmployeeManage`).
- **Site engineer** sees neither list. He keeps today's project pool and the crew sheet, which show no phone and no code (D-224 item 6: `DayLabour/ListPool/Handler.cs:48,84,86`).
- **Read-only** (`workers-ro`): no role reads the register without managing it today (`ListEmployees` requires `EmployeeManage`, `Employees/ListEmployees/Endpoint.cs:26`). The view is drawn in case one is added: no «تسجيل عامل», no row menu, a panel with read-only fields and a "للاطلاع فقط" badge, and **no phone column**, because contact is withheld from anyone without `EmployeeManage` (D-224 item 6). Draw-only; see Q7.

## How a site registration reaches the list

`workers-site`: a worker registered from site (`RegisterFromSite`, request `Request.cs:21`) is an `Employee` of kind DayLabour, so he is one more row of «عمالة اليومية», with his trade and specialty and times engaged 0 («لم يُشغَّل من قبل»). The row carries a plain badge «جديد» (next to the name on desktop, at the end of the trade line on a phone) until HR opens or edits it; opening it clears the mark in the mock. Nothing says "from site": the badge is a state, not a provenance tag. The site pool and the crew sheet are not redrawn; they read the same people.

**No stored fact can drive «جديد» today.** `Employee` has `CreatedAt` and no "reviewed" flag and no source field (`Employee.cs:73-98`); the site registration is only an audit record, which no list reads. So «جديد» is a server gap, not just a mark: it needs a stored "reviewed by HR" fact on the employee (set on first open or edit) and a way to tell a site registration from an HR one. See gap 6 and Question 1.

## The edge cases the model forces

- **A person cannot be moved between the two.** The screen offers nothing: no kind field, no «نقل إلى…» in the row menu (the menu has تعديل and أرشفة…/إلغاء الأرشفة only, `workers-menu`, `staff-menu`). A control the user may not use is absent. The way a man who was on يومية is hired on payroll is: archive the worker, register him as an employee (D-232 item 5). His phone then matches the archived worker, which draws the warning below. The server accepts this (`CreateEmployee/Handler.cs:63-90`: only an ACTIVE salaried match refuses; every other match is warn-and-acknowledge).
- **Duplicate phone, warn** (`staff-dup`, `workers-dup`): a note banner «هذا الرقم مسجل بالفعل:» listing each match as name, a badge with the match's population (موظف بالراتب / يومية) and the archived tag, then the checkbox «رأيت من يحمل الرقم وأريد المتابعة.». Pressing «حفظ» without the box refuses in place: «علّم هذا المربع للمتابعة.» No code is shown for a match. The banner and the checkbox sit in their own block directly above the buttons (pinned, outside the scrolling fields), so on a phone they never push الباب or any other field off screen and they sit next to «حفظ».
- **Duplicate phone, refuse** (`staff-taken`): a new employee whose phone is an ACTIVE employee's is refused on the phone field: «رقم الهاتف هذا مسجل بالفعل لموظف بالراتب نشط.» There is no checkbox: it cannot be overridden. This refusal exists only on the staff form (the server only refuses salaried-to-salaried; every worker-form match is warn-and-acknowledge), so the worker form has no refusal wording at all. The existing text «لموظف آخر» (`errors.master.employee_phone_taken`) is replaced by one that names the population.
- **Archive and unarchive**: the catalogue's recoverable pattern. Row menu «أرشفة…» opens the inline confirmation («أرشفة «الاسم»؟» and one line); the panel's «أرشفة» does the same. «إلغاء الأرشفة» restores the row in place (`workers-unarch`): under the «مؤرشف» filter the restored row stays visible once as the green confirmation line, the count line counts only the rows still archived, and the two agree. **The API has no unarchive for employees today** (only `Employee.Archive`, `Employee.cs:176`); it is drawn ahead, like the أبواب screen. Until it exists the confirmation line must not promise a restore.
- **First run, no results, loading, failed**: the kit's four list states. First run is an icon and the existing title only (the primary is already in the header and bottom bar, so no second button). Failed is the refusal banner with «إعادة المحاولة».
- **Archived rows** keep the dashed «مؤرشف» tag and dim text.

## Reused from the kit and earlier mocks, unchanged

App shell, menu sheet, table shell with pinned identity column, phone row, segmented filter, search field, row menu, inline archive confirmation, side panel and bottom sheet, text field, select, checkbox, note banner, refusal banner, saved-in-place row, four list states, skeleton, totals strip (`tot-in`), the read-only badge with a lock, the just-created row tint (after a save in the mock).

## New in the kit

Nothing visual. The plain badge «جديد» is the kit's neutral badge. One new phone row shape: three lines (name and phone; trade and specialty; times engaged and rating) for a worker, two lines for an employee. Both use the existing `li` grid.

## New words (every one is a guess; Nabil or the BA confirms)

«تسجيل عامل» and «تسجيل موظف» exist (`hr.worker.action.register`, `hr.employee.action.create`). New: «ابحث بالاسم أو الهاتف»; «اختر الباب» (the select's placeholder, as in the catalogue mock); «تعذّر تحميل الموظفين.» / «تعذّر تحميل العمال.»; «لا يوجد موظفون مؤرشفون» / «لا يوجد عمال مؤرشفون»; «علّم هذا المربع للمتابعة.»; «أدخل رقم هاتف صحيحاً، مثل 010 1234 5678.»; «14 رقماً»; «شُغِّل 12 مرة» / «شُغِّل مرة واحدة» (the phone-row label for times engaged; the zero case reuses «لم يُشغَّل من قبل»); «جديد»; «رقم الهاتف هذا مسجل بالفعل لموظف بالراتب نشط.»; «تمت استعادة «الاسم» إلى القائمة النشطة.»; count lines «يعرض 7 موظفين نشطين», «يعرض 7 عمال نشطين». Existing words reused: «الموظفون», «عمالة اليومية», «موظف بالراتب», «يومية», «الباب», «التخصص», «الرقم القومي», «المسمى الوظيفي», «القسم», «تاريخ التعيين», «عدد مرات التشغيل», «متوسط التقييم», «الأجر اليومي», «سجل التشغيل», «لم يُشغَّل من قبل», «رقم الهاتف مطلوب», «عامل اليومية يجب تسجيله على باب.», «الاسم مطلوب.», «مؤرشف», «أرشفة», «إلغاء الأرشفة», «إعادة المحاولة».

## Left off the screen, on purpose

The employee code (`E-…`) in every list, panel and warning; the kind badge on rows; the kind select and its "permanent" line; salary (no field); a worker's current engagement (the API sends none on a list); any rate or average rate; the «سجل التشغيل» link; any sentence explaining that the two populations are separate.

## What the API does not send (the build stops here until it does)

1. `EmployeeSummary` (`Employees/ListEmployees/Response.cs:7`) carries no job title, department or hire date, so the salaried list columns need them added. It carries a `BabId` and no باب name, so the worker list needs the name resolved (the site pool has the same gap).
2. Times engaged and average rating are computed only in `ListPool`, which is project-scoped and needs `DayLabourSiteManage`, which HR does not hold. HR's worker list needs a company-wide read of the same two figures.
3. Void under D-232 item 4: no average rate is shown.
4. No unarchive for employees (`Employee.Archive` only, `Employee.cs:176`). Until it exists the archive line must not promise a restore.
5. `errors.master.duplicate_phone_not_acknowledged` says «راجع العميل الذي يحمله», a client's wording, on an employee form. The mock uses its own line.
6. «جديد» has no stored fact behind it: no "reviewed" flag and no site-versus-HR source on `Employee` (see "How a site registration reaches the list").

## What the API does not send (the build stops here until it does)

1. `EmployeeSummary` (`Employees/ListEmployees/Response.cs:7`) carries no job title, department or hire date, so the salaried list columns need them added. It carries a `BabId` and no باب name, so the worker list needs the name resolved (the site pool has the same gap).
2. Times engaged and average rating are computed only in `ListPool`, which is project-scoped and needs `DayLabourSiteManage`, which HR does not hold. HR's worker list needs a company-wide read of the same two figures.
3. Void under D-232 item 4: no average rate is shown.
4. No unarchive for employees (`Employee.Archive` only, `Employee.cs:176`). Until it exists the archive line must not promise a restore.
5. `errors.master.duplicate_phone_not_acknowledged` says «راجع العميل الذي يحمله», a client's wording, on an employee form. The mock uses its own line.
6. «جديد» has no stored fact behind it: no "reviewed" flag and no site-versus-HR source on `Employee` (see "How a site registration reaches the list").

## Not decided here: questions for Nabil, each drawn on its recommendation

1. **Does HR review site registrations?** Drawn: yes, a worker registered from site shows «جديد» until HR opens or edits him. If HR does not review them, drop the badge and gap 6. If it does, the server needs the stored fact.
2. **The code on people lists.** Drawn: off; search is by name or phone. Today's list shows `E-…` under each name. Ask HR once whether they quote it (for a payslip or a contract).
3. **A read-only role.** Recommended: do not build `workers-ro` until a role needs it. No role reads the register without managing it today.
4. **التخصص on a salaried form.** Drawn: dropped, because المسمى الوظيفي says the same. The API still accepts it for salaried records; the form sends nothing.

Answered by D-232 and removed from this list: menu labels (kept), one heading or two entries (two rows), screens only versus records (screens only), who sees the day rate (no rate on this register), moving a person (no action), salary (none now).

## What the mock does not draw

The English/LTR text; the register-from-site form and the crew sheet (not redrawn by order); the worker history screen; payroll and salary; any rate; the HR landing page's own menu (the `layout.html` sample gives HR three admin rows and no register, which the real guard `employeeManageGuard` contradicts; the sample is stale, not this mock). The count line «يعرض 7 …» is kept: `catalogue.html` keeps its count line, so this follows the approved list pattern; the consultant's note that it explains is for Nabil to overrule.

# Extract chain (`extracts.html`)

**APPROVED, D-242 (2026-11-19).** Round 2; rejection routing by reason ruled in D-242 item 2 and built server-side in D-243 item 10. Thirteen screens from the screen briefs E1 to E13 in `ux/journey-extract-chain-2026-11-18.md` §8, plus the withholding category in the contract details (`#wh`). The journey was written before D-235 and D-238 to D-241; where they disagree, the mock follows the decisions (listed below). Round 2 answers `ux/consultant-review-extracts-mock-2026-11-19.md`: all seven musts and the shoulds that need no ruling (list at the end).

Open `extracts.html` directly: every screen is an artboard on one page, anchors `#e1` … `#e13` and `#wh`. Each artboard has its own state buttons (preview only). URL: `?view=e3:issued,e6:deposited` sets states per screen; `?view=loading` sets every screen that has that state; `?only=e5` draws one screen alone; `?embed=1` hides every preview label. A sheet or dialog is drawn open in place of the page it sits over, on a scrim; in the build it is the kit overlay (bottom sheet on a phone, 460 px side panel on desktop, centred dialog).

## One sample story for every screen

Every figure on every screen comes from one story, so the build is checked against figures that agree. Today is 19/11/2026. The contract is spec §15 (1,000,000, hold 20%, advance 25%, تشوينات 75%), the client withholds at «مقاولات وتوريدات 1%».

- Extract 1 (07/2026, net 240,000) and extract 2 (08/2026, net 120,000): تم تحصيله.
- **Extract 3** (01/09/2026 – 30/09/2026): spec §15 row, work 400,000, hold −80,000, advance recovery −100,000, تشوينات 0 (no new material on site), recovery −30,000 (75% of 40,000 installed), net 190,000; hold to date 200,000. Issued 08/10/2026.
  - Transfer TR-55821 on 12/10/2026 settles 100,000: 99,000 received, 1,000 withheld (1%).
  - Cheque 004512 received 16/11/2026 settles 90,000: face value 89,100, 900 withheld with it.
  - Now: «في شيكات» 90,000, «المتبقي» 0, the next task is «إيداع الشيك». E1, E3 `issued`, E5 `part`, E6 `received` and E7 all say this.
- Extract 4 (10/2026) is with the Technical Office. It bills change order 1. No cheque exists on it.
- **The dispute branch** (another path through the same story, so it has no cheque): after the transfer, the client disputes extract 3 on 20/10/2026. A price dispute ends in a 20,000 credit note (E9 `normal`, E10, then E3 `aftercn` and E5 `aftercn`: due 170,000, remaining 70,000). A quantity dispute ends in a revised extract, extract 5, that cancels extract 3 (E9 `qty`, E3 `cancelled`, E1 `revised`).
- **«في شيكات» counts what a cheque in hand will settle: its face value plus the amount withheld with it.** That is what makes «المتبقي» reach 0 when the last cheque arrives, and it is how D-238 counts cheques against what remains. The cheque row shows the face value and the withheld amount separately.

## The screens

| # | Screen | Who sees it | Primary action (shell slot) | Secondary, below the content |
|---|---|---|---|---|
| E1 | المستخلصات (project tab), with two sub-section links: «الشيكات تحت التحصيل», «أوامر التغيير» | Owner, Finance, Technical Office, Supervisor; the junior site engineer sees number, status and period, no money and no sub-sections (D-241 item 1) | «مستخلص جديد», only for the preparer and only when no extract is in flight | none |
| E2 | مستخلص جديد | Junior site engineer, no money (D-241 item 1) | «إنشاء المستخلص» | none |
| E3 | مستخلص {n} | every role; each control by its server flag | one by state and role: «تسليم تقرير الجودة» (Supervisor), «اعتماد الكميات» (TO), «تطبيق الخصومات» (Finance), «اعتماد» then, separately, «إصدار» (Owner), «تسجيل دفعة» or the cheque's next move «إيداع الشيك» (Finance), «حل الاعتراض» (Finance), «إعادة التجهيز» (site engineer after a quantity rejection) | «اعتماد جزء», «رفض», «تسجيل اعتراض» |
| E4 | اعتماد جزء (sheet on E3) | Technical Office | «اعتماد النسبة» | «إلغاء» |
| E5 | تسجيل دفعة | Finance | «تسجيل دفعة» | none |
| E6 | الدفعات | Finance | the next cheque move, one only: «إيداع الشيك», «تأكيد الصرف»; «تسجيل دفعة» when nothing is in hand and something remains | «الشيك مرتد» and «عكس الدفعات», both destructive triggers |
| E7 | الشيكات تحت التحصيل (sub-section of المستخلصات) | Finance; the Owner read-only (D-239 item 4), whose rows open the extract, not the payment | none: each row opens E6 (Finance) or E3 (Owner) | none |
| E8 | تسجيل اعتراض | Finance | «تسجيل الاعتراض» | none |
| E9 | حل الاعتراض | Finance | by the kind of error: «إصدار إشعار دائن» (price or amount) or «إنشاء مستخلص معدّل» (quantities) | none |
| E10 | إشعار دائن | Owner decides, Finance reads | «اعتماد الإشعار الدائن» (Owner, not the raiser) | «رفض» |
| E11 | أوامر التغيير (sub-section of المستخلصات) | Technical Office, Owner, Supervisor (no money) | «أمر تغيير جديد» (preparer) | none |
| E12 | أمر تغيير جديد | Technical Office | «حفظ أمر التغيير» | «إضافة بند» inside the editor |
| E13 | أمر التغيير | everyone who reads the project; each step by its flag | the caller's step, one only: «إرسال للمراجعة», «اعتماد المراجعة» (TO), «تسجيل موافقة العميل» (project manager or Supervisor, D-241 item 4), «اعتماد المالك» | «سحب», «رفض», «رفض العميل» |
| WH | الخصم من المنبع, a row of «شروط العقد» on the overview (D-241 item 3) | Finance edits through a sheet; the Owner reads | none on the page; the sheet's «حفظ» | «تعديل» on the row (Finance) |

Primary is in the shell slot on every screen: the header's end edge on desktop, the bottom bar beside «القائمة» on a phone. While a sheet or dialog is open the page's primary is hidden and the sheet carries its own. No primary is ever disabled: a missing field keeps the primary live and names the field on submit (E2 `err`, E3 `qcerr`, E5 `err` and `future`, E8 `err`, E9 `err`, E12 `nolines` and `qty0`). A control the caller may not use is absent (E1 `sup` and `prep`, E3 `barred`, E5 `credit`, E7 `owner`, E10 `raiser`, E11 `sup`, E13 `barred` and `sup`, WH `owner`).

**The waiting line has one form everywhere: «بانتظار {person}: {task}»** («بانتظار منى سامي: إيداع الشيك»). The client is the one holder with no person: «بانتظار العميل: الموافقة». A barred holder or the age is a dim second line.

## Server truth applied (D-235, D-238 to D-241)

1. **Prepare takes the period only** (D-238 item 4). The junior site engineer starts the extract (D-241 item 1). E2: the last extract, «الفترة» (من, إلى), the quantities by باب with no prices, and the change orders that go on.
2. **The Supervisor enters both material values with the QC report, both required** (D-235 item 4, D-238 item 4). The sheet is titled for what it holds, «قيم التشوينات»; its button is «تسليم تقرير الجودة». Extract 3's values are 0.00 on site and 40,000.00 installed, which give the §15 row.
3. **The junior site engineer sees no money.** E1 `prep`, E2, E3 `sedraft` and `rejected` carry quantities, dates and status only.
4. **A rejection lands on the person who can change something** (D-242 item 2). The reject sheet (TO, Finance, Owner; E3 `reject`, `rejerr`) asks «الخطأ في» first, required: «الكميات» (يعود إلى مهندس الموقع, server kind `Quantities`) or «قيم التشوينات» (يعود إلى المشرف, `MaterialValues`), then the reason. A quantity reason goes to the site engineer: E3 `rejected` shows the reason, a «فتح يومية الفترة» link where he corrects the log, and «إعادة التجهيز»; the chain marks «التجهيز». A material-value reason goes straight to the Supervisor: E3 `rejsup` waits on him at «تقرير الجودة» with «تسليم تقرير الجودة»; the junior sees nothing to do. A reason the site engineer cannot act on is never shown to him. Ruled in D-242 item 2 (amends spec §7) and built in D-243 item 10; the web must send the kind.
5. **Evidence revision has no body.** «إعادة التجهيز» re-reads the corrected daily logs.
6. **The Owner keeps two steps**, «اعتماد» then «إصدار» (kept by the coordinator; S1 is not taken).
7. **Finance sees the deductions after applying them.** E3 `applied`: the extract moves to the Owner with «طُبقت الخصومات.» and the document showing.
8. **The document shows «الحجز حتى تاريخه»** (spec §5.1 MUST) in its own row under the total, outside the deductions. The «المجموع مطابق» badge is gone: a document that adds up says nothing; one that does not would show an error.
9. **A payment records what arrived** (D-238 item 3). E5: method, date (today or earlier, D-240 item 3), reference (its label and its error follow the method: «أدخل رقم التحويل.»), «المبلغ المستلم» required, «المخصوم من المنبع» optional and absent when the contract has no category (`nowh`). The withheld field's hint is the category, «مقاولات وتوريدات 1%». A payment past what remains is named under the field only, no banner (`exceeds`).
10. **The collection position** on E3, E5, E6 and E8 is five figures: «المستحق» (net payable less approved credit notes, with «بعد إشعار دائن 20,000.00» under it when one applies), «المستلم», «المخصوم من المنبع», «في شيكات», «المتبقي». Received and withheld are separate so «المستلم» matches the bank.
11. **A cheque is cash only when it clears** (D-238 item 2). E6 walks مستلم → تم إيداعه → تم صرفه; at clearance the extract is تم تحصيله. Bounce needs a reason, posts nothing and keeps the extract صادر.
12. **«عكس الدفعات» is one name** for the link, the sheet, its button and the banner after («عُكست الدفعات»). It is a destructive trigger, and it reverses every standing payment at once (D-238 item 3); the sheet's consequence line names the total.
13. **A raised credit note blocks collection until the Owner decides** (D-239 item 1): E3 `credit` has no primary, E5 `credit` is refused. **After approval** the expected figure drops: E3 `aftercn` and E5 `aftercn`, due 170,000, remaining 70,000.
14. **No cap on a credit note** (D-239 item 2). E9 shows net payable and earlier credit notes as facts; the amount field has no limit hint.
15. **A credit note on a collected extract cannot be reversed** (D-239 item 3): no reversal control anywhere.
16. **Ruled in D-242:** payments already received on a cancelled extract move to the revised extract as already paid (item 3); a revised extract may run beside the next normal extract (item 4); the original's hold carries to the revision (D-244 item 1).
17. **A price dispute ends in a credit note; a quantity dispute ends in a revised extract that cancels the original** (D-235 item 3, D-241 item 2). E9 asks «نوع الخطأ» and draws both: `normal` (price, credit note, then E10) and `qty` (quantities, «إنشاء مستخلص معدّل»). After a quantity resolution, the original is «ملغى» (E3 `cancelled`, no actions, a link to its replacement) and the revised extract 5 starts again at the Supervisor (E1 `revised`, «يحل محل مستخلص 3»). **Build plan: E9 is built with both kinds. The revised extract must be built on the server before E9 ships; a credit-note-only E9 is not an acceptable interim, because it would settle quantity disputes with money.**
18. **Cheques in hand and change orders are sub-sections of the Extracts tab; the withholding category sits with the contract details** (D-241 item 3). E1 ends with two link rows (with counts); E7 and E11 go back to «المستخلصات». WH is the overview's «شروط العقد», opened, with «الخصم من المنبع» as a row and «تعديل» for Finance.
19. **The client's approval of a change order is recorded by the project manager or Supervisor** (D-241 item 4). E13 `client` waits on «العميل»; the Supervisor holds «تسجيل موافقة العميل» and sees no prices.
20. **Words** (D-240): «الحجز» everywhere, never «المحجوز»; a money entry is «حركة», never «قيد» (neither appears); material is «تشوينات». The Owner's confirmation says «ويُضاف إلى الحجز» (no ledger verb).
21. **Each action shows only when the read's flag says so** (`ExtractActions`).

## Reused from the kit and earlier mocks, unchanged

Shell (sidebar, phone header, bottom bar, destinations sheet), project frame with stage line and tabs (E1, WH), back link, status pill, badge, buttons (filled, `sec2`, `quiet`, `dtrig`, `dconf`), fields (text, money with affix, number with affix, date with the 44 px calendar button, select, textarea), the radio group (`rdg`), refusal and note banners, the saved-in-place banner (`rec`), state block (normal and refused), skeleton, list (`lst`), section heading, bottom sheet and side panel (`sx`), confirmation dialog (`dlg`), the details disclosure, the desktop table and the phone row.

## New in the kit (put these in `components.html` before building)

1. **Extract status pill set.** The app's words on existing tones: مسودة hollow ring; عند المكتب الفني, معتمد جزئيًا, عند الحسابات, عند المالك, معتمد من المالك the in-progress dot; **صادر a new ink outline with an arrow (`s-out`)**; تم تحصيله the finished check; معترض عليه the stalled triangle, the only red; **ملغى** the postponed tone with a cross. Cheque: مستلم ring, تم إيداعه dot, تم صرفه check, مرتد triangle. Change order, in the «عند …» form of the extract pills: مسودة ring, عند المكتب الفني, عند العميل, عند المالك dot, معتمد and مسجل check, مرفوض triangle, مسحوب cross. Credit note: عند المالك dot, معتمد check, مرفوض triangle.
2. **Chain line (`stl x7`).** The stage line with seven steps (التجهيز … التحصيل), .6875rem labels on a phone, capped at 860 px on desktop; four steps for a change order. A cancelled extract has none.
3. **Waiting line (`wait`).** One band, «بانتظار {person}: {task}», with a dim second line.
4. **Figure list (`fl`).** Label at the start, figure at the end, real minus, ruled total row; the unit is in the section heading once.
5. **Collection position (`pos`).** Five figures, المتبقي in the head fill. Five across on desktop; two by two on a phone with المتبقي full width.
6. **Quantities table (`qt`).** باب band, line name with its unit under it, «هذه الفترة» and «حتى تاريخه», decimals only when the value has them.
7. **Change-order line editor (`edt`, `ln`).**
8. **List band (`lh`).** A head-fill band above a list naming the first column and the money column with its unit.
9. **Forward link row (`back fwd`).** The back link's face with the chevron pointing forward and an optional count badge, for sub-sections (E1) and «الدفعات» (E3).

## Words on the screen beyond labels (each is allowed by the screen-brief rules)

- E3 `barred`, E10 `raiser`, E13 `barred`: the separation-of-duties line (the brief keeps it; the layout cannot say "you took part").
- E3 `stopped`: «المشروع متوقف، فلا يصدر المستخلص.» (brief: one line saying why issue is absent).
- E3 `confirm` and E6 `reverse`: consequence lines before a money action.
- Cut in round 2: the E4 sentence (folded into the label «معلّق حتى إغلاق الملاحظة»), the E6 bounce sentence, the E9 revised sentence.

## Codes left on the screen

Extract number and change-order number: people read and say them. Cheque number and transfer reference on E6 and E7: Finance reads and types them. Dates DD/MM/YYYY. No id, no NCR code (title only), no category code, no account name, no enum name.

## New words (every one is a guess; Nabil or `arabic-copy` confirms)

«إعادة التجهيز», «فتح يومية الفترة», «الخطأ في», «اختر موضع الخطأ.», «يعود إلى مهندس الموقع», «يعود إلى المشرف», «قيم التشوينات», «تشوينات» (the advance row), «الحجز حتى تاريخه», «طُبقت الخصومات.», «ويُضاف إلى الحجز», «اعتماد جزء», «اعتماد النسبة», «المعتمد للدفع», «معلّق حتى إغلاق الملاحظة», «تسجيل دفعة», «المبلغ المستلم», «المخصوم من المنبع», «رقم الإيصال», «رقم التحويل», «أدخل رقم التحويل.», «التاريخ اليوم أو قبله.», «أكبر من المتبقي (…)», «المستحق», «المستلم», «في شيكات», «المتبقي», «بعد إشعار دائن …», «الدفعات», «الدفعة», «إيداع الشيك», «تأكيد الصرف», «الشيك مرتد», «سبب الارتداد», «تسجيل الارتداد», «عكس الدفعات», «عُكست الدفعات», «الشيكات تحت التحصيل», «لا توجد شيكات تحت التحصيل», «منذ», cheque states «مستلم», «تم إيداعه», «تم صرفه», «مرتد», «تسجيل اعتراض», «سبب الاعتراض», «معترض عليه», «حل الاعتراض», «نوع الخطأ», «السعر أو المبلغ», «الكميات», «مستخلص معدّل», «إنشاء مستخلص معدّل», «ملغى», «أُلغي. يحل محله مستخلص 5.», «يحل محل مستخلص 3», «مبلغ الإشعار», «أدخل مبلغ الإشعار.», «إشعارات دائنة سابقة», «إصدار إشعار دائن», «اعتماد الإشعار الدائن», «رفض الإشعار», «اعتراض العميل», «المستحق بعد الإشعار», «أنشأه», change-order states «عند المكتب الفني», «عند العميل», «عند المالك», «مسحوب», «مسجل», «إرسال للمراجعة», «اعتماد المراجعة», «تسجيل موافقة العميل», «رفض العميل», «بانتظار العميل: الموافقة», «سحب», «قبل الأمر», «بعد الأمر», «السجل», «الخصم من المنبع», «فئة الخصم», «لا يخصم», «مقاولات وتوريدات», «خدمات», «أتعاب مهنية», «حُفظ الخصم من المنبع: …». The server's refusal `errors.extract.material_values_required` says «الخامات الموردة للموقع / الخامات المركبة»; the app and this mock say «قيمة التشوينات بالموقع / المركّبة», and D-240 says «تشوينات»: the server key should change.

## Not decided here: questions for Nabil (each drawn on a default)

1. **Line-by-line quantity confirmation at QC** (D-235 item 4): no read exists; the sheet holds the two values only.
2. **Withheld amount against the rate** (Q99): the field accepts any figure.
3. **Category changeable at any time** (Q30): drawn always editable by Finance.
4. **Partial approval (E4)** needs an NCR object and a ruling on what the percentage scales (journey Q12, Q13).
5. **Draft change orders cannot be edited** (S8, journey gap 10): a typo means withdraw and retype.
6. **The client's approval of a change order leaves no evidence** (S9): no date, reference or document, because the route takes no body.
7. **The withholding word** «الخصم من المنبع» is not ruled (D-240 item 4).
8. **A client who has paid more than is owed** after an uncapped credit note (D-239 item 2).
9. **Deposit and clearance carry no date or bank** (D-238): the state date is the day it was recorded.
10. **A cross-project cheque list** for Finance, who banks cheques for every project at once (S4): not drawn; E7 is per project as D-241 places it.

## What the API does not send for this design (the build stops here until it does)

- Received and withheld as two figures in the extract's `collection` block (it sends `Collected` as their sum), and per payment.
- A line-by-line quantity read per extract and a quantities preview before prepare (journey gap 6).
- The change orders the next extract will bill, with titles (gap 10); the title on `ExtractChangeOrderResponse`.
- The dispute reason on the extract read and on the credit note read; the credit note summary on the extract read.
- The holder's name and task for credit notes, change orders and cheques (gap 7).
- Open NCRs (gap 5).
- The date a cheque's state last changed, on `GET /api/projects/{projectId}/cheques`.
- The revised-extract resolution, the cancelled state (`Void`) and the link between the original and its replacement.

## Consultant review round 2: what changed, what was left

Fixed: M1 to M7; S3 (sheet title), S4 (sub-sections, contract details, D-241 item 3), S5 (`applied`), S6 (client claim and due after the note on E10), S7 (`aftercn` on E3 and E5), S10 (one waiting-line form), S11 (waits on the client), S12 («عند …» change-order pills), S13 (one name «عكس الدفعات», destructive trigger), S14 («تسجيل دفعة» title and primary), S15 («المستحق» for the total, «المستلم» and «المخصوم من المنبع» separate, no «المتوقع»), S16 (E7 empty state), S17 (age beside the state), S18 (Owner rows open E3), S19, S20, S21; C1, C2, C4, C5, C6, C7, C9.

Left: S1 (the Owner keeps two steps, coordinator), S2 (the junior starts the extract, D-241 item 1), S8 and S9 (need rulings, questions 8 and 9), C3 (the pill stays on the detail page: «معترض عليه» and «ملغى» are not said by the chain line), C8 (the sidebar shows one user for every artboard: the preview label names the role of each view).

## What the mock does not draw

English/LTR text beyond the direction switch; a project in Cost Plus or Design on E1 to E10 (only E11 `cp` and E12 `cp`); the Owner's cross-project waiting list; the printed extract document; the derivation panels behind «التفاصيل»; the daily log the rejection link opens (`daily-log.html`); a payment made by several cheques at once; the moment a busy button spins.

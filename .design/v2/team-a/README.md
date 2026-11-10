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

The project's own page, the hub a user lands on after opening a project from the list. Mockup: `project.html` (open directly, no build step). Query parameters select a state for screenshots: `?type=ls|cp|ds&role=owner|site&view=normal|loading|failed|partial|setup|empty|refused|stop`; `embed=1` hides the preview strip. Screenshots are `shots/project-*.png`. The strip at the top (type, role, state, theme, direction, review notes, data-source flags, phone preview) and every dashed amber "Review note" block are review chrome: do not build them. The "data source" toggle outlines each region as served today (solid) or needing an endpoint (dashed red).

## What it is

Five groups, which are intentions and not tables: الفهم (understand), الكميات (quantify), التنفيذ (execute), المال (pay), التنظيم (organise). All three contract types hold all five groups; they differ in what each group declares. A control or whole group the viewer may not use is absent, never disabled (the site engineer role has no المال group and no money in any other group). The screen declares exactly one `primaryAction` and never renders it itself: the shell places it in the page header on desktop and the bottom bar on phone. The proposed actions are listed under "Not decided here".

| Group | Lump Sum | Cost Plus | Design |
|---|---|---|---|
| الفهم | contract value (carried from signed BOQ), hold rate, advance rate, hold at handover, certified percent bar | supervision rate, cost to date, supervision to date. No contract value row, no percent bar | area, rate, fee, five-segment stage strip (width is the fixed weight) |
| الكميات | BOQ by باب (frozen copy, signed date), change orders in their own list | classification of contract lines: supervised, exempt, non-billable. No BOQ | fee derivation, five fixed-weight stages (locked). No BOQ |
| التنفيذ | executed vs surveyed per باب (percent only), daily logs, open NCR, subcontractors | daily logs, internal engineer hours. No executed-vs-surveyed | per-stage progress, revision rounds, deliverable state, six-step chain for the current stage. No site, subcontractors or materials |
| المال | pending extract chain with gate actors, consequence block, five-ledger strip scoped to the project accounts, last extract | period invoice chain and waterfall, revenue and cost accounts. No hold, advance, تشوينات, strip | stage dues waterfall, revenue and consulting-cost accounts. No chain, strip, hold |
| التنظيم | team, linked projects, stop control | team, stop control | team, linked execution project, stop control |

## Layout

Page header (shell): back link (المشاريع), title, status pill, then one meta line (code, client, contract type badge). The header ships the state alone. The health tags متعثرة and تم تأجيلها are tags, not states (D-044 §7) and have no data behind them: nothing is reserved or drawn for them.

Below the header, a sticky jump line of five underlined links (one per group the viewer may see; 48px). Then five sections. Desktop: a 232px heading column (icon, group name, one intention question that changes by type) that sticks while its section scrolls, and a body column. At 1100px and narrower the heading becomes a row above the body. Phone: single column, jump line scrolls horizontally, key-value rows stack label over value, tables become rows (no horizontal scroll), the pending-extract chain turns vertical, ledger tiles stack. Sections are separated by hairlines; there are no cards as containers (bordered surfaces are the kit's own: key-value list, waterfall, tiles).

## Parts: reused, and new

Reused unchanged from the kit: status pill, badge, back link, link, buttons, refusal and notice banners, the four list states (the refused state is the project-scoped version), skeleton, `fig` (figure), provenance chip (`pv` calculated / entered / carried) and assumption chip (`asm`), waterfall (`wf`), five-ledger tile strip with drill-down, reconciliation line and unclassified box, approval chain, consequence block (`dec-con`), gate-actor statement (`dec-who`), table frame, quiet recoverable confirmation. Added by this screen (put them in the kit before building):

- Group section: heading column plus body column, as above.
- Key-value fact list (`kv`): label, value, provenance chips at the end. The null variant has an info icon, a bold reason and a smaller explanation: it is how contract value reads while the BOQ is unsigned (a null is never drawn as 0.00 or a dash).
- Progress block (`pg`): label, big number, 12px track, footer with chip. The number is always printed beside the bar.
- Design stage strip (`stg`): segment width is the fixed weight; fill (solid, hatched, half) plus a legend carries state, so colour is never the cue. Under 820px it wraps to two columns.
- Percent bar rows in tables (`bd`): 10px track plus number.
- "What the database may refuse" block (`chk`): three figures (current balance with timestamp, requirement, expected remainder) and one sentence that says it is a reading, not a promise.
- Locked group line (`locked`): dashed box with a lock and one sentence, used when a group is not yet applicable because of state (Setup), as opposed to absent because of type.

## States (each drawn)

Normal for each type and role; loading (header keeps shape, title is a bar, primary slot reserved, all five headings shown with skeleton bodies); failed to load (refusal banner with retry; no groups, no primary); partial failure (the المال group fails alone, banner with retry in place, the rest stays); BOQ not signed (Setup: contract value null with reason, hold and advance as rates only, BOQ draft with its own total labelled "not the contract value", execute and pay locked); new project with an empty BOQ (explicit empty state); refused (role without assignment: dashed lock box, no project details, no retry); stop confirmation (recoverable, quiet, inline).

## Permissions and money rules this screen carries

- Role and assignment are checked server-side on every read. Each group is a separate read so a refusal or failure in one never blanks the page. A group a role may not read is absent from the DOM.
- The site engineer view is a money-free read model, not columns hidden: item counts, executed percent, logs, hours, counts. No rates, amounts, balances or the certified percent.
- Every money figure carries a kind chip. Where a figure is entered the chip names who and when; where carried, the named document or ledger.
- Pending-extract copy states what posting will do as arithmetic, and separately shows what the database may refuse (advance floor) as a current figure with a timestamp. Never "this will succeed".
- The five-ledger strip is presentation, not a guard: drill-down per tile, reconciliation line, unclassified box that must stay at zero. On this page it is scoped to the project's own accounts (client advance, hold, firm advance) because that is what `ProjectAccountSetFactory` opens for Lump Sum; تشوينات (MaterialAdvance) sits beside it, and ClientReceivable is deliberately not drawn because it carries no LedgerKind: that is why the unclassified box must stay at zero.
- Destructive or irreversible actions are never in the phone bottom bar. The only lifecycle control drawn is stop (recoverable).

## What the API serves today

`GET /api/projects/{id}` serves only: id, code, name, client id and name, contract type, status, contract value (null for Cost Plus), hold rate, advance rate, team size. Everything else drawn here needs an endpoint: progress, BOQ, change orders, logs, extracts, ledger balances and postings, roster, linked projects, lifecycle, Cost Plus supervision rate and Design area and rate. Money crosses the wire as strings (D-135): format with the same `fig` rules and never parse to a float. The data-source toggle in the file marks each region.

## Not decided here (questions for Nabil)

1. How are the project states Stopped, HandoverPending, Handover, UnderWarranty and Terminated worded in the header? The five Kaff words cover Setup (لم تبدأ), Active (جاري العمل) and Closed (انتهت) only, and a status pill may only carry those words.
2. The primary action per type, state and role is a proposal (مستخلص جديد, فاتورة جديدة, رفع مخرجات المرحلة, فتح جدول الكميات, سجل يومي جديد).
3. Which roles see which group, and whether a site engineer may see the certified percent. No permission row exists for stop, restart, terminate, practical completion, snag, handover.
4. After a change order is approved, does the page show original value, revised value or both?
5. When an extract posts (the study's first question). The consequence block says "عند الترحيل" and flags assumption A1.
6. Provenance of hold and advance rates (who entered, when). Contract value is settled, not a question: it is derived and never typed (Lump Sum carried from the signed BOQ, named with its signature date; Design calculated as area x rate; Cost Plus has none, so the row is absent).
7. Is the five-ledger strip on a project page scoped to project accounts (three tiles) or all five? عهدة has a per-project ceiling in the spec but no project account; the owner current account is company-level.
8. Arabic labels for the six design-phase states, the number of included revision rounds per stage, and Arabic names for the three contract types (spec §14 has none; the screen uses those from `money.html`).
9. What happens to an extract already in the chain when the project is stopped.
10. Who may assign a user to a project (the refusal sentence names the permission, not a person), and whether a Setup project can take daily logs.
11. Whether Cost Plus invoices use the same seven-step chain as Lump Sum extracts.

## Deliberately left out

Project documents (not in the spec at project level), history or audit timeline for the project (the audit read is Owner-only and the gate history exists only per document), edit-terms screen, collections and withholding, closure, hold release, snags, warranty and handover flows, any terminate control (unruled), any health tag.

# Today's crew (`day-labour.html`)

Approved by Nabil, 2026-11-10, with these two fixes (D-213): the title is neutral and the date shows once.

Mock of the day-labour screen (route `/projects/:projectId/day-labour`), brief 2 of `ux/journey-project-module-2026-11-09.md`. Nabil saw it and accepted the crew screen. His ruling (D-213): labour is hours, per man, per day; a man may work 4 hours one day and 8 another. There is one unit, hours; the first draft's second unit was a misreading of D-212 and is gone. Brief 1 (the daily log without labour) still waits for his word. Open `day-labour.html` directly. `?view=normal|filled|saved|yesterday|empty|sheet|loading|failed|refused|unassigned`; `embed=1` hides the preview strip (review chrome, do not build it). The stepper, the "same crew as yesterday" button, the date switch and the add-a-man sheet work.

## What it holds

For the Site Supervisor or site engineer, on a phone, one job: say how many hours each man worked on the chosen day, in one save.

- Back link carrying the project name (where am I), the title "العمالة" (neutral, so it never contradicts the selected date), the date switch (اليوم / أمس) with the date written out once. Each date holds its own hours: switch to أمس and the rows show yesterday's hours (a man with 8 yesterday may show 4 today).
- Totals strip, sticky: men and hours. Before anything is entered it says "لم يُسجَّل أحد بعد".
- "نفس عمالة أمس" (outline button), present only on today while nothing is entered. It copies yesterday's hours into today's rows, which the supervisor then adjusts.
- The crew: one row per engaged man. Name and trade stacked on the start side, one hours stepper on the end side, on the same line at 390 and 360. A man with hours gets a 5px start bar. A man left at zero did not work that day.
- The unit is carried once, as the column head "ساعات" above the steppers, and again in the totals strip. It is not repeated in each row.
- "أضف عاملاً" (outline) at the foot of the list opens the add-a-man sheet.
- Primary action "حفظ", in the bottom bar on a phone and in the page header on desktop. Disabled until one man has hours; the totals strip carries the reason.

## States

- **Normal**: crew listed, nothing entered.
- **Filled**: hours entered, totals live.
- **Saved**: rows read-only with a tick and hours, banner "تم الحفظ". The primary becomes "تعديل". "أضف عاملاً" is absent.
- **Yesterday**: date switch on أمس, yesterday's own hours per man, and a note banner "بأثر رجعي" above the totals (the date is written once, beside the switch). Yesterday only.
- **Empty**: "لا عمالة مشغَّلة على المشروع". "أضف عاملاً" is the primary here.
- **Add-a-man sheet**: bottom sheet on a phone, side panel on desktop. Search placeholder "اسم أو مهنة", by name or trade, never a code. Each result: name, trade, times engaged, rating, phone, and one "تشغيل" button. A "تسجيل عامل جديد" link at the foot opens the registration route.
- **Loading**: skeleton rows. **Failed**: refusal banner with retry. **Unassigned**: the project-scoped refusal, same as the workspace.
- **Row refused**: saved rows show a tick and are read-only; one row whose hours the server refuses (error border and one line in place, "الحد الأقصى 24 ساعة"); one row whose save failed, which keeps its hours and shows "إعادة المحاولة". Saved rows are not resubmitted; each row carries its own entry id so a retry cannot double it.

## Reused from the kit, unchanged

App shell and bottom bar, back link, buttons (outline `sec2`, small), segmented control, search field, refusal banner, note banner, saved-in-place banner (`rec`), the four list states, skeleton, bottom sheet.

## New in the kit (put these in `components.html` before building)

- **Hours stepper** (`stp`): minus, typed value, plus; 48px field, 44px-plus buttons, no unit text inside. Minus is disabled at zero and plus at the cap (the limit explains itself). In error the border takes the error colour and a line under the row gives the reason with an icon.
- **Crew row** (`man`): name over trade, one stepper on the same line. Variants: entered (start bar), saved (tick and hours, read-only), refused (error bar), failed (retry line). Column head above the first row carries the unit.
- **Totals strip** (`tot-in`): men and hours; the zero variant is one line.
- **Side panel on desktop, bottom sheet on a phone** (`sx`): the kit already has both; this joins them under one markup so a part is not drawn twice.

No new colour: everything is an existing token.

## Left off the screen, on purpose

Worker code, the day rate (D-139), phone number on the crew row (it shows in the sheet, where duplicates are told apart), times engaged and rating on the row (they show in the sheet, where they help choose), the history link, any explanatory sentence. No code of any kind is on the screen.

## Not decided here (guesses in the mock)

1. Hours cap: 24 per man per day is a guess (`DayLabourEntry.MaxHours` is not in the material read).
2. Step size: half-hour steps are a guess.
3. Editing after save: the mock returns to editing with "تعديل". Whether a saved day may be changed, by whom and until when is not ruled.
4. What "same crew as yesterday" copies: the mock copies the men and their hours; copying only the men (hours left empty) is the alternative.
5. How far back an entry may go: yesterday only, per D-212.
6. The Supervisor's history link: dropped; whether he should have it is not asked.
7. The phone number in the sheet: the brief lists it in one place and omits it from the result list in another. The mock shows it.

## What the mock does not draw

Weekly payroll and the screen that follows it; rate entry for a new man; registration (`.../day-labour/new`); closing an engagement and rating; the daily log with labour removed (brief 1); the history route; the English/LTR text.

# The daily log (`daily-log.html`)

Mock of the daily-log screen (route `/projects/:projectId/daily-log`), brief 1 of `ux/journey-project-module-2026-11-09.md`, with Nabil's rulings of 2026-11-10 (D-212, D-213) applied over it. Awaiting Nabil's approval; nothing is built. Open `daily-log.html` directly. `?view=before|empty|saved|qty|picker|mat|photo|yesterday|location|far|stopped|stoppedsaved|closed|loading|failed`; `embed=1` hides the preview strip (review chrome, do not build it). Working in the mock: check-in and check-out, the kind switch, the item picker with search by name, saving an entry (it joins "today so far", the form clears, the executed figure moves), the date switch.

## What it holds

For the site engineer or Supervisor, on a phone, one job: put today's site facts into the log, as quantities, materials, a note or a photo. It is the same tool as the crew screen (`day-labour.html`): the header, the date switch, the back-dated banner and the bottom bar are copied from it, not redrawn.

- Back link with the project name, neutral title "اليومية", date switch اليوم / أمس with the date written once beside it.
- Check-in strip (top, not the page primary): before check-in one outline button "الحضور" (a 56px row); after it, one row with a tick, "الحضور" and the time, and an outline "الانصراف" button; after check-out, both times and no button. A far-from-site check-in is accepted and carries the tag "بعيد عن الموقع" (shape and word) beside the time. No dropdown, no site picker (D-207, D-208).
- Crew link: one navigation row "العمالة" with the day's summary ("5 عمال · 36 ساعة") and a chevron, to the crew screen. It is a link, not a button: outline row, no fill, so the page has one primary. Labour is not entered here (D-213).
- "المسجَّل اليوم": entries grouped by kind (الكميات, الخامات, الملاحظات, الصور), each group with a count badge, newest first inside a group. One figure per entry: quantity and unit for a quantity or a material, nothing for a note, a thumbnail for a photo. After a save, "تم الحفظ" appears above the list.
- The entry form: four kinds in a segmented row, ملاحظة · كمية · خامات · صورة (icon over word, 64px). Note: one text area. Quantity: the BOQ line picked by name, then "الكمية المقاسة" and "المنفَّذ حتى الآن" side by side (unit in the label), then "تنفيذ اليوم" with the unit as the field affix. Materials: name, quantity, unit chosen from a list. Photo: a large dashed "التقاط صورة" and an optional caption.
- Primary "حفظ": bottom bar on a phone, page header on desktop. Disabled until the form is complete; required fields carry the `*`. After saving, the form clears and stays open for the next entry. On a phone the date, strip and crew link come first, then the form, then "المسجَّل اليوم", so a long list never pushes the form off the screen and a saved entry appears right under the form; on desktop (1000px and wider) the form and the list sit side by side.

## States

- **Before check-in** (`before`): strip is the single button; log empty with its one line; form open on note.
- **Checked in, log empty** (`empty`): strip shows the time; "لم يُسجَّل شيء في يومية اليوم بعد." inside the list frame; the form directly below. No second prompt.
- **Entries saved** (`saved`): groups with figures.
- **Quantity form** (`qty`) and **item picker** (`picker`): the picker is the add-a-man sheet part (bottom sheet on a phone, side panel on desktop) with the search "اسم البند". Each result: line name, the باب name in dim, "المنفَّذ x من y unit", and "اختيار". Search by line name or باب name; no code anywhere.
- **Materials form** (`mat`), **photo form** (`photo`).
- **Yesterday** (`yesterday`): switch on أمس, the banner "بأثر رجعي" (the date is not repeated inside it), yesterday's own entries, the form labelled "تنفيذ أمس". The check-in strip is absent: attendance is a live GPS act and cannot be recorded for a past date. Yesterday only (D-212).
- **Location refused** (`location`): refusal banner above the strip saying why and how to switch location on; "الحضور" stays pressable.
- **Far from site** (`far`): accepted, tagged "بعيد عن الموقع".
- **Project stopped** (`stopped`, and `stoppedsaved` for the saved row): a note banner "المشروع متوقف" (state, not a sentence) above the strip; the kind row gains a fifth kind "سبب التوقف", first and selected; the form is the weather choice plus a written reason. The project still takes the other entries (not ruled, see below).
- **Project does not take a log yet** (`closed`): the refused state with "هذا المشروع لا يقبل اليومية بعد" and a link back to the workspace. No form, no date switch, no primary.
- **Loading**: skeleton of the strip and the list. **Failed**: refusal banner with retry; the strip stays, the form is not drawn (the item list may not have loaded; attendance does not depend on it).

## Changes after Nabil's review (D-214)

1. **The quantity is an estimate, and the screen says so by label and by figure, not by a sentence.** The input is labelled "تنفيذ اليوم (تقديري)" (yesterday: "تنفيذ أمس (تقديري)"). Every executed figure carries a "≈" written inside the figure (left-to-right, "≈ 12.5"): the "المنفَّذ حتى الآن" cell, each saved quantity row, and the picker result. The group is headed "الكميات التقديرية". The measured quantity ("الكمية المقاسة") has no "≈" because it is a real figure. The "≈" is a glyph that carries the meaning on its own, so it does not rely on colour. No control to confirm the real value is drawn: who confirms and where is not ruled (journey preamble, D-214).
2. **Stoppage reason returns as its own kind**, "سبب التوقف", with the choice and the written field of item 3. It is a fifth kind and appears only on a stopped project (`?view=stopped`), where it is first and selected. In a running project it is absent. Whether a running project may record a stoppage is not ruled.
3. **Weather is not an entry of its own; it is one reason for a stoppage (D-215, Nabil: "the weather is made as the reason not alone").** The first draft's weather row and its four values are gone from every view. The stoppage form is: the required label "سبب التوقف", one choice "الطقس" (a one-tap button, pressed when chosen), and a written field "سبب آخر" for anything else. The two are mutually exclusive: choosing "الطقس" clears the text, typing clears the choice. "حفظ" is enabled when one of them is given. No other reasons are offered because no fixed list is ruled. The saved row in the list shows "الطقس" or the written text (`?view=stoppedsaved`). The code still has a separate `Weather` kind on `LogEntry`; that changes at build, in the backend lane.
4. **Single-valued entries replace, they do not stack.** Note and stoppage reason hold one value per day (latest wins, as `DailyLogQuantityEntry` remarks do): the form opens with the current text and saving replaces it, and the list shows one row. Quantities, materials and photos still add.
5. **The kind row grows.** Four kinds stay one row at 360px (80px wide, 64px high). Five kinds (stopped) wrap as three then two, each 64px high and wider than 44px, so nothing shrinks below the touch target.

## Reused from the kit, unchanged

App shell, bottom bar with one primary, back link, outline buttons (`sec2`, small), quiet button, segmented control (date switch and the kind row), form field with label / control / affix / `*`, text area, select, tot-in (two figures), badge, pill (`s-none` for the far-from-site tag), refusal banner, note banner, saved-in-place banner (`ok`), state block (refused), skeleton, bottom sheet / side panel (`sx`), search field, result list, text link.

## New in the kit (put these in `components.html` before building)

- **Check-in strip** (`att`): one bordered row, tick + label + time on the start side, outline button on the end side. Variants: before (a single 56px button filling the row), in, out (no button). The only new part with behaviour.
- **Link row** (`clink`): icon, bold name over a dim summary, chevron at the end; 64px; hover tint. A navigation row inside a page, distinct from a list row that opens a detail panel.
- **Log group** (`lg`) and **log entry** (`ent`): a head row in the table-head tone with an icon, the group name and a count badge; entry rows of name at the start, one figure at the end. The photo variant swaps the figure for a 48px thumbnail at the start.
- **Kind row**: the kit segmented control in an equal-buttons variant (icon over word, 64px, fills the width; wraps 3 + 2 when there are five). Not a new colour or shape. New small part: the **choice chip** (`chip`), a 48px outline button with an icon and a word that fills when pressed; used for the one-tap stoppage reason "الطقس".
- **Picker trigger**: a field whose whole 56px surface is a button with a down chevron, opening the picker sheet; used where a value is chosen from a long list by name.
- **Photo capture** (`shot`): a 72px dashed full-width button; once a photo is taken it becomes a row with a 56px thumbnail and "إزالة".

No new colour: everything is an existing token. The saved-in-place banner is the kit's `ban ok`; `day-labour.html` calls its saved banner `rec` but never defines it, so that screen shows it unstyled and should be switched to `ban ok` when it is built.

## Left off the screen, on purpose

The estimated quantity (a Sales number, SPEC section 4.3), project code and contract value, line codes (lines are picked and searched by name, D-212 Q-M), entry ids, the photo's file reference, the المستخلصات link, the worker dropdown, hours box and labour kind (crew screen), the free "unit" text box on materials (replaced by a list), both hint sentences of the old screen (the GPS sentence and the "executed in this period only" sentence).

## Not decided here (guesses in the mock; Nabil to rule)

1. **Offline.** Not drawn. Brief 1 says offline creates drafts but nobody checked whether this page queues anything. Ask the builder first.
2. **Does the engineer see the estimated quantity?** The brief hides it and the mock keeps it hidden. Open.
3. **Photo upload.** The API has `RecordPhoto` but no upload endpoint was found (journey break 13). The photo kind is drawn per the brief and needs backend before it can be built.
4. **Unit list on materials.** The seven units (كيس, م³, م², طن, متر, قطعة, لتر) are a guess; no data source was checked.
5. **Check-in before a form.** The mock does not require check-in before saving entries. Whether it should is not ruled.
6. **Check-out before yesterday's entries, attendance for yesterday.** The strip is absent on yesterday; whether a missed check-in can be filled afterwards is not ruled.
7. **Order and grouping.** The brief lists "today so far" before the form, and says a saved entry appears at the top. The mock puts the form first and the list under it (a long list would otherwise hide the form), and groups by kind with the newest first inside its group.
8. **Check-in button pair.** The brief draws a pair of large buttons before check-in. The mock shows only "الحضور"; "الانصراف" has nothing to close yet, so it is absent (a control the user may not use is absent), and appears after check-in.
9. **Whether the quantity second entry adds or replaces** (Q-T). The mock adds and moves "المنفَّذ حتى الآن"; if the answer is "replaces" the label and the reasoning change.
10. **How many days back.** The mock offers yesterday only.
11. **Who confirms the real quantity, and where** (D-214). Nothing is drawn.
12. **May a running project record a stoppage?** The mock shows the kind only on a stopped project. Also not ruled: whether a stopped project may take quantities, materials and photos (the mock allows them).
13. **Other stoppage reasons.** Only "الطقس" and a written reason are drawn. Whether a fixed list of reasons exists is not ruled. Whether a running project may record a one-day stoppage (rain while the project is still جاري العمل) is not ruled; the stoppage kind is drawn on a stopped project only.
14. **Latest wins for note.** The mock replaces the note; whether an engineer should be able to keep two notes in a day is not ruled.

## What the mock does not draw

An offline state; check-out of a past date; the photo being taken or uploaded; a refused or failed save of one entry; a refused quantity (over the surveyed total); the English/LTR text beyond the direction switch.

---

# The BOQ and quotation builder (`boq.html`)

Mock of the BOQ screen (route `/projects/:projectId/boq`), brief 3 of `ux/journey-project-module-2026-11-09.md`, redrawn after Nabil's review on 2026-11-10: the BOQ "could be better, like drag and drop, maybe drag from the catalogue, and I think there was a base project he can use and edit". Open `boq.html` directly. `?view=empty|base|blank|building|edit|pending|sheet|dropfail|partial|sign|frozen|archived|design|loading|failed`; `embed=1` hides the preview strip (review chrome, do not build it). Not yet approved by Nabil.

**Proposals in this mock, not rules.** Drag, tick-to-add and start-from-a-base-project are PROPOSAL (Nabil's direction, no spec rule). There is no backend route for the base project: no copy-from-project route, no base-project flag, no template entity, no multi-line add.

## What it holds

For the Technical Office (and the Owner), mostly on a laptop: build the priced list of work until it can be signed, then sign it.

- Header: back link with the project name, title "المقايسة", one quiet badge (follow-up end date while unsigned, "مجمّدة في ..." once frozen, "مؤرشف" when archived), "طباعة عرض السعر" (outline, shown once there are lines, in any state), and the ONE primary button.
- Totals strip, sticky: the total with "ج.م" named once beside it, and the line count. Draft reads "الإجمالي", frozen reads "قيمة التعاقد". When any line has no quantity the total is "-" and a dashed tag says how many lines are missing one.
- The lines, grouped under the باب (name at the start, باب subtotal at the end). Columns on a wide screen: item, quantity (typed in place, with its unit), rate, total. Column heads name the columns once; "الإجمالي (ج.م)" carries the currency, so rows do not repeat it. Below 1100 px and on the phone a line is two rows: name and remove; then quantity, `x rate`, total.
- The catalogue pane at the inline-end (desktop only, 400 px, sticky, scrolls inside itself): "الكتالوج", a name search, the items under their باب (collapsible, the first three open). Each row: tick, grip, name over unit and rate, and "+" (44 px). Items already in the BOQ show a tick mark and are not draggable. No codes anywhere (D-212).
- Primary action, one at a time: "ابدأ من مشروع سابق" in the empty state; "توقيع المقايسة" once lines exist (disabled while a line has no quantity; the dashed tag next to the total is the reason); none after signing.

## The three ways to add a line

1. **Drag** a catalogue row into the lines (HTML5 drag, works in the mock). While dragging, the line list shows a dashed slot at the end of the row's own باب section, and a ghost section with the باب name when that باب has no lines yet. Dropping anywhere over the lines adds it to that section.
2. **"+"** on the row.
3. **Tick** several rows and press "أضف (n)" (outline button at the foot of the pane; a second filled button would break the one-primary rule).

Keyboard: every row's tick and "+" are tabbable; Space and Enter work. A new line takes focus in its quantity box, so typing the quantity follows the add directly. At 390 px the pane becomes a full-height sheet opened from "إضافة بنود": ticks only (no "+", no grip), search on top, footer button "أضف (n)" (filled, because the sheet covers the page primary).

## Start from a base project (PROPOSAL)

The empty state offers two buttons: "ابدأ من مشروع سابق" (primary) and "مقايسة فارغة". The catalogue pane stays open beside it, so a drop also starts the BOQ. "ابدأ من مشروع سابق" opens a side panel (bottom sheet at 390): name search, each result with name, line count and signing month, and "استخدام". Choosing one fills the lines, all editable and removable like any other line. "مقايسة فارغة" leaves an empty list that is a drop zone (a plus icon and, on desktop, one line "اسحب بنداً من الكتالوج"; on a phone the button "إضافة بنود").

The mock copies the lines and quantities of the chosen project and prices them at today's catalogue rates. That is a guess (see Not decided).

## States (each drawn)

- **empty**: the choice, pane beside it. **base**: the picker open. **blank**: empty list as a drop zone.
- **building**, **edit** (the first quantity focused), **pending** (a line added with no quantity: dashed start bar, dashed "بلا كمية" tag, total "-", subtotal "-", overall total "-", sign disabled).
- **sheet**: the 390 add sheet (on a wide screen this view looks like building).
- **dropfail**: the item was archived meanwhile; the pane row turns to the error colour with one line, "تعذّرت الإضافة: البند مؤرشف", and nothing is added. Dropping or adding "سيراميك أرضيات 60×60" in any view reproduces it.
- **partial**: four ticked, two landed, the third refused. The banner in the pane (or sheet footer) says "أُضيف 2 من 4"; the lines that did not land stay ticked. The batch stops at the first failure, which matches one add call per line.
- **sign**: the signature panel (bottom sheet at 390): total (read-only, "ستصبح قيمة التعاقد"), hold "نسبة المحجوز" (20 %), advance "نسبة الدفعة المقدمة" (25 %) with their defaults (spec section 15 example), the one freeze sentence, "وقّع وثبّت القيمة" (filled), "إلغاء". Confirming goes to the frozen state in the mock.
- **frozen**: lines read-only, no pane, no remove, no primary, the date as a badge, print stays.
- **archived**: as frozen but labelled "الإجمالي", a "مؤرشف" tag and one line with a link to the project.
- **design** (not for this type): the existing sentence "لا توجد مقايسة لهذا النوع من التعاقد." and a link back to the project.
- **loading** (skeleton strip, rows and pane), **failed** (refusal banner with retry, no pane).

## Reused from the kit, unchanged

App shell and bottom bar, back link, buttons (filled, outline `sec2`, small), search field, checkbox (`ck`), refusal banner, note banner, empty and refused states, skeleton, the side panel and bottom sheet (`sx`, from the crew screen), read-only money field, the باب section band, the dashed tag (`atag`, from the catalogue's archived marker), the saved-in-place banner style.

## New in the kit (put these in `components.html` before building)

- **BOQ line** (`ln`): name, quantity box (`qi`: typed value with the unit as an affix), rate, total, remove. Wide grid of five columns; compact two-row shape below 1100 px. Variants: pending (dashed 5 px start bar, dashed tag, total "-"), read-only (plain quantity and unit, no remove).
- **Totals strip with figure** (`tot2`): a money figure with the currency named once, the line count, and the optional dashed tag.
- **Catalogue pick row** (`ci`): tick, grip, name over unit and rate, "+". Variants: added (tick mark, not draggable), failed (error bar and one line), phone (`sci`, tick only, 60 px).
- **Drop slot and ghost section**: dashed slot at the end of the target باب while a drag is on; a ghost section carries a باب that has no lines yet.
- **Full-height sheet** (`sx.full`): the bottom sheet at 96 % height with a sticky search and a footer button.
- **Collapsible باب band in a pane** (`gbtn` outside a table): the catalogue screen's group row, without the table around it.
- **Neutral badge** (`bdg`): quiet bordered label for the follow-up date and the frozen date.

No new colour: every value is an existing token.

## Left off the screen, on purpose

Item codes (lines and pane). The words "سعر البيع" on rows. Cost price and margin. Both quantities on a row. Line positions and ids. The advance hint. The sentence under the quantity field. "المبالغ بالجنيه المصري (ج.م)". Any explanatory text about drag or ticks: the grip, the dashed slot and the "+" carry it.

Sentences kept: the freeze sentence in the signature panel (irreversible, spec section 4.4); one empty-state line "اسحب بنداً من الكتالوج" (desktop only); the state sentences (failed, partial, drop failed, archived, not for this type).

## Not decided here (the mock runs on these defaults; Nabil to rule)

1. **Q-N**: the quantity on a line is the surveyed one; one quantity is on screen. The estimated quantity is not drawn.
2. **Q-U**: the print title "عرض سعر" (existing key) is not drawn on this screen; print is a button only.
3. **Q-V**: no custom-item control. Not drawn.
4. **Conditions and line markup**: spec section 4.2 has both (the conditions rate on the request, `lineMarkup` per line) but no screen has controls for them. Not drawn.
5. **Follow-up period**: 30 days (10 December 2026 in the mock), the default of D-208.
6. **Base project copy rules (Q-A, Q-B)**: which projects may be a source (signed only? any? the same client?), whether quantities come across, whether rates are today's catalogue rates or the source project's frozen rates (the mock: lines and quantities, today's rates), whether hold and advance carry, and what happens to a source item archived since (not drawn).
7. **Adding an item twice**: the mock shows an added item as added and offers no second add. Whether the same catalogue item may appear twice in a BOQ is not ruled.
8. **Signing with a line that has no quantity**: the mock disables the primary, with the dashed tag as the reason. The spec does not say whether a BOQ with a missing quantity may be signed.
9. **Removing a line**: immediate, no confirmation (the current build asks). The item returns to the pane, so it is recoverable until signing.
10. **Archived BOQ**: read-only in the mock; reopening happens on the project page.
11. **Hold and advance defaults**: 20 % and 25 % are the spec section 15 example; whether the defaults are company settings is not asked.
12. **Quantity saves**: the mock saves as the number is typed. Saving on blur, with a failed-save state per line, is not drawn.

## What the backend lacks for this design

A base project: no source-project route, no flag, no template. A multi-line add (one line per call today, so a batch can stop halfway, which the partial state draws). The pane uses `ListCatalogueItems` (`search`, `babId`); drop and "+" use `AddBoqLine`. Reorder is not needed (lines sort by باب).

## What the mock does not draw

The printed quotation itself; the conditions and markup controls; a custom item; an estimated quantity; reopening an archived project; per-line save failure; the English/LTR text; two people editing at once.

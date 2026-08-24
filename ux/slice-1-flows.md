# Slice 1 flows — auth, roles, assignment, audit, Client master

**Slice 1 gate (`agents.md`): permission tests pass.**
**Twenty-one screens, S-001 to S-016a.** Every string below is an i18n key. Every error state is
named. **Nothing here is marked BLOCKED any more** — Karim's rulings closed every question that
stopped a slice-1 screen. What is left open is marked inline against the screen it shapes, and
`questions.md` says which of those actually stop anything. Where a screen names an open question, the
screen is drawn for the ruled shape and takes no position on the gap — designing past one means
inventing a business rule, and an invented rule is always plausible.

> **Revised 2026-08-21 against D-049, D-050 and D-051.** Karim's two rounds of rulings closed eleven
> of the fifteen questions this design was waiting on. Five screens are new — S-002 (no longer
> blocked), S-003a, S-008a, S-009a/S-009b, S-016a — and four are materially changed: S-004 (the
> session is a cookie the page cannot read), S-007 (the Owner sets a temporary password), S-012/S-013
> (the duplicate phone warns instead of refusing, and withholding has left the client record).

### The three rulings that changed the most screens

| Ruling | Where it lands |
|---|---|
| **D-050** — the token is in an `HttpOnly` cookie; `GET /api/auth/me` is the only way the UI learns anyone is signed in | S-004's **pre-resolution state**, which every screen inherits. There is no token to show, store or clear anywhere in this document. |
| **D-049 ruling 8** — a duplicate client phone **warns and the save proceeds** | S-013 stopped being a refusal. Any screen that still asserts one is wrong. |
| **D-051 Q32** — HR gets a **separate** Project Team surface with zero financial detail | S-009a and S-009b, on their own routes against their own API surface — not a filtered S-009. |

---

## Wireframe convention — read this before looking at a box

Wireframes are drawn **right-to-left**: the **inline-start edge is the RIGHT edge** of the box.

- Labels sit at the inline-start (right) of their field, or above it.
- The primary action sits at the inline-start (right) of an action row; the secondary follows toward
  the inline-end (left). On mobile both go full width and stack, primary on top.
- Back / close affordances sit at the inline-start (right) of a header.
- Captions inside the boxes are **Latin placeholders, not strings.** Real strings are named by their
  i18n key in the table under each wireframe.

**Arabic is deliberately kept out of the ASCII.** A box-drawing line containing mixed Arabic and Latin
is reordered by the bidi algorithm differently in different editors, so an "Arabic wireframe" would
render as a lie. The layout logic is the deliverable; the words are in the key tables.

Mobile is 390px. Desktop is shown only where it differs.

---

## S-001 · Login

```
      inline-end (LEFT)                    inline-start (RIGHT)
┌──────────────────────────────────────────────────────────────┐ 390
│ [AR] [EN]                                              Kaff  │  ← app.name at inline-start
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                                                Sign in       │  h1
│                                                              │
│                                                Username      │  label
│   ┌────────────────────────────────────────────────────┐     │
│   │ karim                                              │     │  dir=ltr
│   └────────────────────────────────────────────────────┘     │
│                                                              │
│                                                Password      │  label
│   ┌────────────────────────────────────────────────────┐     │
│   │ ••••••••                                      [eye]│     │  dir=ltr, reveal at field's LEFT
│   └────────────────────────────────────────────────────┘     │
│                                                              │
│   ┌────────────────────────────────────────────────────┐     │
│   │                    Sign in                         │     │  full width, min-block-size 2.75rem
│   └────────────────────────────────────────────────────┘     │
│                                                              │
│   ┌────────────────────────────────────────────────────┐     │
│   │ !  Error message, one line, role="alert"           │     │  error banner, appears above button
│   └────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────┘
```

Desktop: identical, centred, `max-inline-size: 24rem`. No marketing panel, no illustration.

| Element | Key |
|---|---|
| Page title | `auth.login.title` |
| Username label | `auth.field.username` |
| Password label | `auth.field.password` |
| Reveal password (a11y) | `a11y.show_password` / `a11y.hide_password` |
| Submit | `auth.action.sign_in` |
| Working | `auth.login.signing_in` |

### Error states

**Every failed sign-in renders the same message.** KAFF-101a rules 13 and 14: a wrong password, a
username that does not exist and an account locked by five failures all come back as one
`messageKey`, in one time envelope. **So there is no lockout message to design — the absence of one
is the design**, and it is what stops the screen from telling an attacker that a username is real and
that their lockout worked.

| Condition | Key | Behaviour |
|---|---|---|
| Wrong password · unknown username · **locked account** | `errors.auth.invalid_credentials` **(new — backend must emit)** | One message for all three. Password field cleared, username kept, focus to password. **The client shows no attempt counter and no "locked" state**, because it is not told one. |
| Account deactivated | `errors.auth.account_inactive` **(new)** | No retry. Tells the user to contact the office. |
| Password was set by the Owner and must be changed | `errors.auth.password_change_required` | Not an error the user sees on this screen — S-004 routes them to S-003 in forced mode. |
| Role has no login (Subcontractor) | `errors.auth.role_cannot_log_in` | Plain refusal. **No password-reset affordance** — there is no password to reset. |
| Network / server down | `errors.unknown` | Retry button, `action.retry`. |
| Empty field | `validation.required` | Client-side, on submit, focus the first invalid field. |

### The locked-out engineer, and the only honest thing the screen can say

The security rule creates a usability hole: a site engineer who really is locked out is told "wrong
username or password" for fifteen minutes and has no idea why. The screen closes it **without
learning anything from the server**:

```
│  ┌────────────────────────────────────────────────────┐     │
│  │ !  errors.auth.invalid_credentials                 │     │
│  └────────────────────────────────────────────────────┘     │
│      auth.login.trouble_signing_in                          │  ← after the 3rd failure in this
│                                                              │    browser tab, unconditionally
```

`auth.login.trouble_signing_in` states the **policy**, not this account's state: repeated wrong
passwords lock any account for fifteen minutes, and the office can send a new password link
(D-049 ruling 3 · D-051 Q38). It is shown to everyone who fails three times, whether the username
exists or not, so it leaks nothing. **The counter lives in the component and dies with the tab.** It
is presentation, it is never sent to the server, and no branch anywhere reads it.

### Rules

- **The client never decides whether credentials are valid.** No lockout counter derived from a
  response, no "attempts remaining", no per-username state.
- **Password policy is now settled and it is two facts: at least 8 characters, and nothing else**
  (D-049 ruling 3). **There is still no strength meter and no complexity hint** — Karim removed
  complexity deliberately *"so site workers don't struggle to log in"*, and a meter would put it back
  as a picture.
- **The sign-in screen itself validates only "not empty".** The 8-character minimum is a policy about
  *setting* a password and lives on S-002, S-003 and S-003a. Enforcing it here would refuse a
  correctly-typed password on the client before the server ever sees it, and tell the typist
  something about the stored credential. *(KAFF-101b rule 2 reads the other way. Recorded in
  `questions.md` as a wording conflict for the BA, not resolved here.)*
- **There is no token to store.** D-050: the session is an `HttpOnly` cookie the page cannot read.
  On a successful POST the screen stores **nothing** and hands control to S-004, which asks
  `GET /api/auth/me` who it is.
- **No "keep me signed in".** The session is 30 minutes of inactivity, sliding, and the number is the
  server's (`JwtOptions.InactivityMinutes`). A checkbox here would promise something the cookie does
  not do.
- **Clients never reach this screen.** The portal is a separate host (D-051 Q33), so a client typing
  Kaff's staff address arrives at a sign-in form that is not theirs. See S-004.

---

## S-002 · One-time Owner setup — **ANSWERED, D-051 (Q31), Shape B**

**The screen appears only while the users table is empty. It creates the Owner, and then it is gone
for the life of the system.** Karim chose this over a seeded account, and his reason is an audit
reason rather than a convenience one:

> *"I do not want hidden database scripts. My name and account creation date must appear naturally in
> the Audit Trail from day one."* — D-051, Q31

A seeded account has no actor, so the first row of the trail names nobody. This screen is what puts a
human at the top of it.

**It is also the most privileged endpoint the system will ever have** — anonymous, and it mints an
Owner. Everything below exists to make that survivable.

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [AR] [EN]                                              Kaff  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                                          Set up Kaff         │  h1 · setup.title
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ i  This happens once. This account becomes the Owner   │  │  setup.hint.one_time
│  │    and this screen will not appear again.              │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│                                          Full name *         │  dir=auto
│  ┌────────────────────────────────────────────────────────┐  │
│  └────────────────────────────────────────────────────────┘  │
│                                          Username *          │  dir=ltr, spellcheck=false
│  ┌────────────────────────────────────────────────────────┐  │
│  └────────────────────────────────────────────────────────┘  │
│                                          Phone *             │  dir=ltr, inputmode=tel
│  ┌────────────────────────────────────────────────────────┐  │
│  └────────────────────────────────────────────────────────┘  │
│                                          Role                │
│   Owner                                                      │  STATIC TEXT, not a field
│           This account is the Owner. It cannot be another    │  setup.hint.role_is_owner
│           role, and no second one can be created here.       │
│                                                              │
│                                          Password *          │  dir=ltr
│  ┌────────────────────────────────────────────────────┐      │
│  │ ••••••••                                      [eye]│      │
│  └────────────────────────────────────────────────────┘      │
│           At least 8 characters.                             │  auth.password.rule_min_length
│                                          Confirm password *  │
│  ┌────────────────────────────────────────────────────┐      │
│  └────────────────────────────────────────────────────┘      │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ i  Your name and the time you created this account     │  │  setup.hint.audit_notice
│  │    are recorded in the audit trail.                    │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                    Create the Owner                    │  │  setup.action.create
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

Desktop: identical, centred, `max-inline-size: 28rem`. No side navigation, no drawer, no account
menu — **there is no session and no shell here.**

### How the screen knows to appear, and how it disappears

The page cannot look in the database and, after D-050, cannot look at a cookie either. It needs one
boolean from the server:

```
GET /api/setup    ->  { "available": true }    while the users table is empty
                  ->  { "available": false }   for ever afterwards
```

- `/` -> if `available`, replace the route with `/setup`; otherwise S-001.
- `/setup` when `available` is `false` -> **S-016 not found.** Not a redirect to sign-in: not-found is
  the state that says least, and a bookmarked `/setup` is exactly the request that should learn
  nothing.
- **`available` is the emptiness test, never a flag.** D-051: *"'locks permanently' must mean the
  emptiness test, not a flag anyone can clear."* If the UI ever gains a way to re-enable this screen,
  that is the defect.
- Publishing `available` to an anonymous caller costs nothing: before setup it tells a stranger the
  system is empty, which the screen itself does anyway; after setup it is a constant `false`.

### Error states

| Condition | Key | Behaviour |
|---|---|---|
| **Somebody else completed setup first** (the concurrent-request case D-051 names) | `errors.setup.already_completed` **(new — backend must emit)** | Not an inline field error. Replace the whole screen with a terminal panel — `setup.taken.title` / `setup.taken.body` and one action, `auth.action.sign_in`. **No retry.** The form must not stay on screen offering a second attempt at something that can never succeed again. |
| Password shorter than 8 | `errors.auth.password_too_short` | At the field. |
| Confirm does not match | `validation.passwords_do_not_match` | Client-side, at the confirm field. |
| Username already taken | `errors.identity.username_taken` | Cannot happen against an empty table; render it anyway rather than assume. |
| Missing name / username | `errors.identity.full_name_required` · `errors.identity.username_required` | At the field. |
| Phone problems | `errors.phone.required` · `.too_long` · `.too_short` | At the field. |
| Network / server down | `errors.unknown` + `action.retry` | The POST is safe to retry: a second one landing after the first succeeded is refused with `errors.setup.already_completed`, which is the terminal panel above — never a second Owner. |

### What happens after submit — and the one thing this screen deliberately does not decide

On success the screen **does not** route anywhere itself. It signs in with the credentials just
entered and hands control to **S-004**, which asks `GET /api/auth/me`.

That matters because of an open point. KAFF-100 rule 4 says the first Owner *"MUST change it before
the account can do anything else"* — a rule written for a credential somebody **else** chose. Here the
Owner chose it, so nobody else has ever known it and the non-repudiation argument behind the rule is
already satisfied. **The screen takes no position.** If the server answers
`errors.auth.password_change_required`, S-004 routes to S-003 in forced mode and the Owner changes a
password he set ninety seconds ago; if it does not, he lands on the user list. Either is correct from
the UI's side, and **which one is right is `questions.md` Q-UX-17, not a design choice.**

### Rules

- **No "skip" and no "do this later".** There is nothing to skip to.
- **The username must not be `admin`, `root` or `kaff`** (KAFF-100 AC4). The server refuses it; the
  screen renders the refusal. It does not carry its own list.
- Full name is `dir="auto"` — Karim types Arabic. Username and phone are `dir="ltr"`.
- **No email field.** `spec.md` requires none, D-049 ruling 4 records that engineers have no company
  email, and the recovery path is the phone (D-051 Q38). An email field here would be the first
  invented requirement in the system.
- **No department field.** The Owner is not one of §9's four departments (KAFF-100 rule 2).

| Element | Key |
|---|---|
| Title | `setup.title` |
| Hints | `setup.hint.one_time` · `setup.hint.role_is_owner` · `setup.hint.audit_notice` |
| Fields | `setup.field.full_name` · `.username` · `.phone` · `.password` · `.confirm_password` |
| Password rule | `auth.password.rule_min_length` |
| Submit / working | `setup.action.create` · `setup.creating` |
| Already done | `setup.taken.title` · `setup.taken.body` · `auth.action.sign_in` |
## S-003 · Change password — one screen, two modes

**Mode `forced`** — the user signed in with a password the Owner set, and can reach nothing else
until it is replaced (D-049 ruling 4 · KAFF-103 rule 2).
**Mode `voluntary`** — reached from S-005 by a user who simply wants a new password.

The two modes differ in three things and nothing else: the explanation line, whether the shell chrome
is present, and where the user goes afterwards. **Both ask for the current password.**

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [AR] [EN]                                              Kaff  │  forced: no drawer, no nav,
├──────────────────────────────────────────────────────────────┤  only a sign-out affordance
│                                          Change your password│  h1 · auth.password.title
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ i  Your password was set for you. Choose your own       │ │  auth.password.must_change
│  │    before you continue.                                 │ │  ← FORCED MODE ONLY
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│                                          Current password *  │  BOTH modes — KAFF-103 rule 5
│  ┌────────────────────────────────────────────────────┐      │
│  │ ••••••••                                      [eye]│      │
│  └────────────────────────────────────────────────────┘      │
│                                          New password *      │
│  ┌────────────────────────────────────────────────────┐      │
│  └────────────────────────────────────────────────────┘      │
│           At least 8 characters.                             │  auth.password.rule_min_length
│                                          Confirm password *  │
│  ┌────────────────────────────────────────────────────┐      │
│  └────────────────────────────────────────────────────┘      │
│                                                              │
│           Changing your password signs you out on every      │  auth.password.hint.ends_other_
│           other device.                                      │  sessions
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                        Save                            │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

| Element | Key |
|---|---|
| Title | `auth.password.title` |
| Forced-mode explanation | `auth.password.must_change` |
| Fields | `auth.field.current_password` · `auth.field.new_password` · `auth.field.confirm_password` |
| Length rule | `auth.password.rule_min_length` — *"At least 8 characters."* |
| Consequence | `auth.password.hint.ends_other_sessions` |
| Save / working | `action.save` · `auth.password.saving` |
| Success | `auth.password.changed` |

### The current-password field is not optional in forced mode

The previous version of this screen omitted it on first sign-in. **KAFF-103 rule 5 says otherwise, and
gives the reason:** without it, an unattended signed-in phone is a password reset. In forced mode the
session is by definition one nobody has yet proved they own — it was opened with a credential the
Owner also knows — so it is the mode that needs the check most.

### What forced mode must not offer

- **No skip, no "later", no close button, no drawer, no navigation.** KAFF-103 AC2: every endpoint
  except the change-password one is refused with `errors.auth.password_change_required`. A menu that
  renders links to screens that will all refuse is a menu that lies.
- **Sign out is the one other thing the screen offers** (`auth.logout`), because a person who cannot
  or will not continue must be able to leave rather than close the tab on a live session.
- **The route cannot be escaped by typing a URL.** Any other route re-runs S-004, which asks
  `/api/auth/me`, is refused with `errors.auth.password_change_required`, and lands back here. The
  loop is the enforcement, and the enforcement is the server's.

### Error states

| Condition | Key |
|---|---|
| New password shorter than 8 | `errors.auth.password_too_short` — client-side **and** server-side; the client check exists to save a round trip, not to be the rule |
| Confirm does not match | `validation.passwords_do_not_match` (client-side only — the server never sees the confirm field) |
| Current password wrong | `errors.auth.current_password_incorrect` |
| Session expired mid-form | S-016a. **The typed passwords are discarded, never retained** — see S-016a's exception. |
| Server-side refusal of any other kind | whatever key the backend emits — **the client renders it, it does not predict it** |

**No strength meter, no complexity hints, no rule list beyond the one line.** Karim removed complexity
deliberately. A meter is a policy statement wearing a progress bar, and here it would be a policy that
does not exist.

### After a successful change

The password change rotates the security stamp, which ends **every** session for that user
(D-049 ruling 2 · KAFF-103 AC6) — possibly including this one, depending on how KAFF-101a re-issues
the cookie. **The screen does not assume.** It shows `auth.password.changed`, then re-runs S-004:

- if `/api/auth/me` answers, continue to the role's landing;
- if it is refused, render **S-016a** with `auth.password.changed_sign_in_again` and send the user to
  S-001.

Both are correct outcomes, and guessing which one happens would put a wrong sentence on the screen.

---

## S-003a · Set a new password from a reset link — **NEW, D-051 (Q38)**

**The recipient's half of password recovery.** The Owner generates a temporary reset link (S-008a); it
arrives by SMS or WhatsApp on the phone registered to the account, because *"site engineers often have
no company email"* (D-049 ruling 4 · D-051 Q38). This screen is what the link opens.

**It is opened on a phone, from a message, by somebody who is locked out and probably standing on
site.** Design at 390px and assume nothing else is available — no session, no shell, no navigation.

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [AR] [EN]                                              Kaff  │
├──────────────────────────────────────────────────────────────┤
│                                          Choose a new        │  h1 · auth.reset.title
│                                          password            │
│                                                              │
│                                          Ahmed Nabil         │  the account the link belongs to,
│                                                              │  dir=auto — NOT the username
│                                          New password *      │
│  ┌────────────────────────────────────────────────────┐      │
│  │ ••••••••                                      [eye]│      │
│  └────────────────────────────────────────────────────┘      │
│           At least 8 characters.                             │  auth.password.rule_min_length
│                                          Confirm password *  │
│  ┌────────────────────────────────────────────────────┐      │
│  └────────────────────────────────────────────────────┘      │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                    Save and sign in                    │  │  auth.reset.action.save
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### Rules

- **No current-password field.** The whole premise is that they do not have one. The link is the
  proof, and the link is the server's to validate.
- **No username field.** The link identifies the account. Asking for a username would let somebody
  holding one link try it against a second account.
- **The display name is shown; the username is not.** Showing the name confirms to the recipient that
  the link is theirs. Showing the username would put a live credential half into a message thread.
  *(If the server declines to return even the name, the screen simply omits the line — it must not
  substitute the username.)*
- **The screen shows no expiry countdown.** The lifetime of a link has not been decided
  (`questions.md` Q-UX-19), and a countdown rendered from a guessed lifetime would be a policy the UI
  invented. When the link has expired the server says so and the screen renders it.
- **On success the user is sent to S-001 to sign in with the new password**, not signed in
  automatically. Typing it once more is the cheapest proof that what they set is what they remember,
  and it costs one screen.
- **The user is not then forced through S-003.** They chose this password themselves, so the
  non-repudiation reason for the forced change (D-049 ruling 4) is already satisfied — and KAFF-104
  rule 3 requires a forced change only when the credential *"is set by anyone other than the user"*.
  **If the server disagrees and answers `errors.auth.password_change_required`, S-004 routes to S-003
  and the screen has no opinion.**

### The dead ends, and why they all read the same

| Condition | Key | Behaviour |
|---|---|---|
| Link expired · link already used · link unknown or tampered with | `errors.auth.reset_link_invalid` **(new — backend must emit)** | **One key for all four.** Distinguishing "already used" from "expired" tells whoever is holding the link that it was once real — the same reasoning as KAFF-101a rule 13. |
| The account was deactivated after the link was sent | the same key | KAFF-104 rule 5: recovery is not a way back in for somebody who has left. The link must not explain that the account is gone. |
| Password shorter than 8 | `errors.auth.password_too_short` | At the field. |
| Confirm does not match | `validation.passwords_do_not_match` | Client-side. |

The invalid-link state replaces the whole form:

```
┌──────────────────────────────────────────────────────────────┐ 390
│                                          This link no longer │  auth.reset.invalid.title
│                                          works               │
│                                                              │
│   Ask the office to send you a new one.                      │  auth.reset.invalid.body
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                      Go to sign in                     │  │  auth.action.sign_in
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

**There is no "send me another link" button**, and there must not be. Recovery is Owner-generated
(D-051 Q38); there is no self-service request path, so a button offering one would be a business rule
the UI invented. The body text points at the office, which is the actual path.

**A locked account is not unlocked by this screen.** KAFF-104 AC5: recovery does not shortcut the
fifteen minutes. If the user sets a new password and is then refused at S-001, they see
`errors.auth.invalid_credentials` like everybody else, and `auth.login.trouble_signing_in` after the
third try. **The screen does not warn about this in advance** — it would have to know the account is
locked to say so, and knowing that is exactly what the lockout design refuses to tell a page.

| Element | Key |
|---|---|
| Title | `auth.reset.title` |
| Fields | `auth.field.new_password` · `auth.field.confirm_password` |
| Length rule | `auth.password.rule_min_length` |
| Submit / working | `auth.reset.action.save` · `auth.reset.saving` |
| Success | `auth.reset.done.title` · `auth.reset.done.body` · `auth.action.sign_in` |
| Dead end | `auth.reset.invalid.title` · `auth.reset.invalid.body` |

---

## S-004 · Session resolution and landing dispatcher

Not a screen the user perceives — but after D-050 it is the screen that decides whether anything else
renders at all, and **it needs a state this design did not previously have.**

### The pre-resolution state, which every screen inherits

> The access token is in an `HttpOnly` cookie the page cannot read. `GET /api/auth/me` is the **only**
> way the UI learns anyone is signed in. — D-050

So on every load and every reload the application starts out **not knowing**. There are exactly three
session states and they are not two:

| State | What renders |
|---|---|
| **`resolving`** | A neutral boot surface: the app name, the locale switch, and a progress indicator (`app.loading`). **Not the sign-in form. Not the staff chrome. Not an empty shell.** |
| **`signed-in`** | The role's landing, per the table in `navigation.md`. |
| **`signed-out`** | S-001. |

**The failure this exists to prevent is a flash of the sign-in screen for somebody who is already
signed in.** With a token in `localStorage` the page knew instantly; with a cookie it cannot, and a
component that treats "no session object yet" as "signed out" will render S-001 for a fraction of a
second on every single reload. That is the whole practical cost of D-050 and it is paid here, once.

**Route guards await resolution — they never resolve against `null`.** A guard that runs before
`/api/auth/me` answers will redirect a signed-in user to the sign-in screen and lose the URL they
typed.

**There is nothing to clear on sign-out.** No token, no `localStorage`, no `sessionStorage` key. The
server clears the cookie (KAFF-102); the shell drops its in-memory profile and returns to `resolving`.

### The dispatch itself

```
GET /api/auth/me
  ├─ 200, role = Client ────────────→ see "A client on the staff host" below
  ├─ 200 ───────────────────────────→ staff shell, role landing (navigation.md)
  ├─ 403 password_change_required ──→ S-003, forced mode
  ├─ 401 ───────────────────────────→ signed out: S-001 (or S-002 if GET /api/setup says available)
  └─ 5xx / network ─────────────────→ S-016 failed, action.retry
```

- **`errors.auth.password_change_required` arrives as a refusal, not as a field.** KAFF-105 AC5 says
  this endpoint is *refused* with that key while a temporary password stands; KAFF-105 rule 3 says the
  response *carries* a flag saying the same thing. **Both cannot be the shape.** The dispatcher is
  written against the refusal, which is the stricter of the two and the one an AC asserts. Recorded as
  `questions.md` Q-UX-18 for the BA — it is a payload-shape question, not a business rule.
- **If `/api/auth/me` fails, render S-016 and stop.** Never fall back to a default menu: a navigation
  built from a guess is a navigation built from no data.
- The endpoint is `GET /api/auth/me` (KAFF-105a), not `/api/me`. KAFF-105b — the project list — is
  still blocked, so **slice 1's dispatcher must work with identity and roles alone.**

### A client on the staff host

The portal is **a separate URL and host** (D-051 Q33): *"their portal must be a completely isolated
interface."* Clients never see the staff sign-in screen, and there is no route in the staff
application that a client is meant to reach.

The dispatcher still has to handle it, because KAFF-101a rule 16 accepts a `Role.Client` credential at
the sign-in endpoint and nothing has been ruled about refusing it there
(`questions.md` **Q-UX-20**, open). If `/api/auth/me` ever answers with `role = Client` on the staff
host, the shell renders **S-016 in forbidden mode** and mounts no staff chrome.

- **Not a redirect to the portal host carrying a live session.** The cookie is `__Host-` prefixed with
  no `Domain` (D-050), so it does not travel to another host anyway — a redirect would land the client
  on the portal signed out and looking like a bug.
- **Not "one frame" of the staff shell.** Branch before the shell renders, not inside it. A client who
  sees the staff chrome has seen the shape of the internal application.
- The staff application therefore contains **no portal route, no portal landing and no portal
  placeholder.** The "portal not available until slice 8" state belongs to the portal host, which is
  the portal team's to build.

---

## S-005 · My profile

The honest landing for every role that has nothing else in slice 1.

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [≡]                                     [AR][EN]      Kaff   │  drawer toggle at inline-start
├──────────────────────────────────────────────────────────────┤
│                                                My profile    │  h1
│                                                              │
│                                       Full name              │
│                                       Ahmed Nabil            │  dir=auto
│                                       Username               │
│                                       ahmed                  │  <bdi> dir=ltr
│                                       Phone                  │
│                                       0101 234 5678          │  <bdi>, PhoneEntered as typed
│                                       Role                   │
│                                       [Site Engineer]        │  enum.Role.SiteEngineer
│                                       Department             │
│                                       [Operations / Admin]   │  enum.Department.*
│                                                              │
│                                       My projects            │  h2
│  ┌────────────────────────────────────────────────────────┐  │
│  │                       Project name          Supervisor │  │  level from the ASSIGNMENT
│  │                       Project name          Junior     │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│   ┌───────────────────────┐                                  │
│   │   Change password     │                                  │  → S-003
│   └───────────────────────┘                                  │
└──────────────────────────────────────────────────────────────┘
```

| Element | Key |
|---|---|
| Title | `profile.title` |
| Fields | `profile.field.full_name` · `.username` · `.phone` · `.role` · `.department` |
| Projects section | `profile.projects.title` |
| Level | `enum.AssignmentLevel.Junior` · `.Supervisor` · `.Standard` |
| Empty projects | `profile.projects.empty` — "You are not assigned to any project." |
| Change password | `auth.action.change_password` |

**The level shown is per project, always.** Rendering one seniority for the person contradicts
D-044 §5 and would make the screen say something false the first time an engineer is Junior on one
site and Supervisor on another.

**Nothing on this screen is editable except the password.** Name, phone, role and department are the
Owner's to change (S-008).

---

## S-006 · User list — Owner only

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [≡]                                     [AR][EN]      Kaff   │
├──────────────────────────────────────────────────────────────┤
│                                                    Users     │  h1
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [search]                          Search name or phone │  │  dir=auto, inputmode=search
│  └────────────────────────────────────────────────────────┘  │
│  [ All ] [ Active ] [ Inactive ]                             │  filter chips, wrap at 390
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                        Ahmed Nabil  ›  │  │  card, whole card is the link
│  │                          Site Engineer · Operations    │  │  chevron flips in RTL
│  │                                        0101 234 5678   │  │  <bdi>
│  ├────────────────────────────────────────────────────────┤  │
│  │                                        Mona Adel    ›  │  │
│  │                          Finance · Finance             │  │
│  │                          [ Inactive ]                  │  │  neutral chip, not an error colour
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│   ┌────────────────────────────────────────────────────┐     │
│   │                   + New user                       │     │  primary; on desktop it moves to
│   └────────────────────────────────────────────────────┘     │  the header at inline-start
└──────────────────────────────────────────────────────────────┘
```

Desktop: the cards become a table — column order right to left is
**Name · Username · Role · Department · Phone · State · (actions at the inline-end)**.

| Element | Key |
|---|---|
| Title | `users.title` |
| Search | `users.search.placeholder`, `a11y.search` |
| Filters | `users.filter.all` · `.active` · `.inactive` |
| New | `users.action.create` |
| Inactive chip | `users.state.inactive` |
| Empty (no users match) | `users.empty.filtered.title` / `.body` + `action.clear_filters` |
| Empty (no users at all) | `users.empty.title` / `.body` — should be unreachable once S-002 is settled |

### Error states

| Condition | Key |
|---|---|
| Not the Owner (URL typed directly) | `errors.auth.forbidden` → S-016. **Expected. The route may be reached; the API refuses it.** |
| Load failed | `errors.unknown` + `action.retry` |

---

## S-007 · Create user — the most privileged screen in the system

> Once this endpoint exists it becomes the most privileged operation in the system: because grants may
> be written against a department, **whoever can set a user's department can grant project-assignment
> power.** — slice-1 kickoff §2.1

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [×]                                              New user    │  close at inline-start (right)
├──────────────────────────────────────────────────────────────┤
│                                          Full name *         │
│  ┌────────────────────────────────────────────────────────┐  │  dir=auto
│  └────────────────────────────────────────────────────────┘  │
│                                          Username *          │
│  ┌────────────────────────────────────────────────────────┐  │  dir=ltr, spellcheck=false
│  └────────────────────────────────────────────────────────┘  │
│                                          Phone *             │
│  ┌────────────────────────────────────────────────────────┐  │  dir=ltr, inputmode=tel
│  └────────────────────────────────────────────────────────┘  │
│                                          Email              │
│  ┌────────────────────────────────────────────────────────┐  │  dir=ltr, inputmode=email
│  └────────────────────────────────────────────────────────┘  │
│                                          Role *              │
│  ┌────────────────────────────────────────────────────────┐  │  select, 9 options
│  │ Site Engineer                                       ▾  │  │  chevron is vertical: no flip
│  └────────────────────────────────────────────────────────┘  │
│                                          Department          │
│  ┌────────────────────────────────────────────────────────┐  │  conditional — see rules
│  └────────────────────────────────────────────────────────┘  │
│                                          Sub-department *    │
│  ┌────────────────────────────────────────────────────────┐  │  only when Department = Operations
│  └────────────────────────────────────────────────────────┘  │
│                                          Client *            │
│  ┌────────────────────────────────────────────────────────┐  │  only when Role = Client
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ── not shown when Role = Subcontractor ──────────────────    │
│                                          Temporary password *│  dir=ltr
│  ┌────────────────────────────────────────────────────┐      │
│  │ ••••••••                                      [eye]│      │
│  └────────────────────────────────────────────────────┘      │
│           At least 8 characters. Give it to them             │  users.hint.temporary_password
│           yourself — they must change it when they first     │
│           sign in, and after that you will not know it.      │
│                                          Confirm *           │
│  ┌────────────────────────────────────────────────────┐      │
│  └────────────────────────────────────────────────────┘      │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ i  What this role can do                             │    │  a plain-language summary of the
│  │    · …                                               │    │  grants, from the catalogue data
│  └──────────────────────────────────────────────────────┘    │  catalogue data — NOT hardcoded
│                                                              │
│  ┌───────────────────────┐  ┌───────────────────────┐        │
│  │       Create          │  │       Cancel          │        │  primary at inline-start (right)
│  └───────────────────────┘  └───────────────────────┘        │
└──────────────────────────────────────────────────────────────┘
```

### Conditional field rules — every one of these is enforced in `User.Create`

| Role selected | Department | Sub-department | Client |
|---|---|---|---|
| Owner, Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales | optional | required **iff** Department = Operations | must be absent |
| **Hr** | **forced to HR and not editable** | absent | absent |
| **Client** | **absent — the field is not rendered** | absent | **required** |
| **Subcontractor** | absent | absent | absent — **and no password can ever be set** |

- HR is pinned to `Department.Hr` because an HR user placed in Operations/Administrative would inherit
  `SiteExpenseConfirm` through a department-only grant — the same piggyback D-044 §2 exists to
  prevent, arriving from the other direction. **Render it as a fixed, disabled value with a
  hint (`users.hint.hr_department_fixed`), not as a select with one option.**
- Client and Subcontractor cannot hold a department at all
  (`errors.identity.external_role_cannot_hold_department`) — this is one of the two paths that nearly
  leaked the portal (`decisions.md` D-035).
- Choosing `Subcontractor` must show `users.hint.subcontractor_no_login` before submission, because
  the record will be created and will never be able to sign in.

### The Owner sets the password, deliberately — D-049 ruling 4

The previous version of this screen created an account with **no** password and left the user to set
their own through an invitation. **Karim ruled the other way**, and the reason is what makes the
window safe:

> Onboarding is a temporary password set by the Owner, which the user MUST change on first sign-in.
> Forcing the change is what keeps the audit trail meaningful: **after it, the Owner does not know the
> credential that acts as that user.** — D-049 ruling 4 · KAFF-103

So the Owner knowingly holds a working credential for a short window, and S-003's forced mode is what
closes it. **The screen must say this out loud** (`users.hint.temporary_password`), because an Owner
who does not understand that the password is temporary will pick one he intends to keep working.

- The field obeys the same rule as any other password: **at least 8 characters, no complexity**
  (KAFF-106 rule 9). No meter, no hints, no generator.
- **Not rendered at all for `Role.Subcontractor`** — that role can never hold a password
  (`User.SetPasswordHash` refuses it), and rendering a disabled field invites somebody to ask why.
- **The password never appears again anywhere**: not in the success message, not on S-008, not in the
  audit record (`[AuditRedacted]`), not in a toast. If the Owner forgets what he typed, the path is
  S-008a, not a screen that shows it back.
- **The list is not a delivery channel.** How the Owner gets the password to the person is outside the
  system — he tells them. The system's reset link (S-008a) is the only thing it sends.

### The "what this role can do" panel

Render it from catalogue data the API returns, keyed as `permission.<Name>.summary`. **Do not hardcode
a description list in the component** — a second copy of the permission catalogue in TypeScript is the
exact drift D-012 designed the catalogue as data to prevent. If the API cannot supply it yet, show
nothing rather than a plausible list.

| Element | Key |
|---|---|
| Title | `users.create.title` |
| Fields | `users.field.full_name` · `.username` · `.phone` · `.email` · `.role` · `.department` · `.sub_department` · `.client` · `.temporary_password` · `.confirm_password` |
| Hints | `users.hint.hr_department_fixed` · `users.hint.subcontractor_no_login` · `users.hint.temporary_password` |
| Actions | `action.create` · `action.cancel` |
| Success | `users.created` (with `{name}` — isolated by `t()`) |

### Error states

| Condition | Key |
|---|---|
| Missing username / full name | `errors.identity.username_required` · `errors.identity.full_name_required` |
| Phone empty / too long / too short | `errors.phone.required` · `.too_long` · `.too_short` |
| Operations without sub-department | `errors.identity.operations_requires_sub_department` |
| Sub-department on a non-Operations user | `errors.identity.sub_department_only_for_operations` |
| Client role without a client | `errors.identity.client_user_requires_client` |
| Client set on a non-client | `errors.identity.non_client_user_cannot_carry_client` |
| Department on Client / Subcontractor | `errors.identity.external_role_cannot_hold_department` |
| Username already taken | `errors.identity.username_taken` **(new — backend must emit)** |
| Temporary password shorter than 8 | `errors.auth.password_too_short` |
| Confirm does not match | `validation.passwords_do_not_match` (client-side) |
| Phone already belongs to a user | **still unknown — `questions.md` Q-UX-7.** Karim's ruling 8 softened duplicate phones for **clients**; nobody has ruled on users. Until then the screen renders whatever the server returns and offers no "proceed anyway" affordance — S-013's warning must not be copied here on the assumption that the same answer applies. |
| Not the Owner | `errors.auth.forbidden` |

**Field-level errors are rendered at the field**, via `aria-describedby`, and the first invalid field
takes focus. A form-level error banner is for refusals that belong to no single field.

---

## S-008 · User detail and edit

Same layout as S-007 in read mode, with an edit affordance per section and a **danger zone** at the
end of the page (inline-end of the block axis, i.e. the bottom).

```
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                        Danger zone     │  │  users.danger.title
│  │   ┌──────────────────────┐                             │  │
│  │   │   Deactivate user    │                             │  │  destructive styling
│  │   └──────────────────────┘                             │  │
│  │   What deactivation does: …                            │  │  users.danger.deactivate_explains
│  └────────────────────────────────────────────────────────┘  │
```

| Element | Key |
|---|---|
| Deactivate / reactivate | `users.action.deactivate` · `users.action.reactivate` |
| Confirm dialog | `users.confirm.deactivate.title` · `.body` · `action.confirm` · `action.cancel` |
| Reset password | `users.action.reset_password` |

### Error states

| Condition | Key |
|---|---|
| Already active / inactive | `errors.identity.user_already_active` · `errors.identity.user_already_inactive` |
| Department move invalid | as S-007 |

### Both of the things this screen could not say are now answered

**1. The role is mutable, and changing it revokes every project assignment.** D-051 Q27 — which
*reverses* D-049 ruling 6, so read it carefully and do not "correct" it back:

> Moving a Site Engineer to the Technical Office **automatically revokes every project assignment they
> hold — Supervisor and Junior alike.** If they are still needed on the project, HR re-assigns them in
> the new role. — D-051, Q27

So the role becomes an editable select, and **the confirmation is the screen's main job**. It must
name the consequence in counted, concrete terms, not in the abstract:

```
┌────────────────────────────────────────────────────┐
│                     Change this person's role?     │  users.confirm.change_role.title
│                                                    │
│   Ahmed Nabil will move from Site Engineer to      │  users.confirm.change_role.body
│   Technical Office.                                │  {name} {from} {to} — bidi-isolated
│                                                    │
│   They will be removed from 3 projects.            │  users.confirm.change_role.revokes
│                                                    │  {count} — plural rules apply
│   ┌────────────────────────────────────────────┐   │
│   │              Project name                  │   │  the projects by name, so the
│   │              Project name                  │   │  Owner can hand them over first
│   │              Project name                  │   │
│   └────────────────────────────────────────────┘   │
│                                                    │
│   HR can put them back on a project in the new     │  users.confirm.change_role.reassign
│   role afterwards.                                 │
│                                                    │
│  ┌───────────────────┐  ┌───────────────────┐      │
│  │   Change role     │  │      Cancel       │      │
│  └───────────────────┘  └───────────────────┘      │
└────────────────────────────────────────────────────┘
```

- **The count and the names come from the server, in the same response that describes the user.** Do
  not compute them in the client from an assignment list and do not guess the number.
- **The dialog is destructive-styled and Cancel is focused first.** Karim's earlier ruling worried
  about *"leaving a construction site headless"*; the reversal accepted that risk in exchange for no
  lingering responsibilities, and the dialog is where the Owner is told which sites he is about to
  empty.
- If the user holds **no** assignments, the project list and the revocation line are omitted rather
  than rendered as "0 projects".

**2. Deactivating a user revokes their active assignments** (D-049 ruling 5 · KAFF-111), and a
returning employee comes back with **zero** (KAFF-112). The confirmation now says both:

| Element | Key |
|---|---|
| Deactivate confirm | `users.confirm.deactivate.title` · `.body` — *"They will not be able to sign in, and they will be removed from their projects."* |
| Reactivate confirm | `users.confirm.reactivate.title` · `.body` — *"They will be able to sign in again. They will not be put back on any project."* |
| Role change confirm | `users.confirm.change_role.title` · `.body` · `.revokes` · `.reassign` |

**The history stays.** A revoked assignment row is kept, not deleted (`ProjectAssignment.Revoke`), so
S-015 can still answer who was on a project last March. The screen says nothing about that — it is the
audit trail's sentence, not a reassurance to put in a dialog.

---

## S-008a · Send a password reset link — **NEW, D-051 (Q38)**

**The Owner's half of password recovery.** A user who has forgotten their password tells the office;
the Owner opens their record and sends them a link.

> The employee tells the office; the Owner generates a **temporary reset link**; it goes to their
> registered phone [by SMS or WhatsApp]. — D-051, Q38

**The Owner cannot simply type a new password for them**, and the reason is the one that runs through
every auth decision in this system: *"that would compromise the non-repudiation of the Audit Trail"*
— if the Owner sets a password the user keeps, every action that account takes has two possible
authors.

Reached from S-008's `users.action.reset_password`. It is a dialog over the user's record, not a page,
because it names the person it is about and losing that context is how the wrong link gets sent.

```
┌────────────────────────────────────────────────────┐
│                        Send a reset link?          │  users.reset.title
│                                                    │
│   Ahmed Nabil                                      │  dir=auto
│   0101 234 5678                                    │  <bdi>, PhoneEntered as typed
│                                                    │
│   A link will be sent to this number. They choose  │  users.reset.body
│   their own new password — you will not see it.    │
│                                                    │
│   Their other devices stay signed in until they    │  users.reset.hint.sessions_end_on_use
│   set the new password.                            │
│                                                    │
│  ┌───────────────────┐  ┌───────────────────┐      │
│  │    Send link      │  │      Cancel       │      │
│  └───────────────────┘  └───────────────────┘      │
└────────────────────────────────────────────────────┘
```

After sending, the dialog becomes a confirmation rather than closing silently — the Owner is about to
tell somebody on the phone that it is on its way:

```
┌────────────────────────────────────────────────────┐
│                        Link sent                   │  users.reset.sent.title
│                                                    │
│   Sent to 0101 234 5678.                           │  users.reset.sent.body {channel} {phone}
│                                                    │
│                             ┌───────────────────┐  │
│                             │       Close       │  │
│                             └───────────────────┘  │
└────────────────────────────────────────────────────┘
```

### What this screen must never do

- **It never displays the link, and it has no "copy link" button.** D-051 puts the link on the user's
  registered phone. A link the Owner can read is a credential the Owner holds — which is precisely
  what the ruling was written to avoid, arriving through the back door.
- **It never displays a password**, temporary or otherwise. Reset and "set a temporary password" are
  different mechanisms with different audit consequences; only the first belongs on this screen.
- **It never says whether the account is locked.** See below.
- **It offers no channel picker.** Whether the message goes by SMS or WhatsApp is the server's; the
  confirmation names the channel the server reports back. A picker would make the UI decide something
  nobody has asked Karim about.

### Two conditions the screen must state, because both are ruled and both are counter-intuitive

**A reset does not unlock a locked account.** KAFF-104 AC5: an account locked by five failed attempts
stays locked for the remaining minutes even if the password is reset in the meantime. The Owner will
try exactly this — somebody is locked out, so he resets them — so the dialog carries
`users.reset.hint.does_not_unlock` **as static text on every reset, for every user**. It is a
statement of the rule, not a report about this account: the screen is not told whether this account is
locked, and must not be.

**A deactivated user cannot be reset.** KAFF-104 rule 5: recovery is not a route back in for somebody
who has left; a returning employee comes back through reactivation with zero assignments (KAFF-112).
On an inactive user the action is rendered disabled with `users.reset.hint.inactive_cannot_reset`, and
**the server refuses it as well** — the disabled button is convenience, not the control.

### Error states

| Condition | Key |
|---|---|
| The user is inactive | `errors.identity.user_already_inactive` |
| The role has no password (Subcontractor) | `errors.identity.subcontractor_cannot_log_in` — the action is not rendered for this role at all, and the server refuses it anyway |
| Sending failed (no gateway, message rejected) | `errors.unknown` + `action.retry`. **The dialog must not claim success it did not get** — an Owner who is told the link was sent will tell the engineer to wait for a message that never arrives. |
| Caller is not the Owner | `errors.auth.forbidden` |

| Element | Key |
|---|---|
| Trigger on S-008 | `users.action.reset_password` |
| Dialog | `users.reset.title` · `users.reset.body` |
| Hints | `users.reset.hint.does_not_unlock` · `users.reset.hint.sessions_end_on_use` · `users.reset.hint.inactive_cannot_reset` |
| Actions | `users.reset.action.send` · `action.cancel` · `action.close` |
| Confirmation | `users.reset.sent.title` · `users.reset.sent.body` |
| Channel names | `enum.ResetChannel.Sms` · `enum.ResetChannel.WhatsApp` — **only if the server returns a channel**; if it does not, `users.reset.sent.body_no_channel` |

### What is not decided here, and is not the design's to decide

D-051 itself flags it: *"The story must decide link lifetime, single-use, and what happens to active
sessions on reset."* Three more sit underneath, and all six are in `questions.md` **Q-UX-19**:

- how long a link lives, and whether the screen should say so before sending;
- whether a second link invalidates the first;
- **whether Kaff has an SMS or WhatsApp gateway at all** — the ruling assumes a channel the system
  does not yet have, and if there is none, this screen cannot work as drawn;
- who pays for and monitors delivery failures.

**The screen is drawn for the ruled shape** — the system sends, the Owner never sees the link — and it
does not hedge toward a copy-the-link variant, because hedging would build the thing the ruling
avoided.

---

## S-009 / S-010 · Project team and assignment — Owner and HR

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [‹]                                            Project team  │  back chevron FLIPS in RTL
│                                        Project name          │  context line
├──────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                    Ahmed Nabil     [×] │  │  revoke at inline-end (LEFT)
│  │                    Site Engineer · Supervisor          │  │
│  ├────────────────────────────────────────────────────────┤  │
│  │                                    Mona Adel       [×] │  │
│  │                    Finance · Standard                  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│   ┌────────────────────────────────────────────────────┐     │
│   │                 + Assign someone                   │     │  opens S-010
│   └────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────┘

S-010, as a sheet sliding in FROM THE RIGHT (inline-start):
┌──────────────────────────────────────────────────────────────┐
│ [×]                                          Assign someone  │
│                                          User *              │
│  ┌────────────────────────────────────────────────────────┐  │  searchable select, name + role
│  └────────────────────────────────────────────────────────┘  │
│                                          Level *             │
│  ( ) Standard   ( ) Junior   ( ) Supervisor                  │  radio group, 44px targets
│                                          Level applies to    │
│                                          this project only.  │  assignments.hint.level_per_project
│  ┌───────────────────────┐  ┌───────────────────────┐        │
│  │        Assign         │  │        Cancel         │        │
│  └───────────────────────┘  └───────────────────────┘        │
└──────────────────────────────────────────────────────────────┘
```

| Element | Key |
|---|---|
| Title | `assignments.title` |
| Assign | `assignments.action.assign` · `assignments.assign.title` |
| Fields | `assignments.field.user` · `assignments.field.level` |
| Hint | `assignments.hint.level_per_project` |
| Revoke | `assignments.action.revoke`, `a11y.revoke_assignment` |
| Confirm revoke | `assignments.confirm.revoke.title` · `.body` |
| Empty team | `assignments.empty.title` / `.body` — expected on a new project, and the reason HR has global reach |

### Error states

| Condition | Key |
|---|---|
| Client or Subcontractor selected | `errors.identity.client_is_not_assignable` |
| Level not applicable to the role | `errors.identity.assignment_level_not_applicable` |
| Already revoked | `errors.identity.assignment_already_revoked` |
| Caller is neither Owner nor HR | `errors.auth.forbidden` |
| Caller is not assigned and has no global reach | `errors.auth.not_assigned_to_project` |

### Rules

- **The list is built from `ProjectAssignment` rows, never from the access check.** Otherwise Karim
  appears on every project team in the system — his reach is global and leaves no row. (Kickoff §4.)
- The user picker offers only users who **can** be assigned. Clients and Subcontractors are excluded
  from the list *and* refused by the server.
- Level is per project. The hint says so, because the whole point of D-044 §5 is that the same person
  is Junior here and Supervisor there.
- **HR does not reach this screen at all.** That gap is closed: D-051 (Q32) gives HR its own
  surface, S-009a / S-009b below, on its own routes against its own API. **S-009 is the internal
  screen, and it requires `ProjectRead`** (KAFF-115), which HR does not hold and is not to be given.
  Do not add a role check to this screen to make it serve both — a shared view is what the ruling
  refused.

---

## S-009a / S-009b · HR's Project Team surface — **NEW, D-051 (Q32)**

**HR asked to be able to staff a project without being able to read one. This is the answer, and it is
a separate surface rather than a filtered view.**

> *"HR may only see the project name and the list of assigned engineers … If the main project
> dashboard contains financial data, HR must be routed to a separate 'Project Team' tab/screen that
> contains zero financial details."* — D-051, Q32

That shape is not a preference. It is the same pattern `spec.md` §12 uses for the client portal, for
the same reason D-035 records: **a filtered view leaks the first time somebody adds a field.** A
separate surface cannot leak a field it was never given.

### The structural rules — read these before drawing anything

1. **Its own routes.** `/hr/projects` and `/hr/projects/:id/team`. Not `/projects/:id/team` with a
   role check inside. A route that is shared is a view that is shared.
2. **Its own API surface** — `/api/hr/...`, with **unshared response types**. Do not reuse the
   internal project DTO with an `if (isHr) omit` branch. That is exactly the failure D-035 and action
   A9 exist to prevent, and it fails silently the first time a `contractValue` field is added.
3. **Its own narrow permission**, which D-051 says *"implies a new narrow permission rather than
   granting HR `ProjectRead`"* and adds that **naming it is the story's**. This document therefore
   does not name it, and neither should the frontend — the guard reads whatever `GET /api/auth/me`
   returns in the permission set.
4. **The payload carries a name and a team, and nothing else.** No status, no dates, no client, no
   value, no progress, no health tag, no photo. Karim's sentence is the field list.
5. **S-009 and S-009b are two screens that happen to look alike.** They are not one component with a
   flag. The moment slice 4 puts a contract value on S-009, that similarity ends — and if they shared
   a component, HR would inherit it.

### S-009a · HR's project list

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [≡]                                     [AR][EN]      Kaff   │
├──────────────────────────────────────────────────────────────┤
│                                                  Projects    │  h1 · hr.projects.title
│           Choose a project to see who is on it.              │  hr.projects.subtitle
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [search]                          Search projects      │  │  dir=auto
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                 Project name        ›  │  │  NAME ONLY — no status chip,
│  │                                 4 people               │  │  no dates, no value
│  ├────────────────────────────────────────────────────────┤  │
│  │                                 Project name        ›  │  │
│  │                                 No one assigned        │  │  hr.projects.no_team
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

- **The member count is the one number on the screen and it is not financial.** It is what tells HR
  which sites are unstaffed, which is the job. If even that is judged to be more than "the project
  name and the list of assigned engineers", it comes off — raise it rather than defend it.
- **No filter chips.** Status is a project field HR does not have.
- Empty list: `hr.projects.empty.title` / `.body`. It is a real state — before slice 4, projects come
  from seed data (KAFF-113), and HR may open this screen before any exist.

### S-009b · HR's project team

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [‹]                                            Project team  │  back chevron FLIPS in RTL
│                                        Project name          │  context line — the name, from
├──────────────────────────────────────────────────────────────┤  the HR payload, not /projects
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                    Ahmed Nabil     [×] │  │  revoke at inline-end (LEFT)
│  │                    Site Engineer · Supervisor          │  │
│  ├────────────────────────────────────────────────────────┤  │
│  │                                    Mona Adel       [×] │  │
│  │                    Finance · Standard                  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│   ┌────────────────────────────────────────────────────┐     │
│   │                 + Assign someone                   │     │  opens S-010
│   └────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────┘
```

Visually S-009's twin, and deliberately so — HR should not have to learn a second idiom. Structurally
separate, for the reasons above.

- **Built from `ProjectAssignment` rows, never from the access check** (KAFF-115). Otherwise the Owner
  — and HR itself — appears on every team in the company, because both reach every project without an
  assignment row (D-010 · D-044 ruling 3).
- **A deactivated user is absent because their assignment was revoked** (D-049 ruling 5 · KAFF-111),
  not because this screen filters on `IsActive`. One mechanism, in one place.
- The assign sheet **S-010 is reused as drawn**, and that reuse is safe: it writes assignments and
  reads nothing about the project. It is the *read* surface that had to be separated.

### The gap S-010 still has for HR, and it is not the one that was just closed

**S-010's user picker needs a list of users, and HR holds no `UserManage`.** Q32 answered what HR may
see of a *project*; nobody has answered what HR may see of a *user*, and HR cannot assign somebody it
cannot name. `questions.md` **Q-UX-16**, new and open.

It does not block S-009a or S-009b. It blocks HR's ability to complete an assignment, which is the
whole reason the role exists — so it is raised at the same weight as Q32 was.

Until it is answered, **do not** solve it by giving HR the Owner's user list (S-006): that list
carries usernames, roles, departments and active state for every account in Kaff, and handing it to HR
repeats the mistake Q32 was answered to avoid.

### Where HR now lands

**HR's landing changes from S-005 My profile to S-009a**, because HR finally has a screen. See
`navigation.md`.

| Element | Key |
|---|---|
| List title / subtitle | `hr.projects.title` · `hr.projects.subtitle` |
| Search | `hr.projects.search.placeholder` |
| Member count | `hr.projects.member_count` — `{count}`, plural rules |
| No team yet | `hr.projects.no_team` |
| Empty list | `hr.projects.empty.title` · `.body` |
| Team screen | reuses `team.title`, `team.empty`, `assignments.*` from S-009 / S-010 |

### Error states

| Condition | Key |
|---|---|
| Caller is not HR (or the Owner) | `errors.auth.forbidden` → S-016 |
| Project id does not exist | `errors.not_found.title` / `.body` |
| Load failed | `errors.unknown` + `action.retry` |

---

## S-015 · Audit trail

**The reader is settled: the Owner, company-wide, and nobody else** (D-049 ruling 1). `AuditRead` is
no longer marked `Unresolved`, and **the answer was to keep the assumption, not to widen it.** Karim
explicitly rejected a project-scoped audit read for the people working on that project — *"completely
hidden from all other roles, **even for their own projects**"* — because the trail carries financial
movements, so scoping it by project would have reopened the zero-financial-visibility rule from a
direction nobody was watching.

**So do not build a filter, a tab or a route that would let anyone else in**, and do not add the
"Global Finance/Audit role" the ruling mentions in passing: D-049 records that it was deliberately
**not** created. The awkwardness the kickoff named still stands and is now explicit and accepted —
the only person who reaches every project is the only person who can read the record of what he did
there. That is Karim's business to accept, and he has.

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [≡]                                            Audit trail   │
├──────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [search]                     Search actor or entity    │  │
│  └────────────────────────────────────────────────────────┘  │
│  [ All ] [ Created ] [ Modified ] [ Deleted ]                │
│  From: [ 2026-08-01 ]   To: [ 2026-08-20 ]                   │  dates dir=ltr, Gregorian
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                        20/08/2026 14:32:07          ›  │  │  <bdi>, tabular-nums
│  │                        Karim · Owner                   │  │  actor + role at the time
│  │                        Created · Client                │  │  action + entity type
│  │                        3 fields changed                │  │
│  ├────────────────────────────────────────────────────────┤  │
│  │                        20/08/2026 14:29:55          ›  │  │
│  └────────────────────────────────────────────────────────┘  │
│                              [ Load more ]                   │  cursor paging, never "page 47"
└──────────────────────────────────────────────────────────────┘

Detail (full screen on mobile, side panel on desktop opening from the RIGHT):
┌──────────────────────────────────────────────────────────────┐
│ [×]                                          Audit record    │
│                          When   20/08/2026 14:32:07          │
│                          Who    Karim (Owner)                │
│                          What   Created · Client · <id>      │  <bdi> on the id
│                          Why    "reason text, if any"        │  dir=auto; omitted when null
│                          Where  POST /api/clients            │  <bdi>, dir=ltr
│                          Correlation  <guid>                 │  <bdi>, copyable
│                                                              │
│                          Changes                             │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Field          │ Before        │ After                 │  │  column order right→left:
│  │ Name           │ —             │ "…"                   │  │  Field · Before · After
│  │ PasswordHash   │ [redacted]    │ [redacted]            │  │  AuditRedactedAttribute
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

| Element | Key |
|---|---|
| Title | `audit.title` |
| Filters | `audit.filter.all` · `audit.filter.action` · `audit.field.from` · `audit.field.to` |
| Row fields | `audit.field.when` · `.who` · `.what` · `.why` · `.where` · `.correlation` |
| Actions | `enum.AuditAction.Created` · `.Modified` · `.Deleted` |
| Changes table | `audit.changes.title` · `audit.changes.field` · `.before` · `.after` |
| Redacted | `audit.value.redacted` |
| Null / absent value | `audit.value.none` |
| Empty | `audit.empty.title` / `.body` |
| Load more | `action.load_more` |

### Rules

- **A redacted value renders as `audit.value.redacted`, never as blank.** `AuditRedactedAttribute`
  exists precisely so secrets do not land in the trail; a blank cell reads as "nothing changed",
  which is the opposite of what happened.
- **Nothing on this screen is editable, and there is no delete control.** The table is append-only and
  a database trigger enforces it: evidence that can be edited is not evidence.
- `ActorRole` is the role **at the time of the action** and is displayed as such. Do not join to the
  user's current role.
- The Owner's global reach currently **leaves no assignment row and no record of how access was
  granted**. BA raised this to the Architect at the kickoff (§4): `AuditRecord` should record whether
  access came from an assignment, Owner-global reach, or client-of-project. If that field lands, this
  screen shows it; if it does not, **do not invent a column that implies it exists.**
- Paging is cursor-based. An audit trail grows without limit and an offset page number becomes wrong
  between two clicks.

---

## S-011 · Client list

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [≡]                                     [AR][EN]      Kaff   │
├──────────────────────────────────────────────────────────────┤
│                                                  Clients     │  h1
│  ┌────────────────────────────────────────────────────────┐  │
│  │ [search]                     Search name or phone      │  │  dir=auto
│  └────────────────────────────────────────────────────────┘  │
│  [ All ] [ Active ] [ Archived ]                             │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                                 Client name         ›  │  │  dir=auto
│  │                       C-10001 · Corporate              │  │  <bdi> on the generated code
│  │                       0101 234 5678                    │  │  <bdi>, PhoneEntered
│  ├────────────────────────────────────────────────────────┤  │
│  │                                 Client name         ›  │  │
│  └────────────────────────────────────────────────────────┘  │
│   ┌────────────────────────────────────────────────────┐     │
│   │                  + New client                      │     │
│   └────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────┘
```

Desktop table, column order **right → left**: Name · Code · Kind · Phone · State · actions.

| Element | Key |
|---|---|
| Title | `clients.title` |
| Search | `clients.search.placeholder` |
| Filters | `clients.filter.all` · `.active` · `.archived` |
| Kind | `enum.ClientKind.Individual` · `.Corporate` |
| New | `clients.action.create` |
| Empty | `clients.empty.title` / `.body` |
| No search results | `clients.empty.filtered.title` / `.body` |

**Searching by phone must normalise the query the way the Domain does** — `+20 10 …`, `0020 10 …` and
`010 …` are the same client. Send the raw query and let the server normalise; do not normalise in the
client and create a second implementation of `PhoneNumber.Normalise`.

---

## S-012 · Create client, and S-013 · the duplicate-phone warning

**Two rulings rewrote this screen.** The code is generated rather than typed (D-049 ruling 7), and
withholding has left the client record entirely (D-049 rulings 9 and 10). What is left is smaller and
truer than what was drawn before.

```
┌──────────────────────────────────────────────────────────────┐ 390
│ [×]                                            New client    │
├──────────────────────────────────────────────────────────────┤
│                                          Phone *             │  dir=ltr, inputmode=tel
│  ┌────────────────────────────────────────────────────────┐  │  FIRST FIELD — see rules
│  └────────────────────────────────────────────────────────┘  │
│           This is how clients are matched. Enter it first.   │  clients.hint.phone_is_the_key
│                                                              │
│                                          Name *              │
│  ┌────────────────────────────────────────────────────────┐  │  dir=auto
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│                                          Code                │
│   Assigned when you save                                     │  clients.field.code.generated_
│                                                              │  notice — NO INPUT, no box
│                                          Kind *              │
│  ( ) Individual        ( ) Corporate                         │
│                                                              │
│  ── shown only when Kind = Corporate ─────────────────────    │
│                                          Tax reg. number     │  dir=ltr
│  ┌────────────────────────────────────────────────────────┐  │
│  └────────────────────────────────────────────────────────┘  │
│  ───────────────────────────────────────────────────────     │
│                                          Alternate phone     │
│                                          Email               │
│                                          Address             │
│                                          Notes               │  dir=auto, INTERNAL — never portal
│                                                              │
│  ┌───────────────────────┐  ┌───────────────────────┐        │
│  │        Create         │  │        Cancel         │        │
│  └───────────────────────┘  └───────────────────────┘        │
└──────────────────────────────────────────────────────────────┘
```

### The code is generated — D-049 ruling 7

Sequential, of the form `C-10001`, and **manual entry and later editing are both forbidden**. So it is
not a field: it is a line of text saying the system will assign one
(`clients.field.code.generated_notice`), and after the save it is a `<bdi>`-isolated read-only value
on S-014 with `clients.field.code.not_editable`.

- **No input, not even a disabled one.** A disabled input is a field somebody will later enable.
- `Client` has no setter for the code, deliberately (KAFF-119 rule 2), and a request that supplies one
  has it ignored or refused. The UI must never send the field at all.
- This closes the first half of D-040, which had flagged `Client.Code` as a required field `spec.md`
  never asked for. It was right to flag it, and the answer is that it should exist.

### Withholding is gone from this screen — D-049 rulings 9 and 10

The withholding category **is no longer a property of a client**. It moved to the contract, and
**Finance sets it, not Marketing**:

> *"The same client (e.g. a government body) might sign a design contract (one rate) and an execution
> contract (another rate). Storing it on the client profile breaks this reality."* … the rate
> *"directly dictates ledger entries and money reconciliation. It is a strict accounting parameter,
> not a marketing detail."* — D-049 rulings 9 and 10

Concretely, for whoever builds this form:

- **There is no withholding select on any Marketing screen.** Not hidden, not disabled, not
  conditional — absent. `Client` no longer has the column.
- It **reappears in slice 4 on the contract**, set by Finance during contract creation or approval
  (KAFF-416). Not here, and not in slice 1.
- **The tax registration number stays on the client**, because it identifies the legal entity and does
  not vary by contract (KAFF-120 rule 4). It is still Corporate-only: an individual carrying one is
  refused with `errors.master.individual_does_not_withhold`, in the domain
  (`Client.SetTaxRegistration`) and not only in the UI.
- Switching Corporate → Individual with a registration number entered clears the field and confirms
  first — the server refuses the combination either way, and a silent clear loses something the user
  typed.

`questions.md` Q-UX-11 asked which department sets the category. The answer moved the field rather
than assigning it, which is why this section is a deletion rather than a change.

---

### S-013 · Duplicate phone — **it warns, and the save proceeds** · D-049 ruling 8

**This reverses what this document said yesterday, and the reversal is the whole point of the
section.** The old design refused the save and offered to open the existing client, citing `spec.md`
§2's "deduplicated by phone" and §3's "never create a duplicate client". Karim amended both:

> **Warn, name the client that already holds the number, and do not block the save.** *"A corporate
> client and its CEO might be registered as two separate entities sharing the same contact number."*
> — D-049 ruling 8

The unique index `ux_clients_phone` became the non-unique `ix_clients_phone`. **Any screen, sentence
or component in this folder that still asserts a refusal is now wrong.**

```
┌──────────────────────────────────────────────────────────────┐
│                     This number is already registered        │  clients.duplicate.warning_title
│                                                              │
│    This number is already registered to Hassan Farouk.       │  clients.duplicate.warning_body
│    Do you want to proceed?                                   │  {name} — bidi-isolated
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │                              Hassan Farouk             │  │  the matched record, read-only
│  │                    C-10001 · 0101 234 5678             │  │  <bdi> on code and phone
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌───────────────────────┐  ┌───────────────────────┐        │
│  │       Proceed         │  │   Open that client    │        │
│  └───────────────────────┘  └───────────────────────┘        │
│                                                              │
│                                              Cancel          │  tertiary, back to the form
└──────────────────────────────────────────────────────────────┘
```

| Element | Key |
|---|---|
| Title / body | `clients.duplicate.warning_title` · `clients.duplicate.warning_body` |
| Matched client is archived | `clients.duplicate.matched_archived` |
| Actions | `clients.duplicate.proceed` · `clients.duplicate.open_existing` · `action.cancel` |

**These are KAFF-119's keys, verbatim.** The old `clients.duplicate.title` / `.body` /
`.action.open_existing` / `.action.edit_phone` are retired with the refusal they belonged to.

### The rules that make the warning worth having

- **Proceed is a real outcome and the primary action.** A warning whose only paths are "open the other
  one" and "go back" is a refusal wearing softer words.
- **The warning names the client.** KAFF-119 rule 4: *"a warning that says only 'this number exists'
  is not what was ruled."* If the server returns only a count and no name, the dialog is not
  buildable as ruled — raise it, do not ship the anonymous version.
- **Cancel returns to the form with everything the user typed intact.** The dialog is an interruption,
  not a submission boundary.
- **The check still fires on blur of the phone field**, which is why phone is still the first field.
  The information is worth the least at the end of a completed form.
- **The save is audited as having proceeded past a warning**, naming the matched client (KAFF-119 rule
  7 · audit note). The screen does not have to say so — but that record is the only durable trace that
  a human made the call, and it is what a later question about two client records gets answered from.
- **The match runs on the normalised phone**, server-side, so `+20 10…`, `0020 10…` and `010…` all
  match. **This matters more after the ruling, not less.** Before, a missed match meant a wrongly
  accepted save that the index would have caught; now a missed match means a warning nobody ever sees.
  Never normalise in the client — a second copy of `PhoneNumber.Normalise` is how the two drift.

### The archived client gets the same treatment

An archived client is still a client, and `spec.md` §3 requires a reopened opportunity to attach to
the original. So the warning fires, **says that the match is archived**
(`clients.duplicate.matched_archived`), and still lets the save proceed (KAFF-119 rule 6).

- **No offer to reactivate from inside this dialog.** Un-archiving somebody else's record while
  creating a new one is two master-data changes behind one button. `clients.duplicate.open_existing`
  goes to S-014, where archiving and un-archiving live and where the act is visible for what it is.
- The archived note is part of the same warning, not a second dialog.

### Editing a phone into a collision — the same interaction, not a special case

KAFF-121 rule 4: changing a client's primary phone re-runs the check on the normalised number and
**warns without blocking, naming the client that already holds it** — the same dialog, from S-014.
There is no separate refusal path on edit, and building one would contradict the ruling from a
direction nobody is watching.

### Other error states, S-012

| Condition | Key |
|---|---|
| Name missing or too long | `errors.master.name_required` |
| Phone problems | `errors.phone.required` · `.too_long` · `.too_short` |
| Tax registration number on an individual | `errors.master.individual_does_not_withhold` — this is the real key, in the code and in both catalogues (KAFF-120, finding F-08) |
| Not Marketing or Owner | `errors.auth.forbidden` |

**There is no `errors.master.code_required`** on this form any more — nothing supplies a code. And
`errors.master.client_phone_already_exists` is retired: the server no longer refuses that save.

---

## S-014 · Client detail and edit

Read view with the same section order as S-012, plus:

- **The code is read-only and cannot be edited by any route** (D-049 ruling 7 · KAFF-121 rule 5).
  Render it as a `<bdi>`-isolated value with `clients.field.code.not_editable`, never as a disabled
  input.
- **No withholding anywhere on this screen.** It is the contract's, and Finance's, from slice 4
  (D-049 rulings 9 and 10 · KAFF-416). The tax registration number remains, Corporate-only.
- **Changing the primary phone re-runs the duplicate check** and shows S-013's warning — warn, name
  the existing client, proceed if the user says so (KAFF-121 rule 4).
- **Changing Kind from Corporate to Individual** re-applies §6.7: an individual carrying a tax
  registration number is refused (KAFF-121 rule 6). Clear the field and confirm rather than submitting
  a combination the server will reject.
- **History** — placeholder in slice 1. `spec.md` §2 says a Client is "project-independent, full
  history"; the projects and opportunities that make up that history do not exist until slice 4. Show
  an empty state (`clients.history.empty`), **not an invented list of tiles.**
- **Archive** in a danger zone: `clients.action.archive`, confirm via
  `clients.confirm.archive.title` / `.body`, server errors `errors.master.already_archived` /
  `errors.master.not_archived`.
- **Notes are internal.** Label them so (`clients.hint.notes_internal`). `spec.md` §12 forbids internal
  notes reaching the client, and the portal is built in slice 8 by somebody who will not read this
  form.

---

## S-016 · Refusals and failures

One component, **four** modes. `components.md` §10.

| Mode | When | Keys |
|---|---|---|
| **Forbidden** | 403 from any endpoint | `errors.auth.forbidden`, or the more specific `errors.auth.not_assigned_to_project` / `errors.auth.assignment_level_too_low` when the server sends it. Offer `action.back` only. **Never a retry** — retrying a refusal is theatre. |
| **Not found** | 404, an unknown route, or `/setup` after setup is done | `errors.not_found.title` / `.body`, `action.back` |
| **Failed** | 5xx, network, `GET /api/auth/me` unreachable | `errors.unknown`, `action.retry` |
| **Session expired** | 401 **after** the session had resolved as signed-in | S-016a below — not a page, a dialog |

**A 403 must read as a refusal, in the user's language, with the app chrome intact.** It is a normal,
expected outcome of a correctly designed system: `spec.md` §9 makes the server the decider, so a user
reaching a screen their role does not have is the mechanism working, not a bug.

**A 401 before resolution is not an expiry, it is "signed out".** The difference is whether this tab
ever held a resolved session (S-004). Signed out goes to S-001; expired goes to S-016a.

---

## S-016a · Session expired — **NEW, D-049 ruling 2 + D-050**

**The session is 30 minutes of inactivity, sliding** (D-049 ruling 2, `JwtOptions.InactivityMinutes`).
**And the page cannot see it happen** — the cookie is `HttpOnly`, so nothing in the browser can read
the expiry, count down to it, or notice it passing (D-050).

That fixes the shape of this experience before any design opinion enters:

### There is no warning before expiry, and there must not be

A "your session ends in 2 minutes" banner needs a client-side timer mirroring the server's sliding
clock. It would be a second implementation of the expiry rule, in a language that cannot see the first
one, and it would drift the moment a background request slides the server's window without the timer
noticing. **The application discovers expiry the way it discovers everything else: a request comes
back 401.**

### What the user sees

A dialog over the screen they were on. **The screen behind it is not unmounted, not cleared, and not
navigated away from.**

```
┌────────────────────────────────────────────────────┐
│                        You were signed out         │  auth.expired.title
│                                                    │
│   You were away for a while. Sign in again to      │  auth.expired.body
│   carry on.                                        │
│                                                    │
│                                       Ahmed Nabil  │  the last known display name,
│                                                    │  dir=auto — no username field
│                                          Password  │
│   ┌────────────────────────────────────────────┐   │  dir=ltr, autofocus
│   │ ••••••••                              [eye]│   │
│   └────────────────────────────────────────────┘   │
│                                                    │
│  ┌───────────────────┐  ┌───────────────────┐      │
│  │     Sign in       │  │  Sign in as       │      │
│  │                   │  │  someone else     │      │
│  └───────────────────┘  └───────────────────┘      │
└────────────────────────────────────────────────────┘
```

- **On success the dialog closes and the request that triggered it is retried, once.** The user is
  back where they were, with what they had typed.
- **"Sign in as someone else"** warns first if a form on the screen behind has unsaved changes
  (`auth.expired.confirm_leave`), then unmounts everything and goes to S-001. Switching identity
  cannot keep the previous person's half-filled form on screen.
- **A wrong password inside the dialog renders `errors.auth.invalid_credentials` in the dialog**, and
  the same lockout rules apply — five failures here lock the account exactly as they do at S-001, and
  the dialog says nothing about it, for the reasons in S-001.
- Focus is trapped, `Escape` does **not** dismiss it (there is nothing behind it the user can do), and
  the password field is autofocused because that is the only thing to do.

### Unsaved work is recoverable, and this is why the dialog exists

The alternative — redirect to S-001 with a `returnTo` — is less code and loses the form. In slice 1
that costs a client record somebody re-types. **In slice 6 it costs a daily log written by an engineer
standing on a roof, and that is the screen this decision is really about.** A daily log records period
deltas (CLAUDE.md), so a half-entered one cannot simply be re-derived from what is on screen.

**What is *not* preserved, deliberately:** anything the user typed into a password field, on any
screen, is discarded when the session expires. S-003 mid-change loses both boxes.

> `ponytail:` slice 1 may ship the redirect-to-S-001 variant instead — no slice-1 form is longer than
> one screen, and the modal is the more complex of the two. **The upgrade point is the daily log
> (slice 6), not "later"**, and whoever builds slice 6 against a redirect will be rebuilding this.

### The one thing to be uncomfortable about

A dialog asking for a password over an application screen is, structurally, the shape of a phishing
prompt, and teaching users to type a password into an overlay is not costless. Two things make it
acceptable here and both must hold: it is **the same origin with the app chrome intact and the URL
unchanged**, and there is **no other window or iframe involved**. If either stops being true, this
becomes a redirect.

| Element | Key |
|---|---|
| Title / body | `auth.expired.title` · `auth.expired.body` |
| Password | `auth.field.password` |
| Actions | `auth.action.sign_in` · `auth.expired.action.switch_user` |
| Leaving with unsaved work | `auth.expired.confirm_leave` |
| After a password change ended this session | `auth.password.changed_sign_in_again` |

### Where else a 401 arrives

- **A password change or a deactivation kills every session immediately** (D-049 ruling 2 · D-048 ·
  `User.SecurityStamp`). So this dialog also appears on a device the user did not touch — the phone in
  a pocket, after they changed their password on a laptop. It reads identically, and that is correct:
  the user knows why.
- **A deactivated user gets `errors.auth.account_inactive` when they try to sign in from the dialog**,
  and the dialog then becomes a terminal panel with `action.back` and no password field. Do not leave a
  password box on screen for an account that can never accept one.

---

## New i18n keys slice 1 needs the **backend** to emit

`errors.*` keys are the server's, not the client's — `CLAUDE.md` forbids the server sending prose, so
these are the keys the API must return and both catalogues must carry.

| Key | Raised by |
|---|---|
| `errors.auth.invalid_credentials` | S-001 — and it covers a locked account too |
| `errors.auth.account_inactive` | S-001, S-016a |
| `errors.auth.password_change_required` | S-004, S-003 (already in KAFF-101a) |
| `errors.auth.current_password_incorrect` | S-003 (already in KAFF-103) |
| `errors.auth.password_too_short` | S-002, S-003, S-003a, S-007 (already in KAFF-103) |
| `errors.auth.reset_link_invalid` | **S-003a — new.** One key for expired, used, unknown and deactivated |
| `errors.setup.already_completed` | **S-002 — new.** The concurrent-setup case D-051 requires |
| `errors.identity.username_taken` | S-002, S-007 |
| `errors.master.individual_does_not_withhold` | S-012, S-014, `spec.md` §6.7, KAFF-120 |

### Retired, with the rules they belonged to

| Key | Why it is gone |
|---|---|
| `errors.master.client_phone_already_exists` | The save is no longer refused (D-049 ruling 8). The duplicate is a **client-side dialog** built from a lookup response, not a server refusal. |
| `errors.master.code_required` | Nothing supplies a code any more (D-049 ruling 7). |
| `users.hint.password_set_later` | The Owner sets a temporary password at creation (D-049 ruling 4). |
| `clients.duplicate.title` · `.body` · `.action.open_existing` · `.action.edit_phone` | Replaced by KAFF-119's `clients.duplicate.warning_title` · `.warning_body` · `.matched_archived` · `.open_existing` · `.proceed`. |

---

## Slice 1 demo script — what this design must make possible

Rewritten after D-049 and D-051. **It no longer starts with a blocked step**, which is the visible
result of Karim answering Q31.

1. **The system is empty.** Opening Kaff shows **S-002**, the one-time setup screen. Karim creates his
   own Owner account with his real name and phone, and a password he chooses.
2. Karim signs in (**S-001**). The shell shows the boot state, calls `GET /api/auth/me`, and lands him
   on the user list — **no flash of the sign-in form on the way** (S-004).
3. He opens the audit trail (**S-015**) and finds his own account creation as the first record, with
   his name and the time on it. *That is the reason Shape B was chosen, so the demo has to show it.*
4. He creates an HR user (**S-007**), giving them a temporary password. Department is fixed to HR and
   cannot be changed.
5. He creates a Site Engineer and a Marketing user the same way.
6. **HR signs in and is sent straight to the change-password screen** (S-003, forced mode). Every other
   route bounces back to it. HR chooses a new password; Karim's temporary one stops working.
7. HR lands on **S-009a — HR's project list.** No treasury, no project financials, no user list, no
   audit trail: the name of each project and how many people are on it, and nothing else.
8. HR opens a project's team (**S-009b**) and assigns the Site Engineer as Supervisor (**S-010**) — on
   a project HR was never assigned to. **[Q-UX-16 — the user picker still needs a source HR is allowed
   to read.]**
9. The Site Engineer signs in, is forced through S-003, then sees `Supervisor` on that project in
   **S-005**, and **is refused** on any project he is not assigned to (S-016,
   `errors.auth.not_assigned_to_project`).
10. Marketing signs in, lands on the Client list (**S-011**), and creates a client (**S-012**). **The
    code is assigned by the system** — nobody typed one.
11. Marketing enters the same phone again and **is warned, not stopped** (**S-013**): the dialog names
    the existing client, and Marketing chooses **Proceed**. Both clients now exist. The audit record
    for the second one says it was created past a warning, and names the match.
12. Marketing creates an **individual** client. **No withholding fields appear anywhere** — not for an
    individual, not for a corporate client. The tax registration number appears only for the corporate
    one.
13. The Site Engineer types the URL of the user list and is refused with 403 — **the route resolves,
    the API refuses, and the refusal is legible** (S-016).
14. Karim forgets his password... *(only if Q-UX-19 is answered and a channel exists)*: an Owner sends
    a reset link from **S-008a**, it arrives on the phone, and **S-003a** sets a new one.
15. A session is left idle for 30 minutes. The next action raises **S-016a**, the user signs back in,
    **and the half-filled form is still there.**
16. Every screen renders correctly at 390px in Arabic with no horizontal overflow on the body, and
    every figure is in Latin digits.

**Step 14 is the only conditional one.** Everything else runs on rulings that are closed.

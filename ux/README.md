# `ux/` — Kaff ERP design documentation

**Owner:** UX Agent · **Status:** slice 1 approved-pending (Nabil gates flows before the Frontend agent builds)
**Last written:** 2026-08-21 — revised against `decisions.md` **D-049, D-050 and D-051**

---

## The design position, in one paragraph

Kaff ERP is an **Arabic, right-to-left product first and only** — RTL is the primary direction, not a
mirror of an English design, which means there is no `left` or `right` anywhere in the stylesheet and
every layout is authored once with logical properties. It is **mobile-first where the work is
mobile**: the daily log is written by an engineer standing on a building site holding a phone, so
390px is the design width and 44px is the minimum tap target, and every other screen inherits that
baseline rather than being retrofitted to it. Navigation is **role-driven and derived from the
server** — nine roles see nine different applications, but what a role can *do* is decided by the
API on every request, and what the client hides is convenience, never security. And the **client
portal is an isolated surface**, not a filtered view of the internal one: it never renders a cost, a
margin, a catalogue item, a subcontractor, an internal note, or another client's data, and the way it
achieves that is by not being given them.

---

## What is in this folder

| File | What it answers |
|---|---|
| `rtl-and-i18n.md` | How to write RTL and bidi-correct Angular. Logical-property table, bidi isolation, number/money/date/phone formatting, icon mirroring, input direction, i18n key naming. **Read this before writing any component.** |
| `screen-inventory.md` | Every screen across slices 1–9: which roles, project-scoped or not, mobile priority, purpose. The map. |
| `navigation.md` | One section per role — all nine. What they see, what they must never see, where they land. |
| `slice-1-flows.md` | Slice 1 in detail — **21 screens**: setup, login, forced password change, reset links, user creation, project assignment, HR's Project Team surface, session expiry, audit trail, Client master. Wireframes, i18n keys, error states. |
| `components.md` | The shared component inventory and its Angular 22 shape, accessibility and tap-target requirements. |
| `questions.md` | Everything the design needs that `spec.md` does not answer. **Ten are now closed with the ruling that closed them; seven are new.** **Not decisions.** |

---

## How to use this if you are the Frontend agent

1. Read `CLAUDE.md` → Angular conventions and Language and terminology. Those are prohibitions.
2. Read `rtl-and-i18n.md` here. It is the concrete form of the CLAUDE.md rule.
3. Read `navigation.md` for the role you are building for, then `slice-1-flows.md` for the flow.
4. Take component shapes from `components.md`. Do not invent a second form field.
5. **If a screen you need is not here, or a rule you need is in `questions.md`, stop and ask.**
   `questions.md` is a list of things nobody has decided. Building against one is inventing a business
   rule, which `agents.md` names the single most expensive failure mode in this project.

## Three rules this folder will not bend on

- **Nothing in the frontend enforces a permission.** Every screen here hides things. The server
  refuses them. If a screen is reachable by typing a URL, that is expected and the API must still say
  no — see `navigation.md` §"What hiding is and is not".
- **No hardcoded user-facing string, in either language.** Every label in every wireframe here is
  named by its i18n key.
- **The frontend performs no money arithmetic, ever.** Every total comes from the server. Agreed at
  the slice-1 kickoff, 2026-08-18 §3.
- **There is no token anywhere in the frontend.** D-050 puts the session in an `HttpOnly` cookie the
  page cannot read, so `GET /api/auth/me` is the only way the UI learns anyone is signed in — and
  every screen has a defined state for before it answers.

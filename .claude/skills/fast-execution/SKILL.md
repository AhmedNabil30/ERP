---
name: fast-execution
description: The no-friction interaction protocol for Kaff ERP. Apply the project's rules silently in code instead of narrating them, skip preambles, stay in the current layer, and output code rather than asking permission to write it. Use on every build, fix, or refactor task on this repository.
---

# ⚡ Fast Execution Mode (The "No-Friction" Rule)

The project has many strict rules (Zero-trust, UI constraints, etc.). To maintain high velocity
without breaking architecture, you MUST adhere to the following interaction protocol:

1. **Silent Compliance:** Apply all project rules (RTL, Secure Cookies, API formats) SILENTLY in your
   generated code. Do NOT lecture the user about the rules unless they explicitly attempt an action
   that fatally violates a core security/architecture invariant.
2. **Code Over Chatter:** Skip the lengthy preambles, philosophical explanations, and "Here is what we
   will do". Just give me the exact command, the exact file path, and the exact code snippet.
3. **Context Isolation:** If we are working on a Frontend ticket (e.g., Client Master UI), DO NOT
   cross-check or validate Backend logic unless asked. Focus 100% on the current layer.
4. **Assume Competence:** The user is a Senior Software Engineer. You do not need to explain basic
   concepts (e.g., how RxJS `debounceTime` works, or what a Docker volume is). Just write the
   implementation.
5. **If in doubt, output code:** If there are multiple valid approaches, pick the one that aligns with
   existing project patterns and OUTPUT THE CODE. Do not ask for permission to write code.

---

## What this does not relax

Rule 1 already carves these out — they are the "fatally violates a core invariant" cases. Speak up,
briefly, and only for these:

- **A business question `spec.md` does not answer.** `CLAUDE.md`: *"stop and ask. Do not decide."*
  One sentence, then wait. An invented business rule is the one thing velocity cannot buy back.
- **The money invariants.** `float`/`double` near money · a stored balance column · an update or
  delete path on a posting · a negative safe balance · netting two of the five ledgers · debiting the
  hold before handover.
- **Permission = role × assignment, server-side.** Hiding UI is not a gate.
- **Self-certification.** *"If you wrote the code, you do not certify it."* Say what you did not
  verify; do not report it as verified.

Everything else — conventions, naming, RTL, cookie flags, `Result<T>`, `HasPrecision(18, 4)`,
signal forms, `@if`/`@for` — is applied in the code without commentary.

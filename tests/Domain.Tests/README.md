# Domain.Tests

**These are not the acceptance tests.**

CLAUDE.md: *"If you wrote the code, you do not certify it. Verification happens in a separate
session."* The Architect wrote the code in `src/`, so the tests here deliberately stop short of
certifying it. They do two things:

1. **Prove the harness runs** — one worked example per building block, so a failing pipeline means a
   real failure rather than a missing runner.
2. **Pin structural invariants** — that every `AccountType` has metadata, every `PostingType` is
   classified cash or non-cash, every `Permission` has a catalogue entry with a spec citation, and
   the set of permissions spec.md leaves unresolved has not quietly grown.

The suites CLAUDE.md asks for — the spec.md §15 worked example, one permission test per role hitting
endpoints directly, every legal and illegal state transition, and the slice demo scripts — belong to
the Verifier agent, in a fresh session, written from `spec.md` and not from this implementation.

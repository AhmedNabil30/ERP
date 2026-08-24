# CLAUDE.md — Kaff ERP

Read this file completely before doing anything. You have no memory of previous sessions. This file, `spec.md`, and `decisions.md` are the only continuity that exists.

**Project:** ERP for Kaff, an Egyptian construction and finishing contractor. Real production system with real money in it.

---

## Before you start

1. Read this file.
2. Read `spec.md` — it is the business truth. If code and `spec.md` disagree, `spec.md` wins.
3. Read the story file for your slice in `stories/`.
4. Check `decisions.md` for why things are the way they are before proposing a change.

**If `spec.md` doesn't answer a business question, stop and ask. Do not decide.** An invented rule is always plausible, which is why it survives review and surfaces months later during acceptance. Raising the question costs a message; guessing costs a rebuild.

---

## Stack — pinned, do not substitute

| | |
|---|---|
| Runtime | .NET 10 (LTS) |
| API | ASP.NET Core, minimal APIs |
| Data | EF Core + PostgreSQL (Npgsql) |
| Frontend | Angular 22 |
| Tests | xUnit + FluentAssertions, Playwright for E2E |
| Style | `.editorconfig` enforced, `dotnet format` clean |

Do not add a package that duplicates something the framework already does. If you believe a new dependency is genuinely required, add it to `decisions.md` with the reason.

---

## Architecture — one pattern, no alternatives

**Vertical slices.** One folder per feature containing everything that feature needs.

```
src/
  Api/
    Features/
      Extracts/
        CreateExtract/
          Endpoint.cs · Handler.cs · Request.cs · Response.cs · Validator.cs
        ApproveExtract/
        ...
      Clients/
      Projects/
  Domain/          entities, value objects, domain services, calculators
  Infrastructure/  EF context, configurations, migrations, external services
  Web/             Angular application
tests/
  Domain.Tests/ · Api.Tests/ · E2E.Tests/
```

**Do not introduce** Clean Architecture layering, a repository pattern over EF Core, MediatR, or a service layer that only forwards calls. EF Core's `DbContext` is the unit of work. Handlers talk to it directly.

Cross-feature logic belongs in `Domain/`. If two features need the same thing, it moves to `Domain/` — it does not get copied.

---

## The rules that must never break

These are prohibitions, not preferences. Violating one is a defect even if every test passes.

### Money

**Never use `float` or `double` anywhere near money.** `decimal` only, and EF precision configured explicitly:

```csharp
builder.Property(x => x.Amount).HasPrecision(18, 4);
```

EF Core silently truncates decimals when precision isn't configured. Every money property gets this. No exceptions.

**Never store a balance.** Balances are derived by summing postings, always. If you find yourself adding a `Balance` column, stop — that's the bug.

**Never update or delete a posting.** Postings are append-only. Corrections are new reversing postings that reference the original through `ReversesId`. There is no update path and no delete path. Do not add one, not even for admins, not even for "fixing test data."

**The safe balance can never go negative.** Enforced by a database constraint, not application code. A payment that would breach it fails and prompts an owner injection.

**The five ledgers never net against each other**: client advance, hold, firm advance, عهدة, owner current account. No calculation may offset one against another.

**The hold only grows.** Nothing comes out of it mid-project — no snag, no debit note, no adjustment. It releases once, in full, at handover. If you write code that debits the hold ledger before handover, you have misread the spec.

### Permissions

**Every endpoint checks two things: role and assignment.** A user with the correct role but no assignment to the project is refused. Server-side, always. Hiding a menu item is presentation, not security.

**Nobody creates and approves the same movement.** If your handler lets the same user do both, it's wrong.

### Audit

**Every state change writes an audit record**: who, when, what changed (before and after), and where the flow requires it, why. This is one mechanism in `Domain/`, not per-feature code.

**Rejections return to origin with a reason.** Never a silent step-back, never a rejection without a stored reason.

### Data

**Signed BOQs are frozen by copy, not reference.** The BOQ holds catalogue *values* at signature time. It must not hold a foreign key that would let a later price change reach a signed contract.

**Daily logs record period deltas, never cumulative totals.** This is what makes offline sync additive.

**Money never moves offline.** Offline creates drafts. Approval and disbursement happen online against a live balance.

### Contract types

**Type dispatches, it does not fork.** One `Project` entity, one treasury, one approval engine. Lump Sum, Cost Plus and Design differ only through `IBillingCalculator` and `IProgressMetric`. Copying the project module three times is the mistake this rule exists to prevent.

---

## C# conventions

```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<AnalysisLevel>latest-recommended</AnalysisLevel>
```

**The compiler is the first gate. If it doesn't build clean, your work isn't done.**

- Entities have private setters and behaviour. No anaemic classes with public setters everywhere.
- Money is a `Money` value object wrapping `decimal`, not a bare `decimal` passed around.
- Domain errors are `Result<T>`, not exceptions. Exceptions are for genuinely exceptional cases.
- Async everywhere, `CancellationToken` threaded through.
- No `#pragma warning disable` without a comment explaining why.

---

## Angular conventions

Angular's idiom shifted significantly across recent versions. Mixing eras across files is the main frontend risk here.

**Required:**
- Standalone components only. **No NgModules.**
- Signals for state. **No BehaviorSubject** for component state.
- Zoneless change detection. **Do not add Zone.js.**
- Signal forms; typed reactive forms only where signal forms don't fit.
- `strictTemplates` on.
- `inject()` over constructor injection.
- New control flow (`@if`, `@for`) — not `*ngIf`, not `*ngFor`.

**RTL is the primary direction, not a mirror.** Use logical properties (`margin-inline-start`, not `margin-left`). Test at mobile width. The daily log is designed mobile-first.

**Never enforce permissions in the frontend alone.** UI hiding is convenience; the server decides.

---

## Language and terminology

The UI is Arabic. Code identifiers are English, using the mapping in `spec.md` §14 — `Extract`, `Hold`, `MaterialAdvance`, `PettyCashAdvance`, `Bab`, `DayLabour`, `OwnerCurrentAccount`.

Use exactly these names. Do not invent synonyms — `Invoice` for `Extract`, or `Retention` for `Hold`, will fragment the codebase across sessions.

**Kaff's status vocabulary appears verbatim in the UI:** لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها. No translations, no substitutes.

No hardcoded user-facing strings. Everything through i18n from the first commit.

---

## Testing

**Write tests for behaviour described in `spec.md`, not for the implementation you just wrote.**

Priority order:
1. **Money** — the `spec.md` §15 worked example and its invariants. These are the acceptance criteria for the whole system.
2. **Permissions** — one test per role asserting what it cannot reach, hitting endpoints directly rather than through the UI.
3. **State machines** — every legal transition and every illegal one.
4. **End-to-end** — the slice's demo script.

If you wrote the code, you do not certify it. Verification happens in a separate session.

---

## Definition of done

- [ ] Builds clean with warnings as errors
- [ ] `spec.md` acceptance criteria for this slice pass
- [ ] Permission tests pass
- [ ] Runs on staging, not only locally
- [ ] Arabic RTL correct at mobile width
- [ ] Audit records written for every state change
- [ ] No hardcoded strings
- [ ] `decisions.md` updated if anything structural changed
- [ ] Demo script runs end to end

---

## Out of scope — do not add these

Someone will be tempted. The answer is no.

- Any tax module or ETA e-invoicing. Withholding tax is two fields and two accounts per `spec.md` §6.7 — that is the whole of it.
- Multi-company, multi-branch, multi-currency. A currency field exists; conversion logic does not.
- A general ledger with free-form manual journal entries.
- The consultant role.
- Supplier bidding, RFQ, quote comparison.
- Bank guarantee letters.
- Any endpoint that edits or deletes a posting.
- Any stored balance column.

---

## When you finish

1. Update `decisions.md` with anything structural you decided and why.
2. Note in your summary any place where `spec.md` was ambiguous — those become questions for Nabil, not silent choices.
3. List what you did **not** do, so the next session doesn't assume it exists.

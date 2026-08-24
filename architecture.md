# architecture.md — Kaff ERP

The map. `spec.md` is the business truth, `CLAUDE.md` is the rules, `decisions.md` is why things are
the way they are. This file is what is where.

---

## Shape

```
KaffErp.sln
Directory.Build.props        nullable, warnings-as-errors, analysers — applies to every project
Directory.Packages.props     one version per package, solution-wide
docker-compose.yml           local PostgreSQL

src/
  Domain/            entities · value objects · calculators · the permission rule. No EF, no ASP.NET.
  Infrastructure/    DbContext · configurations · interceptors · guard SQL · seeding
  Api/               minimal APIs in vertical slices · authorization · Program
  Web/               Angular 22, standalone, zoneless, signals, RTL-first

tests/
  Domain.Tests/      pure rules — no database
  Api.Tests/         real PostgreSQL: schema invariants, database guards, the permission gate
  E2E.Tests/         Playwright, mobile viewport, Arabic

ci/                  the static server the end-to-end job uses
scripts/             one-off operational scripts
.github/workflows/   ci.yml · deploy-staging.yml
```

Dependency direction: `Api → Infrastructure → Domain`. Domain references nothing. There is no
Application layer, no repository, no MediatR, no pass-through service — CLAUDE.md names one pattern
and forbids the alternatives.

---

## The vertical slice

```
src/Api/Features/<Area>/<Action>/
    Endpoint.cs      route, permission, filters
    Handler.cs       talks to KaffDbContext directly
    Request.cs
    Response.cs
    Validator.cs     IRequestValidator<Request>
```

Slices are discovered by scanning the assembly for `IEndpoint`, so adding a feature never means
editing a shared registration file two agents would both be editing.

Cross-feature logic moves to `Domain/`. It does not get copied.

Slice 0 ships exactly one slice — `Features/Health/GetHealth` — which reports whether the database
guards are installed on this deployment.

---

## The ledger

Everything else posts into this. It is the part to understand first.

```
Posting          id · date · fromAccount · toAccount · amount · type · sourceDocument
                 projectId? · createdBy · createdAt · reversesId?        (spec.md §6.1, exactly)
Account          the tree of spec.md §6.3, two dimensions only: project × party
AccountingPeriod month-end close; a closed period is immutable
account_balances a VIEW. The only way to read a balance.
```

**Direction is the account pair, never the sign.** `amount > 0` is a check constraint. A signed
amount plus a from/to pair would give two ways to express one movement and therefore two ways to get
it wrong.

**Postings are append-only.** No update path, no delete path, in the domain or in the database.
Corrections are `Posting.Reverse`, which mirrors the original exactly and points at it through
`reversesId`.

**Balances are derived.** There is no balance column anywhere. See `decisions.md` D-001.

### What the database enforces, and why it is not the application

`src/Infrastructure/Persistence/Sql/001_guards.sql`:

| Guard | Rule | spec.md |
|---|---|---|
| `trg_postings_append_only` | no UPDATE, DELETE or TRUNCATE on postings | §6.1 |
| `trg_audit_records_append_only` | same for the audit trail | CLAUDE.md |
| `trg_postings_validate` | postable, active, one currency, ledgers never net, hold only grows, project tag matches accounts, closed period, reversal mirrors original, a reversal cannot itself be reversed | §5.1 · §6.4 · §6.6 · §6.10 |
| `trg_postings_non_negative_balance` | signed balance of a floored account cannot go below zero — the floored types are **Safe, ClientAdvance and عهدة**, and no others (Karim, 2026-08-20) | §6.1 · §15 |
| `trg_postings_hold_release_in_full` | after any `HoldRelease` the hold is exactly zero | §5.1 |
| `trg_accounts_configuration_immutable` | account type, class, direction, ledger kind, floor and scope cannot change after creation | §6.1 · §6.3 |
| `ux_postings_reverses` | a posting is reversed once | §6.1 |
| `ux_accounts_project_dimension` | one account per type × project × party | §6.3 |

`002_views.sql` defines `account_balances`.

Both scripts are idempotent embedded resources, applied after the schema on every start-up. Outside
Development the application **refuses to start** if a guard is missing: a database that lost its
triggers serves traffic normally and passes every application-level test while enforcing nothing.

### The five ledgers

`LedgerKind` marks the five of spec.md §6.4 — client advance, hold, firm advance, عهدة, owner current
account. A trigger refuses any posting between two different ledger kinds, so no code path can net
them. `LedgerBalances` reports five separate figures and deliberately has no `Total`.

تشوينات (`MaterialAdvance`) is **not** one of the five. spec.md §6.4 lists exactly five.

---

## Permissions

```
Permission            what an endpoint can require
PermissionCatalogue   who holds it — data, every grant citing spec.md
PermissionEvaluator   the rule, as a pure synchronous function
IProjectAccessPolicy  the assignment lookup (EF)
```

`permission = role × assignment` (spec.md §9). An endpoint declares:

```csharp
app.MapPost("/api/projects/{projectId:guid}/extracts/{id:guid}/approve", ApproveExtract.HandleAsync)
   .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute());
```

`PermissionPolicyProvider` builds the policy from the encoded name, so a slice author cannot forget
to register one. The fallback policy requires an authenticated caller, so an endpoint that declares
nothing is locked rather than open.

Two axes, because spec.md uses both: `Role` (the eight of §9, **plus `Hr` as a ninth** — Karim,
2026-08-20) and `Department` / `OperationsSubDepartment` (§9's segregation — "site expenses are
entered by Finance or Admin", where Admin is a sub-department, not a role).

**Reach and capability are separate.** Owner and HR reach every project without an assignment row;
what they may *do* there is still only what the catalogue grants. HR holds exactly three permissions —
`EmployeeManage`, `ProjectAssignmentManage` and `UserRead` — and no `ProjectRead`, which is what makes
global reach safe for a role required to have zero financial visibility.

> **Updated 2026-08-22, `decisions.md` D-055 §2.** This said *"exactly two"* until `UserRead` was
> added. `UserRead` is company-wide, returns **names and roles only**, and exists because HR could
> reach every project and could not name a single person to put on one. It touches no money, holds
> nothing on a project and reaches no gate, so the zero-financial-visibility rule is unchanged. The
> count is the part that moved.

`SeparationOfDuties.EnsureDifferentActor` covers the rule role and assignment cannot express: nobody
creates and approves the same movement.

Deny by default. A permission spec.md does not assign an owner to is granted to nobody, logged at
start-up, and pinned by a test. See `decisions.md` D-012.

---

## Audit

One EF `SaveChanges` interceptor, opt-out rather than opt-in, writing in the same transaction as the
change. Actor from `ICurrentUser`; reason and correlation id from a scoped `IAuditContext`;
`[AuditRedacted]` properties written as a placeholder so a credential change is recorded without the
credential.

Audit records are append-only, enforced the same way postings are.

---

## Contract types

One `Project` entity, one treasury, one approval engine. Type selects two things and nothing else:

```
IBillingCalculator   period evidence → billable result
IProgressMetric      state → a progress reading
```

Inputs are a sealed hierarchy per type, so Cost Plus cannot receive تشوينات and Design cannot receive
a BOQ. `ProgressReading.MonetaryOnly` takes no percentage, which is how spec.md §5.2's "no percentage
progress bar" becomes a property of the type instead of a review comment.

Registration is six lines in `DependencyInjection.AddKaffContractTypes`. All six implementations are
seams today and return `billing.calculator_not_implemented`; the arithmetic is slice 5.

---

## Frontend

Standalone components, zoneless change detection, signals, new control flow, `strictTemplates`, no
NgModules, no Zone.js. `inject()` throughout.

RTL is the primary direction. Every stylesheet uses logical properties; `dir` and `lang` are written
onto the document element by an effect on the locale signal. Mobile-first, 44px tap targets.

i18n is a runtime catalogue (`public/locales/{ar,en}.json`) because the API returns error *keys*, not
sentences. `I18nService.t()` reads signals, so template expressions calling it re-render on a language
change without Zone.js.

Nothing in the frontend enforces a permission. The server decides; the client hides.

---

## Running it

```bash
docker compose up -d db

# Migrations are already generated. To add one after a model change:
# dotnet dotnet-ef migrations add <Name> \
#     --project src/Infrastructure/Kaff.Infrastructure.csproj \
#     --startup-project src/Infrastructure/Kaff.Infrastructure.csproj \
#     --output-dir Persistence/Migrations

dotnet run --project src/Api/Kaff.Api.csproj      # http://localhost:5080
cd src/Web && npm ci && npm start                  # http://localhost:4200
```

Tests. The projects are Microsoft.Testing.Platform executables, run directly — `dotnet test` reports
`Zero tests ran` on this stack (`decisions.md` D-046):

```bash
dotnet build KaffErp.sln --configuration Release

./tests/Domain.Tests/bin/Release/net10.0/Kaff.Domain.Tests
KAFF_TEST_DB="Host=localhost;Port=5432;Database=kaff;Username=kaff;Password=kaff" \
  ./tests/Api.Tests/bin/Release/net10.0/Kaff.Api.Tests
KAFF_E2E_BASE_URL=http://localhost:4200 ./tests/E2E.Tests/bin/Release/net10.0/Kaff.E2E.Tests
```

---

## What slice 0 does not contain

BOQ · extracts · change orders · daily logs · عهدة workflow · snags · handover · warranty · design
stages · the client portal · Excel import · authentication endpoints · reports · the mobile app.

The entities of spec.md §2 exist as master records; their behaviour arrives with their slices.
`Opportunity` and `Project` are deliberately thin. Nothing here should be read as an indication that
a feature is partly built — see the completion report and `decisions.md` D-026.

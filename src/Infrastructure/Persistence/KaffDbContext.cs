using System.Reflection;
using Kaff.Domain.Accounting;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Auditing;
using Kaff.Domain.Boqs;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Closure;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Sales;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence.Constants;
using Kaff.Infrastructure.Persistence.Conventions;
using Kaff.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kaff.Infrastructure.Persistence;

/// <summary>
/// The unit of work. CLAUDE.md: "EF Core's <c>DbContext</c> is the unit of work. Handlers talk to it
/// directly." There is no repository over it and none may be added.
/// </summary>
public sealed class KaffDbContext : DbContext
{
    /// <summary>
    /// The PostgreSQL sequence client codes are drawn from. Named once, so the handler that reads it
    /// and the model that declares it cannot drift apart. decisions.md D-107 §1.
    /// </summary>
    public const string ClientCodeSequence = "client_code_seq";

    /// <summary>
    /// The PostgreSQL sequence employee codes are drawn from. Same shape as
    /// <see cref="ClientCodeSequence"/> — decisions.md D-130 §6: "the code is generated; nobody typed
    /// it and nobody can edit it", drawn last by <c>CreateEmployee</c>'s handler.
    /// </summary>
    public const string EmployeeCodeSequence = "employee_code_seq";

    /// <summary>
    /// The PostgreSQL sequence subcontractor codes are drawn from. Same shape and reason as
    /// <see cref="ClientCodeSequence"/> and <see cref="EmployeeCodeSequence"/> — KAFF-211: no
    /// <c>Code</c> member on the create request, drawn last by <c>CreateSubcontractor</c>'s handler.
    /// </summary>
    public const string SubcontractorCodeSequence = "subcontractor_code_seq";

    /// <summary>
    /// The PostgreSQL sequence supplier codes are drawn from. Same shape and reason as
    /// <see cref="ClientCodeSequence"/>, <see cref="EmployeeCodeSequence"/> and
    /// <see cref="SubcontractorCodeSequence"/> — KAFF-212: no <c>Code</c> member on the create request,
    /// drawn last by <c>CreateSupplier</c>'s handler.
    /// </summary>
    public const string SupplierCodeSequence = "supplier_code_seq";

    public KaffDbContext(DbContextOptions<KaffDbContext> options)
        : base(options)
    {
    }

    // ---- Identity and access ----
    public DbSet<User> Users => Set<User>();

    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    // ---- Audit ----
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    // ---- Treasury ----
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Posting> Postings => Set<Posting>();

    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();

    /// <summary>
    /// Derived balances, read from the <c>account_balances</c> view. Keyless and read-only.
    /// There is no balance column anywhere in the schema for this to drift from.
    /// </summary>
    public DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();

    // ---- Master records (spec.md §2) ----
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Bab> Babs => Set<Bab>();

    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Subcontractor> Subcontractors => Set<Subcontractor>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Engagement> Engagements => Set<Engagement>();

    /// <summary>spec.md section 4 — a project's bill of quantities. Its lines are reached through <c>Boq.Lines</c>.</summary>
    public DbSet<Boq> Boqs => Set<Boq>();

    /// <summary>KAFF-418 — prepared base BOQs. Their lines are reached through <c>BoqBase.Lines</c>.</summary>
    public DbSet<BoqBase> BoqBases => Set<BoqBase>();

    // ---- Daily log (spec.md section 8, KAFF-600) ----
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();

    public DbSet<ExecutedQuantityEntry> ExecutedQuantityEntries => Set<ExecutedQuantityEntry>();

    public DbSet<DayLabourEntry> DayLabourEntries => Set<DayLabourEntry>();

    public DbSet<MaterialEntry> MaterialEntries => Set<MaterialEntry>();

    public DbSet<PhotoEntry> PhotoEntries => Set<PhotoEntry>();

    public DbSet<FieldEntry> FieldEntries => Set<FieldEntry>();

    public DbSet<SiteExpenseDraft> SiteExpenseDrafts => Set<SiteExpenseDraft>();

    public DbSet<Extract> Extracts => Set<Extract>();

    public DbSet<ExtractGateRecord> ExtractGateRecords => Set<ExtractGateRecord>();

    public DbSet<ExtractFigure> ExtractFigures => Set<ExtractFigure>();

    public DbSet<CollectionRecord> CollectionRecords => Set<CollectionRecord>();

    public DbSet<Cheque> Cheques => Set<Cheque>();

    public DbSet<ChangeOrder> ChangeOrders => Set<ChangeOrder>();

    public DbSet<ChangeOrderLine> ChangeOrderLines => Set<ChangeOrderLine>();

    public DbSet<ChangeOrderGateRecord> ChangeOrderGateRecords => Set<ChangeOrderGateRecord>();

    public DbSet<ExtractChangeOrderLine> ExtractChangeOrderLines => Set<ExtractChangeOrderLine>();

    public DbSet<ExtractQuantityLine> ExtractQuantityLines => Set<ExtractQuantityLine>();

    public DbSet<Snag> Snags => Set<Snag>();

    public DbSet<Ncr> Ncrs => Set<Ncr>();

    public DbSet<WarrantyCallback> WarrantyCallbacks => Set<WarrantyCallback>();

    public DbSet<HoldReleaseRequest> HoldReleaseRequests => Set<HoldReleaseRequest>();
    public DbSet<SubcontractorRetentionReleaseRequest> SubcontractorRetentionReleaseRequests => Set<SubcontractorRetentionReleaseRequest>();

    public DbSet<Adjustment> Adjustments => Set<Adjustment>();

    public DbSet<AssetRegisterEntry> AssetRegisterEntries => Set<AssetRegisterEntry>();

    public DbSet<Accrual> Accruals => Set<Accrual>();

    public DbSet<PrepaymentSchedule> PrepaymentSchedules => Set<PrepaymentSchedule>();

    public DbSet<BudgetBaseline> BudgetBaselines => Set<BudgetBaseline>();

    public DbSet<AccountingRequest> AccountingRequests => Set<AccountingRequest>();

    /// <summary>KAFF-321 — staff departments, master data, superseding the D-153 §2 enum.</summary>
    public DbSet<Department> Departments => Set<Department>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // CLAUDE.md: "Never use float or double anywhere near money. decimal only, and EF precision
        // configured explicitly. EF Core silently truncates decimals when precision isn't configured.
        // Every money property gets this. No exceptions."
        //
        // Applied as a convention rather than property by property, so a Money added in a later
        // session inherits it and cannot be forgotten. Covers Money? as well as Money.
        configurationBuilder.Properties<Money>()
            .HaveConversion<MoneyConverter>()
            .HavePrecision(18, 4);

        configurationBuilder.Properties<Percentage>()
            .HaveConversion<PercentageConverter>()
            .HavePrecision(18, Percentage.Scale);

        // Bare decimals are only used for non-money quantities (area in m²). Give them an explicit
        // precision too, so no decimal column in this database is ever provider-default.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);

        // Deliberately no global string length. PostgreSQL treats text and varchar(n) identically,
        // and a blanket default would silently cap the audit trail's JSON columns.
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // The client-code sequence of spec.md §2's amendment — "sequential, of the form C-10001,
        // manual entry and later editing both forbidden" — drawn by CreateClient's handler
        // immediately before it saves. decisions.md D-107 §1.
        //
        // Declared here rather than in hand-written migration SQL, and that is the load-bearing part.
        // DatabaseInitializer runs two strategies: the Api migrates on boot, the test harness calls
        // EnsureCreated, which builds the schema from this model and knows nothing about migrations.
        // A sequence authored only in a migration would exist in production and not exist under the
        // harness, and every registration in the Api suite would fail on 42P01 — observed, on
        // 2026-09-04, by deleting this line. See SchemaInvariantTests
        // .The_client_code_sequence_is_materialised_by_a_schema_built_from_the_model.
        //
        // A sequence is non-transactional, so a rolled-back save burns a number and that code never
        // exists. Whether Kaff will accept gaps is Karim's, unanswered, and the mechanism is one
        // expression in one handler either way — see decisions.md D-107's open question 1.
        modelBuilder.HasSequence<long>(ClientCodeSequence).StartsAt(10_001);

        // The employee-code sequence — decisions.md D-130 §6, the same precedent and the same reason
        // this must be declared on the model rather than in migration SQL alone (see the comment
        // above ClientCodeSequence): the test harness builds its schema from this model and never
        // runs a migration.
        modelBuilder.HasSequence<long>(EmployeeCodeSequence).StartsAt(10_001);

        // KAFF-211, same precedent as the two sequences above.
        modelBuilder.HasSequence<long>(SubcontractorCodeSequence).StartsAt(10_001);

        // KAFF-212, same precedent as the three sequences above.
        modelBuilder.HasSequence<long>(SupplierCodeSequence).StartsAt(10_001);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            ApplyEnumsAsStrings(entityType);
            ApplySnakeCaseColumns(entityType);
        }
    }

    /// <summary>
    /// Stores enums as text.
    /// </summary>
    /// <remarks>
    /// The database guards are written in SQL and read these columns —
    /// <c>ledger_kind = 'Hold'</c> is a rule a person can check, <c>ledger_kind = 2</c> is a rule that
    /// silently inverts the day somebody reorders the enum. Renaming a member becomes a data
    /// migration, which is the correct amount of friction for a vocabulary that spec.md fixes.
    /// </remarks>
    private static void ApplyEnumsAsStrings(IMutableEntityType entityType)
    {
        foreach (IMutableProperty property in entityType.GetProperties())
        {
            Type clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

            if (!clrType.IsEnum)
            {
                continue;
            }

            Type converterType = typeof(EnumToStringConverter<>).MakeGenericType(clrType);
            var converter = (ValueConverter)Activator.CreateInstance(converterType)!;

            property.SetValueConverter(converter);
            property.SetMaxLength(DbLimits.EnumLength);
        }
    }

    private static void ApplySnakeCaseColumns(IMutableEntityType entityType)
    {
        foreach (IMutableProperty property in entityType.GetProperties())
        {
            property.SetColumnName(SnakeCase.Convert(property.Name));
        }
    }
}

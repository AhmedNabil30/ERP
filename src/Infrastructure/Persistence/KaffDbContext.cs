using System.Reflection;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
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

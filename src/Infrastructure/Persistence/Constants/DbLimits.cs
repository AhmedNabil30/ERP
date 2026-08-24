namespace Kaff.Infrastructure.Persistence.Constants;

/// <summary>Column length and table name constants, in one place so SQL and C# agree.</summary>
internal static class DbLimits
{
    /// <summary>Enum members are stored as text; nothing in spec.md's vocabulary is longer than this.</summary>
    public const int EnumLength = 64;

    /// <summary>Free text a user types: an address, a rejection reason, a note.</summary>
    public const int LongTextLength = 2000;
}

/// <summary>
/// Physical table names. The guard SQL references these strings, so they live next to the mapping
/// rather than being typed twice.
/// </summary>
internal static class DbTables
{
    public const string Users = "users";
    public const string ProjectAssignments = "project_assignments";
    public const string AuditRecords = "audit_records";
    public const string Accounts = "accounts";
    public const string Postings = "postings";
    public const string AccountingPeriods = "accounting_periods";
    public const string AccountBalancesView = "account_balances";
    public const string Clients = "clients";
    public const string Babs = "babs";
    public const string CatalogueItems = "catalogue_items";
    public const string Employees = "employees";
    public const string Subcontractors = "subcontractors";
    public const string Suppliers = "suppliers";
    public const string Opportunities = "opportunities";
    public const string Projects = "projects";
}

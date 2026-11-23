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
    public const string Engagements = "engagements";
    public const string Boqs = "boqs";
    public const string BoqLines = "boq_lines";
    public const string BoqBases = "boq_bases";
    public const string BoqBaseLines = "boq_base_lines";
    public const string DailyLogs = "daily_logs";
    public const string ExecutedQuantityEntries = "executed_quantity_entries";
    public const string DayLabourEntries = "day_labour_entries";
    public const string MaterialEntries = "material_entries";
    public const string PhotoEntries = "photo_entries";
    public const string FieldEntries = "field_entries";
    public const string SiteExpenseDrafts = "site_expense_drafts";
    public const string Extracts = "extracts";
    public const string ExtractGateRecords = "extract_gate_records";
    public const string ExtractFigures = "extract_figures";
    public const string CollectionRecords = "collection_records";
    public const string Cheques = "cheques";
    public const string ChangeOrders = "change_orders";
    public const string ChangeOrderLines = "change_order_lines";
    public const string ChangeOrderGateRecords = "change_order_gate_records";
    public const string ExtractChangeOrderLines = "extract_change_order_lines";
    public const string ExtractQuantityLines = "extract_quantity_lines";
    public const string Snags = "snags";
    public const string Ncrs = "ncrs";
    public const string WarrantyCallbacks = "warranty_callbacks";
    public const string HoldReleaseRequests = "hold_release_requests";
    public const string SubcontractorRetentionReleaseRequests = "subcontractor_retention_release_requests";
    public const string Adjustments = "adjustments";
}

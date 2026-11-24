using Kaff.Domain.Common;

namespace Kaff.Domain.Sales;

/// <summary>Refusals of the sales pipeline (spec.md §3, §13).</summary>
public static class SalesErrors
{
    public static readonly Error OpportunityNotFound =
        Error.NotFound("sales.opportunity_not_found", "errors.sales.opportunity_not_found");

    /// <summary>The opportunity is on hold or closed, so it cannot move, be held or be put on hold again.</summary>
    public static readonly Error OpportunityNotLive =
        Error.Conflict("sales.opportunity_not_live", "errors.sales.opportunity_not_live");

    public static readonly Error AlreadyAtLastStage =
        Error.Conflict("sales.already_at_last_stage", "errors.sales.already_at_last_stage");

    public static readonly Error OpportunityNotOnHold =
        Error.Conflict("sales.opportunity_not_on_hold", "errors.sales.opportunity_not_on_hold");

    public static readonly Error OpportunityAlreadyClosed =
        Error.Conflict("sales.opportunity_already_closed", "errors.sales.opportunity_already_closed");

    public static readonly Error OpportunityNotClosedLost =
        Error.Conflict("sales.opportunity_not_closed_lost", "errors.sales.opportunity_not_closed_lost");

    /// <summary>The list's stage or status filter named something that is not one.</summary>
    public static readonly Error OpportunityListFilterUnknown =
        Error.Validation("sales.opportunity_list_filter_unknown", "errors.sales.opportunity_list_filter_unknown");
}

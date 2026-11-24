using Kaff.Domain.Sales;

namespace Kaff.Api.Features.Opportunities.ListOpportunities;

/// <summary>
/// One row. <c>Status</c> is the status as read now: an Active opportunity quiet past the Stalled window reads
/// Stalled. <c>Inactivity</c> is the quiet flag, derived on read. Neither is stored.
/// </summary>
public sealed record OpportunitySummary(
    Guid Id,
    string Code,
    Guid ClientId,
    string ClientName,
    string ClientPhone,
    string Title,
    OpportunityStage Stage,
    OpportunityStatus Status,
    InactivityLevel Inactivity,
    string? ClosedLostReason,
    DateTimeOffset LastActivityAt);

public sealed record Response(IReadOnlyList<OpportunitySummary> Opportunities);

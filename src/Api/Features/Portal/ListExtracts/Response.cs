namespace Kaff.Api.Features.Portal.ListExtracts;

/// <summary>
/// One extract the client has been issued (R5-B AC-B-4, PortalExtractVisibility). The work certified, what is payable, and what he has
/// paid on it. No hold, advance, deduction line or review history: those are Kaff's working, and the client owes the net.
/// </summary>
public sealed record PortalExtract(
    Guid Id,
    int Number,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CertifiedWork,
    string NetPayable,
    string Paid,
    bool IsPaid);

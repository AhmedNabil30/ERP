using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.ProofOfNumbers;

/// <summary>The proof screen's whole read. Every amount is a <see cref="Money"/> (a JSON string, D-135).</summary>
/// <param name="ExpectedState">
/// <c>NoExtractsToCompare</c> while no event carries an expectation (a designed state, not a failure), otherwise
/// <c>Available</c>. With <c>Available</c> an individual event can still have a null expectation (see
/// <see cref="ProofEvent.ExpectedUnavailableReason"/> and the non-extract events).
/// </param>
/// <param name="ProjectCashNet">
/// Net of this project's postings into and out of the safe. Not the safe's balance, which is company-wide.
/// </param>
public sealed record Response(
    DateTimeOffset ReadAt,
    ProofProject Project,
    string ExpectedState,
    Money ProjectCashNet,
    IReadOnlyList<ProofEvent> Events,
    IReadOnlyList<ProofCheck> Checks,
    IReadOnlyList<ProofLedger> Ledgers,
    ProofUnclassified Unclassified,
    IReadOnlyList<ProofRuling> Rulings,
    IReadOnlyList<ProofPosting> Postings);

/// <summary>Contract terms the checks are measured against. <c>ContractValue</c> is null until a BOQ is signed.</summary>
public sealed record ProofProject(
    Guid Id,
    string Code,
    string Name,
    ContractType ContractType,
    Money? ContractValue,
    string HoldRatePercent,
    string AdvanceRecoveryRatePercent);

/// <summary>
/// The five comparison columns. Sign is the effect on the payment: a deduction is negative.
/// <c>Work</c>: certified value. <c>Hold</c>: accrual negative, release positive. <c>Advance</c>:
/// recovery negative. <c>MaterialAdvance</c>: issue positive, recovery negative. <c>Collection</c>:
/// cash received from the client.
/// </summary>
public sealed record ProofColumns(Money Work, Money Hold, Money Advance, Money MaterialAdvance, Money Collection);

/// <summary>
/// One source document (an extract, a collection, a handover) and what its postings did. <c>Expected</c>
/// and <c>Difference</c> (actual minus expected, computed here and never in the browser) are null when no calculator
/// stands behind the event (anything that is not an extract) or the calculator refused.
/// </summary>
/// <param name="ExpectedUnavailableReason">
/// The calculator's error code when it refused this extract (for example <c>billing.terms_missing</c>); null otherwise.
/// Additive: absent readers are unaffected.
/// </param>
public sealed record ProofEvent(
    SourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    string? Reference,
    DateOnly Date,
    ProofColumns Actual,
    ProofColumns? Expected,
    ProofColumns? Difference,
    IReadOnlyList<Guid> PostingIds,
    string? ExpectedUnavailableReason = null);

/// <summary>
/// One live invariant. <see cref="Status"/> is <c>Match</c>, <c>Mismatch</c>, <c>Pending</c> (the contract
/// has not reached the point where it can be judged) or <c>Unavailable</c> (an input is missing, e.g. no
/// signed contract value). <see cref="Difference"/> is actual minus expected.
/// </summary>
public sealed record ProofCheck(
    string Key,
    string Status,
    Money? Expected,
    Money? Actual,
    Money? Difference,
    IReadOnlyList<Guid> PostingIds);

/// <summary>A ledger tile: the balance as the account view derives it, and the sum of its own postings' effect.</summary>
public sealed record ProofLedger(string Key, Money Balance, Money PostingsSum, IReadOnlyList<Guid> PostingIds);

/// <summary>Amount of ledger-column postings between two accounts that carry no ledger kind. Must be zero.</summary>
public sealed record ProofUnclassified(Money Amount, IReadOnlyList<Guid> PostingIds);

/// <summary>Whether a business default on this screen is still assumed (<c>Assumed</c>) or ruled (<c>Ruled</c>).</summary>
public sealed record ProofRuling(string Key, string Status);

/// <summary>One posting, with the ledgers it touches (if any) and its signed effect on each.</summary>
public sealed record ProofPosting(
    Guid Id,
    DateOnly Date,
    PostingType Type,
    SourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    string? SourceDocumentReference,
    string FromAccount,
    string ToAccount,
    Money Amount,
    Guid? ReversesId,
    IReadOnlyList<ProofPostingEffect> Effects);

/// <summary>A posting's signed effect on one ledger, in that ledger's own direction.</summary>
public sealed record ProofPostingEffect(string LedgerKey, Money Effect);

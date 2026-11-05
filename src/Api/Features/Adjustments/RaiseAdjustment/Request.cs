using Kaff.Domain.Adjustments;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Adjustments.RaiseAdjustment;

/// <summary>
/// Raises an adjustment (spec.md section 6.9): type, reason, source document and target account, plus the amount.
/// </summary>
/// <param name="Type">The adjustment type. Only the client credit note is built.</param>
/// <param name="Reason">Why. Required and stored.</param>
/// <param name="SourceExtractId">The extract the credit note credits (the source document).</param>
/// <param name="TargetAccountId">The account the credit lands on: the project's contract revenue account.</param>
/// <param name="Amount">A positive amount. A negative or zero amount is refused.</param>
public sealed record Request(
    AdjustmentType? Type, string? Reason, Guid? SourceExtractId, Guid? TargetAccountId, Money Amount);

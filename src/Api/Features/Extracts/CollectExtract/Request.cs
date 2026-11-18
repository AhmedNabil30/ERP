using Kaff.Domain.Collections;
using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// The client paid an issued extract, in full or in part. Method, date and reference are all required (spec.md section
/// 6.5 rule 1), and so is the amount received (D-235 item 2); they are nullable here only so a missing one is refused
/// with a translated message instead of binding to a default.
/// </summary>
/// <param name="Method">Cash, Cheque or Transfer. The recorded fact, never inferred.</param>
/// <param name="Date">The day the client paid.</param>
/// <param name="Reference">The receipt, transfer or cheque number.</param>
/// <param name="CashReceived">What the client paid by this method. For a cheque, its face value: cash only when it clears.</param>
/// <param name="WithheldAtSource">What the client withheld at source on this payment. Zero when absent.</param>
public sealed record Request(
    CollectionMethod? Method, DateOnly? Date, string? Reference, Money? CashReceived = null, Money? WithheldAtSource = null);

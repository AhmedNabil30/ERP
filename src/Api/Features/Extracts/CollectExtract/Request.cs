using Kaff.Domain.Collections;

namespace Kaff.Api.Features.Extracts.CollectExtract;

/// <summary>
/// The client paid an issued extract. Method, date and reference are all required (spec.md section 6.5 rule 1);
/// they are nullable here only so a missing one is refused with a translated message instead of binding to a
/// default. No amount is carried: the collection records what the extract's calculation expects (Q98, Q100).
/// </summary>
/// <param name="Method">Cash, Cheque or Transfer. The recorded fact, never inferred.</param>
/// <param name="Date">The day the client paid.</param>
/// <param name="Reference">The receipt, transfer or cheque number.</param>
public sealed record Request(CollectionMethod? Method, DateOnly? Date, string? Reference);

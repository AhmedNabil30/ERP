using System.Text.Json.Serialization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Progress;

namespace Kaff.Api.Features.Projects.GetProgress;

/// <summary>One Bab of a Lump Sum project and how far it has got.</summary>
/// <param name="Code">The Bab code copied onto the BOQ lines.</param>
/// <param name="Completion">Absent when the Bab has no contract value to measure against.</param>
/// <param name="ExecutedValue">Money: absent for a caller who may not see money.</param>
/// <param name="IsActive">Work has begun and it is not complete (ruling <c>BabActivity</c>).</param>
public sealed record BabProgress(
    string Code,
    string NameAr,
    string NameEn,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Percentage? Completion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? ExecutedValue,
    bool IsActive);

/// <summary>
/// A progress reading. <b>Every member after <see cref="AsOf"/> is absent, not zero, when it does not apply.</b>
/// A Cost Plus reading has <see cref="ProgressKind.MonetaryOnly"/> and no <see cref="Completion"/> property at all
/// (spec.md section 5.2: no percentage progress bar), because <c>ProgressReading.MonetaryOnly</c> cannot carry one
/// and the null is never written. A client that finds no <c>completion</c> must draw no bar.
/// </summary>
/// <param name="Completion">A fraction: 0.5 is 50%. Present only for the percentage and stage-weighted kinds.</param>
/// <param name="ExecutedValue">Lump Sum: executed quantity priced at the frozen BOQ rate. Money.</param>
/// <param name="ContractValue">Lump Sum: the signed contract value the completion is measured against. Money.</param>
/// <param name="CertifiedToDate">Lump Sum: the certified value of the extracts issued to the client. Money, informational.</param>
/// <param name="CostToDate">Cost Plus: cost paid against the project. Money.</param>
/// <param name="SupervisionToDate">Cost Plus: supervision on that cost. Money.</param>
public sealed record ProgressResponse(
    ProgressKind Kind,
    DateOnly AsOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Percentage? Completion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? ExecutedValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? ContractValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? CertifiedToDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? CostToDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Money? SupervisionToDate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BabProgress>? Babs);

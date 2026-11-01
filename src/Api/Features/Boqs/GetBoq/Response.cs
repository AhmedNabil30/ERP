using Kaff.Domain.Common;

namespace Kaff.Api.Features.Boqs.GetBoq;

/// <param name="Total">
/// The BOQ total on the ruled quantity basis. Null while any line lacks that quantity, so a half-entered
/// BOQ never shows a figure that looks like a contract value.
/// </param>
public sealed record Response(
    Guid Id,
    Guid ProjectId,
    DateOnly? FrozenOn,
    Money? Total,
    IReadOnlyList<BoqLineResponse> Lines);

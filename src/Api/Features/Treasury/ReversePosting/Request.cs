using Kaff.Domain.Common;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// What Finance sends to reverse an existing posting. KAFF-303.
/// </summary>
/// <remarks>
/// The posting being reversed travels in the route (<c>{id}</c>), not here — same reasoning as
/// <c>PostMovement.Request</c>'s missing <c>ProjectId</c>: the route is what a caller cannot lie to
/// authorization about. Everything else about the reversal — accounts, amount, type, source document
/// — is copied verbatim from the original by <c>Posting.Reverse</c> (rule 2: a reversal is a full
/// mirror, never a free-form correction). The only thing left for a caller to say is when it happens.
/// </remarks>
/// <param name="PostingDate">
/// The accounting date the reversal books against. Distinct from the original's own
/// <c>PostingDate</c> — a correction made today reverses a posting from last month, but is dated
/// today's open period (<c>AC-303-E</c>).
/// </param>
public sealed record Request(DateOnly PostingDate);

using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.PostMovement;

/// <summary>
/// What Finance sends to move value between two named accounts. KAFF-301.
/// </summary>
/// <remarks>
/// <b>There is deliberately no <c>ProjectId</c> member.</b> <c>ProjectScope</c>'s own remarks
/// (<c>src/Api/Authorization/ProjectScope.cs</c>) say the body is excluded from authorization by
/// design — a body has to be parsed before authorization can run, which would read an unauthorised
/// request before refusing it. The project this movement belongs to therefore travels in the route,
/// not here: <c>POST /api/treasury/postings</c> for a company-level movement, or
/// <c>POST /api/projects/{projectId}/treasury/postings</c> for a project-tagged one. See
/// <c>Endpoint.cs</c> for both routes and the permission each one requires.
/// </remarks>
/// <param name="FromAccountId">The account the value leaves.</param>
/// <param name="ToAccountId">The account the value lands in.</param>
/// <param name="Amount">Always positive — direction lives in the account pair, never the sign (rule 3).</param>
/// <param name="Type">Sent as the member name, per every other enum on the wire.</param>
/// <param name="PostingDate">
/// The accounting date. Distinct from when the request was made — a back-dated posting is checked
/// against the accounting period it names, not against today (<c>AC-301-H</c>).
/// </param>
/// <param name="SourceDocumentType">The kind of business document this movement traces to.</param>
/// <param name="SourceDocumentId">That document's id.</param>
/// <param name="SourceDocumentReference">Optional human-visible reference, e.g. an extract number.</param>
public sealed record Request(
    Guid FromAccountId,
    Guid ToAccountId,
    Money Amount,
    PostingType Type,
    DateOnly PostingDate,
    SourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    string? SourceDocumentReference);

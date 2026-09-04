namespace Kaff.Api.Features.Clients.PhoneCheck;

/// <summary>
/// Who already holds this number. Empty when nobody does.
/// </summary>
/// <remarks>
/// <b>A 200 either way.</b> The warning is not a refusal and must not arrive as one — spec.md §2's
/// amendment says the system asks, and a ProblemDetails could not name the matched client in any
/// case, because the SPA keeps only <c>status</c>, <c>code</c> and <c>messageKey</c> from one
/// [Verified: 2026-09-04 @ <c>src/Web/src/app/core/api/problem-details.ts</c> -&gt; <c>toProblem</c>].
/// </remarks>
/// <param name="Matches">
/// Every match, archived included, ordered by code. A list rather than a single match because
/// D-049 ruling 8 makes several normal — one number can legitimately be a company's and its CEO's.
/// </param>
public sealed record Response(IReadOnlyList<PhoneMatch> Matches);

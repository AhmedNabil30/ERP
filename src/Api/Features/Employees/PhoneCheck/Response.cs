using Kaff.Api.Common;

namespace Kaff.Api.Features.Employees.PhoneCheck;

/// <summary>
/// Who already holds this number. Empty when nobody does.
/// </summary>
/// <remarks>
/// <b>A 200 either way</b> — same reasoning as <c>Clients.PhoneCheck.Response</c>. Every match's name
/// is returned regardless of <c>Kind</c>; the caller does not learn which kind a match is
/// (decisions.md D-146 point 3 — "not added: a kind on PhoneMatch"). On a salaried form, the refusal
/// itself only appears when the operator submits.
/// </remarks>
/// <param name="Matches">Every match, archived included, ordered by code.</param>
public sealed record Response(IReadOnlyList<PhoneMatch> Matches);

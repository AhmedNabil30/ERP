using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.ListClients;

/// <summary>
/// One client, as a row in the list. KAFF-124.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately the same six members as <c>CreateClient.Response</c> and
/// <c>EditClient.Response</c>.</b> Three client-shaped payloads that differ would be three separate
/// things to keep money out of; one shape is one whitelist, asserted in one place.
/// </para>
/// <para>
/// <b>No <c>Notes</c>.</b> spec.md §12 — the client MUST NEVER see internal notes — and this is the
/// payload with the widest reach in the slice: it returns <i>every</i> client in Kaff. KAFF-124 rule
/// 4 closes it to <c>Role.Client</c>, and the omission is the second lock (KAFF-121 rule 8).
/// </para>
/// <para>
/// <b>No money, and rule 5 is about what this must not <i>join</i>.</b> The entity carries no balance
/// to project, so the risk here is not a column — it is a later hand adding "total billed" to a list
/// screen because a list screen is where somebody will want it. spec.md §6.1 and CLAUDE.md: balances
/// are derived by summing postings, and a projection that computes one here is the same defect as a
/// stored one. <c>AC-124-G</c> is a whitelist, not a search for suspect words (decisions.md D-106).
/// </para>
/// </remarks>
/// <param name="Id">The client.</param>
/// <param name="Code">Generated, sequential. Searchable, and never editable.</param>
/// <param name="Name">As Marketing entered it. Arabic, normally.</param>
/// <param name="Phone">The entered form, not the normalised key — what the operator typed and what support calls.</param>
/// <param name="Kind">Individual or Corporate.</param>
/// <param name="IsActive">False for an archived client, which the default filter excludes (rule 2).</param>
public sealed record ClientSummary(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    ClientKind Kind,
    bool IsActive);

/// <summary>
/// The clients that matched. KAFF-124.
/// </summary>
/// <remarks>
/// <b>A list, never "the client with this number"</b> — KAFF-124 rule 1b. Duplicates are permitted
/// (D-049 ruling 8, spec.md §2 amended), so a phone search legitimately returns more than one, and a
/// contract shaped as a single optional client would have to pick one of them. <c>AC-124-B</c> is the
/// criterion: both come back, and neither is silently preferred.
/// </remarks>
/// <param name="Clients">Ordered by code, so the same search returns the same order twice. Empty when nothing matched — <c>AC-124-H</c>.</param>
public sealed record Response(IReadOnlyList<ClientSummary> Clients);

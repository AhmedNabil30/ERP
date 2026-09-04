using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.GetClient;

/// <summary>
/// One client's whole editable file. KAFF-126's S-014 needs it; nothing else does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wider than <c>ClientSummary</c> on purpose, and the difference is the reason this type exists
/// rather than the list row being widened.</b> The list returns every client Marketing can see; this
/// returns one they asked for by id. Putting <c>Notes</c> on the list row would ship every internal
/// note about every client to a screen that shows none of them — spec.md §12 is absolute, and the
/// narrowest payload that serves the screen is the one that cannot leak.
/// </para>
/// <para>
/// <b>Still no money, and still no withholding category.</b> Balances are derived by summing postings
/// and there is none on the entity to project (spec.md §6.1, CLAUDE.md); the withholding category
/// moved to the contract on 2026-08-21. The field set is pinned by a whitelist rather than a search
/// for suspect words — decisions.md D-106.
/// </para>
/// <para>
/// <b>Found by writing the screen, not by reading the story</b> — KAFF-124 shipped a list and
/// KAFF-121 shipped a `PUT`, and between them there was no way to load the record the `PUT` edits.
/// S-014 is reachable by URL, so router state cannot stand in for it. decisions.md D-113 §1.
/// </para>
/// </remarks>
/// <param name="Id">The client.</param>
/// <param name="Code">Generated. Rendered read-only, never as a disabled input (S-014).</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="Phone">The entered form, not the normalised key.</param>
/// <param name="Kind">Individual or Corporate.</param>
/// <param name="AlternatePhone">Optional. Not matched on — only the primary phone deduplicates.</param>
/// <param name="Email">Optional.</param>
/// <param name="Address">Optional.</param>
/// <param name="TaxRegistrationNumber">Corporate only. Identity, not rate.</param>
/// <param name="Notes">
/// <b>Internal.</b> This is the only payload in the slice that carries them, it is gated
/// <c>ClientManage</c>, and <c>Role.Client</c> does not hold that row. spec.md §12.
/// </param>
/// <param name="IsActive">False for an archived client.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    ClientKind Kind,
    string? AlternatePhone,
    string? Email,
    string? Address,
    string? TaxRegistrationNumber,
    string? Notes,
    bool IsActive);

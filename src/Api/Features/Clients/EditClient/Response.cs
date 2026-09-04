using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.EditClient;

/// <summary>
/// The client as it now stands. KAFF-121.
/// </summary>
/// <remarks>
/// <para>
/// <b>No money field, ever</b> — <c>AC-119-I</c>'s rule applies to every client-shaped payload, not
/// only the one it was written against: balances are derived by summing postings and there is no
/// stored one to expose (spec.md §6.1, CLAUDE.md). <b>And no withholding category</b>, which moved
/// to the contract on 2026-08-21. The field set is pinned by a <c>BeEquivalentTo</c> whitelist rather
/// than by a search for suspect words — decisions.md D-106, where a seven-word blocklist let a
/// <c>decimal RetainedAmount</c> onto the wire against a green suite.
/// </para>
/// <para>
/// <b>No <c>Notes</c> member, and that is <c>AC-121-H</c> rather than an oversight.</b> spec.md §12
/// says the client MUST NEVER see internal notes. This route is closed to <c>Role.Client</c> by its
/// permission, so the omission is a second lock on a door that is already bolted — which is the
/// point: §12 is absolute, and a payload that carries notes is one route re-gating away from leaking
/// them.
/// </para>
/// <para>
/// The duplicate warning is not echoed here, for the same reason it is not echoed on registration:
/// if a match was acknowledged, that is a fact about the decision and it lives in the audit trail.
/// </para>
/// </remarks>
/// <param name="Id">The client.</param>
/// <param name="Code">Unchanged, always — <c>AC-121-E</c>. It is returned so the screen can show that it did not move.</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="Phone">The entered form, not the normalised key. It is what the operator typed and what support calls.</param>
/// <param name="Kind">Individual or Corporate.</param>
/// <param name="IsActive">Untouched by an edit — KAFF-121 rule 9. Archiving is KAFF-123.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    ClientKind Kind,
    bool IsActive);

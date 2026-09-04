using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Clients.CreateClient;

/// <summary>
/// The registered client, as the screen that just created it needs to see it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No money field, ever — not a balance, not a credit limit, not a figure of any name.</b>
/// <c>AC-119-I</c>, spec.md §6.1 and CLAUDE.md: balances are derived by summing postings and there is
/// no stored one to expose. <b>And no withholding category</b> (<c>AC-119-J</c>): it moved to the
/// contract on 2026-08-21 and the entity has no such member. The field set is pinned by a
/// <c>BeEquivalentTo</c> whitelist rather than by a search for suspect words — decisions.md D-106,
/// where a seven-word blocklist let a <c>decimal RetainedAmount</c> onto the wire against a green
/// 241/241 suite, because <c>Amount</c> was not one of the seven.
/// </para>
/// <para>
/// The duplicate warning is not echoed here. If a match was acknowledged, that is a fact about the
/// decision and it lives in the audit trail; the screen already knows, because it is what the
/// operator clicked through.
/// </para>
/// </remarks>
/// <param name="Id">The new client.</param>
/// <param name="Code">Generated, sequential, <c>C-10001</c>. Never supplied and never editable.</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="Phone">The entered form, not the normalised key. It is what the operator typed and what support calls.</param>
/// <param name="Kind">Individual or Corporate.</param>
/// <param name="IsActive">Always true on registration — <c>Client.Create</c> says so.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    ClientKind Kind,
    bool IsActive);

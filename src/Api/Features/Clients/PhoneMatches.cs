using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients;

/// <summary>
/// One client already holding the phone number that was typed.
/// </summary>
/// <remarks>
/// <b>The name is the point.</b> spec.md §2's amendment says the system <i>"shows the operator which
/// client already holds it"</i>, and KAFF-119 rule 4 says so again: a warning that says only "this
/// number exists" is not what was ruled. <see cref="IsArchived"/> is here because rule 6 requires the
/// warning to fire on an archived client and to say that it is one — §3 attaches a reopened
/// opportunity to the original client, so an archived match is exactly the case the operator most
/// needs to see.
/// </remarks>
/// <param name="Id">The matched client.</param>
/// <param name="Code">Its generated code, e.g. <c>C-10001</c>.</param>
/// <param name="Name">Its name, as Marketing entered it. Arabic, normally.</param>
/// <param name="IsArchived">Derived from <c>Client.IsActive</c>; there is no archived column.</param>
public sealed record PhoneMatch(Guid Id, string Code, string Name, bool IsArchived);

/// <summary>
/// The one query behind the duplicate-phone warning.
/// </summary>
/// <remarks>
/// <para>
/// <b>One query, two callers, and they must not be able to disagree.</b>
/// <c>POST /api/clients/phone-check</c> produces the warning the operator reads, and
/// <c>POST /api/clients</c> re-runs the same match server-side to decide whether the
/// acknowledgement flag means anything. A match found by one and missed by the other is a warning
/// nobody sees or an acknowledgement of nothing.
/// </para>
/// <para>
/// <b>It is not KAFF-124's search.</b> That is a fuzzy search across name, code and phone with an
/// archived filter; this is exact equality on the normalised phone, archived rows included.
/// Conflating them means a later change to search ranking silently changes what warns
/// (decisions.md D-107 §2).
/// </para>
/// <para>
/// <b>Not a repository and not a service layer</b> — CLAUDE.md forbids both. One static method
/// returning data, called directly, living beside the two slices that share it rather than in
/// <c>Domain/</c>, which has no EF Core reference at all. The only <i>domain</i> logic in the match
/// is normalisation, and that is already shared and uncopied
/// [Verified: 2026-09-04 @ <c>src/Domain/Common/PhoneNumber.cs</c> -&gt; <c>Normalise</c>].
/// </para>
/// </remarks>
internal static class PhoneMatches
{
    /// <summary>
    /// Every client whose normalised phone equals <paramref name="normalisedPhone"/>, archived
    /// included, ordered by code so the same request warns in the same order twice.
    /// </summary>
    /// <param name="excluding">
    /// A client that is not a match against itself. <b>KAFF-121's edit path is why this exists</b>
    /// (decisions.md D-107 §2, deliberately left unbuilt by KAFF-119): a client saved with its phone
    /// unchanged matches its own row, and without this the operator is asked to acknowledge a
    /// duplicate of the record in front of them — every single time, on an edit that changed the
    /// address. Worse, acknowledging it would write a <c>DuplicatePhoneAcknowledged</c> row pointing
    /// at the client itself, into an append-only table. Null on the registration path, where there is
    /// no self to exclude.
    /// </param>
    /// <remarks>
    /// The comparison is against <c>PhoneNormalised</c> and never against the entered text, so
    /// <c>+20 100 123 4567</c>, <c>0020 100 1234567</c> and <c>01001234567</c> are one number
    /// (KAFF-119 rule 5). <c>ix_clients_phone</c> is the index it reads, kept when the unique
    /// constraint was dropped precisely so this stays fast.
    /// </remarks>
    public static async Task<List<PhoneMatch>> FindAsync(
        KaffDbContext database,
        string normalisedPhone,
        CancellationToken cancellationToken,
        Guid? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return await database.Clients
            .Where(client => client.PhoneNormalised == normalisedPhone)
            .Where(client => excluding == null || client.Id != excluding)
            .OrderBy(client => client.Code)
            .Select(client => new PhoneMatch(client.Id, client.Code, client.Name, !client.IsActive))
            .ToListAsync(cancellationToken);
    }
}

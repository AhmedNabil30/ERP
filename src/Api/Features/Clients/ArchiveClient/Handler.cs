using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.ArchiveClient;

/// <summary>
/// Archives one client. KAFF-123.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three lines of work, and the refusal is the entity's.</b> <c>Client.Archive</c> refuses an
/// already-archived client with <c>errors.master.already_archived</c> (<c>AC-123-C</c>); this handler
/// returns what it says rather than checking <c>IsActive</c> first, because a second copy of the rule
/// in a handler is the copy that drifts from the entity every other caller goes through.
/// </para>
/// <para>
/// <b>Archiving is not an edit and an edit cannot archive</b> — KAFF-121 rule 9. This is its own act
/// with its own meaning in the trail, which is why <c>EditClient.Request</c> carries no
/// <c>IsActive</c> member and this route exists at all.
/// </para>
/// <para>
/// <b>The phone stays in the duplicate check</b> (<c>AC-123-B</c>, KAFF-123 rule 2). Nothing here
/// touches the phone and <c>PhoneMatches.FindAsync</c> filters on nothing but the normalised number,
/// so an archived client still warns and still says it is archived. That is not incidental: spec.md
/// §3 attaches a reopened opportunity to the <i>original</i> client, so the archived match is exactly
/// the one the operator most needs to see.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>IsActive</c> moves, so the change tracker sees it and
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction with
/// <c>ChangedProperties</c> naming that column and the actor the gate verified (<c>AC-123-A</c>).
/// <c>GrantPath</c> stays null: <c>ClientManage</c> is company-wide, so there is no project and no
/// access path to name.
/// </para>
/// <para>
/// <b>No money moves and no account is settled.</b> KAFF-123 rule 5 raises — and deliberately does
/// not resolve — whether a client with an open project may be archived at all: projects and postings
/// do not exist until slices 3 and 4, and spec.md §11 makes closure an accounting condition.
/// <b>Slice 4 must revisit this.</b> It is written here as well as in the story so the next session
/// does not read the absence of a check as a decision that one is unnecessary.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid clientId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Client? client = await database.Clients
            .FirstOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);

        if (client is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.ClientNotFound);
        }

        Result archived = client.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. The act has no result of its own to report, and S-011 re-reads the list it is showing
        // rather than patching a row it already has.
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}

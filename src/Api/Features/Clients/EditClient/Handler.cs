using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.EditClient;

/// <summary>
/// Corrects one client's file, warns about a phone somebody else already holds, and leaves the
/// before-state in the trail. KAFF-121.
/// </summary>
/// <remarks>
/// <para>
/// <b>A client is never its own duplicate.</b> The match excludes the row being edited, which is the
/// <c>excluding</c> parameter decisions.md D-107 §2 specified and KAFF-119 deliberately did not
/// build. Without it, saving a client with its phone untouched matches itself, so an edit that
/// changed only the address would demand an acknowledgement — and acknowledging it would write a
/// <c>DuplicatePhoneAcknowledged</c> row pointing the client at itself, permanently, in an
/// append-only table.
/// </para>
/// <para>
/// <b>Editing warns exactly as registering does, and that reading is stated rather than assumed.</b>
/// spec.md §2's amendment is written as a property of the record — <i>"a repeated number shows the
/// operator which client already holds it and asks whether to proceed. It does not block the
/// save."</i> Karim was asked about <i>registering</i> a client; KAFF-121 finding F-19 applies his
/// answer to editing one, and the old criterion that refused a phone collision on edit was withdrawn
/// because of it. If that extension is wrong, this handler and the story are where to look — not the
/// entity, which refuses nothing about phones on purpose.
/// </para>
/// <para>
/// <b>The kind and the tax registration number go in together.</b> §6.7 constrains the pair, so
/// <c>Client.SetClassification</c> takes both and there is no order for this handler to get wrong —
/// see that method for what each of the two possible orders would have broken. This is
/// <c>AC-121-F</c>, and rule 6 requires the guard to be the entity's rather than the validator's.
/// </para>
/// <para>
/// <b>No audit record is hand-written for the edit itself.</b> The client is an entity change, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction, with
/// the before and after states and <c>ChangedProperties</c> naming the columns that moved
/// (<c>AC-121-A</c>, <c>AC-121-B</c>). KAFF-121's audit note is specific about why the before-state
/// matters here: <i>"the phone number on file when we sent that invoice"</i> is a question that gets
/// asked. A hand-written record is what decisions.md D-031 and KAFF-118 rule 2 forbid.
/// </para>
/// <para>
/// The acknowledgement is the one fact the change tracker cannot see — nothing about the matched
/// client changes — so it is declared through <c>IAuditContext.Record</c>, the mechanism D-061 built
/// for exactly that. <c>GrantPath</c> stays null because <c>ClientManage</c> is company-wide: no
/// project, no access policy, no path to name.
/// </para>
/// <para>
/// No money moves and none is stored (spec.md §6.1). <c>IsActive</c> is not touched: archiving is its
/// own act, with its own meaning in the trail, and it is KAFF-123 (rule 9).
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid clientId,
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        Client? client = await database.Clients
            .FirstOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);

        if (client is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.ClientNotFound);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches = await PhoneMatches.FindAsync(
            database, phone.Value.Normalised, cancellationToken, excluding: client.Id);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            // 409, and it carries no match data. The names belong to the 200 from phone-check: a
            // ProblemDetails cannot deliver them, because the SPA keeps only status, code and
            // messageKey from one. See decisions.md D-107 §2.
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
        }

        Result renamed = client.Rename(request.Name);

        if (renamed.IsFailure)
        {
            return ResultExtensions.Problem(renamed.Error);
        }

        Result classified = client.SetClassification(request.Kind, request.TaxRegistrationNumber);

        if (classified.IsFailure)
        {
            return ResultExtensions.Problem(classified.Error);
        }

        client.SetPrimaryPhone(phone.Value);
        client.SetContactDetails(request.AlternatePhone, request.Email, request.Address, request.Notes);

        // AC-121-C's last clause. One event per match, and the subject is the client that was
        // MATCHED — the same shape KAFF-119 established, so "which clients were saved as an
        // acknowledged duplicate of this one" stays a join on keys rather than prose in a text
        // column. Empty when there was no match, which is how the flag is ignored rather than
        // believed.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Client>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(client.Id, client.Code, client.Name, client.PhoneEntered, client.Kind, client.IsActive));
    }
}

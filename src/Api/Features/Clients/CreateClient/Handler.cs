using System.Globalization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Clients.CreateClient;

/// <summary>
/// Registers one client, generates its code, and records a decision taken past a warning. KAFF-119.
/// </summary>
/// <remarks>
/// <para>
/// <b>The duplicate phone warns and does not block</b> — spec.md §2, amended: <i>"A repeated number
/// shows the operator which client already holds it and asks whether to proceed. It does not block
/// the save."</i> Karim's reason is that a corporate client and its CEO may legitimately share a
/// number. The 409 below is that question, not a refusal: the same request by the same actor with
/// the same data plus <c>acknowledgedDuplicatePhone</c> succeeds. What it prevents is a caller who
/// never ran the check creating a duplicate <i>silently</i>, in a trail that can never be corrected.
/// </para>
/// <para>
/// <b>The match is re-run here rather than trusted from the caller.</b> The flag says "I saw a
/// warning"; this handler decides whether there was one. No match and the flag is ignored — a
/// duplicate that was not there is never recorded.
/// </para>
/// <para>
/// <b>The code is drawn from a PostgreSQL sequence, and it is drawn last.</b> A sequence is
/// non-transactional, so a rolled-back save burns a number and that code never exists. Every
/// failure this handler can produce before the draw — a bad phone, an unacknowledged duplicate, and
/// the permission and validator refusals that happen before it runs at all — therefore costs
/// nothing. <b>Two failures remain after it and are named rather than hidden:</b> a blank name
/// (<c>Client.Create</c>) and a tax registration number on an individual
/// (<c>Client.SetTaxRegistration</c>) each burn a number. Both are domain rules and neither is
/// restated here, because a second copy in a handler is a copy that drifts from the entity.
/// <b>Whether Kaff accepts gaps at all is Karim's question and is open</b> — decisions.md D-107,
/// open question 1. The mechanism is this one expression; if the answer is "unbroken", it becomes a
/// counter row under lock and every code already issued stays valid.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> The client is an entity change, so
/// <c>AuditSaveChangesInterceptor</c> writes its <c>Created</c> record in the same transaction with
/// the actor the gate verified, the generated code and the kind in the after-state
/// (<c>AC-119-A</c>). The acknowledgement is the one fact the change tracker cannot see — nothing
/// about the matched client changes — so it is declared through <c>IAuditContext.Record</c>, the
/// mechanism D-061 built for exactly that, and the same interceptor writes it. One event per match,
/// in one save, under one correlation id. <c>GrantPath</c> stays null because <c>ClientManage</c> is
/// company-wide: no project, no access policy, no path to name.
/// </para>
/// <para>
/// No money moves and none is stored. spec.md §6.1 and <c>AC-119-I</c>: a client's balance is
/// derived by summing postings, and there is no column here for one to be written to.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches =
            await PhoneMatches.FindAsync(database, phone.Value.Normalised, cancellationToken);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            // 409, and it carries no match data. The names belong to the 200 from phone-check: a
            // ProblemDetails cannot deliver them, because the SPA keeps only status, code and
            // messageKey from one. See decisions.md D-107 §2.
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
        }

        Result<Client> created = Client.Create(
            await NextCodeAsync(database, cancellationToken),
            request.Name ?? string.Empty,
            phone.Value,
            request.Kind,
            clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Client client = created.Value;

        client.SetContactDetails(request.AlternatePhone, request.Email, request.Address, notes: null);

        Result registered = client.SetTaxRegistration(request.TaxRegistrationNumber);

        if (registered.IsFailure)
        {
            return ResultExtensions.Problem(registered.Error);
        }

        // AC-119-E. One event per match, and the subject is the client that was MATCHED — so "which
        // clients were registered as an acknowledged duplicate of this one" is a join on keys rather
        // than prose parsed out of a text column. Empty when there was no match, which is how the
        // flag is ignored rather than believed.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Client>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        database.Clients.Add(client);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/clients/{client.Id}",
            new Response(client.Id, client.Code, client.Name, client.PhoneEntered, client.Kind, client.IsActive));
    }

    /// <summary>
    /// The next client code, of the form <c>C-10001</c>. No zero padding — the sequence starts above
    /// the padding width and never needs it.
    /// </summary>
    /// <remarks>
    /// <c>nextval</c> is the whole generator, and that is deliberate: two registrations in the same
    /// instant cannot receive the same code, which a read-max-and-add-one would lose as a failed
    /// insert against <c>ux_clients_code</c> (KAFF-119's architect note N6, answered by decisions.md
    /// D-107 §1). The sequence is declared on the EF model rather than in migration SQL, because the
    /// Api migrates on boot while the test harness builds the schema from the model — see
    /// <c>KaffDbContext.ClientCodeSequence</c>.
    /// </remarks>
    private static async Task<string> NextCodeAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // The sequence name is a compile-time constant of this assembly, never user input.
        long number = await database.Database
            .SqlQueryRaw<long>($"SELECT nextval('{KaffDbContext.ClientCodeSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
#pragma warning restore EF1002

        return string.Create(CultureInfo.InvariantCulture, $"C-{number}");
    }
}

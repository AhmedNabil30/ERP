using System.Globalization;
using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.CreateSubcontractor;

/// <summary>
/// Registers one مقاول باطن, generates its code, and warns rather than refuses on a repeated phone.
/// KAFF-211.
/// </summary>
/// <remarks>
/// <para>
/// <b>The phone is warn-and-acknowledge, never refused</b> — decisions.md D-139 §1, D-141. Unlike a
/// salaried employee (D-144 §1, D-146), a subcontractor has no refusal branch at all: every match is
/// re-run here and, with no acknowledgement, answered with <c>409 duplicate_phone_not_acknowledged</c>
/// naming nothing — the names belong to <c>POST /api/subcontractors/phone-check</c>'s 200.
/// </para>
/// <para>
/// <b>The code is drawn from a PostgreSQL sequence, last</b> — the same reasoning as
/// <c>CreateClient</c>'s <c>NextCodeAsync</c> (decisions.md D-107 §1): every failure this handler can
/// produce before the draw costs no number.
/// </para>
/// <para>
/// <b>No <c>TaxRegistrationNumber</c> and no rate card</b> — D-147 point 3, D-139 §4. Neither field
/// exists on <see cref="Request"/>, so there is nothing here to ignore or refuse.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c>
/// record in the same transaction. The acknowledgement is declared through <c>IAuditContext.Record</c>,
/// one <c>DuplicatePhoneAcknowledged</c> event per match. <c>GrantPath</c> stays null —
/// <c>SubcontractorManage</c> is company-wide.
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
            await PhoneMatches.SubcontractorsAsync(database, phone.Value.Normalised, cancellationToken);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
        }

        if (request.TradeBabId is not null)
        {
            bool babExists = await database.Babs
                .AnyAsync(bab => bab.Id == request.TradeBabId, cancellationToken);

            if (!babExists)
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }
        }

        // D-151: RetentionRate is already Percentage — a negative value is refused by the type's own
        // constructor during deserialisation (ApiErrors.MalformedBody), so there is nothing left to
        // convert or catch here.
        Result<Subcontractor> created = Subcontractor.Create(
            await NextCodeAsync(database, cancellationToken),
            request.Name ?? string.Empty,
            phone.Value,
            clock.GetUtcNow(),
            request.TradeBabId,
            request.RetentionRate);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Subcontractor subcontractor = created.Value;

        // AC-211-K. One event per match, subject is the record that was MATCHED — same shape as
        // CreateClient/CreateEmployee (D-141 §4/§5). Empty when there was no match.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Subcontractor>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        database.Subcontractors.Add(subcontractor);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/subcontractors/{subcontractor.Id}",
            new Response(
                subcontractor.Id,
                subcontractor.Code,
                subcontractor.Name,
                subcontractor.PhoneEntered,
                subcontractor.TradeBabId,
                subcontractor.RetentionRate,
                subcontractor.IsActive));
    }

    /// <summary>The next subcontractor code, of the form <c>SC-10001</c>.</summary>
    private static async Task<string> NextCodeAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // The sequence name is a compile-time constant of this assembly, never user input.
        long number = await database.Database
            .SqlQueryRaw<long>($"SELECT nextval('{KaffDbContext.SubcontractorCodeSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
#pragma warning restore EF1002

        return string.Create(CultureInfo.InvariantCulture, $"SC-{number}");
    }
}

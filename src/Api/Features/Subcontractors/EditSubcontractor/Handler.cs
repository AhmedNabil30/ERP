using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.EditSubcontractor;

/// <summary>
/// Corrects one subcontractor's file, including the retention rate, and leaves the before-state in
/// the trail. KAFF-211.
/// </summary>
/// <remarks>
/// <para>
/// <b>A subcontractor is never its own duplicate</b> — the match excludes the row being edited
/// (decisions.md D-107 §2), same as <c>EditClient</c> and <c>EditEmployee</c>.
/// </para>
/// <para>
/// <b>The retention rate is the field somebody will later ask who changed</b> (rule 12) —
/// <c>AuditSaveChangesInterceptor</c> writes <c>RetentionRate</c>'s before and after in the same
/// <c>Modified</c> record as the rest of the profile, because both are edited by the same role through
/// the same endpoint (contrast the tax registration number, split onto its own endpoint by D-147, and
/// so its own audit record — AC-211-K).
/// </para>
/// <para>
/// <b>No audit record is hand-written for the edit itself.</b> The acknowledgement is declared through
/// <c>IAuditContext.Record</c>, same as <c>CreateSubcontractor</c>. <c>GrantPath</c> stays null —
/// <c>SubcontractorManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid subcontractorId,
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        Subcontractor? subcontractor = await database.Subcontractors
            .FirstOrDefaultAsync(candidate => candidate.Id == subcontractorId, cancellationToken);

        if (subcontractor is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.SubcontractorNotFound);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        List<PhoneMatch> matches = await PhoneMatches.SubcontractorsAsync(
            database, phone.Value.Normalised, cancellationToken, excluding: subcontractorId);

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

        Percentage retentionRate;

        try
        {
            retentionRate = Percentage.FromPercent(request.RetentionRate);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ResultExtensions.Problem(MasterDataErrors.RetentionRateMustNotBeNegative);
        }

        Result edited = subcontractor.Edit(request.Name ?? string.Empty, phone.Value, request.TradeBabId);

        if (edited.IsFailure)
        {
            return ResultExtensions.Problem(edited.Error);
        }

        subcontractor.SetRetentionRate(retentionRate);

        foreach (PhoneMatch match in matches)
        {
            audit.Record<Subcontractor>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(
                subcontractor.Id,
                subcontractor.Code,
                subcontractor.Name,
                subcontractor.PhoneEntered,
                subcontractor.TradeBabId,
                subcontractor.RetentionRate.Fraction,
                subcontractor.IsActive));
    }
}

using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.SetTaxRegistration;

/// <summary>
/// Sets or clears one subcontractor's tax registration number. decisions.md D-147.
/// </summary>
/// <remarks>
/// <b>No audit record is hand-written.</b> <c>TaxRegistrationNumber</c> moves, so the change tracker
/// sees it and <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same
/// transaction, naming the Finance actor the gate verified (AC-211-K) — a record of its own, separate
/// from whatever the Technical Office does through <c>SubcontractorManage</c>, because the two are two
/// requests on two endpoints and never one combined save. <c>GrantPath</c> stays null —
/// <c>SubcontractorTaxRegistrationEdit</c> is company-wide.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid subcontractorId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);

        Subcontractor? subcontractor = await database.Subcontractors
            .FirstOrDefaultAsync(candidate => candidate.Id == subcontractorId, cancellationToken);

        if (subcontractor is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.SubcontractorNotFound);
        }

        subcontractor.SetTaxRegistration(request.TaxRegistrationNumber);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(subcontractor.Id, subcontractor.TaxRegistrationNumber));
    }
}

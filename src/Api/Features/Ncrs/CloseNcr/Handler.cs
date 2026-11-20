using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Ncrs.CloseNcr;

/// <summary>
/// Closes the NCR and releases its extract's held portion in one save, under the extract's row lock (the extract
/// chain's own spine, <see cref="ExtractSupport.RunAsync"/>), so two closes cannot both pass. Posts nothing: the
/// release is recorded on the extract, and how the remainder is billed is <c>ExtractPartialApprovalScope</c>'s.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(audit);

        Guid? extractId = await database.Ncrs.AsNoTracking()
            .Where(ncr => ncr.Id == id && ncr.ProjectId == projectId)
            .Select(ncr => (Guid?)ncr.ExtractId)
            .FirstOrDefaultAsync(cancellationToken);
        if (extractId is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NcrNotFound);
        }

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        Ncr? closed = null;
        Extract? held = null;

        return await ExtractSupport.RunAsync(
            projectId,
            extractId.Value,
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                closed = await services.Database.Ncrs.SingleAsync(ncr => ncr.Id == id, token);
                held = extract;
                Result done = closed.Close(
                    extract, request.Note, services.CurrentUser.UserId ?? Guid.Empty, services.Clock.GetUtcNow());
                if (done.IsSuccess)
                {
                    audit.SetReason(request.Note!.Trim());
                }

                return ExtractSupport.NoPostingsOr(done);
            },
            cancellationToken,
            respond: () => Microsoft.AspNetCore.Http.Results.Ok(NcrResponse.From(closed!, held!, seesMoney, mayClose: false)));
    }
}

using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.ReviseEvidence;

/// <summary>
/// Re-derives the certified work from the BOQ as it stands and replaces the evidence of a rejected Draft extract.
/// Writes no posting.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExtractSupport.RunAsync(
            projectId,
            id,
            needsAccounts: false,
            new StepServices(database, rulings, clock, currentUser, authorization, user),
            async (extract, project, context, services, token) =>
            {
                Boq? boq = await services.Database.Boqs
                    .AsNoTracking()
                    .Include(candidate => candidate.Lines)
                    .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, token);
                if (boq is null)
                {
                    return Result.Failure<IReadOnlyList<Kaff.Domain.Treasury.Posting>>(BoqErrors.NotFound);
                }

                if (!boq.IsFrozen)
                {
                    return Result.Failure<IReadOnlyList<Kaff.Domain.Treasury.Posting>>(DailyLogErrors.BoqNotFrozen);
                }

                Guid actor = services.CurrentUser.UserId ?? Guid.Empty;
                return ExtractSupport.NoPostingsOr(extract.ReviseEvidence(
                    actor,
                    ExtractSupport.CumulativeCertifiedWork(boq, services.Rulings),
                    request.MaterialValueOnSite,
                    request.MaterialInstalled,
                    services.Clock.GetUtcNow()));
            },
            cancellationToken);
    }
}

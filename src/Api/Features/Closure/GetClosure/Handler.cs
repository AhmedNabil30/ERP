using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.GetClosure;

/// <summary>An extract still short of Collected or Void.</summary>
public sealed record OpenExtract(int Number, ExtractStatus Status);

/// <summary>
/// The closure read (D-257).
/// </summary>
/// <param name="ProjectId">The project.</param>
/// <param name="Hold">
/// The hold ledger summed from postings: what the release would move. Zero once released, because the release leaves
/// the hold at exactly zero. Null for a viewer who does not see money.
/// </param>
/// <param name="OpenExtracts">Every extract not yet Collected or Void, by number.</param>
/// <param name="HoldReleaseRequest">
/// The latest hold release request (AC-802-L): waiting for the Owner, approved, or rejected with its reason. Null when
/// none was ever made.
/// </param>
public sealed record Response(
    Guid ProjectId, Money? Hold, IReadOnlyList<OpenExtract> OpenExtracts, HoldReleaseRequestResponse? HoldReleaseRequest);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!await database.Projects.AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        List<OpenExtract> open = await database.Extracts
            .AsNoTracking()
            .Where(extract => extract.ProjectId == projectId
                && extract.Status != ExtractStatus.Collected
                && extract.Status != ExtractStatus.Void)
            .OrderBy(extract => extract.Number)
            .Select(extract => new OpenExtract(extract.Number, extract.Status))
            .ToListAsync(cancellationToken);

        Money? hold = await ExtractSupport.MaySeeMoneyAsync(authorization, user)
            ? BalanceQueries.GetLedgerBalances(database.AccountBalances, projectId).Hold
            : null;

        HoldReleaseRequestResponse? request = await database.HoldReleaseRequests
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .OrderByDescending(candidate => candidate.RequestedAt)
            .Select(candidate => new HoldReleaseRequestResponse(
                candidate.Id,
                candidate.ProjectId,
                candidate.Status,
                candidate.RequestedByUserId,
                candidate.RequestedAt,
                candidate.DecidedByUserId,
                candidate.DecidedAt,
                candidate.RejectionReason))
            .FirstOrDefaultAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(projectId, hold, open, request));
    }
}

using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Closure.GetSubcontractorRetention;

/// <summary>A posted retention release. <c>Reversed</c> is true once a mirror posting through <c>ReversesId</c> exists.</summary>
public sealed record ReleasedPosting(Guid PostingId, Money Amount, DateOnly PostingDate, bool Reversed);

/// <summary>A release request waiting for the Owner. It carries no reason.</summary>
public sealed record WaitingRelease(Guid Id, Guid AskedByUserId, string AskedByName, DateTimeOffset CreatedAt);

/// <summary>A reversal request waiting for the Owner. It names the release posting it would reverse.</summary>
public sealed record WaitingReversal(Guid Id, Guid PostingId, Guid AskedByUserId, string AskedByName, string Reason, DateTimeOffset CreatedAt);

/// <summary>One subcontractor with a retention account on the project. <c>RetentionHeld</c> is summed from postings.</summary>
public sealed record SubcontractorRetentionRow(
    Guid SubcontractorId,
    string Name,
    Money RetentionHeld,
    IReadOnlyList<ReleasedPosting> Releases,
    WaitingRelease? ReleaseRequest,
    WaitingReversal? ReversalRequest);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(authorization);

        // D-273 answer 4: an assigned Technical Office user reads too. The gate already required the assignment.
        bool mayRead = currentUser.Role == Role.TechnicalOffice
            || (await authorization.AuthorizeAsync(
                user, resource: null, PermissionRequirement.ToPolicyName(Permission.SubcontractorRetentionRelease, ProjectScope.FromRoute()))).Succeeded
            || (await authorization.AuthorizeAsync(
                user, resource: null, PermissionRequirement.ToPolicyName(Permission.FinancialMovementApprove, ProjectScope.FromRoute()))).Succeeded;
        if (!mayRead)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        if (!await database.Projects.AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        var held = await database.AccountBalances.AsNoTracking()
            .Where(balance => balance.ProjectId == projectId && balance.Type == AccountType.SubcontractorRetention && balance.PartyId != null)
            .Select(balance => new { SubcontractorId = balance.PartyId!.Value, balance.SignedBalance })
            .ToListAsync(cancellationToken);
        List<Guid> ids = [.. held.Select(row => row.SubcontractorId)];

        Dictionary<Guid, string> names = await database.Subcontractors.AsNoTracking()
            .Where(subcontractor => ids.Contains(subcontractor.Id))
            .ToDictionaryAsync(subcontractor => subcontractor.Id, subcontractor => subcontractor.Name, cancellationToken);

        var releases = await database.Postings.AsNoTracking()
            .Where(posting => posting.ProjectId == projectId
                && posting.Type == PostingType.SubcontractorRetentionRelease
                && posting.ReversesId == null)
            .Select(posting => new
            {
                posting.Id,
                SubcontractorId = posting.SourceDocumentId,
                posting.Amount,
                posting.PostingDate,
                Reversed = database.Postings.Any(mirror => mirror.ReversesId == posting.Id),
            })
            .ToListAsync(cancellationToken);

        List<SubcontractorRetentionReleaseRequest> releaseRequests = await database.SubcontractorRetentionReleaseRequests.AsNoTracking()
            .Where(request => request.ProjectId == projectId && request.Status == HoldReleaseRequestStatus.Requested)
            .ToListAsync(cancellationToken);
        List<SubcontractorRetentionReversalRequest> reversalRequests = await database.SubcontractorRetentionReversalRequests.AsNoTracking()
            .Where(request => request.ProjectId == projectId && request.Status == HoldReleaseRequestStatus.Requested)
            .ToListAsync(cancellationToken);

        List<Guid> askers = [.. releaseRequests.Select(request => request.RequestedByUserId), .. reversalRequests.Select(request => request.RequestedByUserId)];
        Dictionary<Guid, string> askerNames = await database.Users.AsNoTracking()
            .Where(account => askers.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.FullName, cancellationToken);

        List<SubcontractorRetentionRow> rows = [.. held
            .Select(row =>
            {
                SubcontractorRetentionReleaseRequest? release = releaseRequests.FirstOrDefault(request => request.SubcontractorId == row.SubcontractorId);
                SubcontractorRetentionReversalRequest? reversal = reversalRequests.FirstOrDefault(request => request.SubcontractorId == row.SubcontractorId);
                return new SubcontractorRetentionRow(
                    row.SubcontractorId,
                    names.GetValueOrDefault(row.SubcontractorId, string.Empty),
                    row.SignedBalance,
                    [.. releases.Where(found => found.SubcontractorId == row.SubcontractorId)
                        .OrderBy(found => found.PostingDate)
                        .Select(found => new ReleasedPosting(found.Id, found.Amount, found.PostingDate, found.Reversed))],
                    release is null ? null : new WaitingRelease(
                        release.Id, release.RequestedByUserId, askerNames.GetValueOrDefault(release.RequestedByUserId, string.Empty), release.RequestedAt),
                    reversal is null ? null : new WaitingReversal(
                        reversal.Id, reversal.OriginalPostingId, reversal.RequestedByUserId, askerNames.GetValueOrDefault(reversal.RequestedByUserId, string.Empty), reversal.Reason, reversal.RequestedAt));
            })
            .OrderBy(row => row.Name, StringComparer.CurrentCulture)];

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}

using System.Security.Claims;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.ListAdjustments;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(database);

        List<Adjustment> adjustments = await database.Adjustments
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .OrderBy(candidate => candidate.Number)
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(await AdjustmentReads.BuildAsync(
            database, projectId, adjustments, currentUser.UserId ?? Guid.Empty,
            await Projects.CallerPermissions.HoldsAsync(authorization, user, Permission.FinancialMovementApprove), cancellationToken));
    }
}

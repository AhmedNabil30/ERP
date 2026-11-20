using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Adjustments.GetAdjustment;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(database);

        Adjustment? adjustment = await database.Adjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);

        return adjustment is null
            ? ResultExtensions.Problem(AdjustmentErrors.NotFound)
            : Microsoft.AspNetCore.Http.Results.Ok((await AdjustmentReads.BuildAsync(
                    database, projectId, [adjustment], currentUser.UserId ?? Guid.Empty,
                    await Projects.CallerPermissions.HoldsAsync(authorization, user, Permission.FinancialMovementApprove), cancellationToken))[0]);
    }
}

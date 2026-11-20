using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.PreviewPortion;

/// <summary>
/// The split a portion would make, from <see cref="Extract.PreviewSplit"/>: the same derivation the read's
/// <c>approvedWork</c> and <c>heldWork</c> use. Both are null for a viewer who may not see extract money.
/// </summary>
public sealed record PortionPreview(Percentage Portion, Money? ApprovedWork, Money? HeldWork);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        decimal portion,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Extract? extract = await database.Extracts.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (extract is null || project is null)
        {
            return ResultExtensions.Problem(ExtractErrors.NotFound);
        }

        if (portion < 0m)
        {
            return ResultExtensions.Problem(ExtractErrors.ApprovedPortionOutOfRange);
        }

        Percentage asked = Percentage.FromFraction(portion);
        Result<(Money Approved, Money Held)> split = extract.PreviewSplit(project.Status, asked);
        if (split.IsFailure)
        {
            return ResultExtensions.Problem(split.Error);
        }

        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);
        return Microsoft.AspNetCore.Http.Results.Ok(new PortionPreview(
            asked, seesMoney ? split.Value.Approved : null, seesMoney ? split.Value.Held : null));
    }
}

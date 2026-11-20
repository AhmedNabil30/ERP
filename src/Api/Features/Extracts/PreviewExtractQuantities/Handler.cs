using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.PreviewExtractQuantities;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        RulingSet rulings,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Project? project = await database.Projects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        if (await PrepareExtract.Handler.RefusalAsync(database, project, cancellationToken) is { } refused)
        {
            return ResultExtensions.Problem(refused);
        }

        Boq boq = await database.Boqs.AsNoTracking().Include(candidate => candidate.Lines)
            .FirstAsync(candidate => candidate.ProjectId == projectId, cancellationToken);
        Func<Guid, decimal?> previousOf = await ExtractQuantities.PreviousOfAsync(database, projectId, null, cancellationToken);
        bool seesMoney = await ExtractSupport.MaySeeMoneyAsync(authorization, user);

        return Microsoft.AspNetCore.Http.Results.Ok(boq.Lines
            .OrderBy(line => line.Position)
            .Select(line => ExtractQuantities.Row(line, previousOf(line.Id), line.QuantityExecuted, seesMoney, rulings))
            .ToList());
    }
}

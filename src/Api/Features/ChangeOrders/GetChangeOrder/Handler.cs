using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.ChangeOrders.GetChangeOrder;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

        ChangeOrder? order = await ChangeOrderSupport.WithChildren(database)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (order is null || project is null)
        {
            return ResultExtensions.Problem(ChangeOrderErrors.NotFound);
        }

        ChangeOrderViewer viewer = await ChangeOrderSupport.ViewerAsync(
            new ChangeOrderServices(database, rulings, clock, currentUser, authorization, user));

        return Microsoft.AspNetCore.Http.Results.Ok(ChangeOrderView.Build(order, project, rulings, viewer));
    }
}

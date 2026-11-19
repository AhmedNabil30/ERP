using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.Collections;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Cheques.ListCheques;

/// <summary>
/// <c>GET /api/projects/{projectId}/cheques</c>. The cheques in hand on a project: received or deposited, not yet cleared
/// or bounced, oldest first, each with the number of the extract it paid. Gated <c>ChequeRead</c> (Finance and the Owner,
/// D-239 item 4) with the project from the route.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/cheques";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, HandleAsync)
            .RequirePermission(Permission.ChequeRead, ProjectScope.FromRoute())
            .WithName("ListCheques")
            .WithTags("Cheques");
    }

    private static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        List<GetCheque.Response> all = await GetCheque.Handler.Project(database, projectId).ToListAsync(cancellationToken);

        List<GetCheque.Response> inHand = [.. all.Where(row => Cheque.IsInHand(row.State)).OrderBy(row => row.Collection.Date).ThenBy(row => row.Id)];
        return Results.Ok(GetCheque.Handler.WithWaiting(
            inHand, await Extracts.ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken)));
    }
}

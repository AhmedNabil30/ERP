using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.ListProjects;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(database);

        User caller = LiveSession.Caller(http);

        // The temporary password must be replaced before anything is read (R5-A AC-A-3), exactly as the gate refuses a project route.
        if (caller.MustChangePassword)
        {
            return ResultExtensions.Problem(AuthorizationErrors.PasswordChangeRequired);
        }

        Guid? clientId = caller.ClientId;

        // A client user always carries a client file (User.Create refuses one without). Defence in depth: none means nothing.
        List<PortalProjectSummary> projects = clientId is null
            ? []
            : [.. (await database.Projects.AsNoTracking()
                .Where(project => project.ClientId == clientId)
                .OrderBy(project => project.Name)
                .ToListAsync(cancellationToken))
                .Select(project => new PortalProjectSummary(
                    project.Id, project.Name, project.Status.ToString(), project.ContractType.ToString()))];

        return Results.Ok(projects);
    }
}

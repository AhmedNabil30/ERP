using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.SetWithholding;

/// <summary>The contract's withholding category (spec.md section 6.7). Nullable so an absent one is refused, never read as None.</summary>
/// <param name="Category">None, ContractingAndSupplies (1%), Services (3%) or ProfessionalFees (5%).</param>
public sealed record Request(WithholdingCategory? Category);

/// <param name="ProjectId">The project.</param>
/// <param name="WithholdingCategory">The category the contract now carries.</param>
public sealed record Response(Guid ProjectId, WithholdingCategory WithholdingCategory);

/// <summary>
/// <c>PUT /api/projects/{projectId}/withholding</c>. Sets the contract's withholding category (Verifier F-8). spec.md
/// section 6.7, Karim's amendment: "Finance sets it, during contract creation or approval. Marketing cannot." Gated
/// <c>ProjectFinancialsEdit</c>, Finance and the Owner (D-055 section 2), project-scoped: role and assignment. An
/// individual client's contract carries none (<c>Project.SetWithholding</c>). The change is audited by the interceptor:
/// who, when, the category before and after. Whether it may change after the first issued extract is unruled (Q30), so
/// it is not refused.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/withholding";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, HandleAsync)
            .RequirePermission(Permission.ProjectFinancialsEdit, ProjectScope.FromRoute())
            .WithName("SetProjectWithholding")
            .WithTags("Projects");
    }

    private static async Task<IResult> HandleAsync(
        Guid projectId, Request request, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Category is not { } category || !Enum.IsDefined(category))
        {
            return ResultExtensions.Problem(ProjectErrors.WithholdingCategoryRequired);
        }

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        ClientKind kind = await database.Clients
            .Where(client => client.Id == project.ClientId)
            .Select(client => client.Kind)
            .FirstAsync(cancellationToken);

        Result set = project.SetWithholding(category, kind);
        if (set.IsFailure)
        {
            return ResultExtensions.Problem(set.Error);
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(new Response(project.Id, project.WithholdingCategory));
    }
}

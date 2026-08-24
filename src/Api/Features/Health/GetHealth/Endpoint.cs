using Kaff.Api.Common.Endpoints;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Health.GetHealth;

/// <summary>
/// Liveness and guard verification.
/// </summary>
/// <remarks>
/// The only endpoint slice 0 ships. It exists to prove the slice registration convention works end
/// to end, and to answer a question that matters operationally: are the database guards actually
/// installed on this deployment? A database that lost its triggers serves traffic normally and
/// passes every application-level test, while the rule spec.md §6.1 insists must live in the
/// database is simply not there.
///
/// Anonymous by design — a health probe has no credentials. It reveals nothing beyond whether the
/// database answers and whether the guards are present.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/health", HandleAsync)
            .AllowAnonymous()
            .WithName("GetHealth")
            .WithTags("Diagnostics");
    }

    private static async Task<IResult> HandleAsync(
        KaffDbContext context,
        DatabaseInitializer initializer,
        CancellationToken cancellationToken)
    {
        bool databaseReachable = await context.Database.CanConnectAsync(cancellationToken);

        if (!databaseReachable)
        {
            return Microsoft.AspNetCore.Http.Results.Json(
                new Response(Status: "unhealthy", DatabaseReachable: false, GuardsInstalled: false, []),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        IReadOnlyList<string> missingGuards = await initializer.FindMissingGuardsAsync(cancellationToken);

        var response = new Response(
            Status: missingGuards.Count == 0 ? "healthy" : "degraded",
            DatabaseReachable: true,
            GuardsInstalled: missingGuards.Count == 0,
            MissingGuards: missingGuards);

        return missingGuards.Count == 0
            ? Microsoft.AspNetCore.Http.Results.Ok(response)
            : Microsoft.AspNetCore.Http.Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

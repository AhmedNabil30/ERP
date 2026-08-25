using Kaff.Api.Common.Endpoints;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Setup.GetSetupAvailability;

/// <summary>
/// <c>GET /api/setup</c> — whether the one-time setup screen may still be reached. KAFF-100.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous, and deliberately named in <c>EndpointPermissionCoverageTests</c>'s allow-list rather
/// than left to fall under the fallback policy (decisions.md D-067, D-069): the SPA must be able to
/// ask this before anybody has signed in, or it never learns to route to <c>/setup</c> at all.
/// </para>
/// <para>
/// <b>The answer is rule 4 read back, not a second gate.</b> <c>available</c> is exactly
/// <c>!Users.AnyAsync()</c> — the same emptiness test <c>CreateOwner</c>'s handler runs as its
/// courtesy check. There is no flag, no cached value and no configuration switch behind this response
/// (rule 5): a row appearing in the table is the only way the answer ever changes, and it changes in
/// one direction, permanently.
/// </para>
/// <para>
/// Publishing this to an anonymous caller discloses nothing a stranger could not already infer: before
/// setup, the setup screen itself says the system is empty; after setup, the answer is a constant
/// <c>false</c> forever. See <c>ux/slice-1-flows.md</c> S-002.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/setup";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .WithName("GetSetupAvailability")
            .WithTags("Setup");
    }

    private static async Task<IResult> HandleAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        bool systemInitialised = await database.Users.AnyAsync(cancellationToken);

        return Results.Ok(new Response(Available: !systemInitialised));
    }
}

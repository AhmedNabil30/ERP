using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.BoqBases.ListBoqBases;

/// <summary><c>GET /api/boq-bases</c>. Lists the active bases (D-218 item 13). Company-wide: a base belongs to no project.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/boq-bases";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BoqBaseRead)
            .WithName("ListBoqBases")
            .WithTags("BoqBases");
    }
}

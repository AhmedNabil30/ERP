using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.BoqBases.GetBoqBase;

/// <summary><c>GET /api/boq-bases/{baseId:guid}</c>. One base with its lines. No rate.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/boq-bases/{baseId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BoqBaseRead)
            .WithName("GetBoqBase")
            .WithTags("BoqBases");
    }
}

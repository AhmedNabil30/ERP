using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.BoqBases.CreateBoqBase;

/// <summary><c>POST /api/boq-bases</c>. Prepares a named base (D-217 item 19).</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/boq-bases";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BoqBaseManage)
            .WithName("CreateBoqBase")
            .WithTags("BoqBases");
    }
}

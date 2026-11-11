using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.BoqBases.UpdateBoqBase;

/// <summary><c>PUT /api/boq-bases/{baseId:guid}</c>. Renames a base and replaces its lines with the list sent.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/boq-bases/{baseId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BoqBaseManage)
            .WithName("UpdateBoqBase")
            .WithTags("BoqBases");
    }
}

using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.BoqBases.ArchiveBoqBase;

/// <summary><c>POST /api/boq-bases/{baseId:guid}/archive</c>. Archives a base. It leaves the list and becomes read-only.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/boq-bases/{baseId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BoqBaseManage)
            .WithName("ArchiveBoqBase")
            .WithTags("BoqBases");
    }
}

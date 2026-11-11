using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.ClientPicker;

/// <summary>
/// <c>GET /api/clients/picker</c> - the client list of the project-create screen. KAFF-419, D-222. Gated by
/// <c>ProjectCreate</c> (the Technical Office holds it, and does not hold <c>ClientManage</c>), and it returns an id and a name
/// only. <c>GET /api/clients</c> keeps <c>ClientManage</c>.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/picker";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectCreate)
            .WithName("ClientPicker")
            .WithTags("Clients");
    }
}

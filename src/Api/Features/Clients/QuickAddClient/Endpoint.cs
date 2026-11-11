using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.QuickAddClient;

/// <summary>
/// <c>POST /api/clients/quick</c> - add a missing client from inside the project-create form. KAFF-419, D-218 item 8.
/// </summary>
/// <remarks>
/// Gated by <c>ProjectCreate</c>, not <c>ClientManage</c>: whoever may open a project may register the client it is for. The
/// full client screen keeps <c>ClientManage</c>. Name, phone and kind only; everything else goes through the same
/// <c>CreateClient</c> handler, so the validation, the code sequence and the audit record are not copied.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/quick";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectCreate)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("QuickAddClient")
            .WithTags("Clients");
    }
}

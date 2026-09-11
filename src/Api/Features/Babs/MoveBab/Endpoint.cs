using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.MoveBab;

/// <summary>
/// <c>PUT /api/babs/{babId}/parent</c> — re-parent one باب, or make it a root. KAFF-205.
/// </summary>
/// <remarks>
/// A separate sub-resource from <c>EditBab</c> on purpose: re-parenting runs through
/// <c>Bab.SetParent</c>'s cycle guard and nothing else does. Gated <c>BabManage</c>, company-wide, no
/// assignment (KAFF-205 rule 8, D-129 §1). Every other role refused <c>403</c> (<c>AC-205-H</c>).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs/{babId:guid}/parent";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("MoveBab")
            .WithTags("Babs");
    }
}

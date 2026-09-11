using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.EditBab;

/// <summary>
/// <c>PUT /api/babs/{babId}</c> — correct a باب's names or its default markup. KAFF-204.
/// </summary>
/// <remarks>
/// Gated <c>BabManage</c>, company-wide, no assignment — spec.md §2, D-129 §1. Every other role
/// refused <c>403</c> (<c>AC-204-G</c>). No <c>ProjectScope</c> — أبواب belong to no project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs/{babId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("EditBab")
            .WithTags("Babs");
    }
}

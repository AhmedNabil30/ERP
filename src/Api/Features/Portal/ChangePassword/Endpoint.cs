using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.ChangePassword;

/// <summary>
/// <c>POST /api/portal/auth/change-password</c> - a client replaces the temporary password the Owner handed over (R5-A AC-A-3,
/// KAFF-103 mirrored). Self-only like its staff sibling, and it must be reachable while MustChangePassword is true, so it
/// carries the portal live-session check instead of a permission.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/auth/change-password";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .RequirePortalSession()
            .WithName("PortalChangePassword")
            .WithTags("Portal");
    }
}

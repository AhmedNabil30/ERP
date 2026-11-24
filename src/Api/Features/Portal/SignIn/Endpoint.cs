using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Portal.SignIn;

/// <summary>
/// <c>POST /api/portal/auth/sign-in</c> - the client's door (R5-A, D-051 Q33). Anonymous by construction, as the staff door is:
/// producing an identity is its whole job. A staff credential at this door gets the same generic 401 a wrong password gets.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/portal/auth/sign-in";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AllowAnonymous()
            .WithName("PortalSignIn")
            .WithTags("Portal");
    }
}

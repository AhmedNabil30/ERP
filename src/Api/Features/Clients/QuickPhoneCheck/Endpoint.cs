using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.QuickPhoneCheck;

/// <summary>
/// <c>POST /api/clients/quick/phone-check</c> - the warning half of the quick add. KAFF-419 AC-4, D-218 item 9. The same
/// handler and the same body as <c>PhoneCheck</c>, gated by <c>ProjectCreate</c> so the Technical Office can see the warning.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/quick/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, PhoneCheck.Handler.HandleAsync)
            .RequirePermission(Permission.ProjectCreate)
            .WithName("QuickClientPhoneCheck")
            .WithTags("Clients");
    }
}

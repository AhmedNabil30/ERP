using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Setup.CreateOwner;

/// <summary>
/// <c>POST /api/setup</c> — creates the first Owner. KAFF-100.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonymous by construction, not by an oversight.</b> This is the second and last anonymous
/// endpoint in the system (the first is <c>GET /api/setup</c>, its own sibling; sign-in is the third,
/// KAFF-101a). It holds no <c>RequirePermission</c> because there is no identity to check — the gate
/// is rules 4, 5 and 6, which are properties of the database (the emptiness of the <c>users</c> table
/// and <c>ux_users_bootstrap_owner_once</c>), not of this handler. <c>AllowAnonymous()</c> is what
/// lets it clear the fallback policy that requires an authenticated caller on every other route
/// [Verified: 2026-08-26 @ <c>Program.cs</c> -&gt; <c>SetFallbackPolicy</c>], and
/// <c>EndpointPermissionCoverageTests</c> requires this exact route to be named, with a reason, in its
/// allow-list — an unnamed anonymous route is D-067's shape (decisions.md D-069).
/// </para>
/// <para>
/// <b>POST, not PUT.</b> This creates exactly one row and can never be repeated (rule 5) — there is no
/// resource at a stable URL to replace.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/setup";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("CreateOwner")
            .WithTags("Setup");
    }
}

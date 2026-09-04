using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.CreateClient;

/// <summary>
/// <c>POST /api/clients</c> — Marketing registers a client. KAFF-119.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ClientManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> and <c>Role.MarketingSales</c> [Verified: 2026-09-04 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.ClientManage</c> row] — spec.md §2, "Client
/// is owned by Marketing", and decisions.md D-044 ruling 4. Both halves of "role × assignment" are
/// decided by <c>PermissionEvaluator</c> from that row: the role half against the grants, and the
/// assignment half by the scope, which is company-wide and therefore names no project to be assigned
/// to. Declaring <c>ProjectScope.FromRoute()</c> on a route with no project in it would refuse every
/// caller including the Owner.
/// </para>
/// <para>
/// <b>Finance, the Technical Office, HR and Site Engineers are all refused by this one line</b>
/// (<c>AC-119-H</c>) — none of them holds the row — and so is <c>Role.Client</c> (<c>AC-119-G</c>),
/// which matters most: a portal user reaching the client master would see clients other than their
/// own, which spec.md §12 forbids absolutely.
/// </para>
/// <para>
/// The role is read from the database on every request, not from the token
/// [Verified: 2026-09-04 @ <c>PermissionAuthorizationHandler.cs</c> -&gt; <c>BuildSubjectAsync</c>],
/// so a deactivated Marketing account and a forged <c>role=MarketingSales</c> claim are both refused
/// here — decisions.md D-048.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("CreateClient")
            .WithTags("Clients");
    }
}

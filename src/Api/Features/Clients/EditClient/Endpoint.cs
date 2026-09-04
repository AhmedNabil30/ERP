using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.EditClient;

/// <summary>
/// <c>PUT /api/clients/{clientId}</c> — Marketing corrects a client's file. KAFF-121.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ClientManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> and <c>Role.MarketingSales</c> [Verified: 2026-09-04 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.ClientManage</c> row] — spec.md §2, "Client
/// is owned by Marketing", and decisions.md D-044 ruling 4. That one line refuses Finance, the
/// Technical Office, HR and Site Engineers (<c>AC-121-G</c>) and, most importantly,
/// <c>Role.Client</c>: a portal user reaching this route could rewrite another client's file, which
/// spec.md §12 forbids absolutely.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>, and the route's <c>{clientId}</c> is not one.</b> The catalogue row is
/// company-wide, so declaring a scope would make the evaluator look for an assignment to a project
/// that does not exist and refuse every caller including the Owner. A client is
/// project-independent (spec.md §2); <c>EndpointPermissionCoverageTests</c> asserts the endpoint and
/// the catalogue row agree on this.
/// </para>
/// <para>
/// <b>PUT rather than PATCH</b> — see <c>Request</c>. The kind and the tax registration number are
/// one constraint under spec.md §6.7, so a partial body would leave the server guessing which half
/// of the pair the operator meant to keep.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/{clientId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("EditClient")
            .WithTags("Clients");
    }
}

using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.CreateBab;

/// <summary>
/// <c>POST /api/babs</c> — the Technical Office (or the Owner, D-129 §1) adds one باب. KAFF-204.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.BabManage</c> — company-wide, granted to <c>Role.Owner</c> and
/// <c>Role.TechnicalOffice</c> [Verified: 2026-09-10 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.BabManage</c> row] — spec.md §2, KAFF-204 rule 9, D-129 §1. Every other role is
/// refused <c>403</c> (<c>AC-204-G</c>).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> أبواب belong to no project (rule 9), so declaring one would make
/// the evaluator look for an assignment that does not exist and refuse every caller, Owner included.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("CreateBab")
            .WithTags("Babs");
    }
}

using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.CreateSubcontractor;

/// <summary>
/// <c>POST /api/subcontractors</c> — the Technical Office (or the Owner, D-129 §1) registers one
/// مقاول باطن. KAFF-211.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.SubcontractorManage</c> is <c>CompanyWide</c>, granted to <c>Role.Owner</c> and
/// <c>Role.TechnicalOffice</c> [Verified: 2026-09-12 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.SubcontractorManage</c> row] — spec.md §2, D-044 ruling 4, D-129 §1. **Finance is
/// deliberately absent** — §2 gives Finance the disbursement, never the record (AC-211-J).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> A subcontracting firm belongs to no project (rule 11), so declaring
/// one would make the evaluator look for an assignment that does not exist and refuse every caller,
/// Owner included.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("CreateSubcontractor")
            .WithTags("Subcontractors");
    }
}

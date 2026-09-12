using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.SetTaxRegistration;

/// <summary>
/// <c>PUT /api/subcontractors/{subcontractorId}/tax-registration</c> — Finance sets or clears one
/// firm's tax registration number. decisions.md D-147.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.SubcontractorTaxRegistrationEdit</c> is <c>CompanyWide</c>, granted to
/// <c>Role.Owner</c> and <c>Role.Finance</c> [Verified: 2026-09-12 @ <c>PermissionCatalogue.cs</c> -&gt;
/// the <c>Permission.SubcontractorTaxRegistrationEdit</c> row]. The Technical Office holds
/// <c>SubcontractorManage</c>, not this row, so it is refused here even though it owns the rest of the
/// record (AC-211-N).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> A firm belongs to no project, same as <c>SubcontractorManage</c>
/// itself.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/{subcontractorId:guid}/tax-registration";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorTaxRegistrationEdit)
            .WithName("SetSubcontractorTaxRegistration")
            .WithTags("Subcontractors");
    }
}

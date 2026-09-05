using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Audit.ReadAuditTrail;

/// <summary>
/// <c>GET /api/audit</c> — the Owner reads the history of who changed what. KAFF-117.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.AuditRead</c> is <c>CompanyWide</c> and granted to <c>Role.Owner</c> alone
/// [Verified: 2026-09-05 @ <c>src/Domain/Authorization/PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.AuditRead</c> row] — spec.md §9 amendment of 2026-08-21, decisions.md D-049
/// ruling 1.
/// </para>
/// <para>
/// <b>This is the strictest gate in the system, and the clause that makes it so is
/// "even for their own projects".</b> Every other permission here is <c>role × assignment</c>: a
/// Technical Office lead who runs project A reaches project A. This one refuses them, on A, on their
/// own changes, on everything. Karim ruled it in those words and the rejected option is the one worth
/// keeping visible — a project-scoped audit read for the people working on that project. From slice 3
/// the trail carries every movement of money, so scoping it by project would reopen §9's
/// zero-financial-visibility rule from a direction nobody was watching. <b>A filtered trail for a
/// non-Owner is a defect, not a partial success.</b>
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>, and <c>?projectId=</c> is a filter rather than a scope.</b> That
/// distinction is the whole of the ruling. A project-scoped declaration would send the request through
/// <c>ProjectAccessPolicy</c> and admit anybody the policy admits to that project — which is the read
/// D-049 rejected — as well as failing
/// <c>Every_permission_requirement_declares_the_scope_its_catalogue_row_names</c>, because the
/// catalogue row is company-wide. The gate answers "is this the Owner", and the query string then
/// narrows what the Owner is shown. Rule 5 of the story: <i>filtering is not the same as scoping, and
/// only the Owner does either.</i>
/// </para>
/// <para>
/// <b>⚠️ On a read there is no audit backstop</b> (decisions.md D-110 §2). On a write, deleting the
/// line below reddens most of a suite for a second, unrelated reason: nothing calls
/// <c>ActorVerifiedAs</c>, so <c>ck_audit_records_actor_is_named_completely</c> refuses the row. This
/// endpoint writes nothing, so no constraint fires — an ungated <c>GET /api/audit</c> would simply
/// hand every state change Kaff has ever recorded to whoever asked. <b>The permission test is very
/// nearly the entire control here</b>, which is why it derives its refused set from the <c>Role</c>
/// enum rather than from a list somebody remembered to keep up to date (<c>V-33-A</c>, D-118).
/// </para>
/// <para>
/// <b>No audit record.</b> Reading writes nothing — story rule 10: an audit record per audit read
/// would bury the records that matter. Asserted by <c>AuditCoverageTests</c> -&gt;
/// <c>Ten_reads_write_no_audit_record</c>, which reads this route in its loop and carries a positive
/// control so that "the count did not change" cannot be satisfied by a counter that cannot change
/// (decisions.md D-116 §3).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/audit";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.AuditRead)
            .WithName("ReadAuditTrail")
            .WithTags("Audit");
    }
}

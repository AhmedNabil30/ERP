using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Users.ListUsers;

/// <summary>
/// <c>GET /api/users</c> — the Owner's user administration list. KAFF-127, S-006.
/// </summary>
/// <remarks>
/// <para>
/// <b>Added by the Frontend story, and reported rather than slipped in.</b> Five identity endpoints
/// were merged with no way to reach them and <b>no read at all</b>: <c>AC-127-A</c> asks for a user
/// list and nothing could populate one. This is the same shape as <c>GET /api/clients/{clientId}</c>
/// on 2026-09-04 (D-113 §1) — writing the screen found the missing endpoint — and it carries the same
/// three obligations: its own permission gate, its own whitelisted response type, and its own tests.
/// </para>
/// <para>
/// <b><c>UserManage</c>, not <c>UserRead</c>, and the choice is spec.md's rather than this file's.</b>
/// <c>Permission.UserRead</c> exists and is held by <c>Role.Owner</c> and <c>Role.Hr</c> — but
/// spec.md §9's 2026-08-22 amendment (D-055 §3) draws the line in as many words: HR's grant is
/// <i>"names and roles only"</i> and it <i>"does not hand HR the Owner's user administration surface —
/// usernames, departments and active state for every account"</i>. This payload is exactly that
/// surface, so gating it <c>UserRead</c> would hand HR the screen that amendment exists to withhold.
/// <b><c>UserRead</c> therefore still has no endpoint</b>, which is the state this story found and did
/// not change: HR's narrow names-and-roles list is a screen nobody has cut. Reported, not decided.
/// </para>
/// <para>
/// Company-wide, so no scope is declared — the same reasoning the four sibling endpoints carry:
/// <c>ProjectScope.FromRoute()</c> would find no project and refuse every caller including the Owner.
/// A test fails the build if the <c>RequirePermission</c> line below is absent
/// [Verified: 2026-09-05 @ <c>EndpointPermissionCoverageTests.cs</c> -&gt;
/// <c>Every_mapped_endpoint_carries_a_permission_requirement</c>], decisions.md D-067 and D-069.
/// </para>
/// <para>
/// <b>No search parameter and no status filter.</b> S-006 draws both; <c>AC-127-A</c>…<c>AC-127-I</c>
/// ask for neither, and a query parameter with no criterion behind it is a second implementation of
/// <c>ListClients</c>' matching rules built on the assumption that users are searched the same way as
/// clients. Owed, and recorded as owed rather than guessed (KAFF-127 report, 2026-09-05).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/users";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.UserManage)
            .WithName("ListUsers")
            .WithTags("Users");
    }
}

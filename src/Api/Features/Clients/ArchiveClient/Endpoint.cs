using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.ArchiveClient;

/// <summary>
/// <c>POST /api/clients/{clientId}/archive</c> — take a client off the working list. KAFF-123.
/// </summary>
/// <remarks>
/// <para>
/// <b>A verb on a sub-resource, and deliberately not <c>DELETE /api/clients/{id}</c>.</b> KAFF-123
/// rule 1: a client is archived, never deleted — spec.md §2 requires full history and §3 requires a
/// reopened opportunity to attach to the same client, and both are impossible if the row can
/// disappear. A route spelled `DELETE` invites somebody to make it do what it says.
/// <c>AC-123-D</c> asserts no delete route exists on the whole surface, by enumerating what the host
/// actually mapped rather than grepping source for the word (`V-32-A`'s lesson: an absence proved by
/// a word search is proved against the word, not the behaviour).
/// </para>
/// <para>
/// <b>No body.</b> There is one thing this act can do, it takes no options, and a request that
/// carries nothing cannot carry the wrong thing. Whether the operator meant it is the confirm dialog's
/// job (`ux/slice-1-flows.md` -&gt; `S-014 · Client detail and edit`, the danger zone), not a flag.
/// </para>
/// <para>
/// <b>Gated <c>ClientManage</c>, company-wide</b> [Verified: 2026-09-04 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.ClientManage</c> row] — spec.md §2, D-044
/// ruling 4. That one line is <c>AC-123-E</c>, and it is also what keeps <c>Role.Client</c> out.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/{clientId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .WithName("ArchiveClient")
            .WithTags("Clients");
    }
}

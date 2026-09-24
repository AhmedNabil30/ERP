using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Projects.CreateProject;

/// <summary>
/// <c>POST /api/projects</c> — open a project. KAFF-400, S-051.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.ProjectCreate</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> and <c>Role.TechnicalOffice</c> — Karim, 2026-08-21: "The only people who can open
/// (create) a new project on the system are Me (the Owner) and the Technical Office," decisions.md
/// D-052 §2, D-055 §3. No <c>ProjectScope</c>: a create request cannot name the project it is about to
/// create, which is the entire reason <c>ProjectCreate</c> is a separate, company-wide row from
/// <c>ProjectManage</c> rather than <c>ProjectManage</c> being widened — decisions.md D-055 §3 names
/// the merge as the mistake a later session must not make.
/// </para>
/// <para>
/// <b>Marketing, Finance, HR and every Site Engineer are refused by this one line.</b> Marketing
/// registers the client and owns the client master file; opening a project "triggers engineering items,
/// accounting ledgers, and cost tracking" (Karim), which is why it moved to the Owner and the Technical
/// Office rather than staying with whoever brought the client in.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ProjectCreate)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("CreateProject")
            .WithTags("Projects");
    }
}

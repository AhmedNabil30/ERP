using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>
/// <c>POST /api/projects/{projectId}/extracts/{id}/technical-approval</c>. The Technical Office quantity gate, which
/// gates quantities and never money (spec.md section 9). Gated <c>QuantityGateApprove</c>; role and assignment.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/technical-approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.QuantityGateApprove, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ApproveTechnicalOffice")
            .WithTags("Extracts");
    }
}

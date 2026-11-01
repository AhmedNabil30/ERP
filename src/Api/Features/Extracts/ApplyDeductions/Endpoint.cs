using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ApplyDeductions;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/deductions</c>. Accounts applies the deductions; the calculator derives every figure. Finance alone: the Owner is deliberately excluded (D-189 section 2). Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/deductions";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractDeductionsApply, ProjectScope.FromRoute())
            .WithName("ApplyDeductions")
            .WithTags("Extracts");
    }
}

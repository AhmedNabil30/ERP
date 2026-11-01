using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.RejectExtract;

/// <summary>
/// Three routes, one handler. Any rejection at any gate returns the extract to Draft with a stored reason
/// (spec.md section 7), and the permission to reject is the permission to decide at that gate, so each gate has
/// its own route and its own gate: an endpoint can declare only one permission. A route called for an extract
/// that is not waiting at its gate is refused, so the Technical Office cannot reject at the Owner's gate.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string TechnicalOfficeRoute = "/api/projects/{projectId:guid}/extracts/{id:guid}/technical-rejection";
    public const string AccountsRoute = "/api/projects/{projectId:guid}/extracts/{id:guid}/accounts-rejection";
    public const string OwnerRoute = "/api/projects/{projectId:guid}/extracts/{id:guid}/owner-rejection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapGate(app, TechnicalOfficeRoute, Permission.QuantityGateApprove, "RejectExtractAtTechnicalOffice", [ExtractStatus.QcSubmitted]);
        MapGate(app, AccountsRoute, Permission.ExtractDeductionsApply, "RejectExtractAtAccounts", [ExtractStatus.TechnicalApproved, ExtractStatus.PartiallyApproved]);
        MapGate(app, OwnerRoute, Permission.FinancialMovementApprove, "RejectExtractAtOwner", [ExtractStatus.DeductionsApplied]);
    }

    private static void MapGate(
        IEndpointRouteBuilder app, string route, Permission permission, string name, ExtractStatus[] waitingAt)
        => app.MapPost(route, (
                [FromRoute] Guid projectId,
                [FromRoute] Guid id,
                Request request,
                KaffDbContext database,
                RulingSet rulings,
                TimeProvider clock,
                ICurrentUser currentUser,
                IAuthorizationService authorization,
                ClaimsPrincipal user,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(
                    projectId, id, request, waitingAt, database, rulings, clock, currentUser, authorization, user, cancellationToken))
            .RequirePermission(permission, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName(name)
            .WithTags("Extracts");
}

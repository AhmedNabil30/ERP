using System.Security.Claims;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.ChangeOrders.ApproveTechnicalReview;

/// <summary>TechnicalReview to ClientApproval. A different person from everyone who acted so far.</summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
        => ChangeOrderSupport.RunAsync(
            projectId,
            id,
            new ChangeOrderServices(database, rulings, clock, currentUser, authorization, user),
            (order, project, services) => order.ApproveTechnicalReview(ChangeOrderSupport.Actor(services), ChangeOrderSupport.Now(services)),
            cancellationToken);
}

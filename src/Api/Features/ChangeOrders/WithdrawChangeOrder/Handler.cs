using System.Security.Claims;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.ChangeOrders.WithdrawChangeOrder;

/// <summary>Takes the change order back before approval.</summary>
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
            (order, project, services) => order.Withdraw(ChangeOrderSupport.Actor(services), ChangeOrderSupport.Now(services)),
            cancellationToken);
}

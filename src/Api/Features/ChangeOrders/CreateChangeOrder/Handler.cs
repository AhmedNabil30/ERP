using System.Security.Claims;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.ChangeOrders.CreateChangeOrder;

/// <summary>
/// Creates the change order. The number is the project highest plus one; the unique index on (project, number) is
/// the enforcement of record. Whether the project may take a change order is the ruled project-state check inside
/// <c>ChangeOrder.Create</c>.
/// </summary>
internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<ChangeOrderLineDraft> lines =
        [
            .. (request.Lines ?? []).Select(line => new ChangeOrderLineDraft(
                line.Description ?? string.Empty, line.Unit ?? string.Empty, line.Quantity, line.UnitPrice)),
        ];

        return ChangeOrderSupport.CreateAsync(
            projectId,
            request.Title,
            lines,
            new ChangeOrderServices(database, rulings, clock, currentUser, authorization, user),
            cancellationToken);
    }
}

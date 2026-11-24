using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.PettyCash;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.PettyCash.ListRequests;

/// <summary>One عهدة request as a list row. <c>DecidedAt</c> is the submission or the return instant; <c>SupervisorName</c> is who submitted or returned it, null while it is a draft.</summary>
public sealed record Row(
    Guid Id,
    Money Amount,
    string Purpose,
    Guid DrafterUserId,
    string DrafterName,
    string? SupervisorName,
    PettyCashRequestStatus Status,
    string? ReturnedReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        PettyCashRequestStatus? status,
        KaffDbContext database,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(authorization);

        Guid me = currentUser.UserId ?? Guid.Empty;
        bool sees = (await authorization.AuthorizeAsync(
            user, resource: null, PermissionRequirement.ToPolicyName(Permission.DraftSubmit, ProjectScope.FromRoute()))).Succeeded;

        List<Row> rows = await (
            from request in database.PettyCashRequests.AsNoTracking()
            join drafter in database.Users.AsNoTracking() on request.RequestedByUserId equals drafter.Id
            let supervisorId = request.SubmittedByUserId ?? request.ReturnedByUserId
            where request.ProjectId == projectId
                && (status == null || request.Status == status)
                && (sees || request.RequestedByUserId == me)
            orderby request.CreatedAt descending
            select new Row(
                request.Id,
                request.Amount,
                request.Purpose,
                drafter.Id,
                drafter.FullName,
                database.Users.Where(person => person.Id == supervisorId).Select(person => person.FullName).FirstOrDefault(),
                request.Status,
                request.ReturnReason,
                request.CreatedAt,
                request.SubmittedAt ?? request.ReturnedAt)).ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(rows);
    }
}

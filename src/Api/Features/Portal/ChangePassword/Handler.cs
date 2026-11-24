using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Api.Identity;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.Portal.ChangePassword;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        HttpContext http,
        KaffDbContext database,
        IAuditContext auditContext,
        StaffSessionMinter minter,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(http);

        // Active, current stamp and the client role, already applied by RequirePortalSession on this scoped DbContext.
        User user = LiveSession.Caller(http);

        if (!PasswordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return ResultExtensions.Problem(AuthorizationErrors.CurrentPasswordIncorrect);
        }

        Result changed = user.SetOwnPassword(PasswordHasher.Hash(request.NewPassword ?? string.Empty));

        if (changed.IsFailure)
        {
            return ResultExtensions.Problem(changed.Error);
        }

        // No permission gate ran on this self-only route, so the actor is named here, after the inbound identity is discarded
        // (the same shape the staff change-password uses, D-075).
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        auditContext.AttributeTo(new AuditActor(user.Id, user.FullName, user.Role));

        await database.SaveChangesAsync(cancellationToken);

        // SetOwnPassword rotated the stamp, which ended every other session. This device gets a fresh cookie.
        minter.IssuePortal(http.Response, user, clock.GetUtcNow());

        return Results.NoContent();
    }
}

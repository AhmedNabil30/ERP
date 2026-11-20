using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kaff.Api.Features.Ncrs;

internal static class NcrSupport
{
    /// <summary>
    /// The permission that closes an NCR (<c>NcrCloser</c>, an assumption, D-250 item 1): the Technical Office's gate,
    /// the one that held the portion back. The close route and every read's close flag ask this one constant.
    /// </summary>
    public const Permission ClosePermission = Permission.QuantityGateApprove;

    /// <summary>Whether the caller holds the close permission on the route's project, asked of the real evaluator.</summary>
    public static async Task<bool> MayCloseAsync(IAuthorizationService authorization, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        AuthorizationResult result = await authorization.AuthorizeAsync(
            user, resource: null, PermissionRequirement.ToPolicyName(ClosePermission, ProjectScope.FromRoute()));
        return result.Succeeded;
    }
}

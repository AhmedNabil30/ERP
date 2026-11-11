using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kaff.Api.Features.Projects;

/// <summary>
/// Asks, for the project named in the route, whether the caller holds a permission. The server decides what the project
/// frame shows (KAFF-420): a tab or a figure is sent only to a role that may open the route behind it.
/// </summary>
internal static class CallerPermissions
{
    public static async Task<bool> HoldsAsync(IAuthorizationService authorization, ClaimsPrincipal user, Permission permission)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        AuthorizationResult result = await authorization.AuthorizeAsync(
            user, resource: null, PermissionRequirement.ToPolicyName(permission, ProjectScope.FromRoute()));
        return result.Succeeded;
    }
}

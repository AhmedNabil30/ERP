using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Authorization;

/// <summary>
/// Builds an authorization policy on demand from an encoded permission name.
/// </summary>
/// <remarks>
/// Without this, every permission × project-scope combination would have to be registered by hand in
/// <c>Program</c>, and a slice author adding an endpoint would have to remember to add its policy too.
/// Forgetting would leave the endpoint unprotected. Generating the policy from the name it declares
/// removes that failure mode entirely.
/// </remarks>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionRequirement.TryParse(policyName, out PermissionRequirement? requirement) && requirement is not null)
        {
            AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(requirement)
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>How an endpoint declares what it needs.</summary>
public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires a company-level permission.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapPost("/api/clients", CreateClient.HandleAsync)
    ///    .RequirePermission(Permission.ClientManage);
    /// </code>
    /// </example>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(PermissionRequirement.ToPolicyName(permission, ProjectScope.None));

    /// <summary>
    /// Requires a project-scoped permission. The caller must also be assigned to the project
    /// (spec.md §9).
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapPost("/api/projects/{projectId:guid}/extracts/{id:guid}/approve", ApproveExtract.HandleAsync)
    ///    .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute());
    /// </code>
    /// </example>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission, ProjectScope scope)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(PermissionRequirement.ToPolicyName(permission, scope));
}

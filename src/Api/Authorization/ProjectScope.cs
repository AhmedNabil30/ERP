using System.Globalization;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Kaff.Api.Authorization;

/// <summary>Where the project identifier is found on the request.</summary>
public enum ProjectScopeSource
{
    /// <summary>Company-level. No project is looked for.</summary>
    None = 0,

    /// <summary>A route value, e.g. <c>/api/projects/{projectId}/extracts</c>.</summary>
    Route = 1,

    /// <summary>A query-string value.</summary>
    Query = 2,
}

/// <summary>
/// How a project-scoped endpoint says where its project identifier lives.
/// </summary>
/// <remarks>
/// Route and query only. The body is deliberately excluded: a body has to be buffered and parsed
/// before authorization can run, which means an unauthorised request would be read and deserialised
/// before it was refused. Project-scoped endpoints therefore carry the project in the path — which
/// is also the shape that makes the URL say what it operates on.
/// </remarks>
public sealed record ProjectScope(ProjectScopeSource Source, string Key)
{
    public const string DefaultKey = "projectId";

    public static readonly ProjectScope None = new(ProjectScopeSource.None, string.Empty);

    public static ProjectScope FromRoute(string key = DefaultKey) => new(ProjectScopeSource.Route, key);

    public static ProjectScope FromQuery(string key = DefaultKey) => new(ProjectScopeSource.Query, key);
}

/// <summary>The requirement an endpoint declares. Carries the permission and where to find the project.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(Permission permission, ProjectScope scope)
    {
        Permission = permission;
        Scope = scope;
    }

    public Permission Permission { get; }

    public ProjectScope Scope { get; }

    /// <summary>Policy-name prefix. Endpoints never see this; the policy provider round-trips it.</summary>
    public const string PolicyPrefix = "kaff.perm";

    /// <summary>Encodes the requirement into a policy name.</summary>
    public static string ToPolicyName(Permission permission, ProjectScope scope)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{PolicyPrefix}:{permission}:{scope.Source}:{scope.Key}");

    /// <summary>Decodes a policy name produced by <see cref="ToPolicyName"/>.</summary>
    public static bool TryParse(string policyName, out PermissionRequirement? requirement)
    {
        requirement = null;

        if (string.IsNullOrEmpty(policyName))
        {
            return false;
        }

        string[] parts = policyName.Split(':', 4);

        if (parts.Length != 4
            || !string.Equals(parts[0], PolicyPrefix, StringComparison.Ordinal)
            || !Enum.TryParse(parts[1], ignoreCase: false, out Permission permission)
            || !Enum.TryParse(parts[2], ignoreCase: false, out ProjectScopeSource source))
        {
            return false;
        }

        requirement = new PermissionRequirement(permission, new ProjectScope(source, parts[3]));
        return true;
    }
}

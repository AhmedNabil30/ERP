using Kaff.Domain.Identity;

namespace Kaff.Domain.Authorization;

/// <summary>
/// Answers "may this user reach this project at all", independently of what they want to do there.
/// </summary>
/// <remarks>
/// Two ways in, and only two:
/// <list type="bullet">
/// <item>Staff — an active <see cref="ProjectAssignment"/> row (spec.md §9).</item>
/// <item>A portal client — the project's client is the user's client (spec.md §12).</item>
/// </list>
/// spec.md §12 is absolute that a client MUST NEVER see another client's data, so the client branch
/// compares identifiers rather than trusting anything in the request.
/// </remarks>
public interface IProjectAccessPolicy
{
    Task<ProjectAccess> EvaluateAsync(PermissionSubject subject, Guid projectId, CancellationToken cancellationToken);
}

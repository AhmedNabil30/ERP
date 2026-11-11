using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Boqs;

/// <summary>
/// Whether the caller may open another project's BOQ: the same rule as <c>GET /boq</c>, role and assignment, checked
/// here because the source project comes from the body and not from the route (D-217 item 19).
/// </summary>
internal static class SourceProjectAccess
{
    public static async Task<bool> MayOpenBoqAsync(
        ICurrentUser currentUser,
        IPermissionSubjectReader subjectReader,
        IProjectAccessPolicy accessPolicy,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        PermissionSubject? subject = currentUser.UserId is { } userId
            ? await subjectReader.ReadAsync(userId, currentUser.SecurityStamp, cancellationToken)
            : null;

        if (subject is null)
        {
            return false;
        }

        ProjectAccess access = await accessPolicy.EvaluateAsync(subject, projectId, cancellationToken);

        return PermissionEvaluator.Evaluate(subject, Permission.ProjectManage, projectId, access) == PermissionDecision.Granted;
    }
}

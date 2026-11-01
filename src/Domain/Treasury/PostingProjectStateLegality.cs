using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Treasury;

/// <summary>
/// The posting-type × project-state check. Sits beside <see cref="PostingAccountLegality"/>, which
/// answers "may this type touch these accounts"; this answers "may this type be posted to a project in
/// this state".
/// </summary>
/// <remarks>
/// The table itself is <see cref="PostingProjectStatePolicy"/>, a ruling. <see cref="Posting.Create"/>
/// cannot see a project, so the callers that hold one — <see cref="PostingSetBuilder"/>, PostMovement,
/// ReversePosting — call this. A company-level posting names no project and is not checked.
/// </remarks>
public static class PostingProjectStateLegality
{
    public static Result Validate(
        PostingProjectStatePolicy policy,
        ProjectStatus projectStatus,
        PostingType type,
        bool isReversal)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (isReversal && !policy.ReversalsFollowTheTable)
        {
            return Result.Success();
        }

        return policy.Allows(projectStatus, type)
            ? Result.Success()
            : Result.Failure(TreasuryErrors.PostingNotAllowedInProjectState);
    }
}

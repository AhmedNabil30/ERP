using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour;

/// <summary>
/// "Responsible" means the engineer who opened the engagement, or the Owner. decisions.md D-152 §4,
/// D-153 §1 point 4 and point 7 — the one guard <c>SetEngagementDayRate</c> and <c>RateEngagement</c>
/// both call, written once rather than copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>Q77 does NOT extend here.</b> D-152 §3 lets any engineer assigned to the project close an
/// engagement another engineer opened — an administrative act, so an engagement does not dangle while
/// somebody is away. A day rate is a term one engineer agreed on site and a rating is that engineer's
/// judgement of a man; D-153 §1 point 4 reads Karim's own wording ("the responsible Site Engineer",
/// "Only the responsible Site Engineer rates") as refusing the same widening for these two acts. See
/// <c>CloseEngagement.Handler</c>, which carries no such check by design.
/// </para>
/// <para>
/// <b>The caller's role is re-read from the database, not trusted from the token.</b> The permission
/// gate has already re-read role, department and active state fresh (decisions.md D-048) to grant
/// <c>DayLabourRateManage</c> or <c>DayLabourSiteManage</c> in the first place; a stale "Owner" claim
/// surviving past a role change would otherwise bypass the opener check the gate itself cannot
/// express, because the gate only knows the permission was granted, not which of its grantees is
/// calling. The opener comparison is checked first and needs no query at all.
/// </para>
/// </remarks>
internal static class EngagementResponsibility
{
    public static async Task<bool> IsResponsibleOrOwnerAsync(
        KaffDbContext database, Guid? callerUserId, Engagement engagement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(engagement);

        if (callerUserId is not null && callerUserId == engagement.OpenedByUserId)
        {
            return true;
        }

        if (callerUserId is null)
        {
            return false;
        }

        Role? role = await CallerRoleAsync(database, callerUserId, cancellationToken);

        return role == Role.Owner;
    }

    /// <summary>
    /// The caller's role, re-read fresh from the database — same reasoning as the type's remarks.
    /// Shared with <c>ListEngagements.Handler</c>, which branches on the role itself rather than only
    /// on whether it is the Owner.
    /// </summary>
    public static Task<Role?> CallerRoleAsync(
        KaffDbContext database, Guid? callerUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        return callerUserId is null
            ? Task.FromResult<Role?>(null)
            : database.Users
                .Where(user => user.Id == callerUserId)
                .Select(user => (Role?)user.Role)
                .SingleOrDefaultAsync(cancellationToken);
    }
}

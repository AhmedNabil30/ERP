using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Authorization;

/// <summary>
/// What a route outside <c>RequirePermission</c> owes, applied by construction rather than by each
/// author remembering.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Every endpoint exempted from the permission gate for a good reason has to
/// re-apply, by hand, the checks the gate would have applied. <c>POST /api/auth/change-password</c>
/// (D-086) and <c>GET /api/auth/me</c> (D-087) each copied two of them —
/// <see cref="User.IsActive"/> and the security stamp — and both dropped the third, the role bar that
/// <c>PermissionEvaluator</c> applies before the catalogue is consulted and that
/// <c>StaffSessionMinter.Issue</c> applies before a session can exist at all. So
/// <c>GET /api/auth/me</c> answered a <see cref="Role.Subcontractor"/> with <c>200</c> and their name
/// — spec.md §9, "record only, no login" — and <c>POST /api/auth/sign-out</c>, which copied none of the
/// three, wrote a permanent audit row on the authority of a token every gated route already refused.
/// See qa/slice-1/verification-2026-08-26.md, <c>V-26-B</c> and <c>V-26-C</c>.
/// </para>
/// <para>
/// <b>The list of exempt routes is designed to grow.</b> Each entry in
/// <c>EndpointPermissionCoverageTests.AllowList</c> and <c>SelfOnlyEndpoints</c> records *why* it is
/// exempt; nothing recorded what it therefore *owes*, and nothing went red when an author paid two of
/// three. A hand-copy is one item short eventually — that is what a hand-copy is. This is the one
/// place the three checks are written, and <see cref="RequireLiveSession"/> is what makes applying
/// them the same act as declaring the exemption.
/// </para>
/// <para>
/// <b>The refusal is the blanket one and must stay blanket.</b> A caller refused here gets
/// <c>403</c> / <c>errors.auth.forbidden</c> for all three reasons alike — D-071, D-080. Nabil, on why
/// the shape matters more than the accuracy: <i>"If we return a specific
/// <c>errors.auth.role_cannot_log_in</c>, we are explicitly telling the attacker: 'This account exists
/// and belongs to a subcontractor.' That is a security breach."</i>
/// <c>AuthorizationErrors.RoleCannotLogIn</c> is deliberately not used here, and neither is
/// <see cref="SpecificRefusal"/> (D-086) — that mechanism exists for a refusal that discloses nothing
/// beyond what holding the credential already implies, which this is not.
/// </para>
/// <para>
/// <b>What it does not cover.</b> A future handler could still load the caller's own row by hand from
/// <c>ICurrentUser.UserId</c> and skip all three. Two tests narrow that:
/// <c>EndpointPermissionCoverageTests.Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own</c>
/// fails when a self-only route does not carry <see cref="Marker"/>, and
/// <c>No_feature_handler_reads_the_callers_identity_from_the_token_itself</c> fails when any file under
/// <c>src/Api/Features/</c> mentions <c>KaffClaimTypes</c> — the hand-roll all three defective handlers
/// actually used.
/// </para>
/// </remarks>
public static class LiveSession
{
    private const string CallerKey = "Kaff.LiveSessionCaller";

    /// <summary>
    /// Metadata proving a route applies these checks. Added by <see cref="RequireLiveSession"/> and by
    /// nothing else, so a route cannot claim the exemption without paying for it.
    /// </summary>
    public sealed class Marker
    {
        internal static readonly Marker Instance = new();

        private Marker()
        {
        }
    }

    /// <summary>
    /// The caller's row, or <see langword="null"/> when this request holds no session the rest of the
    /// system would still honour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same three facts <c>PermissionSubjectReader</c> and <c>PermissionEvaluator</c> establish
    /// together on every <c>RequirePermission</c> route: the account is active, the token's stamp is
    /// still the stored one (D-048, D-053 — the global kill), and the role may hold a staff session at
    /// all (spec.md §9, decisions.md D-062 §2).
    /// </para>
    /// <para>
    /// <b>Tracked, not <c>AsNoTracking</c>.</b> <c>ChangePassword</c> mutates the row it is handed, and
    /// the filter and the handler share one scoped <c>KaffDbContext</c>. A read-only caller tracking
    /// one row it never saves costs nothing; a second identical query would.
    /// </para>
    /// <para>
    /// Public and callable directly, for the anonymous half of the exemption list: sign-out must answer
    /// <c>204</c> whether or not a session exists (KAFF-102 rule 7), so it cannot use the refusing
    /// filter — it asks this the same question and writes its audit row only on a live answer.
    /// </para>
    /// </remarks>
    public static async Task<User?> ResolveAsync(
        HttpContext http,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(database);

        // ICurrentUser is the same reader the permission gate uses for the token half of a request,
        // so the claim names live in one place rather than being re-parsed per handler.
        ICurrentUser current = http.RequestServices.GetRequiredService<ICurrentUser>();

        if (!current.IsAuthenticated
            || current.UserId is not { } userId
            || string.IsNullOrEmpty(current.SecurityStamp))
        {
            return null;
        }

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null
            || !user.IsActive
            || !string.Equals(user.SecurityStamp, current.SecurityStamp, StringComparison.Ordinal)
            || !user.Role.MayHoldStaffSession())
        {
            return null;
        }

        http.Items[CallerKey] = user;

        return user;
    }

    /// <summary>
    /// Applies <see cref="ResolveAsync"/> before the handler runs and refuses the request when it
    /// answers <see langword="null"/>. The whole of what a <c>SelfOnlyEndpoints</c> route owes.
    /// </summary>
    /// <remarks>
    /// An endpoint filter rather than an authorization policy: the checks produce the caller's row,
    /// which both self-only handlers then need, and a policy would have to hand it over through the
    /// same <see cref="HttpContext.Items"/> channel while also being parsed as a permission by
    /// <c>EndpointPermissionCoverageTests</c>, which it is not.
    /// </remarks>
    public static RouteHandlerBuilder RequireLiveSession(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddEndpointFilter(async (context, next) =>
            {
                HttpContext http = context.HttpContext;

                User? caller = await ResolveAsync(
                    http,
                    http.RequestServices.GetRequiredService<KaffDbContext>(),
                    http.RequestAborted);

                return caller is null
                    ? ResultExtensions.Problem(AuthorizationErrors.Forbidden)
                    : await next(context);
            })
            .WithMetadata(Marker.Instance);
    }

    /// <summary>The row <see cref="RequireLiveSession"/> already checked. Never null behind that filter.</summary>
    public static User Caller(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return http.Items[CallerKey] as User
            ?? throw new InvalidOperationException(
                "No live session was resolved for this request. A handler reading the caller's row "
                + "must sit behind RequireLiveSession() — see LiveSession's remarks.");
    }
}

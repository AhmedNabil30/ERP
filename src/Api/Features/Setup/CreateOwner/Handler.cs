using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Setup.CreateOwner;

/// <summary>
/// Mints the one Owner the system starts with. KAFF-100.
/// </summary>
/// <remarks>
/// <para>
/// <b>The friendly check and the real one are different mechanisms, and only one of them is
/// load-bearing.</b> <c>Users.AnyAsync()</c> below is the courtesy path — it saves hashing a password
/// for a request that cannot possibly succeed. What actually decides two concurrent requests is
/// <c>ux_users_bootstrap_owner_once</c>
/// [Verified: 2026-08-26 @ <c>IdentityConfigurations.cs</c> -&gt; <c>UserConfiguration</c>], a unique
/// index the database enforces regardless of what either request's own read believed — the same shape
/// <c>ux_users_user_name</c> already uses for the identical class of race (<c>CreateUser/Handler.cs</c>
/// -&gt; <c>IsUserNameCollision</c>). <b>Rule 6</b>, and CLAUDE.md's safe-balance precedent: "enforced
/// by a database constraint, not application code."
/// </para>
/// <para>
/// <b>No audit code is hand-written here.</b> The <c>User</c> row is a change the tracker sees, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c> record in the same transaction — the
/// only thing this handler adds is <i>who</i>: <see cref="IAuditContext.AttributeTo"/>, naming the new
/// Owner as their own actor, because an anonymous request has no other identity to attribute it to and
/// a null actor is exactly what D-051 (Q31) rejected the seed to avoid. decisions.md D-061.
/// </para>
/// <para>
/// No <c>Money</c> moves. It creates the account every financial movement in the system will
/// eventually be approved by.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        IAuditContext auditContext,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The courtesy path. AC-100-B, AC-100-E: any row at all — any role, active or not — refuses
        // this endpoint for good. Rule 5: there is no other switch behind this check.
        bool systemInitialised = await database.Users.AnyAsync(cancellationToken);

        if (systemInitialised)
        {
            return ResultExtensions.Problem(SetupErrors.AlreadyCompleted);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);
        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        Result<User> created = User.CreateBootstrapOwner(
            request.UserName ?? string.Empty,
            request.FullName ?? string.Empty,
            phone.Value,
            PasswordHasher.Hash(request.Password ?? string.Empty),
            clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        User owner = created.Value;

        // The guard decisions.md D-061 names: legal only because this request carries no identity at
        // all. An authenticated caller naming a different actor here would throw in the interceptor —
        // this is the one endpoint where the actor being created IS the actor doing the creating.
        auditContext.AttributeTo(new AuditActor(owner.Id, owner.FullName, owner.Role));

        database.Users.Add(owner);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsBootstrapRace(exception))
        {
            // AC-100-C. The loser of the race gets the same refusal as a plain second call — never a
            // 500, and never a second Owner.
            return ResultExtensions.Problem(SetupErrors.AlreadyCompleted);
        }
        catch (DbUpdateException exception) when (IsUserNameCollision(exception))
        {
            // Unreachable against an empty table in practice, but the same defence CreateUser's
            // handler carries for the identical index, kept here rather than assumed away.
            return ResultExtensions.Problem(IdentityErrors.UserNameTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/users/{owner.Id}",
            new Response(owner.Id, owner.UserName, owner.FullName, owner.Role, owner.IsActive));
    }

    /// <summary>A unique-violation on the bootstrap index, and nothing else.</summary>
    private static bool IsBootstrapRace(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_users_bootstrap_owner_once", StringComparison.Ordinal);

    /// <summary>A unique-violation on the username index, and nothing else.</summary>
    private static bool IsUserNameCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_users_user_name", StringComparison.Ordinal);
}

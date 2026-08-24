using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Users.CreateUser;

/// <summary>
/// Creates one user. KAFF-106.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule about who may hold what goes through <c>User.Create</c>, and nothing here
/// reproduces one.</b> That is not tidiness — it is <c>AC-106-K</c>'s entire subject. The domain
/// guard binding <c>Role.Hr</c> to <c>Department.Hr</c>
/// [Verified: 2026-08-23 @ <c>User.cs</c> -> <c>ValidateDepartment</c>] can only be bypassed by a
/// handler that never calls <c>Create</c>, or by one that "helpfully" corrects the department before
/// it does. This handler passes the request's department through untouched and returns whatever
/// <c>Create</c> says, so the refusal reaches the caller as a 400 rather than as a silently
/// corrected account.
/// </para>
/// <para>
/// <b>No audit record is written here.</b> Creation is an entity change, so the change tracker sees
/// it and <c>AuditSaveChangesInterceptor</c> writes the record in the same transaction — actor, role
/// and department in the after state, <c>PasswordHash</c> and <c>SecurityStamp</c> redacted by
/// <c>[AuditRedacted]</c>. A hand-written record here is what decisions.md D-031 and KAFF-118 rule 2
/// forbid; D-061's <c>IAuditContext.Events</c> is the sanctioned path for the things the change
/// tracker cannot see, and this is not one of them. <c>GrantPath</c> stays null because
/// <c>UserManage</c> is company-wide: no project, no access policy, no path to name.
/// </para>
/// <para>
/// No <c>Money</c> moves. It decides who may later move it, which is why the record above is not
/// optional.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);
        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        Result<User> created = User.Create(
            request.UserName ?? string.Empty,
            request.FullName ?? string.Empty,
            phone.Value,
            request.Role,
            clock.GetUtcNow(),
            request.Department,
            request.OperationsSubDepartment,
            request.ClientId,
            employeeId: null,
            request.Email);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        User user = created.Value;

        if (!string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            // SetTemporaryPassword, never SetOwnPassword. D-049 ruling 4: a credential somebody else
            // chose must be replaced on first sign-in, because until it is, two people know the
            // password that acts as this account and the trail cannot tell them apart. The two
            // methods differ in exactly one flag and picking the wrong one is silent.
            Result issued = user.SetTemporaryPassword(PasswordHasher.Hash(request.TemporaryPassword));

            if (issued.IsFailure)
            {
                // Today this is only Role.Subcontractor — "record only, no login" (spec.md §9).
                return ResultExtensions.Problem(issued.Error);
            }
        }

        // AC-106-G. User.Create lower-cases and trims, so an ordinal comparison against the stored
        // form is the case-insensitive check: NABIL cannot be taken while nabil exists.
        bool taken = await database.Users
            .AnyAsync(existing => existing.UserName == user.UserName, cancellationToken);

        if (taken)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNameTaken);
        }

        database.Users.Add(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUserNameCollision(exception))
        {
            // The check above is the friendly path, not the enforcement: two requests can both pass
            // it. ux_users_user_name is what actually holds the rule, and the loser of the race must
            // get the same refusal as everyone else rather than a 500.
            return ResultExtensions.Problem(IdentityErrors.UserNameTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/users/{user.Id}",
            new Response(
                user.Id,
                user.UserName,
                user.FullName,
                user.Role,
                user.Department,
                user.OperationsSubDepartment,
                user.ClientId,
                user.IsActive,
                user.MustChangePassword));
    }

    /// <summary>A unique-violation on the username index, and nothing else.</summary>
    private static bool IsUserNameCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_users_user_name", StringComparison.Ordinal);
}

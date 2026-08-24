using Kaff.Domain.Auditing;
using Kaff.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kaff.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Refuses to save an update or a delete on an append-only table.
/// </summary>
/// <remarks>
/// <para>
/// The database triggers in <c>001_guards.sql</c> are the authority — they hold whatever code runs,
/// including none of ours. This interceptor exists so the attempt fails with a message that names
/// the entity and the rule, at the point the developer wrote it, instead of surfacing as a
/// PostgreSQL exception halfway through a transaction.
/// </para>
/// <para>
/// It throws rather than returning a <c>Result</c>. Reaching here is not a business outcome a user
/// can act on; it is a programming error, and CLAUDE.md keeps exceptions for exactly that.
/// </para>
/// </remarks>
public sealed class AppendOnlySaveChangesInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Guard(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Guard(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Entity is Posting)
            {
                throw new InvalidOperationException(
                    "Postings are append-only (spec.md 6.1, CLAUDE.md). "
                    + "Create a reversing posting with Posting.Reverse instead of modifying or deleting one.");
            }

            if (entry.Entity is AuditRecord)
            {
                throw new InvalidOperationException(
                    "Audit records are append-only. Evidence that can be edited is not evidence.");
            }
        }
    }
}

using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;

namespace Kaff.Api.Common.Results;

/// <summary>
/// Saves postings and turns a database-guard refusal into the translated <see cref="Error"/> every other
/// refusal uses. The one place the guard mapping is applied: <c>PostMovement</c>, <c>ReversePosting</c>
/// and every handler that commits a <see cref="PostingSet"/> call it instead of carrying their own
/// <c>catch</c>.
/// </summary>
public static class GuardedSave
{
    /// <summary>
    /// Saves whatever the context has tracked in one transaction. Returns the translated error when a
    /// named guard in <c>001_guards.sql</c> refused it, or <see langword="null"/> on success.
    /// </summary>
    /// <remarks>
    /// Caught as a bare <see cref="Exception"/>, not <c>DbUpdateException</c>: a deferred constraint
    /// trigger — the non-negative-balance guard — fails at COMMIT, outside the command EF wraps, so it can
    /// arrive as a raw <c>PostgresException</c>. The <c>when</c> clause keeps this from swallowing
    /// anything that is not a named guard (AC-319-D).
    /// </remarks>
    public static async Task<Error?> SaveAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (DatabaseGuardTranslation.Translate(exception) is { } violation)
        {
            return violation;
        }
    }

    /// <summary>Adds every posting of <paramref name="set"/> and saves them together: all commit or none do.</summary>
    public static Task<Error?> CommitAsync(KaffDbContext database, PostingSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(set);

        database.Postings.AddRange(set.Postings);
        return SaveAsync(database, cancellationToken);
    }
}

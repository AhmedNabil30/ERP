using Kaff.Domain.Authorization;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Infrastructure.Authorization;

/// <summary>
/// Builds the caller's authorization facts from the users table. See
/// <see cref="IPermissionSubjectReader"/> for why they do not come from the token.
/// </summary>
public sealed class PermissionSubjectReader : IPermissionSubjectReader
{
    private readonly KaffDbContext _context;

    public PermissionSubjectReader(KaffDbContext context) => _context = context;

    public async Task<PermissionSubject?> ReadAsync(
        Guid userId,
        string? securityStamp,
        CancellationToken cancellationToken)
    {
        // No stamp, no access. Checked before the query so a token without the claim cannot even
        // cause a database round-trip — and so the refusal cannot be reached by a null-vs-null match.
        if (string.IsNullOrEmpty(securityStamp))
        {
            return null;
        }

        // Projected rather than materialised: this runs on every authorized request, and loading a
        // tracked User would put the caller's own row into the change tracker on every one of them.
        //
        // The stamp comparison is in the WHERE clause rather than in memory, which keeps it ordinal
        // and case-sensitive at the database. A stamp is an opaque identifier, not text a human
        // reads, so a culture-aware or case-insensitive comparison would only widen what matches.
        return await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId
                           && user.IsActive
                           && user.SecurityStamp == securityStamp)
            .Select(user => new PermissionSubject(
                user.Id,
                user.Role,
                user.Department,
                user.OperationsSubDepartment,
                user.ClientId,

                // Not an authorization fact, and the evaluator never reads it. The audit trail needs
                // the actor's own name and role from the row the gate has just verified rather than
                // from the token's claims, and this is the one read in a request that already has
                // that row in hand. See decisions.md D-075.
                user.FullName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

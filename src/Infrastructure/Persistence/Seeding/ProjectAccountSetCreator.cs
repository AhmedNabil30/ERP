using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Infrastructure.Persistence.Seeding;

/// <summary>
/// Opens a project's ledger-account set the moment the project exists (spec.md §6.3, §5.1-§5.3).
/// KAFF-302.
/// </summary>
/// <remarks>
/// <para>
/// <b>The per-project sibling of <see cref="AccountTreeSeeder"/>.</b> That seeder explicitly does not
/// seed project accounts, "because they are created with the project and the party they belong to"
/// [Verified: 2026-09-14 @ <c>AccountTreeSeeder.cs</c> header remark] — this class is that creation.
/// </para>
/// <para>
/// <b>Additive and idempotent</b> (rule 8, <c>AC-302-F</c>): a type already open for the project is
/// skipped, never re-created and never edited — the same discipline as
/// <c>AccountTreeSeeder.AddIfMissingAsync</c>.
/// </para>
/// <para>
/// <b>No domain-event bus, no MediatR</b> (rule 9): this is meant to be called synchronously by
/// whatever creates a project, in the same <c>SaveChangesAsync</c>. <see cref="CreateAsync"/> adds the
/// accounts to the context and returns without saving — the caller's own save is what makes account-set
/// creation atomic with the project row (<c>AC-302-E</c>): if either half fails, the whole transaction
/// rolls back and no project is left with zero accounts to post against.
/// </para>
/// <para>
/// A metadata defect throws rather than returning a <see cref="Result"/>, the same choice
/// <c>AccountTreeSeeder</c> makes for the same reason: <c>AccountTypes</c> is fixed code, so a failure
/// here is a defect to fix, not a business outcome a caller needs to handle gracefully.
/// </para>
/// </remarks>
public sealed class ProjectAccountSetCreator
{
    private readonly KaffDbContext _context;

    public ProjectAccountSetCreator(KaffDbContext context) => _context = context;

    /// <summary>
    /// Adds (but does not save) the project's still-missing accounts to the context, and returns them.
    /// Returns an empty list, and adds nothing, when every account this project needs already exists.
    /// </summary>
    public async Task<IReadOnlyList<Account>> CreateAsync(
        Project project, DateOnly openedOn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        List<AccountType> existingTypes = await _context.Accounts
            .Where(account => account.ProjectId == project.Id)
            .Select(account => account.Type)
            .ToListAsync(cancellationToken);

        Result<IReadOnlyList<Account>> created = ProjectAccountSetFactory.Create(
            project, existingTypes.ToHashSet(), openedOn);

        if (created.IsFailure)
        {
            // Seed data is code. A failure here is a defect, not a business outcome — the same
            // reasoning AccountTreeSeeder.AddIfMissingAsync applies to its own seed rows.
            throw new InvalidOperationException(
                $"Project account set for '{project.Code}' is invalid: {created.Error.Code}.");
        }

        if (created.Value.Count > 0)
        {
            _context.Accounts.AddRange(created.Value);
        }

        return created.Value;
    }
}

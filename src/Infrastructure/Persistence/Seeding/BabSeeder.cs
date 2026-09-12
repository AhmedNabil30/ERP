using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaff.Infrastructure.Persistence.Seeding;

/// <summary>
/// One trade row to seed. Every member is required — an incomplete row cannot be written, which is
/// what kept <see cref="BabSeeder.Trades"/> empty while the Arabic names and codes were still Nabil's
/// to give (decisions.md D-142 point 5).
/// </summary>
public sealed record BabSeed(string Code, string NameAr, string NameEn, decimal MarkupFraction, int SortOrder);

/// <summary>
/// Creates the eight top-level trades (أبواب) of decisions.md D-145 §1 that must exist before a project
/// can build a BOQ against them.
/// </summary>
/// <remarks>
/// <para>
/// Runs once, on an empty table, and never again once any باب exists (decisions.md D-153 §5, Q81) — it
/// never edits or removes one. "Never overwrite an edit" needs no comparison at all past the guard: a
/// باب's code cannot change after creation (<see cref="Bab.Create"/> is the only writer), and the guard
/// means this seeder never touches an existing row in the first place. A rename, a markup change, a
/// move or an archive all persist.
/// </para>
/// <para>
/// <b>The seeder has no update path.</b> It does not compare values or "repair" a row. Decisions.md
/// D-142 point 3: if it contains the words <c>SetDefaultMarkup</c> or <c>Rename</c>, it is wrong.
/// </para>
/// <para>
/// <b>decisions.md D-153 §5 (Q81): the seeder skips the whole run when ANY باب exists</b>, not merely
/// the seeded codes it already knows. The guard is the first statement of the internal overload, where
/// every caller — the public overload and every test — arrives, rather than at the startup call site
/// alone, which a second caller could bypass. Past the guard the table is empty, so the old per-code
/// skip (a query, a <c>HashSet</c>, a <c>Contains</c> branch) is dead code and is deleted with it.
/// </para>
/// </remarks>
public sealed class BabSeeder
{
    private readonly KaffDbContext _context;
    private readonly ILogger<BabSeeder> _logger;

    public BabSeeder(KaffDbContext context, ILogger<BabSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// decisions.md D-145 §1 — Karim's eight trades, superseding D-139 §8's table. Empty until that
    /// entry released the hold D-142 point 5 placed on this list.
    /// </summary>
    public static IReadOnlyList<BabSeed> Trades { get; } =
    [
        new("CON", "أعمال خرسانة", "Concrete Works", 0.15m, 0),
        new("MAS", "أعمال مباني", "Masonry Works", 0.15m, 1),
        new("PLU", "أعمال صحية", "Plumbing Works", 0.20m, 2),
        new("ELE", "أعمال كهرباء", "Electrical Works", 0.20m, 3),
        new("HVA", "أعمال تكييف وتهوية", "HVAC Works", 0.20m, 4),
        new("FIN", "أعمال تشطيبات", "Finishing Works", 0.30m, 5),
        new("CAR", "أعمال نجارة", "Carpentry Works", 0.25m, 6),
        new("MET", "أعمال معدنية", "Metal Works", 0.25m, 7),
    ];

    public Task<int> SeedAsync(CancellationToken cancellationToken = default) => SeedAsync(Trades, cancellationToken);

    /// <summary>Internal overload so tests can drive the mechanism with their own rows.</summary>
    internal async Task<int> SeedAsync(IReadOnlyList<BabSeed> seeds, CancellationToken cancellationToken)
    {
        // D-153 §5 (Q81). Whole-run guard, not a per-code one: the moment any باب exists — seeded or
        // client-created — the environment is no longer "empty" and the seeder makes no change at all.
        if (await _context.Babs.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Trades (أبواب) already exist; the seeder made no change (D-152 §8).");
            return 0;
        }

        int inserted = 0;

        foreach (BabSeed seed in seeds)
        {
            Result<Bab> result = Bab.Create(
                seed.Code,
                seed.NameAr,
                seed.NameEn,
                Percentage.FromFraction(seed.MarkupFraction),
                parentBabId: null,
                seed.SortOrder);

            if (result.IsFailure)
            {
                // Seed data is code. A failure here is a defect, not a business outcome.
                throw new InvalidOperationException($"Seed باب '{seed.Code}' is invalid: {result.Error.Code}.");
            }

            _context.Babs.Add(result.Value);
            inserted++;
        }

        if (inserted > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} trades (أبواب).", inserted);
        }

        return inserted;
    }
}

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
/// Idempotent and additive, the same shape as <see cref="AccountTreeSeeder"/>: it inserts a باب only if
/// its <c>Code</c> is absent, and never edits or removes one. "Never overwrite an edit" is guaranteed by
/// keying on <c>Code</c> alone — a باب's code cannot change after creation
/// (<see cref="Bab.Create"/> is the only writer), so once a seeded code exists this seeder never
/// touches that row again. A rename, a markup change, a move or an archive all persist.
/// </para>
/// <para>
/// <b>The seeder has no update path.</b> It does not compare values or "repair" a row. Decisions.md
/// D-142 point 3: if it contains the words <c>SetDefaultMarkup</c> or <c>Rename</c>, it is wrong.
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
        List<string> existingCodes = await _context.Babs
            .Select(bab => bab.Code)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(existingCodes, StringComparer.Ordinal);

        int inserted = 0;

        foreach (BabSeed seed in seeds)
        {
            if (existing.Contains(seed.Code))
            {
                continue;
            }

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

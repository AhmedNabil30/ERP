using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaff.Api.Tests;

/// <summary>
/// What a freshly initialised database actually contains — nothing this class seeded, nothing any
/// sibling class seeded either.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not <c>[Collection(DatabaseCollection.Name)]</c>.</b> Every other class in this
/// project joins that collection to share one Postgres database, because standing one up per class
/// would be slow. <c>ListBabsTests.An_empty_database_returns_an_empty_list</c> (renamed
/// <c>The_items_field_is_an_array_never_null_regardless_of_row_count</c>) asserted "the database has
/// zero أبواب" against that shared database and passed only because it happened to run before
/// <c>CreateCatalogueItemTests</c>, <c>EditCatalogueItemTests</c>, <c>ArchiveCatalogueItemTests</c>,
/// <c>UnarchiveCatalogueItemTests</c> and <c>ListCatalogueItemsTests</c> — all five seed أبواب of their
/// own — got to it in whatever order the full suite ran them: 33 rows, once it did not.
/// </para>
/// <para>
/// <c>IClassFixture&lt;PostgresDatabase&gt;</c> below gives this class its own <c>PostgresDatabase</c>,
/// created once for this class alone and touched by nothing else — the only way the zero-أبواب claim
/// can actually be witnessed rather than assumed.
/// </para>
/// </remarks>
public sealed class DatabaseSeedingTests : IClassFixture<PostgresDatabase>
{
    private readonly PostgresDatabase _database;

    public DatabaseSeedingTests(PostgresDatabase database) => _database = database;

    [Fact]
    public async Task Schema_creation_alone_seeds_no_babs()
    {
        // PostgresDatabase.InitializeAsync already ran DatabaseInitializer.InitialiseAsync
        // (SchemaStrategy.CreateFromModel) before this test method started, and nothing else has
        // touched this database — no SeedAsync, no sibling test class. Seeding أبواب is BabSeeder's
        // job (decisions.md D-142); schema creation on its own must insert none.
        await using KaffDbContext context = _database.CreateBareContext();

        int count = await context.Babs.CountAsync(Ct);

        count.Should().Be(
            0, "seeding is BabSeeder's job; schema creation must never insert a باب (D-142)");
    }

    // ---- D-142 — the seeding mechanism itself, each on its OWN private database ---------------------
    //
    // Not _database: that is this class's shared fixture, pinned empty by Schema_creation_alone_seeds_
    // no_babs above, and xUnit does not order test methods within a class. Each test below stands up
    // its own PostgresDatabase so seeding it can never be read by, or race, the zero-count assertion.

    [Fact]
    public async Task The_bab_seeder_is_idempotent()
    {
        await using PostgresDatabase database = await PrivateDatabaseAsync();
        await using KaffDbContext context = database.CreateBareContext();
        var seeder = new BabSeeder(context, NullLogger<BabSeeder>.Instance);

        IReadOnlyList<BabSeed> seeds = TestSeeds();

        await seeder.SeedAsync(seeds, Ct);
        await seeder.SeedAsync(seeds, Ct);

        int count = await context.Babs.CountAsync(bab => seeds.Select(s => s.Code).Contains(bab.Code), Ct);

        count.Should().Be(seeds.Count, "seeding twice must not double the rows");
    }

    [Fact]
    public async Task The_bab_seeder_never_overwrites_an_edited_trade()
    {
        await using PostgresDatabase database = await PrivateDatabaseAsync();
        await using KaffDbContext context = database.CreateBareContext();
        var seeder = new BabSeeder(context, NullLogger<BabSeeder>.Instance);

        IReadOnlyList<BabSeed> seeds = TestSeeds();

        await seeder.SeedAsync(seeds, Ct);

        Bab bab = await context.Babs.SingleAsync(b => b.Code == seeds[0].Code, Ct);
        bab.Rename("اسم معدل", "Edited Name");
        bab.SetDefaultMarkup(Percentage.FromFraction(0.99m));
        bab.Archive();
        await context.SaveChangesAsync(Ct);

        await seeder.SeedAsync(seeds, Ct);

        Bab reread = await context.Babs.SingleAsync(b => b.Code == seeds[0].Code, Ct);
        reread.NameEn.Should().Be("Edited Name", "the seeder never overwrites an edit (D-142)");
        reread.DefaultMarkup.Should().Be(Percentage.FromFraction(0.99m));
        reread.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task The_bab_seeder_skips_a_code_the_client_already_created()
    {
        await using PostgresDatabase database = await PrivateDatabaseAsync();
        await using KaffDbContext context = database.CreateBareContext();
        var seeder = new BabSeeder(context, NullLogger<BabSeeder>.Instance);

        IReadOnlyList<BabSeed> seeds = TestSeeds();

        Bab clientRow = Bab.Create(
            seeds[0].Code, "اسم العميل", "Client Name", Percentage.FromFraction(0.05m)).Value;

        context.Babs.Add(clientRow);
        await context.SaveChangesAsync(Ct);

        await seeder.SeedAsync(seeds, Ct);

        List<Bab> matching = await context.Babs.Where(b => b.Code == seeds[0].Code).ToListAsync(Ct);

        matching.Should().ContainSingle("the client's row is kept and no duplicate is added");
        matching[0].NameEn.Should().Be("Client Name");
    }

    // ---- decisions.md D-153 §5 (Q81) — the seeder skips the whole run when ANY باب exists -----------

    [Fact]
    public async Task The_bab_seeder_seeds_an_empty_environment_and_skips_entirely_when_any_bab_exists()
    {
        IReadOnlyList<BabSeed> seeds = TestSeeds();

        // Half one: empty.
        await using PostgresDatabase emptyDatabase = await PrivateDatabaseAsync();
        await using KaffDbContext emptyContext = emptyDatabase.CreateBareContext();
        var emptySeeder = new BabSeeder(emptyContext, NullLogger<BabSeeder>.Instance);

        int insertedIntoEmpty = await emptySeeder.SeedAsync(seeds, Ct);

        insertedIntoEmpty.Should().Be(seeds.Count);

        foreach (BabSeed seed in seeds)
        {
            (await emptyContext.Babs.AnyAsync(bab => bab.Code == seed.Code, Ct)).Should().BeTrue();
        }

        // Half two: not empty — one باب whose code is NOT in the seed list, so a code that seeds[]
        // would also skip under the old per-code branch cannot make this half pass under both
        // mechanisms. ZZZ is the whole point.
        await using PostgresDatabase notEmptyDatabase = await PrivateDatabaseAsync();
        await using KaffDbContext notEmptyContext = notEmptyDatabase.CreateBareContext();
        var notEmptySeeder = new BabSeeder(notEmptyContext, NullLogger<BabSeeder>.Instance);

        Bab unrelated = Bab.Create("ZZZ", "غير ذلك", "Unrelated", Percentage.FromFraction(0.10m)).Value;
        notEmptyContext.Babs.Add(unrelated);
        await notEmptyContext.SaveChangesAsync(Ct);

        int insertedIntoNotEmpty = await notEmptySeeder.SeedAsync(seeds, Ct);

        insertedIntoNotEmpty.Should().Be(0, "any existing باب — seeded or not — skips the whole run");

        (await notEmptyContext.Babs.CountAsync(Ct)).Should().Be(1, "only the unrelated row is present");

        foreach (BabSeed seed in seeds)
        {
            (await notEmptyContext.Babs.AnyAsync(bab => bab.Code == seed.Code, Ct)).Should().BeFalse(
                "none of the seeded codes exist — the whole run was skipped, not merely this one code");
        }
    }

    [Fact]
    public async Task The_bab_seeder_inserts_exactly_the_trade_list()
    {
        await using PostgresDatabase database = await PrivateDatabaseAsync();
        await using KaffDbContext context = database.CreateBareContext();
        var seeder = new BabSeeder(context, NullLogger<BabSeeder>.Instance);

        // decisions.md D-142 point 5's hold on BabSeeder.Trades is released by D-145 §1: the list now
        // carries the eight trades asserted below, rather than being empty.
        await seeder.SeedAsync(Ct);

        List<Bab> seeded = await context.Babs
            .Where(bab => BabSeeder.Trades.Select(s => s.Code).Contains(bab.Code))
            .OrderBy(bab => bab.SortOrder)
            .ToListAsync(Ct);

        seeded.Should().HaveCount(BabSeeder.Trades.Count);

        for (int i = 0; i < BabSeeder.Trades.Count; i++)
        {
            BabSeed expected = BabSeeder.Trades[i];
            Bab actual = seeded[i];

            actual.Code.Should().Be(expected.Code);
            actual.NameAr.Should().Be(expected.NameAr);
            actual.NameEn.Should().Be(expected.NameEn);
            actual.DefaultMarkup.Should().Be(Percentage.FromFraction(expected.MarkupFraction));
            actual.ParentBabId.Should().BeNull("D-145 §1: every seeded trade is top-level");
        }

        BabSeeder.Trades.Should().HaveCount(8);
        BabSeeder.Trades.Select(s => s.MarkupFraction).Should().Equal(
            0.15m, 0.15m, 0.20m, 0.20m, 0.20m, 0.30m, 0.25m, 0.25m);
    }

    private static async Task<PostgresDatabase> PrivateDatabaseAsync()
    {
        var database = new PostgresDatabase();
        await database.InitializeAsync();
        return database;
    }

    private static IReadOnlyList<BabSeed> TestSeeds() =>
    [
        new(UniqueNames.Code("DST-BAB"), "اختبار أول", "Test First", 0.10m, 0),
        new(UniqueNames.Code("DST-BAB"), "اختبار ثاني", "Test Second", 0.20m, 1),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}

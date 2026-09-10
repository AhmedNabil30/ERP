using Kaff.Api.Tests.Infrastructure;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    public async Task A_freshly_initialised_database_seeds_no_babs()
    {
        // PostgresDatabase.InitializeAsync already ran DatabaseInitializer.InitialiseAsync
        // (SchemaStrategy.CreateFromModel) before this test method started, and nothing else has
        // touched this database — no SeedAsync, no sibling test class.
        //
        // Which أبواب Kaff has and what each is worth is Q75, still open with Karim, and spec.md
        // §4.2's concrete 15% (concrete) / 30% (finishes) are examples, not defaults (KAFF-204 rule 7).
        // Neither DatabaseInitializer nor AccountTreeSeeder (the only two things that run against a
        // database at start-up — src/Api/Program.cs) inserts a row into Babs; AccountTreeSeeder seeds
        // only company-level Accounts. This pins that absence so it stays a deliberate fact rather than
        // an accident of what nobody happened to add yet.
        await using KaffDbContext context = _database.CreateBareContext();

        int count = await context.Babs.CountAsync(Ct);

        count.Should().Be(
            0,
            "which أبواب Kaff has is Q75, still open with Karim — CLAUDE.md forbids inventing trade "
            + "names or markup percentages, and neither DatabaseInitializer nor AccountTreeSeeder may "
            + "insert one");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}

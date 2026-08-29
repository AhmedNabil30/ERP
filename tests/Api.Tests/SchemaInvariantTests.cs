using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaff.Api.Tests;

/// <summary>
/// The schema rules CLAUDE.md calls prohibitions, checked against the built EF model.
/// </summary>
/// <remarks>
/// These are the tests that survive future sessions. A money property added six slices from now
/// without precision, or a <c>Balance</c> column added because it seemed convenient, fails here
/// rather than in production with silently truncated figures.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SchemaInvariantTests
{
    private readonly PostgresDatabase _database;

    public SchemaInvariantTests(PostgresDatabase database) => _database = database;

    [Fact]
    public void Every_money_property_is_decimal_18_4()
    {
        // CLAUDE.md: "EF Core silently truncates decimals when precision isn't configured.
        // Every money property gets this. No exceptions."
        using KaffDbContext context = _database.CreateBareContext();

        List<string> offenders = [];

        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            foreach (IProperty property in entityType.GetProperties())
            {
                Type clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (clrType != typeof(Money))
                {
                    continue;
                }

                if (property.GetPrecision() != 18 || property.GetScale() != 4)
                {
                    offenders.Add($"{entityType.ShortName()}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty("spec.md §6.1 requires decimal(18,4) on every money column");
    }

    [Fact]
    public void No_decimal_column_is_left_at_the_provider_default()
    {
        using KaffDbContext context = _database.CreateBareContext();

        List<string> offenders = [];

        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            foreach (IProperty property in entityType.GetProperties())
            {
                Type clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                bool isDecimalShaped = clrType == typeof(decimal)
                    || clrType == typeof(Money)
                    || clrType == typeof(Percentage);

                if (!isDecimalShaped)
                {
                    continue;
                }

                if (property.GetPrecision() is null || property.GetScale() is null)
                {
                    offenders.Add($"{entityType.ShortName()}.{property.Name}");
                }
            }
        }

        offenders.Should().BeEmpty("an unconfigured decimal is a silent truncation waiting to happen");
    }

    [Fact]
    public void No_entity_stores_a_balance()
    {
        // CLAUDE.md: "Never store a balance … If you find yourself adding a Balance column, stop —
        // that's the bug." The read-only view is the one legitimate place the word appears.
        using KaffDbContext context = _database.CreateBareContext();

        List<string> offenders = [];

        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(AccountBalance))
            {
                // The derived view. It has no table and cannot be written to.
                entityType.GetViewName().Should().NotBeNull();
                entityType.GetTableName().Should().BeNull();
                continue;
            }

            offenders.AddRange(entityType.GetProperties()
                .Where(property => property.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase)
                                   && property.Name != nameof(Account.NormalBalance))
                .Select(property => $"{entityType.ShortName()}.{property.Name}"));
        }

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Enum_columns_are_stored_as_text()
    {
        // The database guards compare ledger_kind = 'Hold' and type = 'HoldRelease'. Storing ordinals
        // would make those rules silently wrong the day somebody reorders an enum.
        using KaffDbContext context = _database.CreateBareContext();

        IProperty ledgerKind = context.Model
            .FindEntityType(typeof(Account))!
            .GetProperty(nameof(Account.LedgerKind));

        AssertStoredAsText(ledgerKind);

        IProperty postingType = context.Model
            .FindEntityType(typeof(Posting))!
            .GetProperty(nameof(Posting.Type));

        AssertStoredAsText(postingType);
    }

    /// <summary>
    /// Asserts the property reaches the database as text.
    /// </summary>
    /// <remarks>
    /// Read from the value converter, not <c>GetProviderClrType()</c> — that reports only an
    /// explicitly configured provider type and is null when the conversion comes from a
    /// converter, which made an earlier version of this test pass vacuously.
    /// </remarks>
    private static void AssertStoredAsText(IProperty property)
    {
        ValueConverter? converter = property.GetValueConverter();

        converter.Should().NotBeNull(
            $"{property.Name} must convert to text so the SQL guards can compare it by name");

        converter!.ProviderClrType.Should().Be<string>();
    }

    [Fact]
    public void Stored_account_metadata_matches_the_domain_catalogue()
    {
        // Account rows carry class, normal balance, ledger kind and postability so the SQL guards can
        // read them. This asserts the copy cannot disagree with AccountTypes, which is the risk that
        // denormalisation always carries.
        foreach (AccountTypeMetadata meta in AccountTypes.All)
        {
            Result<Account> created = Account.Create(
                meta.Type,
                $"CHK-{(int)meta.Type}",
                "فحص",
                "Check",
                Currency.Egp,
                new DateOnly(2026, 1, 1),
                projectId: meta.Scope == AccountScope.ProjectRequired ? Guid.CreateVersion7() : null,
                partyType: meta.RequiredParty,
                partyId: meta.RequiredParty is null ? null : Guid.CreateVersion7());

            created.IsSuccess.Should().BeTrue($"{meta.Type} should be constructible from its own metadata");

            Account account = created.Value;
            account.Class.Should().Be(meta.Class);
            account.NormalBalance.Should().Be(meta.NormalBalance);
            account.LedgerKind.Should().Be(meta.LedgerKind);
            account.IsPostable.Should().Be(meta.IsPostable);
            account.EnforceNonNegative.Should().Be(meta.EnforceNonNegative);
        }
    }

    /// <summary>
    /// A database missing a check constraint must be reported as missing a guard.
    /// </summary>
    /// <remarks>
    /// decisions.md D-063 A-01. <c>FindMissingGuardsAsync</c> queried <c>pg_trigger</c>,
    /// <c>pg_indexes</c> and <c>pg_views</c> and never <c>pg_constraint</c>, so a database without
    /// <c>ck_postings_amount_positive</c> — CLAUDE.md's "enforced by a database constraint, not
    /// application code" — started, served and reported no missing guards. The constraint is dropped
    /// and restored here rather than asserted about, because a test of a safety check that never
    /// removes the safety cannot fail.
    /// </remarks>
    [Fact]
    public async Task A_dropped_check_constraint_is_reported_as_a_missing_guard()
    {
        await using KaffDbContext context = _database.CreateBareContext();
        var initializer = new DatabaseInitializer(context, NullLogger<DatabaseInitializer>.Instance);

        IReadOnlyList<string> before = await initializer.FindMissingGuardsAsync(Ct);
        before.Should().BeEmpty("the fixture builds the schema from the model, so every guard exists");

        await context.Database.ExecuteSqlAsync(
            $"ALTER TABLE postings DROP CONSTRAINT ck_postings_amount_positive",
            Ct);

        try
        {
            IReadOnlyList<string> missing = await initializer.FindMissingGuardsAsync(Ct);
            missing.Should().Contain("ck_postings_amount_positive");
        }
        finally
        {
            await context.Database.ExecuteSqlAsync(
                $"ALTER TABLE postings ADD CONSTRAINT ck_postings_amount_positive CHECK (amount > 0)",
                Ct);
        }

        IReadOnlyList<string> after = await initializer.FindMissingGuardsAsync(Ct);
        after.Should().BeEmpty("the constraint was restored");
    }

    /// <summary>
    /// <c>V-27-A</c>. The written-out guard list and the EF model say the same thing, both ways round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Neither list can catch a regression alone, which is why there are two.</b> D-064 derived the
    /// required check constraints from the model, because a hand-written list is one somebody forgets
    /// to extend. The Verifier then deleted <c>ck_users_subcontractor_cannot_log_in</c> from
    /// <c>IdentityConfigurations</c> and watched 97/97 and 215/215 stay green,
    /// <c>A_dropped_check_constraint_is_reported_as_a_missing_guard</c> included: a derived list
    /// deletes its own expectation in the same edit, so the D-033 start-up refusal cannot fire and
    /// <c>/api/health</c> goes on reporting <c>guardsInstalled</c>
    /// (qa/slice-1/verification-2026-08-27.md, <c>V-27-A</c>).
    /// </para>
    /// <para>
    /// <b>The two directions are two different defects.</b> A name in
    /// <see cref="DatabaseInitializer.RequiredCheckConstraints"/> that the model no longer declares is
    /// a rule that left the schema — and the host will already have refused to boot before this test
    /// ran, because the fixture builds the database from the model and the constraint is therefore
    /// absent from it. That is the trigger-class coverage the check constraints did not have. A name
    /// in the model that is not written here is D-064's forget-to-extend, arriving as a new constraint
    /// nobody added to the list.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_written_out_check_constraints_and_the_model_agree()
    {
        using KaffDbContext context = _database.CreateBareContext();

        IReadOnlyList<string> model = DatabaseInitializer.ModelCheckConstraints(
            context.GetService<IDesignTimeModel>().Model);

        model.Except(DatabaseInitializer.RequiredCheckConstraints, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a check constraint declared in Persistence/Configurations must also be written out in "
                + "DatabaseInitializer.RequiredCheckConstraints, or nothing notices the day it is "
                + "deleted from the model again — decisions.md D-064's own forget-to-extend risk");

        DatabaseInitializer.RequiredCheckConstraints.Except(model, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a constraint written out as required but no longer declared by the model has left the "
                + "schema. If the removal is deliberate it is two edits and a decisions.md entry, not "
                + "one edit and a green suite (V-27-A)");
    }

    /// <summary>
    /// The count is stated, so a list that quietly halved is visible rather than merely consistent.
    /// </summary>
    /// <remarks>
    /// Both assertions above compare the two lists to each other. Deleting a constraint from the model
    /// <b>and</b> from the written list satisfies both — the two-file act is deliberate by design, and
    /// this is the third statement that makes it loud. 30 on 2026-08-29, the number the Verifier
    /// counted (qa/slice-1/verification-2026-08-27.md §5). Change it when a slice adds constraints, in
    /// the same commit that adds them.
    /// </remarks>
    [Fact]
    public void Thirty_check_constraints_are_required()
        => DatabaseInitializer.RequiredCheckConstraints.Should().HaveCount(30);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}

using System.Text.RegularExpressions;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Npgsql;

namespace Kaff.Api.Tests.Features.Treasury;

/// <summary>
/// KAFF-319 — every named database guard exception in <c>001_guards.sql</c> maps to a translated
/// <see cref="TreasuryErrors"/> member, and nothing else is swallowed.
/// </summary>
/// <remarks>
/// AC-319-A and AC-319-C — a mapped guard reaching a real endpoint as a translated
/// <c>ProblemDetails</c> rather than a 500 — are already proved end to end for
/// <c>KAFF_NEGATIVE_BALANCE</c> and <c>KAFF_CLOSED_PERIOD</c> by
/// <see cref="Kaff.Api.Tests.Features.Treasury.PostMovementTests"/>'s <c>TC_3_028</c> and
/// <c>TC_3_030</c>, and for the other guards this handler pre-checks in the domain by every other
/// <c>PostMovementTests</c> case. What was missing, and what this file adds, is (1) proof that
/// <b>every</b> named prefix — not only the two the handler used to know about — has a mapping
/// (AC-319-B), and (2) proof that an exception this story does not name still falls through
/// unmapped, so the generic 500 handler still sees it (AC-319-D).
/// </remarks>
public sealed class DatabaseGuardTranslationTests
{
    // ---- AC-319-B — the mapping table is exhaustive against 001_guards.sql itself ------------------

    [Fact]
    public void Every_named_posting_guard_prefix_in_001_guards_sql_has_a_mapped_treasury_error()
    {
        string sql = File.ReadAllText(GuardsSqlPath());

        // Positive control (D-116 §3 discipline): assert the search actually found guard text,
        // so an empty or moved file cannot make this test pass by finding nothing wrong.
        sql.Should().Contain("RAISE EXCEPTION", "the guards file must contain real guard bodies");

        // Every 'KAFF_XXX: ...' template the file raises, deduplicated.
        HashSet<string> allPrefixes = Regex.Matches(sql, @"'(KAFF_[A-Z_]+):")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Three prefixes are deliberately out of this story's scope (see KAFF-319 "Not in this
        // story" and DatabaseGuardTranslation's own remarks): KAFF_APPEND_ONLY guards UPDATE/DELETE,
        // not the INSERT path this mapping serves; KAFF_HOLD_PARTIAL_RELEASE and
        // KAFF_ACCOUNT_IMMUTABLE are not in the story's named list of twelve/thirteen prefixes.
        string[] outOfScope = ["KAFF_APPEND_ONLY", "KAFF_HOLD_PARTIAL_RELEASE", "KAFF_ACCOUNT_IMMUTABLE"];

        IEnumerable<string> inScope = allPrefixes.Except(outOfScope, StringComparer.Ordinal);

        inScope.Should().BeEquivalentTo(
            DatabaseGuardTranslation.Map.Keys,
            "every posting-guard prefix 001_guards.sql can raise must resolve to a TreasuryErrors "
            + "member — AC-319-B; none may be left to fall through to the generic exception handler");
    }

    // ---- one test per mapped exception (AC-319-B / AC-319-C) --------------------------------------

    public static IEnumerable<object[]> MappedPrefixes() =>
        DatabaseGuardTranslation.Map.Select(pair => new object[] { pair.Key, pair.Value });

    [Theory]
    [MemberData(nameof(MappedPrefixes))]
    public void Each_named_guard_prefix_translates_to_its_treasury_error(string prefix, Error expected)
    {
        PostgresException raised = RaisedBy(prefix);

        Error? translated = DatabaseGuardTranslation.Translate(raised);

        translated.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(MappedPrefixes))]
    public void Each_mapped_error_carries_the_correct_http_status_and_a_translated_key(string prefix, Error expected)
    {
        _ = prefix;

        // AC-319-A / AC-319-C: never 500, and the key is one the existing errors.treasury.* catalogue
        // resolves — the i18n key format itself, not a fresh mechanism.
        ResultExtensions.StatusFor(expected.Type).Should().NotBe(
            Microsoft.AspNetCore.Http.StatusCodes.Status500InternalServerError,
            "a refused posting reads as a translated message, never a bare 500 — AC-319-A");

        expected.MessageKey.Should().StartWith("errors.treasury.");
    }

    // ---- AC-319-D — an unrecognised exception is not swallowed -------------------------------------

    [Fact]
    public void An_exception_that_names_no_guard_prefix_does_not_translate()
    {
        // A real PostgreSQL failure with no KAFF_ marker at all — e.g. a plain unique-index
        // collision from some other table. This must return null so the caller's own
        // `catch ... when (Translate(exception) is { } error)` clause does not fire, and the
        // exception keeps propagating to the generic 500 handler untouched.
        var unrelated = new PostgresException(
            "duplicate key value violates unique constraint \"ux_some_other_table\"",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);

        DatabaseGuardTranslation.Translate(unrelated).Should().BeNull(
            "AC-319-D — this story narrows what reaches the generic 500 handler, it does not remove the fallback");
    }

    [Fact]
    public void A_non_postgres_exception_does_not_translate()
    {
        DatabaseGuardTranslation.Translate(new InvalidOperationException("unrelated failure"))
            .Should().BeNull();
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private static PostgresException RaisedBy(string prefix) =>
        new($"{prefix}: simulated guard failure for translation testing.", "ERROR", "ERROR", "P0001");

    private static string GuardsSqlPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KaffErp.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must run from inside the repository");

        return Path.Combine(directory!.FullName, "src", "Infrastructure", "Persistence", "Sql", "001_guards.sql");
    }
}

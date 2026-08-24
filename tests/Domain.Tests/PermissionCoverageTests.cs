using Kaff.Domain.Authorization;

namespace Kaff.Domain.Tests;

/// <summary>
/// The enforceable half of SM-30 (process/agile.md, "The Permission Coverage Law").
/// </summary>
/// <remarks>
/// <para>
/// <b>A row with no test makes nothing fail.</b> On 2026-08-22 <c>ProjectCreate</c>,
/// <c>ProjectFinancialsEdit</c> and <c>UserRead</c> shipped reachable in the catalogue and named in no
/// test anywhere, while the suite stood at 74/74 green. The Definition of Done is structurally
/// incapable of catching that: it tests for red and the defect is an absence. This test is the thing
/// that goes red. Recorded as owed by Backend in decisions.md D-057 §1 and again in D-059 §6.
/// </para>
/// <para>
/// <b>It reads the test sources as text, and that is the whole mechanism.</b> A name in a comment
/// counts as a mention — the ceiling of a text scan, accepted deliberately. What it buys is the thing
/// nothing else does: a new catalogue row that nobody tested cannot reach <c>main</c> silently. A
/// Roslyn analyser or an attribute scheme would raise the floor and cost a build step; neither was
/// worth it for a rule whose failure mode is "nobody wrote anything at all".
/// </para>
/// <para>
/// <b>Not merged with <c>scripts/check-citations.ps1</c>.</b> Same shape, different domains — that one
/// asserts a name exists in a source file cited from a markdown document. decisions.md D-059 §6.
/// </para>
/// </remarks>
public sealed class PermissionCoverageTests
{
    /// <summary>
    /// Excluded from the scan. Without that, the baseline below would mention every row it lists, the
    /// scan would report those rows as covered, and the test could not fail — the thing agents.md §3c
    /// says is worse than no test.
    /// </summary>
    private const string OwnFileName = "PermissionCoverageTests.cs";

    /// <summary>
    /// Rows that predate SM-30 and belong to slices nobody has built. Written out rather than skipped,
    /// so the gap is visible and shrinks on the record — the same shape as
    /// <c>The_set_of_unresolved_permissions_has_not_grown</c>. Pinned in both directions: a new
    /// untested row fails, and so does leaving a name here once its slice lands and tests it.
    /// </summary>
    private static readonly Permission[] NamedInNoTestYet =
    [
        Permission.CatalogueManage,     // slice 2 — masters
        Permission.BabManage,           // slice 2
        Permission.SubcontractorManage, // slice 2
        Permission.SupplierManage,      // slice 2
        Permission.OpportunityManage,   // slice 4 — the spine
        Permission.ExtractPrepare,      // slice 5 — billing
        Permission.QuantityGateApprove, // slice 5
        Permission.DailyLogWrite,       // slice 6 — execution
    ];

    [Fact]
    public void Every_permission_catalogue_row_is_named_in_a_test()
    {
        string testSource = string.Join('\n', TestSourceFiles().Select(File.ReadAllText));

        IEnumerable<Permission> uncovered = Enum.GetValues<Permission>()
            .Where(permission => !testSource.Contains(permission.ToString(), StringComparison.Ordinal));

        uncovered.Should().BeEquivalentTo(
            NamedInNoTestYet,
            "SM-30: a permission catalogue row and a test that names it land in the same change. "
            + "A name that appeared here and no longer belongs is a row that has since been tested — "
            + "delete the line. A name that appeared that is not here at all is a row shipped with no "
            + "test, which is the defect this test exists to make red");
    }

    /// <summary>
    /// Every <c>.cs</c> file under <c>tests/</c>, found by walking up from the test assembly rather
    /// than from the current directory — the runner's working directory is not ours to depend on, and
    /// <c>CallerFilePath</c> is rewritten by <c>ContinuousIntegrationBuild</c>, which CI turns on
    /// (Directory.Build.props, and <c>CI: true</c> in .github/workflows/ci.yml).
    /// </summary>
    /// <remarks>
    /// All three suites, not only this one. <c>ClientManage</c> is exercised solely by
    /// <c>Kaff.Api.Tests</c>, and SM-30 asks whether a row is named in a test — not in which project.
    /// </remarks>
    private static IEnumerable<string> TestSourceFiles()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Kaff.Domain.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        if (directory?.Parent is not DirectoryInfo testsRoot)
        {
            throw new InvalidOperationException(
                $"No Kaff.Domain.Tests.csproj above '{AppContext.BaseDirectory}'. This test reads the "
                + "test sources, so it cannot run against a detached copy of the build output.");
        }

        string separator = Path.DirectorySeparatorChar.ToString();
        string binFolder = separator + "bin" + separator;
        string objFolder = separator + "obj" + separator;

        return Directory
            .EnumerateFiles(testsRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(binFolder, StringComparison.Ordinal)
                && !path.Contains(objFolder, StringComparison.Ordinal)
                && !string.Equals(Path.GetFileName(path), OwnFileName, StringComparison.Ordinal));
    }
}

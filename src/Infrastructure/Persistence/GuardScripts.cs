using System.Reflection;

namespace Kaff.Infrastructure.Persistence;

/// <summary>
/// The embedded database guard scripts, in the order they must be applied.
/// </summary>
/// <remarks>
/// One reader, two callers: the <c>DatabaseGuards</c> migration, so a database provisioned with
/// <c>dotnet ef database update</c> arrives with its rules already enforced; and
/// <see cref="DatabaseInitializer"/>, so a schema built from the model — which is what the test
/// harness does — gets them too, and so a running deployment cannot drift.
///
/// The scripts are idempotent by design, which is what makes applying them twice correct rather
/// than merely tolerable.
/// </remarks>
public static class GuardScripts
{
    /// <summary>Reads every embedded <c>.sql</c> guard script, ordered by file name.</summary>
    public static IReadOnlyList<string> ReadAllInOrder()
    {
        Assembly assembly = typeof(GuardScripts).Assembly;

        IEnumerable<string> resources = assembly
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal);

        List<string> scripts = [];

        foreach (string resource in resources)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            scripts.Add(reader.ReadToEnd());
        }

        if (scripts.Count == 0)
        {
            throw new InvalidOperationException(
                "No guard scripts are embedded in Kaff.Infrastructure. The append-only, "
                + "non-negative-balance and ledger rules would silently not exist. Check that "
                + "Persistence/Sql/*.sql is still an EmbeddedResource in the csproj.");
        }

        return scripts;
    }
}

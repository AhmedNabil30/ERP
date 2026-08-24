using System.Globalization;
using System.Threading;
using Kaff.Domain.Common;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Collision-free identifiers for seeded test data.
/// </summary>
/// <remarks>
/// <para>
/// Every test class in this suite seeds users, clients and projects into <b>one shared database</b>
/// — <c>PostgresDatabase</c> is a collection fixture, created once per run — and xUnit constructs a
/// fresh class instance for every test method, so <c>InitializeAsync</c> re-seeds on each one. The
/// codes and usernames therefore have to be unique across the whole run, not merely within a class.
/// </para>
/// <para>
/// They used to be suffixed with <c>Random.Shared.Next(1000, 9999)</c>. Nine thousand values, drawn
/// roughly ninety times a run, is a birthday problem: about a one-in-twenty chance that some run
/// fails on <c>23505: duplicate key value violates unique constraint "ux_users_user_name"</c> during
/// seeding, in a test that has nothing to do with uniqueness. It happened on 2026-08-20 and had
/// simply been lucky before. A flaky suite is worse than a failing one, because the first thing it
/// teaches is to re-run rather than to look. See decisions.md D-046.
/// </para>
/// <para>
/// A process-wide counter is the right scope precisely because the database is process-wide too.
/// </para>
/// </remarks>
internal static class UniqueNames
{
    private static int _counter;

    /// <summary>A number no other call in this process will return.</summary>
    private static int Next() => Interlocked.Increment(ref _counter);

    /// <summary>Suffixes <paramref name="prefix"/> so the result is unique for the run.</summary>
    public static string Code(string prefix) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{Next():D6}");

    /// <summary>
    /// A distinct Egyptian mobile number. Distinct matters beyond the unique index: spec.md §2
    /// deduplicates clients by phone, so two seeded clients sharing one would be merged rather than
    /// rejected — a quieter failure than a constraint violation, and a worse one.
    /// </summary>
    public static PhoneNumber Phone() =>
        PhoneNumber.Create(string.Create(CultureInfo.InvariantCulture, $"010{Next():D8}")).Value;
}

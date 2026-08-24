using Npgsql;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Asserts that the database refused an operation, whichever layer surfaced the refusal.
/// </summary>
/// <remarks>
/// A guard can fail in three different shapes, and which one you get is an implementation detail
/// nobody should have to encode in a test. A <c>BEFORE INSERT</c> trigger fails during the command,
/// so EF wraps it in <c>DbUpdateException</c>. A deferred constraint trigger fails at
/// <c>COMMIT</c>, which is outside the command and therefore not wrapped. Raw SQL through
/// <c>ExecuteSqlAsync</c> throws the provider exception directly.
///
/// What matters in every case is the same: a <see cref="PostgresException"/> somewhere in the chain,
/// carrying the marker the guard raises. Asserting on the marker also pins the *reason* — a test
/// that merely expects "an exception" would pass on a foreign key violation.
/// </remarks>
internal static class DatabaseGuard
{
    public const string AppendOnly = "KAFF_APPEND_ONLY";
    public const string NegativeBalance = "KAFF_NEGATIVE_BALANCE";
    public const string LedgerNetting = "KAFF_LEDGER_NETTING";
    public const string HoldDebit = "KAFF_HOLD_DEBIT";
    public const string ClosedPeriod = "KAFF_CLOSED_PERIOD";
    public const string ReversalMismatch = "KAFF_REVERSAL_MISMATCH";
    public const string ProjectTag = "KAFF_PROJECT_TAG";

    public static async Task<PostgresException> RefusesAsync(Func<Task> operation, string marker)
    {
        Exception? caught = null;

        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        caught.Should().NotBeNull($"the database must refuse this operation with {marker}");

        PostgresException? postgres = Unwrap(caught!);

        postgres.Should().NotBeNull(
            $"the failure should come from PostgreSQL, but was: {caught!.GetType().Name}: {caught.Message}");

        postgres!.MessageText.Should().Contain(marker);

        return postgres;
    }

    private static PostgresException? Unwrap(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }
}

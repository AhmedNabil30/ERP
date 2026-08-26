using System.Globalization;
using System.Security.Cryptography;
using Kaff.Infrastructure.Identity;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>PasswordHasher</c> — the properties that make the stored credential worth anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists separately from <see cref="CreateUserTests"/>.</b> That suite asserts the
/// stored value is not the typed password
/// [Verified: 2026-08-23 @ <c>CreateUserTests.cs</c> -&gt;
/// <c>The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed</c>]. <b>That assertion
/// passes with one iteration and a constant salt.</b> The two properties that actually cost an
/// attacker anything — the work factor and the per-credential salt — were asserted by nothing until
/// this file; decisions.md D-066 §4 documents the design and does not mention the gap.
/// </para>
/// <para>
/// No database and no HTTP: this is a pure function. It lives in <c>Api.Tests</c> only because that
/// is the suite that references <c>Kaff.Infrastructure</c>, and it carries no
/// <c>[Collection]</c> so it never waits on PostgreSQL.
/// </para>
/// <para>
/// <b>Verification exists as of KAFF-101a and is asserted below</b>, including the property the
/// whole sign-in door rests on: <c>Verify</c> does the same work whether or not there is a stored
/// hash to compare against. This paragraph read "verification is deliberately not tested here,
/// because it does not exist" until 2026-08-26.
/// </para>
/// </remarks>
public sealed class PasswordHasherTests
{
    private const string Password = "temporary-one";

    /// <summary>
    /// The format exists so a verifier can read the parameters back — so the parameters in the
    /// string must be the ones the hash was produced with.
    /// </summary>
    /// <remarks>
    /// This is the assertion a bare format check cannot make. Recomputing PBKDF2 from the salt and
    /// the iteration count <b>as read out of the stored string</b> fails if the string names a work
    /// factor the code did not use — and a string that lies about its parameters is worse than a
    /// bare hash, because KAFF-101a will trust it and every credential issued becomes unverifiable.
    /// See decisions.md D-066 §4.
    /// </remarks>
    [Fact]
    public void The_stored_form_names_the_parameters_the_hash_was_actually_produced_with()
    {
        string[] parts = PasswordHasher.Hash(Password).Split('$');

        parts.Should().HaveCount(4, "the form is pbkdf2-sha256$iterations$salt$hash");
        parts[0].Should().Be("pbkdf2-sha256");

        int iterations = int.Parse(parts[1], CultureInfo.InvariantCulture);
        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] hash = Convert.FromBase64String(parts[3]);

        byte[] recomputed = Rfc2898DeriveBytes.Pbkdf2(
            Password, salt, iterations, HashAlgorithmName.SHA256, hash.Length);

        recomputed.Should().Equal(
            hash,
            "the iteration count in the string must be the one the hash was derived with, or "
            + "KAFF-101a's verification reads a parameter that was never applied");
    }

    /// <summary>Two hashes of one password differ — which is the salt, and nothing else.</summary>
    /// <remarks>
    /// A constant salt is the single cheapest way for this primitive to become worthless without
    /// anything else changing: every existing test still passes, the format still parses, and one
    /// rainbow table covers every account in Kaff.
    /// </remarks>
    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        string[] first = PasswordHasher.Hash(Password).Split('$');
        string[] second = PasswordHasher.Hash(Password).Split('$');

        first[2].Should().NotBe(second[2], "each credential gets its own salt");
        first[3].Should().NotBe(second[3], "and therefore its own hash");
    }

    /// <summary>The documented parameters — OWASP's 2023 floor for PBKDF2-HMAC-SHA256.</summary>
    /// <remarks>
    /// Pinned rather than assumed. Lowering the work factor is a one-token edit that changes no
    /// behaviour anything else can see; this is the test that turns red when somebody makes the
    /// suite faster by making the credential cheaper.
    /// </remarks>
    [Fact]
    public void The_work_factor_and_the_salt_and_hash_sizes_are_the_documented_ones()
    {
        string[] parts = PasswordHasher.Hash(Password).Split('$');

        int.Parse(parts[1], CultureInfo.InvariantCulture).Should().Be(600_000);
        Convert.FromBase64String(parts[2]).Should().HaveCount(16, "16-byte salt");
        Convert.FromBase64String(parts[3]).Should().HaveCount(32, "32-byte hash");
    }

    /// <summary>The round trip, both ways.</summary>
    [Fact]
    public void Verify_accepts_the_password_that_produced_the_hash_and_nothing_else()
    {
        string stored = PasswordHasher.Hash(Password);

        PasswordHasher.Verify(Password, stored).Should().BeTrue();
        PasswordHasher.Verify(Password + "x", stored).Should().BeFalse();
        PasswordHasher.Verify(string.Empty, stored).Should().BeFalse();
    }

    /// <summary>
    /// A credential hashed with a work factor other than today's still verifies.
    /// </summary>
    /// <remarks>
    /// The reason the stored form names its own parameters. This is the assertion that fails if
    /// <c>Verify</c> is ever "simplified" to read <c>Iterations</c> from the constant instead of
    /// from the string — at which point raising the work factor would silently invalidate every
    /// credential issued before the change, with no error anywhere.
    /// </remarks>
    [Fact]
    public void Verify_reads_the_work_factor_out_of_the_stored_string()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Password, salt, 1_000, HashAlgorithmName.SHA256, 32);

        string legacy = string.Create(
            CultureInfo.InvariantCulture,
            $"pbkdf2-sha256$1000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");

        PasswordHasher.Verify(Password, legacy).Should().BeTrue();
    }

    /// <summary>Nothing verifies against an account that holds no credential.</summary>
    [Fact]
    public void Verify_refuses_every_password_when_there_is_no_stored_hash()
    {
        foreach (string? stored in new[] { null, string.Empty, "   ", "not-a-hash", "pbkdf2-sha256$0$$" })
        {
            PasswordHasher.Verify(Password, stored).Should().BeFalse(
                "'{0}' names no credential and must never admit anybody",
                stored ?? "null");
        }
    }

    /// <summary>
    /// ⚠️ <b>The enumeration defence, and the one test in this file that is about a clock.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// KAFF-101a rule 14a, decisions.md D-072 §1. The sign-in door answers a username that does not
    /// exist, a subcontractor and a cleared credential with the same 401 as a wrong password — so
    /// the status code tells an attacker nothing. <b>It tells them everything through a clock the
    /// moment <c>Verify</c> returns early when there is nothing to compare against</b>, because
    /// every other refusal pays for 600,000 PBKDF2 iterations and that one would not.
    /// </para>
    /// <para>
    /// <b>Asserted here rather than through the endpoint because this is where the property lives.</b>
    /// <c>Verify</c> is a pure function with no HTTP, no database and no scheduler in the way, so the
    /// measurement is of the thing itself. The endpoint suite asserts the ordering it depends on —
    /// see <c>SignInTests</c>.
    /// </para>
    /// <para>
    /// <b>The margin is three orders of magnitude, not a percentage.</b> A present hash costs
    /// ~10^8 ns; an early return costs ~10^2. The assertion is "at least half", which no amount of
    /// scheduler noise reaches from either side, and the statistic is the <b>minimum</b> of several
    /// runs — the one that cannot be inflated by a garbage collection landing in the sample.
    /// <b>Watched red</b> on 2026-08-26 by giving <c>Verify</c> an <c>if (storedHash is null)
    /// return false;</c> first line: 0.00 of the baseline.
    /// </para>
    /// </remarks>
    [Fact]
    public void Verifying_against_no_stored_hash_costs_what_verifying_against_one_costs()
    {
        string stored = PasswordHasher.Hash(Password);

        // Warm up: the first PBKDF2 call in a process pays for JIT and for the algorithm's own
        // one-time set-up, and that cost lands on whichever case runs first.
        _ = PasswordHasher.Verify(Password, stored);
        _ = PasswordHasher.Verify(Password, storedHash: null);

        long present = FastestVerification(stored);
        long absent = FastestVerification(storedHash: null);

        absent.Should().BeGreaterThan(
            present / 2,
            "an absent credential must cost what a present one costs. It took {0} ticks against "
            + "{1} for a real hash — a fraction of the work, which is the user-enumeration oracle "
            + "KAFF-101a rule 14a exists to close, arriving as a clock rather than as a status code",
            absent,
            present);
    }

    private static long FastestVerification(string? storedHash)
    {
        long fastest = long.MaxValue;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            long started = TimeProvider.System.GetTimestamp();
            _ = PasswordHasher.Verify(Password, storedHash);
            fastest = Math.Min(fastest, TimeProvider.System.GetTimestamp() - started);
        }

        return fastest;
    }
}

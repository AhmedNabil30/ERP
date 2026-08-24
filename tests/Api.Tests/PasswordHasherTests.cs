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
/// <b>Verification is deliberately not tested here, because it does not exist.</b> D-066 §4 places
/// the timing-safe comparison and the rehash decision together in KAFF-101a, which is BLOCKED.
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
}

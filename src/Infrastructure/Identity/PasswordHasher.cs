using System.Globalization;
using System.Security.Cryptography;

namespace Kaff.Infrastructure.Identity;

/// <summary>
/// Turns a plaintext password into the string <c>User.PasswordHash</c> stores.
/// </summary>
/// <remarks>
/// <para>
/// PBKDF2-HMAC-SHA256 from the BCL. decisions.md D-011 pointed at
/// <c>Microsoft.AspNetCore.Cryptography.KeyDerivation</c> for this; that package is a thin wrapper
/// over <see cref="Rfc2898DeriveBytes.Pbkdf2(string, byte[], int, HashAlgorithmName, int)"/>, which
/// is already in the framework, so no dependency is added. CLAUDE.md: "Do not add a package that
/// duplicates something the framework already does."
/// </para>
/// <para>
/// <b>The stored form names its own parameters</b> — <c>pbkdf2-sha256$iterations$salt$hash</c>, both
/// halves Base64. A credential outlives the constant that produced it: raising the iteration count
/// in five years must not invalidate every password issued before it, and a bare hash gives the
/// verifier nothing to work back from. Verification is KAFF-101a's and reads the parameters from the
/// string rather than from these constants.
/// </para>
/// <para>
/// Static rather than an interface with one implementation. There is no second hashing strategy and
/// nothing needs to substitute one; the day a rehash-on-sign-in path exists it lives beside
/// <see cref="Hash"/>, not behind an abstraction over it.
/// </para>
/// </remarks>
public static class PasswordHasher
{
    /// <summary>Names the algorithm and the format of everything after it.</summary>
    private const string Prefix = "pbkdf2-sha256";

    /// <summary>OWASP's 2023 floor for PBKDF2-HMAC-SHA256. Recorded in the hash, not assumed by it.</summary>
    private const int Iterations = 600_000;

    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Separates the four fields of the stored form.</summary>
    private const char FieldSeparator = '$';

    /// <summary>
    /// What <see cref="Verify"/> compares against when there is no stored hash to compare against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the enumeration defence, and it is why <see cref="Verify"/> takes a nullable
    /// stored hash rather than making its caller branch.</b> A username that matches no row, a
    /// subcontractor (whose <c>PasswordHash</c> is null by entity rule and by database check), and
    /// an account whose credential was cleared all reach the sign-in door with nothing to compare —
    /// and the natural implementation returns before hashing. That answer arrives in microseconds
    /// while every other refusal pays for <see cref="Iterations"/> PBKDF2 iterations, so the door
    /// stops leaking which usernames exist through its status code and starts leaking it through a
    /// clock (decisions.md D-072 §1, KAFF-101a rule 14a). Falling back to this makes the absent case
    /// do exactly the same work as the present one, with no branch for a later session to tidy away.
    /// </para>
    /// <para>
    /// Random salt and random expected hash, built once per process. No PBKDF2 runs to construct it
    /// — the point is only that the parameters are the shipped ones, so the comparison costs the
    /// shipped amount, and that nothing can ever match 32 unpredictable bytes.
    /// </para>
    /// </remarks>
    private static readonly StoredHash Absent = new(
        Iterations,
        RandomNumberGenerator.GetBytes(SaltBytes),
        RandomNumberGenerator.GetBytes(HashBytes));

    /// <summary>Hashes <paramref name="password"/> with a fresh random salt.</summary>
    /// <remarks>
    /// No length check here. The minimum is a business rule
    /// (<see cref="Kaff.Domain.Identity.User.MinimumPasswordLength"/>, decisions.md D-049 ruling 3)
    /// and belongs where it can be refused with an i18n key, not thrown from a crypto helper.
    /// </remarks>
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    /// <summary>
    /// Answers whether <paramref name="password"/> is the one behind <paramref name="storedHash"/>.
    /// </summary>
    /// <param name="password">What the caller submitted. May be empty; it is still hashed.</param>
    /// <param name="storedHash">
    /// <c>User.PasswordHash</c>, or <see langword="null"/> when the account holds no credential or
    /// does not exist. <b>A null is not a fast path</b> — see <see cref="Absent"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>The parameters come from the stored string, never from the constants above.</b> A
    /// credential outlives the constant that produced it: raising <see cref="Iterations"/> in five
    /// years must not invalidate every password issued before it.
    /// </para>
    /// <para>
    /// <b>This method always does the full work, on every input, and that is the whole of its
    /// contract.</b> It has no early return — not for a null stored hash, not for an unparsable one,
    /// not for an empty password. KAFF-101a rule 14a and decisions.md D-072 §1: an even time
    /// envelope is what stops the sign-in door telling an attacker which usernames exist, and
    /// "return early when there is nothing to compare" is the optimisation that re-opens it.
    /// </para>
    /// <para>
    /// The final comparison is <see cref="CryptographicOperations.FixedTimeEquals"/> rather than
    /// <c>SequenceEqual</c>, for the same class of reason at a much smaller scale.
    /// </para>
    /// </remarks>
    public static bool Verify(string password, string? storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        StoredHash reference = Parse(storedHash) ?? Absent;

        byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(
            password,
            reference.Salt,
            reference.Iterations,
            HashAlgorithmName.SHA256,
            reference.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(candidate, reference.Hash);
    }

    /// <summary>Reads <c>pbkdf2-sha256$iterations$salt$hash</c>, or null if it is not that.</summary>
    private static StoredHash? Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        string[] fields = stored.Split(FieldSeparator);

        if (fields.Length != 4
            || !string.Equals(fields[0], Prefix, StringComparison.Ordinal)
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations)
            || iterations < 1)
        {
            return null;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(fields[2]);
            byte[] hash = Convert.FromBase64String(fields[3]);

            return salt.Length > 0 && hash.Length > 0 ? new StoredHash(iterations, salt, hash) : null;
        }
        catch (FormatException)
        {
            // A stored credential that is not in the shipped form. It cannot be verified against,
            // and the caller must not learn that from how fast the answer came back.
            return null;
        }
    }

    /// <summary>The three things a stored credential says about itself.</summary>
    private sealed record StoredHash(int Iterations, byte[] Salt, byte[] Hash);
}

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
}

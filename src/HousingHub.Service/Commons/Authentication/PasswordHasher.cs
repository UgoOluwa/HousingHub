using System.Globalization;
using System.Security.Cryptography;

namespace HousingHub.Service.Commons.Authentication;

/// <summary>
/// PBKDF2-SHA512 password hashing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stored value records the iteration count it was created with.</b> Format is
/// <c>iterations-hash-salt</c>. Without that, changing <see cref="Iterations"/> silently
/// invalidates every password in the database, because verification re-derives with
/// whatever the constant happens to say today. Recording it means the cost can be
/// raised whenever hardware makes it cheap enough to be worth raising, which it will
/// be — this is not a number that gets set once.
/// </para>
/// <para>
/// Hashes written before this change have no iteration prefix and are read as
/// <see cref="LegacyIterations"/>. <see cref="NeedsRehash"/> reports them so callers
/// that hold the plaintext (i.e. a successful sign-in) can quietly upgrade them.
/// </para>
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>
    /// OWASP's current figure for PBKDF2-SHA512.
    /// </summary>
    /// <remarks>
    /// Previously 500,000 — roughly 2.4x this. That is not more secure in any way that
    /// matters: it is already far past the point where offline cracking of a decent
    /// password is impractical. What it does buy is 2.4x the CPU on every sign-in,
    /// registration and password change, billed by the millisecond on Lambda and paid
    /// on failed attempts too, which makes each rejected credential-stuffing request
    /// cost the defender more than the attacker.
    /// </remarks>
    private const int Iterations = 210_000;

    /// <summary>What hashes without an iteration prefix were created with.</summary>
    private const int LegacyIterations = 500_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return $"{Iterations}-{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }

    /// <summary>
    /// True when <paramref name="password"/> produced <paramref name="passwordHash"/>.
    /// </summary>
    /// <remarks>
    /// Returns false for anything it cannot parse rather than throwing. A stored hash
    /// that is empty or malformed is a data problem, not a caller problem, and letting
    /// it escape as an exception turned "this account has no usable password" into a
    /// 500 — which is both a crash on demand and an oracle, since a 500 and a 400
    /// distinguish a Google-only account from a wrong password.
    /// </remarks>
    public bool Verify(string password, string passwordHash)
    {
        if (!TryParse(passwordHash, out int iterations, out byte[]? hash, out byte[]? salt))
            return false;

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt!, iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(hash!, inputHash);
    }

    /// <summary>
    /// True when the stored hash was produced with settings we no longer use, and the
    /// caller should re-hash. Only meaningful right after a successful <see cref="Verify"/>,
    /// since re-hashing needs the plaintext.
    /// </summary>
    public bool NeedsRehash(string passwordHash)
    {
        if (!TryParse(passwordHash, out int iterations, out _, out _))
            return false;   // unparseable — re-hashing won't help, and Verify already fails

        return iterations != Iterations;
    }

    /// <summary>
    /// Splits a stored hash into its parts, accepting both the current
    /// <c>iterations-hash-salt</c> form and the legacy <c>hash-salt</c> form.
    /// </summary>
    private static bool TryParse(string? passwordHash, out int iterations, out byte[]? hash, out byte[]? salt)
    {
        iterations = 0;
        hash = null;
        salt = null;

        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;

        string[] parts = passwordHash.Split('-');

        // Two parts is unambiguously the legacy form: hex is not split by '-', so a
        // current-format value always has exactly three.
        string hashHex;
        string saltHex;

        switch (parts.Length)
        {
            case 2:
                iterations = LegacyIterations;
                hashHex = parts[0];
                saltHex = parts[1];
                break;

            case 3:
                if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
                    || iterations <= 0)
                    return false;
                hashHex = parts[1];
                saltHex = parts[2];
                break;

            default:
                return false;
        }

        try
        {
            hash = Convert.FromHexString(hashHex);
            salt = Convert.FromHexString(saltHex);
        }
        catch (FormatException)
        {
            return false;
        }

        // A truncated or padded hash would otherwise reach FixedTimeEquals, which
        // returns false on a length mismatch anyway — but failing here keeps the
        // reason for the failure legible.
        return hash.Length == HashSize && salt.Length == SaltSize;
    }
}

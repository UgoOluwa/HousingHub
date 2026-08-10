using System.Security.Cryptography;
using System.Text;

namespace HousingHub.Core.Security;

/// <summary>
/// Comparison for values where being wrong leaks something — shared secrets,
/// tokens, verification codes.
/// </summary>
public static class SecretComparer
{
    /// <summary>
    /// Compares two secrets without short-circuiting on the first differing byte.
    /// </summary>
    /// <remarks>
    /// <c>==</c> and <c>string.Equals</c> return as soon as they find a difference, so
    /// the time they take is proportional to how many leading characters the guess got
    /// right. Over a network that signal is buried in jitter and hard to exploit — but
    /// it is a real signal, it gets easier to extract the more attempts an attacker is
    /// allowed, and the correct comparison costs nothing.
    ///
    /// Length is not hidden: values of different lengths return immediately. That is
    /// acceptable here, since the length of a configured secret is not the secret.
    /// </remarks>
    public static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }
}

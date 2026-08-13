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

        // Trimmed before comparison. Secrets reach this from environment variables,
        // CI secret stores and pasted config, all of which routinely pick up a
        // trailing newline that nobody can see. A byte-exact comparison then fails
        // for a reason that is invisible in every UI involved, and the only symptom
        // is a 401 with no explanation.
        //
        // This gives nothing away: no secret has meaningful leading or trailing
        // whitespace, so accepting a padded copy of the right value is not accepting
        // a wrong one.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a.Trim()),
            Encoding.UTF8.GetBytes(b.Trim()));
    }

    /// <summary>
    /// Describes why a comparison failed, without revealing either value.
    /// </summary>
    /// <remarks>
    /// A failed shared-secret check is otherwise undiagnosable: the two systems that
    /// hold the values both display them as dots, and the response is a bare 401. The
    /// facts below — configured or not, lengths, and whether whitespace alone
    /// explains it — identify the cause in one look and disclose nothing useful.
    /// Lengths are not sensitive; a secret is not guessable from its length.
    /// </remarks>
    public static string DescribeMismatch(string? presented, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
            return "no secret is configured on the server, so every caller is rejected";

        if (string.IsNullOrEmpty(presented))
            return "the caller sent no secret — check the X-Worker-Secret header is present";

        if (presented.Trim() == expected.Trim())
            return "the values match apart from surrounding whitespace";

        return presented.Trim().Length == expected.Trim().Length
            ? $"different values of the same length ({expected.Trim().Length}) — likely a rotation applied in one place only"
            : $"different lengths — caller sent {presented.Trim().Length} characters, server expects {expected.Trim().Length}";
    }
}

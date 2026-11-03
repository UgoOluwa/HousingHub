namespace HousingHub.Core.Security;

/// <summary>
/// Decides whether a bank account belongs to the person claiming it.
/// </summary>
/// <remarks>
/// <para>
/// The only thing standing between an owner and somebody else's rent. Without it,
/// anyone who can list a property can point payouts at any account they can type.
/// </para>
/// <para>
/// Nigerian banks return names in no fixed order and often with middle names the
/// account holder never gave us — "EZE BAYO CHUKWU" against an account registered
/// as "Bayo Eze". So the rule is containment, not equality: every part of the name
/// we hold must appear in the name the bank returned. Extra names in the bank's
/// answer are allowed; a missing one is not.
/// </para>
/// </remarks>
public static class BankAccountNameMatcher
{
    public static bool Matches(string? verifiedName, string? bankName)
    {
        var expected = Tokenise(verifiedName);
        var actual = Tokenise(bankName);

        // An empty expectation would match everything. Refuse rather than wave it
        // through — a customer with no name on file is a reason to stop, not a
        // reason to trust whatever the bank said.
        if (expected.Count == 0 || actual.Count == 0) return false;

        return expected.IsSubsetOf(actual);
    }

    /// <summary>
    /// Words of two letters or more, upper-cased.
    /// </summary>
    /// <remarks>
    /// Initials are dropped on both sides: a bank returning "B EZE" should not be
    /// refused for an account held as "Bayo Eze", and a one-letter token matches
    /// too much to be worth requiring.
    /// </remarks>
    private static HashSet<string> Tokenise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return [];

        return name
            .Split([' ', '\t', '-', '.', ',', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => new string(part.Where(char.IsLetter).ToArray()).ToUpperInvariant())
            .Where(part => part.Length > 1)
            .ToHashSet();
    }
}

using HousingHub.Service.VerificationService;

namespace HousingHub.Test.Verification;

/// <summary>
/// The name comparison that flags possible impersonation.
/// </summary>
/// <remarks>
/// <para>
/// Two failure directions with very different costs. A false <b>None</b> escalates
/// an honest applicant as a suspected fraudster, which is insulting and slow to
/// undo. A false <b>Exact</b> waves through a real document belonging to somebody
/// else — the fraud this check exists to catch.
/// </para>
/// <para>
/// Nigerian names vary legitimately between documents in ways naive string
/// comparison reads as fraud, so most of these tests are about tolerating
/// variation without tolerating actual mismatch.
/// </para>
/// </remarks>
public class NameMatcherTests
{
    private static NameMatcher.MatchLevel Compare(string? a, string? b) => NameMatcher.Compare(a, b);

    // ── Should match ─────────────────────────────────────────────

    [Fact]
    public void IdenticalNames_MatchExactly()
    {
        Assert.Equal(NameMatcher.MatchLevel.Exact, Compare("Chukwuemeka Nwosu", "Chukwuemeka Nwosu"));
    }

    [Theory]
    [InlineData("CHUKWUEMEKA NWOSU")]
    [InlineData("chukwuemeka nwosu")]
    [InlineData("  Chukwuemeka   Nwosu  ")]
    [InlineData("Nwosu, Chukwuemeka")]
    [InlineData("Nwosu Chukwuemeka")]
    public void CaseSpacingPunctuationAndOrder_DoNotMatter(string onDocument)
    {
        // Registries and ID documents disagree about all four of these constantly.
        Assert.Equal(NameMatcher.MatchLevel.Exact, Compare(onDocument, "Chukwuemeka Nwosu"));
    }

    [Fact]
    public void AMissingMiddleName_IsPartialNotAMismatch()
    {
        // The single most common benign variation. Treating it as a mismatch would
        // escalate a large share of honest applicants.
        Assert.Equal(NameMatcher.MatchLevel.Partial,
            Compare("Chukwuemeka Nwosu", "Chukwuemeka Obinna Nwosu"));
    }

    [Fact]
    public void AMiddleInitial_IsIgnored()
    {
        // "Chukwuemeka O. Nwosu" and "Chukwuemeka Nwosu" are the same person.
        Assert.Equal(NameMatcher.MatchLevel.Exact,
            Compare("Chukwuemeka O. Nwosu", "Chukwuemeka Nwosu"));
    }

    [Theory]
    [InlineData("Mr Chukwuemeka Nwosu")]
    [InlineData("Chief Chukwuemeka Nwosu")]
    [InlineData("Alhaji Chukwuemeka Nwosu")]
    [InlineData("Engr. Chukwuemeka Nwosu")]
    [InlineData("ESV Chukwuemeka Nwosu")]
    public void Honorifics_AreStripped(string onDocument)
    {
        // Estate surveyors in particular put ESV on everything.
        Assert.Equal(NameMatcher.MatchLevel.Exact, Compare(onDocument, "Chukwuemeka Nwosu"));
    }

    [Theory]
    [InlineData("Adeyemi Properties Limited")]
    [InlineData("ADEYEMI PROPERTIES LTD")]
    [InlineData("Adeyemi Properties Nigeria Ltd")]
    [InlineData("Adeyemi Properties Ltd.")]
    public void CorporateSuffixes_DoNotBreakACompanyMatch(string onDocument)
    {
        Assert.Equal(NameMatcher.MatchLevel.Exact, Compare(onDocument, "Adeyemi Properties"));
    }

    // ── Must NOT match — this is the fraud case ──────────────────

    [Fact]
    public void TwoDifferentPeople_DoNotMatch()
    {
        // A real certificate submitted by someone unconnected to it.
        Assert.Equal(NameMatcher.MatchLevel.None, Compare("Chukwuemeka Nwosu", "Fatima Abubakar"));
    }

    [Fact]
    public void TwoDifferentCompanies_DoNotMatch()
    {
        Assert.Equal(NameMatcher.MatchLevel.None,
            Compare("Adeyemi Properties Limited", "Okonkwo Realty Limited"));
    }

    [Fact]
    public void ASharedSurnameAlone_IsNotAMatch()
    {
        // Common family names are weak evidence. One token in common must not be
        // enough, or half of Lagos matches half of Lagos.
        Assert.Equal(NameMatcher.MatchLevel.None, Compare("Chukwuemeka Nwosu", "Ngozi Nwosu"));
    }

    [Fact]
    public void CompaniesSharingOnlyGenericWords_DoNotMatch()
    {
        // "Nigeria", "Global", "Services", "Ltd" are noise. Without stripping them
        // these two would look nearly identical.
        Assert.NotEqual(NameMatcher.MatchLevel.Exact,
            Compare("Global Nigeria Services Ltd", "Adeyemi Global Services Nigeria Ltd"));
    }

    // ── Unknown, which is not the same as suspicious ─────────────

    [Theory]
    [InlineData(null, "Chukwuemeka Nwosu")]
    [InlineData("Chukwuemeka Nwosu", null)]
    [InlineData("", "Chukwuemeka Nwosu")]
    [InlineData("   ", "Chukwuemeka Nwosu")]
    public void AMissingSideIsUnknown(string? onDocument, string? onAccount)
    {
        Assert.Equal(NameMatcher.MatchLevel.Unknown, Compare(onDocument, onAccount));
    }

    [Fact]
    public void ANameThatIsEntirelyNoise_IsUnknownRatherThanAMatch()
    {
        // "Nigeria Global Services Ltd" has no distinguishing token at all. Claiming
        // a match on noise alone would be worse than admitting we cannot tell.
        Assert.Equal(NameMatcher.MatchLevel.Unknown,
            Compare("Nigeria Global Services Ltd", "Nigeria Global Services Ltd"));
    }

    // ── Escalation policy ────────────────────────────────────────

    [Fact]
    public void OnlyANoneResultEscalates()
    {
        Assert.True(NameMatcher.ShouldEscalate(NameMatcher.MatchLevel.None));

        Assert.False(NameMatcher.ShouldEscalate(NameMatcher.MatchLevel.Exact));
        Assert.False(NameMatcher.ShouldEscalate(NameMatcher.MatchLevel.Partial));

        // Unknown is a data gap, not a red flag. Escalating it would bury the real
        // mismatches under everything we simply could not compare.
        Assert.False(NameMatcher.ShouldEscalate(NameMatcher.MatchLevel.Unknown));
    }
}

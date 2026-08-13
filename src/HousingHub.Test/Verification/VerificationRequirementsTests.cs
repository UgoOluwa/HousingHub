using HousingHub.Model.Enums;
using HousingHub.Service.VerificationService;

namespace HousingHub.Test.Verification;

/// <summary>
/// What a case must contain before it can be submitted.
/// </summary>
/// <remarks>
/// Two failure directions, both bad in different ways. Too strict and legitimate
/// owners are rejected for holding the wrong kind of correct paperwork. Too loose
/// and a case reaches a reviewer with nothing to review, wasting the scarcest
/// resource in this whole system — human review time.
/// </remarks>
public class VerificationRequirementsTests
{
    private static List<VerificationDocumentType> Missing(
        VerificationSubjectType subjectType, params VerificationDocumentType[] supplied) =>
        VerificationRequirements.MissingFrom(subjectType, supplied);

    // ── Business ─────────────────────────────────────────────────

    [Fact]
    public void Business_RequiresTheCacCertificate()
    {
        // The CAC certificate is the anchor: it carries the RC number, and its named
        // directors are what the account holder's verified identity is compared
        // against. Without it there is nothing to check.
        Assert.Contains(VerificationDocumentType.CacCertificate,
            Missing(VerificationSubjectType.Business));
    }

    [Fact]
    public void Business_IsSatisfiedByTheCacCertificateAlone()
    {
        // Deliberately not demanding tax clearance up front. It is the hardest
        // document to obtain in this market, and requiring it would stall every
        // applicant on their slowest item.
        Assert.Empty(Missing(VerificationSubjectType.Business, VerificationDocumentType.CacCertificate));
    }

    [Fact]
    public void Business_IsNotSatisfiedByOtherBusinessDocuments()
    {
        var missing = Missing(VerificationSubjectType.Business,
            VerificationDocumentType.TaxClearance,
            VerificationDocumentType.LasreraPermit,
            VerificationDocumentType.ProofOfAddress);

        Assert.Contains(VerificationDocumentType.CacCertificate, missing);
    }

    // ── Property: the "one of" rule ──────────────────────────────

    [Fact]
    public void Property_AcceptsACertificateOfOccupancy()
    {
        Assert.Empty(Missing(VerificationSubjectType.Property,
            VerificationDocumentType.CertificateOfOccupancy));
    }

    [Fact]
    public void Property_AlsoAcceptsADeedOfAssignmentInstead()
    {
        // The case this rule exists for. Which document an owner holds depends on how
        // they acquired the land — a C of O for a direct state grant, a Deed of
        // Assignment for a transfer. Demanding both rejects real owners.
        Assert.Empty(Missing(VerificationSubjectType.Property,
            VerificationDocumentType.DeedOfAssignment));
    }

    [Fact]
    public void Property_RequiresAtLeastOneOfThem()
    {
        var missing = Missing(VerificationSubjectType.Property,
            VerificationDocumentType.SurveyPlan,
            VerificationDocumentType.PurchaseReceipt);

        Assert.NotEmpty(missing);
    }

    [Fact]
    public void Property_WithNothingSupplied_ReportsWhatIsNeeded()
    {
        // The list drives the submitter's checklist, so it must name something rather
        // than being empty-but-failing.
        Assert.NotEmpty(Missing(VerificationSubjectType.Property));
    }

    // ── Tiers ────────────────────────────────────────────────────

    [Theory]
    [InlineData(VerificationSubjectType.Business, VerificationTier.BusinessVerified)]
    [InlineData(VerificationSubjectType.Property, VerificationTier.TitleVerified)]
    [InlineData(VerificationSubjectType.Identity, VerificationTier.IdentityVerified)]
    public void EachSubjectTypeGrantsItsOwnTier(VerificationSubjectType subject, VerificationTier expected)
    {
        Assert.Equal(expected, VerificationRequirements.TierFor(subject));
    }

    [Fact]
    public void TiersAreOrderedSoComparisonsWork()
    {
        // Callers will write `tier >= BusinessVerified`. If the numbering were
        // arbitrary that would silently admit the wrong people.
        Assert.True(VerificationTier.IdentityVerified > VerificationTier.Unverified);
        Assert.True(VerificationTier.BusinessVerified > VerificationTier.IdentityVerified);
        Assert.True(VerificationTier.TitleVerified > VerificationTier.BusinessVerified);
    }

    // ── Enum stability ───────────────────────────────────────────

    [Fact]
    public void DocumentTypeValuesAreStable()
    {
        // These are persisted. Renumbering one silently reinterprets every stored
        // document of that type — a Deed of Assignment becoming a survey plan, for
        // instance — and nothing would fail at the time it happened.
        Assert.Equal(1, (int)VerificationDocumentType.CacCertificate);
        Assert.Equal(3, (int)VerificationDocumentType.LasreraPermit);
        Assert.Equal(20, (int)VerificationDocumentType.CertificateOfOccupancy);
        Assert.Equal(21, (int)VerificationDocumentType.DeedOfAssignment);
        Assert.Equal(22, (int)VerificationDocumentType.GovernorsConsent);
        Assert.Equal(26, (int)VerificationDocumentType.LetterOfAuthorityToLet);
        Assert.Equal(60, (int)VerificationDocumentType.GovernmentIssuedId);
    }

    [Fact]
    public void CaseStatusValuesAreStable()
    {
        Assert.Equal(1, (int)VerificationCaseStatus.Draft);
        Assert.Equal(2, (int)VerificationCaseStatus.Submitted);
        Assert.Equal(4, (int)VerificationCaseStatus.Approved);
        Assert.Equal(7, (int)VerificationCaseStatus.EscalatedNameMismatch);
    }
}

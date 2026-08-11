using System.ComponentModel;

namespace HousingHub.Model.Enums;

/// <summary>
/// What a verification case is about.
/// </summary>
/// <remarks>
/// Deliberately the <i>subject</i> rather than the submitter. A business case is
/// about a company, a property case is about a specific listing, and the same
/// person can be behind several of each. Keying on the submitter would make
/// "this agent is verified" and "this listing's title is verified" the same fact,
/// which they are not.
/// </remarks>
public enum VerificationSubjectType
{
    /// <summary>Subject id is a Customer. Agency, developer or sole-trader registration.</summary>
    Business = 1,

    /// <summary>Subject id is a Property. Title and right-to-let.</summary>
    Property = 2,

    /// <summary>
    /// Subject id is a Customer. Reserved so the existing single-document KYC flow
    /// can be folded onto this pipeline later without a schema change — see
    /// docs/phase-1-readiness-and-phase-2-plan.md. Nothing writes it yet.
    /// </summary>
    Identity = 3,
}

/// <summary>
/// A document a subject can be asked for.
/// </summary>
/// <remarks>
/// Numbered in bands with gaps, so a new business document can be added without
/// renumbering property ones. These values are persisted, so <b>never reuse or
/// renumber an existing value</b> — a stored 21 must mean Deed of Assignment
/// forever.
/// </remarks>
public enum VerificationDocumentType
{
    // ── Business (1–19) ─────────────────────────────────────────
    [Description("CAC certificate of incorporation")]
    CacCertificate = 1,

    [Description("CAC status report")]
    CacStatusReport = 2,

    [Description("LASRERA registration certificate")]
    LasreraPermit = 3,

    [Description("ESVARBON registration")]
    EsvarbonLicence = 4,

    [Description("NIESV membership certificate")]
    NiesvMembership = 5,

    [Description("Tax clearance certificate")]
    TaxClearance = 6,

    [Description("Proof of business address")]
    ProofOfAddress = 7,

    // ── Property title (20–39) ──────────────────────────────────
    [Description("Certificate of Occupancy")]
    CertificateOfOccupancy = 20,

    [Description("Deed of Assignment")]
    DeedOfAssignment = 21,

    [Description("Governor's Consent")]
    GovernorsConsent = 22,

    [Description("Survey plan")]
    SurveyPlan = 23,

    [Description("Purchase receipt")]
    PurchaseReceipt = 24,

    [Description("Land registry search result")]
    LandRegistrySearch = 25,

    /// <summary>
    /// Written authority from the title holder permitting this person to let the
    /// property. Required whenever the lister is not the title holder — an agent
    /// acting for an owner, or a tenant subletting.
    /// </summary>
    [Description("Letter of authority to let")]
    LetterOfAuthorityToLet = 26,

    // ── Developer / build (40–59) ───────────────────────────────
    [Description("Planning permit")]
    PlanningPermit = 40,

    [Description("Authorisation to build")]
    AuthorisationToBuild = 41,

    [Description("Certificate of completion")]
    CertificateOfCompletion = 42,

    // ── Identity (60–79), reserved for the KYC migration ────────
    [Description("Government-issued ID")]
    GovernmentIssuedId = 60,
}

/// <summary>State of one uploaded document within a case.</summary>
public enum DocumentReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,

    /// <summary>
    /// Was approved, but its own expiry date has passed. LASRERA permits lapse
    /// annually, so an approved document is not approved forever.
    /// </summary>
    Expired = 4,
}

/// <summary>
/// State of a whole case.
/// </summary>
/// <remarks>
/// The submitter owns Draft; everything from Submitted onward is owned by review.
/// That boundary is what makes the state machine enforceable — see
/// VerificationCase.CanAcceptDocuments.
/// </remarks>
public enum VerificationCaseStatus
{
    /// <summary>Being assembled. Documents can be added and removed.</summary>
    Draft = 1,

    /// <summary>Handed to review. The submitter can no longer change it.</summary>
    Submitted = 2,

    /// <summary>An admin has picked it up.</summary>
    UnderReview = 3,

    Approved = 4,
    Rejected = 5,

    /// <summary>Approved once, but a document has since expired.</summary>
    Expired = 6,

    /// <summary>
    /// The name on the documents does not match the account holder.
    /// </summary>
    /// <remarks>
    /// Held apart from a plain rejection because it is the single strongest signal
    /// of attempted impersonation in this whole flow — somebody submitting a real
    /// company's real CAC certificate under their own account. It wants a human
    /// looking at it, not a form letter, and it should never be auto-approved by a
    /// later automated check.
    /// </remarks>
    EscalatedNameMismatch = 7,
}

/// <summary>
/// How much diligence has been done, in increasing order.
/// </summary>
/// <remarks>
/// Ordered so comparisons work: <c>tier >= VerificationTier.BusinessVerified</c>.
/// Tiers rather than a binary badge, so supply can join at a low tier and climb,
/// and so each level can make a claim that is precisely true rather than one vague
/// claim covering everything.
/// </remarks>
public enum VerificationTier
{
    Unverified = 0,

    /// <summary>A government ID was checked against the account holder. Phase 1.</summary>
    IdentityVerified = 1,

    /// <summary>Company registration checked — CAC, and the sector body where applicable.</summary>
    BusinessVerified = 2,

    /// <summary>Title documents checked for a specific property. The strongest claim, and the most dangerous to make loosely.</summary>
    TitleVerified = 3,
}

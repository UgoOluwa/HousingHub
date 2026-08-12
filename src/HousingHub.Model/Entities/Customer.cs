using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("Customers")]
public class Customer : BaseEntity
{
    // Authentication
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    /// <summary>
    /// When the last verification email went out. Used to throttle resends
    /// server-side so the endpoint can't be used to spam an inbox.
    /// </summary>
    public DateTime? LastVerificationEmailSentAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    /// <summary>
    /// When the last password-reset email went out. Used to throttle resends
    /// server-side so the endpoint can't be used to spam an inbox.
    /// </summary>
    public DateTime? LastPasswordResetRequestedAt { get; set; }

    /// <summary>
    /// Google subject id for a linked Google identity, null otherwise.
    /// </summary>
    /// <remarks>
    /// GoogleId-index is created on the Customers table but the attribute was never
    /// declared here, so lookups by GoogleId fell back to a full table scan. DynamoDB
    /// GSIs are sparse, so only the rows that actually have a Google identity are
    /// indexed.
    /// </remarks>
    [DynamoDBGlobalSecondaryIndexHashKey("GoogleId-index")]
    public string? GoogleId { get; set; }
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;

    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("Email-index")]
    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; }

    public bool EmailVerified { get; set; } = false;

    [DynamoDBGlobalSecondaryIndexHashKey("PhoneNumber-index")]
    public string PhoneNumber { get; set; } = null!;
    public bool PhoneNumberVerified { get; set; } = false;
    public CustomerType CustomerType { get; set; }
    public DateTime? DateOfBirth { get; set; }


    // KYC Details
    // ----------------------------

    public string? NationalIdNumber { get; set; } = null!;

    public IDType IdType { get; set; }

    public string? IdDocumentUrl { get; set; } = null!;
    public DateTime? KycSubmittedAt { get; set; }
    public bool IsKycVerified { get; set; } = false;
    /// <summary>Set when an admin rejects a KYC submission; cleared on the next submission or approval.</summary>
    public string? KycRejectionReason { get; set; }

    // ----------------------------
    // Occupation Details
    // ----------------------------

    public string? ProfileImageUrl { get; set; }

    public string? JobTitle { get; set; } = null!;
    public string? CompanyName { get; set; } = null!;
    public string? Industry { get; set; } = null!;

    /// <summary>
    /// Set by a SuperAdmin for owners/agents HousingHub fully manages the listing
    /// process for. Only these owners can have properties posted on their behalf
    /// by an admin — see <see cref="PropertyService.PropertyCommandService.CreateProperty"/>.
    /// </summary>
    public bool IsManagedByHousingHub { get; set; } = false;


    // ── Business verification ───────────────────────────────────
    // Set when a VerificationCase of type Business is approved. Written only by
    // VerificationService, never by a user-facing update path — these fields are
    // the badge, and letting a profile edit touch them would let anyone grant
    // themselves one.

    /// <summary>
    /// How far this account's business credentials have been verified.
    /// </summary>
    /// <remarks>
    /// Separate from KYC. <see cref="IsKycVerified"/> answers "is this the person
    /// they say they are"; this answers "is the company they claim to represent
    /// real, and are they part of it". An agent can be identity-verified without
    /// being business-verified, and that distinction is the whole reason for tiers.
    /// </remarks>
    public VerificationTier BusinessVerificationTier { get; set; } = VerificationTier.Unverified;

    public DateTime? BusinessVerifiedAt { get; set; }

    /// <summary>
    /// When the business verification lapses, or null if nothing in it expires.
    /// </summary>
    /// <remarks>
    /// LASRERA registrations are annual, so a Lagos agent's verification has a
    /// shelf life. Copied from the approved case's earliest document expiry.
    /// </remarks>
    public DateTime? BusinessVerificationExpiresAt { get; set; }

    /// <summary>RC or BN number from the approved CAC certificate.</summary>
    public string? CacNumber { get; set; }

    /// <summary>LASRERA registration number, for agents operating in Lagos.</summary>
    public string? LasreraPermitNumber { get; set; }

    /// <summary>
    /// True when business verification is current — approved and not lapsed.
    /// </summary>
    /// <remarks>
    /// Always ask this rather than reading the tier directly. An expired
    /// verification still carries <see cref="VerificationTier.BusinessVerified"/>
    /// until a sweep moves it, and a badge shown on the strength of a lapsed
    /// LASRERA permit is a claim we cannot support.
    /// </remarks>
    [DynamoDBIgnore]
    public bool IsBusinessVerified =>
        BusinessVerificationTier >= VerificationTier.BusinessVerified
        && (BusinessVerificationExpiresAt is null || BusinessVerificationExpiresAt > DateTime.UtcNow);


    // Relationships (foreign keys only, navigation properties ignored by DynamoDB)
    [DynamoDBIgnore]
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    [DynamoDBIgnore]
    public ICollection<PropertyInspection> Inspections { get; set; } = new List<PropertyInspection>();
    [DynamoDBIgnore]
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public Guid? AddressId { get; set; }
    [DynamoDBIgnore]
    public CustomerAddress? Address { get; set; } = null!;

    public Customer() { }

    public Customer(string firstName, string lastName, string email, string phoneNumber, CustomerType customerType, string passwordHash)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        CustomerType = customerType;
        PasswordHash = passwordHash;

        // IsActive on BaseEntity is a plain bool, so it defaulted to false and nothing
        // in the registration path ever set it. Every customer row written so far is
        // therefore "inactive" despite not being suspended — SuspendCustomer sets it
        // false and ReactivateCustomer sets it true, so the field is genuinely meant to
        // mean "not suspended".
        //
        // Anything that treats IsActive as authoritative must not ship until existing
        // rows are backfilled. See docs/data-backfill-required.md.
        IsActive = true;
    }

    public void UpdateKycStatus(bool isVerified, string? rejectionReason = null)
    {
        IsKycVerified = isVerified;
        KycRejectionReason = isVerified ? null : rejectionReason;
    }

    public void AddKYCDetails(DateTime? dateOfBirth, string? nationalIdNumber, IDType idType, string? idDocumentUrl, DateTime submittedAt, string? jobTitle, string? companyName, string? industry)
    {
        DateOfBirth = dateOfBirth;
        NationalIdNumber = nationalIdNumber;
        IdType = idType;
        IdDocumentUrl = idDocumentUrl;
        KycSubmittedAt = submittedAt;
        JobTitle = jobTitle;
        CompanyName = companyName;
        Industry = industry;
        KycRejectionReason = null;
    }
}

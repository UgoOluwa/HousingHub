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

    // ----------------------------
    // Occupation Details
    // ----------------------------

    public string? JobTitle { get; set; } = null!;
    public string? CompanyName { get; set; } = null!;
    public string? Industry { get; set; } = null!;


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
    }

    public void UpdateKycStatus(bool isVerified)
    {
        IsKycVerified = isVerified;
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
    }
}

using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class Customer : BaseEntity
{
    [StringLength(1000)]
    public string FirstName { get; set; } = null!;

    [StringLength(1000)]
    public string LastName { get; set; } = null!;

    [StringLength(1000)]
    public string Email { get; set; } = null!;
    public bool EmailVerified { get; set; } = false;

    [StringLength(50)]
    public string PhoneNumber { get; set; } = null!;
    public bool PhoneNumberVerified { get; set; } = false;
    public CustomerType CustomerType { get; set; }
    public DateTime? DateOfBirth { get; set; }


    // KYC Details
    // ----------------------------

    [StringLength(100)]
    public string? NationalIdNumber { get; set; } = null!; // NIN or equivalent

    public IDType IdType { get; set; }

    [StringLength(1000)]
    public string? IdDocumentUrl { get; set; } = null!;   // Link to uploaded ID doc (Passport, Driver’s License, etc.)
    public DateTime? KycSubmittedAt { get; set; }
    public bool IsKycVerified { get; set; } = false;

    // ----------------------------
    // Occupation Details
    // ----------------------------

    [StringLength(500)]
    public string? JobTitle { get; set; } = null!;        // e.g., "Software Engineer"
    [StringLength(1000)]
    public string? CompanyName { get; set; } = null!;     // e.g., "Kuda Microfinance Bank"
    [StringLength(500)]
    public string? Industry { get; set; } = null!;    


    // Relationships
    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<PropertyInterest> Interests { get; set; } = new List<PropertyInterest>();
    public Guid AddressId { get; set; }
    public CustomerAddress? Address { get; set; } = null!;

    public Customer() { }

    public Customer(string firstName, string lastName, string email, string phoneNumber, CustomerType customerType, DateTime? dateOfBirth)
    {
        Id = Guid.NewGuid();
        DateCreated = DateTime.UtcNow;
        DateModified = DateTime.UtcNow;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        CustomerType = customerType;
        DateOfBirth = dateOfBirth;
    }

    public void UpdateKycStatus(bool isVerified)
    {
        IsKycVerified = isVerified;
        DateModified = DateTime.UtcNow;
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
        DateModified = DateTime.UtcNow;
    }
}

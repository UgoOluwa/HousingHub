using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

[DynamoDBTable("Admins")]
public class Admin : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("Email-index")]
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;

    /// <summary>Set while a login OTP is outstanding; cleared once it's used or replaced.</summary>
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    /// <summary>When the current code was sent — enforces a minimum gap between resends.</summary>
    public DateTime? OtpRequestedAt { get; set; }
    /// <summary>Incorrect verify attempts against the current code; the code is invalidated once this hits the limit.</summary>
    public int OtpAttempts { get; set; }
}

using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

/// <summary>
/// A rotating refresh token for a Customer session. The raw token is never
/// stored — only its SHA-256 hash — so a database read alone can't be used
/// to authenticate. Each successful refresh revokes this record and issues
/// a new one; a revoked token presented again is treated as theft and
/// revokes every other active token for the same customer.
/// </summary>
[DynamoDBTable("RefreshTokens")]
public class RefreshToken : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("TokenHash-index")]
    public string TokenHash { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("CustomerId-index")]
    public Guid CustomerId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}

using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

/// <summary>
/// A rotating refresh token for an Admin session. Mirrors Customer-side
/// RefreshToken (SHA-256 hash stored, never the raw value; rotated on every
/// use; a revoked token presented again revokes every other active token
/// for the same admin). Kept as a separate table/entity rather than shared
/// with the Customer side because Admins live in an entirely separate
/// DynamoDB table and ID space, and AdminAuthService already talks to
/// IDynamoDBContext directly rather than through the Customer-side
/// IUnitOfWOrk/repository pattern.
/// </summary>
[DynamoDBTable("AdminRefreshTokens")]
public class AdminRefreshToken : BaseEntity
{
    [DynamoDBGlobalSecondaryIndexHashKey("TokenHash-index")]
    public string TokenHash { get; set; } = null!;

    [DynamoDBGlobalSecondaryIndexHashKey("AdminId-index")]
    public Guid AdminId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}

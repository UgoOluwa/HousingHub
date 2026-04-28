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
}

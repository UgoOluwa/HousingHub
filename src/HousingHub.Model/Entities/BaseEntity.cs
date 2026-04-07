using Amazon.DynamoDBv2.DataModel;

namespace HousingHub.Model.Entities;

public class BaseEntity
{
    [DynamoDBHashKey]
    public Guid Id { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsActive { get; set; }

    [DynamoDBVersion]
    public int? VersionNumber { get; set; }
}

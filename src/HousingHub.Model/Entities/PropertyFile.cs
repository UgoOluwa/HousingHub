using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("PropertyFiles")]
public class PropertyFile : BaseEntity
{
    public string FileUrl { get; set; } = null!;
    public PropertyFileType Type { get; set; }
    public long FileSizeInBytes { get; set; }
    public DateTime DateUploaded { get; set; }

    // Relationships
    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public Guid PropertyId { get; set; }
    [DynamoDBIgnore]
    public Property Property { get; set; } = null!;

    public PropertyFile() { }

    public PropertyFile(string fileUrl, PropertyFileType type, long fileSizeInBytes)
    {
        Id = Guid.NewGuid();
        FileUrl = fileUrl;
        Type = type;
        FileSizeInBytes = fileSizeInBytes;
        DateUploaded = DateTime.UtcNow;
    }
}

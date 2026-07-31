using Amazon.DynamoDBv2.DataModel;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

[DynamoDBTable("PropertyReports")]
public class PropertyReport : BaseEntity
{
    public string Reason { get; set; } = null!;
    public string? Note { get; set; }
    public PropertyReportStatus Status { get; set; } = PropertyReportStatus.Pending;

    // Relationships
    [DynamoDBGlobalSecondaryIndexHashKey("PropertyId-index")]
    public Guid PropertyId { get; set; }
    [DynamoDBIgnore]
    public Property Property { get; set; } = null!;

    public Guid ReporterId { get; set; }
    [DynamoDBIgnore]
    public Customer Reporter { get; set; } = null!;

    public PropertyReport() { }

    public PropertyReport(Guid propertyId, Guid reporterId, string reason, string? note)
    {
        Id = Guid.NewGuid();
        PropertyId = propertyId;
        ReporterId = reporterId;
        Reason = reason;
        Note = note;
        Status = PropertyReportStatus.Pending;
    }
}

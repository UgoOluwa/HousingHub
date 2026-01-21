using System.ComponentModel.DataAnnotations;
using HousingHub.Model.Enums;

namespace HousingHub.Model.Entities;

public class PropertyFile : BaseEntity
{
    [StringLength(1000)]
    public string FileUrl { get; set; } = null!;
    public PropertyFileType Type { get; set; }
    public DateTime DateUploaded { get; set; }

    // Relationships
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public PropertyFile(){ }

    public PropertyFile(string fileUrl, PropertyFileType type)
    {
        Id = Guid.NewGuid();
        FileUrl = fileUrl;
        Type = type;
        DateUploaded = DateTime.UtcNow;
    }
}

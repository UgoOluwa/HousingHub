using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HousingHub.Model.Entities;

public class BaseEntity
{
    [Required]
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime DateCreated { get; set; }
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime DateModified { get; set; }
    [Required]
    public bool IsActive { get; set; }

    /// <summary>
    /// Meant to validate concurrency en database update
    /// This column is updates itself in database and only works in postgresql
    /// </summary>
    [ConcurrencyCheck]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string xmin { get;}
}

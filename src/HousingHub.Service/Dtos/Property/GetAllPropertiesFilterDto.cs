using HousingHub.Model.Enums;

namespace HousingHub.Service.Dtos.Property;

public record GetAllPropertiesFilterDto
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public PropertyFeature? Features { get; init; }
    public PropertyType? PropertyType { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public int? Bedrooms { get; init; }
}

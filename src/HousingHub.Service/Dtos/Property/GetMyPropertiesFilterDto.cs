namespace HousingHub.Service.Dtos.Property;

public record GetMyPropertiesFilterDto
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

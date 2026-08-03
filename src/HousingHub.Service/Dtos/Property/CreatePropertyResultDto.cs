namespace HousingHub.Service.Dtos.Property;

public record PossibleDuplicateDto(Guid PropertyId, string Title, string Address);

public record CreatePropertyResultDto(PropertyDto? Property, PossibleDuplicateDto? PossibleDuplicate);

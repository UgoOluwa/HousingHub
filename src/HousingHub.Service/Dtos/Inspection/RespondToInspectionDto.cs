namespace HousingHub.Service.Dtos.Inspection;

public record RespondToInspectionDto(
    Guid InspectionId,
    bool Accept,
    string? Note);
